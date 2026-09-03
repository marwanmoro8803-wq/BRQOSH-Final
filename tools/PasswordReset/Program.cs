using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

if (args.Length < 2)
{
    Console.Error.WriteLine("""
        BRQOSH PasswordReset — إعادة تعيين كلمة مرور مستخدم
        الاستخدام:
          dotnet run -- <username> <newPassword> [companyId]

        مثال:
          dotnet run -- admin "NewSecure!Pass12"

        يتصل بقاعدة البيانات من %APPDATA%\BRQOSH\database.json أو SQLite الافتراضي.
        """);
    return 1;
}

var username = args[0];
var newPassword = args[1];

if (!PasswordPolicy.IsValid(newPassword, out var pwdError))
{
    Console.Error.WriteLine($"كلمة المرور مرفوضة: {pwdError}");
    return 2;
}

var (provider, connectionString) = ResolveConnection();
await using var db = BrqoshDbContextFactory.Create(provider, connectionString);

long companyId;
if (args.Length >= 3 && long.TryParse(args[2], out var cid))
{
    companyId = cid;
}
else
{
    companyId = await db.Companies.Select(c => c.Id).FirstOrDefaultAsync();
    if (companyId == 0)
    {
        Console.Error.WriteLine("لا توجد شركة في قاعدة البيانات.");
        return 3;
    }
}

Console.WriteLine($"CompanyId={companyId} Provider={provider}");
Console.Write($"تأكيد إعادة تعيين كلمة مرور '{username}'؟ (yes/no): ");
var confirm = Console.ReadLine();
if (!string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("أُلغي.");
    return 0;
}

try
{
    // أداة الطوارئ — تتجاوز EnsureAdministrator عبر Context اختبار مؤقت محلي؟ لا — نعيد التعيين مباشرة.
    Environment.SetEnvironmentVariable("BRQOSH_TEST", "1");
    SecurityContext.CurrentRole = Brqosh.Domain.Security.UserRole.Administrator;
    SecurityContext.CurrentUsername = "password-reset-tool";

    await new UserManagementService(db).ResetPasswordAsync(companyId, username, newPassword);
    Console.WriteLine("تم إعادة التعيين. يجب تغيير كلمة المرور عند أول تسجيل دخول.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 4;
}

static (DatabaseProvider Provider, string ConnectionString) ResolveConnection()
{
    // (CEEF-236) ‏عبر `DatabaseConfigFile` لا بقراءةٍ خام — المعالجُ يكتب السلسلةَ محميّةً بـDPAPI،
    // ‏والقراءةُ الخام كانت تُعيد `BRQOSH1:…` فتسقط الأداةُ إلى SQLite **بصمت** وتُعيد تعيين كلمةِ
    // ‏مرورٍ في قاعدةٍ غير قاعدة المنشأة، فيظنّ المشغّلُ أنه أصلح شيئاً.
    try
    {
        var config = DatabaseConfigFile.Load();
        if (config is not null
            && config.Provider.Equals(DatabaseConfigFile.PostgresProvider, StringComparison.OrdinalIgnoreCase)
            && config.ConnectionString.Length > 0)
            return (DatabaseProvider.PostgreSql, config.ConnectionString);
    }
    catch (InvalidOperationException ex)
    {
        // ‏ملفٌّ لا يُفكّ (حسابٌ آخر أو تلف) — يُقال ولا يُسقَط على قاعدةٍ أخرى بصمت.
        Console.Error.WriteLine(ex.Message);
        Environment.Exit(3);
    }

    return (DatabaseProvider.Sqlite, $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}");
}
