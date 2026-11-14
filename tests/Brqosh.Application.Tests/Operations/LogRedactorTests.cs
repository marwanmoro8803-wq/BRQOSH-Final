using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (جاهزيّةُ الإنتاج · F) ‏سجلُّ الأعطال يُرسَل إلى الدعم — فما فيه من سرٍّ يخرج من الجهاز. ‏كلُّ حالةٍ هنا نصٌّ تكتبه مكتبةٌ فعلاً
/// ‏في رسالة استثناء، والتأكيدُ مزدوج: <b>السرُّ غاب</b> و<b>اسمُ المفتاح بقي</b> (فالدعمُ يعرف أيَّ سرٍّ كان).
/// </summary>
public class LogRedactorTests
{
    [Theory]
    [InlineData("Host=db;Username=brq;Password=S3cr3t!x;Database=books", "S3cr3t!x", "Password=***")]
    [InlineData("Host=db;Pwd=hunter2", "hunter2", "Pwd=***")]
    [InlineData("Server=x;User Password='a b;c'", "a b;c", "User Password=***")]
    [InlineData("GET /x?api_key=abc123&page=2", "abc123", "api_key=***")]
    [InlineData("{\"binarySecurityToken\":\"TUlJQ0RE\",\"secret\":\"Zm9vYmFy\"}", "TUlJQ0RE", "\"binarySecurityToken\":\"***\"")]
    [InlineData("{\"password\" : \"p@ss\"}", "p@ss", "\"password\" : \"***\"")]
    [InlineData("401 Unauthorized; Authorization: Bearer eyJhbGciOi.payload.sig", "eyJhbGciOi", "Bearer ***")]
    [InlineData("Authorization: Basic dXNlcjpwYXNz", "dXNlcjpwYXNz", "Basic ***")]
    public void ASecretInTheMessage_IsMasked_AndItsKeyNameStays(string message, string secret, string expected)
    {
        var redacted = LogRedactor.Redact(message)!;

        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Contains(expected, redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void APemPrivateKey_IsRemovedWhole()
    {
        var message = "parse failed: -----BEGIN EC PRIVATE KEY-----\nMHQCAQEEIBkg\nAAAA\n-----END EC PRIVATE KEY----- trailing";

        var redacted = LogRedactor.Redact(message)!;

        Assert.DoesNotContain("MHQCAQEEIBkg", redacted, StringComparison.Ordinal);
        Assert.StartsWith("parse failed: ", redacted, StringComparison.Ordinal);
        Assert.EndsWith(" trailing", redacted, StringComparison.Ordinal);
    }

    // ‏الحاجبُ لا يمسّ ما ليس سرّاً: رسالةُ المستخدم العربيّة وأسماءٌ تحوي «password» جزءاً من كلمةٍ أطول.
    [Theory]
    [InlineData("لا يمكن ترحيل قيد غير متوازن: المدين 100.00 والدائن 90.00")]
    [InlineData("duplicate key value violates unique constraint \"IX_Invoices_CompanyId_Number\"")]
    [InlineData("column NoPassword=1 is invalid")]
    [InlineData("Unexpected token '}' at position 5")]
    public void AnOrdinaryMessage_IsLeftAsIs(string message) =>
        Assert.Equal(message, LogRedactor.Redact(message));

    [Fact]
    public void NullAndEmpty_PassThrough()
    {
        Assert.Null(LogRedactor.Redact(null));
        Assert.Equal(string.Empty, LogRedactor.Redact(string.Empty));
    }

    // ‏والوصلُ نفسُه يُقاس: سجلُّ العطل المؤلَّف لا يحمل كلمةَ المرور التي حملتها رسالةُ الاستثناء الداخليّ.
    [Fact]
    public void TheComposedCrashLog_CarriesNoSecretFromAnyInnerException()
    {
        var inner = new InvalidOperationException("Host=db;Password=TopSecret42");
        var outer = new InvalidOperationException("فشل الاتصال بقاعدة البيانات", inner);

        var body = CrashLog.Compose(outer, "test");

        Assert.DoesNotContain("TopSecret42", body, StringComparison.Ordinal);
        Assert.Contains("Password=***", body, StringComparison.Ordinal);
        Assert.Contains("فشل الاتصال بقاعدة البيانات", body, StringComparison.Ordinal);
    }
}
