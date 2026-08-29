using System.Reflection;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (CEEF-229 · م2-٦) <b>بوّابةٌ تُشغّل الحرّاس بدل أن تبحث عن أسمائهم.</b>
///
/// <para><b>ما كانت البوّابة تفعله:</b> <c>scripts/security-static-scan.ps1</c> يقرأ كلّ ملفٍّ
/// اسمه <c>*PostingService.cs</c> ويتحقّق أنّ النصّ يحوي السلسلة <c>EnsureCanPost</c>. وهذا
/// يمرّ إن كان الاستدعاء <b>داخل تعليق</b>، أو في فرعٍ لا يُبلَغ، أو <b>بعد</b> الكتابة في
/// قاعدة البيانات. وأشدُّ فحوصها صرامةً يبحث عن السلسلة الحرفية <c>admin123</c> — فبابٌ خلفيٌّ
/// بأيّ اسمٍ آخر يمرّ.</para>
///
/// <para><b>وما يفعله هذا:</b> يجد <b>كلّ</b> خدمة ترحيلٍ بالانعكاس، ويستدعي <b>كلّ</b> دالّةٍ
/// عامّةٍ فيها <b>بدور «مُطّلِع»</b> — وهو الدور الذي لا يملك الترحيل — ثمّ يسأل: <b>هل رُفض
/// الاستدعاء بسببٍ أمنيّ؟</b></para>
///
/// <para><b>والوسائطُ فارغة عمداً.</b> فإن جرى الحارسُ أوّلاً جاء الرفضُ الأمنيّ ولم تُلمس
/// القاعدة. وإن كان الحارسُ غائباً أو متأخّراً، مضى الكودُ في عمله فسقط بـ
/// <c>NullReferenceException</c> أو ما شابه — <b>وذاك بعينه هو الدليل</b>: نوعُ الاستثناء يفرّق
/// بين «مُنع» و«مضى حتى انكسر». ولا تُكتب بيانات في الحالين، فالقياسُ آمن.</para>
/// </summary>
public class EveryPostingServiceRefusesAViewerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public EveryPostingServiceRefusesAViewerTests()
    {
        _connection = new SqliteConnection("Data Source=viewerguard;Mode=Memory;Cache=Shared");
        _connection.Open();
        _db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, "Data Source=viewerguard;Mode=Memory;Cache=Shared");
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// دوالُّ مُقَرٌّ بأنها لا تصل حارساً — <b>ولكلٍّ سببٌ مكتوب</b>. الاسمُ بصيغة
    /// <c>Service.Method</c>.
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredUnguarded = new()
    {
        // ‏تُملأ بعد أوّل قياس.
    };

    /// <summary>كلُّ خدمة ترحيلٍ في طبقة البنية التحتية.</summary>
    private static IReadOnlyList<Type> PostingServiceTypes() =>
        [.. typeof(BrqoshDbContext).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic
                        && t.Name.EndsWith("PostingService", StringComparison.Ordinal))
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    /// <summary>الدوالُّ العامّة غيرُ المتزامنة الظاهرة — وهي واجهةُ الخدمة للمستدعي.</summary>
    private static IReadOnlyList<MethodInfo> PublicOperations(Type service) =>
        [.. service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType))
            .OrderBy(m => m.Name, StringComparer.Ordinal)];

    /// <summary>
    /// <b>القياس المركزيّ.</b> كلُّ عمليةِ ترحيلٍ تُرفض على «المُطّلِع» برفضٍ <b>أمنيّ</b> —
    /// لا برميٍ عرضيّ من عمقِ الكود بعد أن مضى بلا إذن.
    /// </summary>
    [Fact]
    public async Task EveryPostingOperation_RefusesAViewer_BeforeDoingAnything()
    {
        var unguarded = new List<string>();
        var operationsChecked = 0;

        foreach (var service in PostingServiceTypes())
        {
            var instance = TryConstruct(service);
            if (instance is null) continue;

            foreach (var operation in PublicOperations(service))
            {
                var name = $"{service.Name}.{operation.Name}";
                if (DeclaredUnguarded.ContainsKey(name)) continue;

                operationsChecked++;
                TestActor.Viewer();

                var thrown = await InvokeAndCaptureAsync(instance, operation);

                if (!IsAuthorizationRefusal(thrown))
                {
                    unguarded.Add(
                        $"{name} ⟵ {(thrown is null ? "لم يرمِ إطلاقاً" : thrown.GetType().Name + ": " + Shorten(thrown.Message))}");
                }
            }
        }

        Assert.True(operationsChecked >= 20,
            $"لم تُفحص إلا {operationsChecked} عملية — الانعكاسُ لا يمسح خدمات الترحيل فعلاً.");

        Assert.True(unguarded.Count == 0,
            $"عملياتُ ترحيلٍ لا تُوقِف «المُطّلِع» عند الباب ({unguarded.Count}) — مضت في عملها "
            + "بلا إذنٍ حتى انكسرت لسببٍ آخر:\n  " + string.Join("\n  ", unguarded));
    }

    /// <summary>
    /// <b>حارسٌ للحارس.</b> لو صار «المُطّلِع» يُرفض لسببٍ غير أمنيّ — أو صار كلُّ شيءٍ يرمي —
    /// لمرّ الاختبارُ أعلاه وهو لا يفرّق. فيُثبت هنا أنّ <b>المدير يمرّ من الباب نفسه</b>:
    /// أي أنّ الرفضَ سببُه الدورُ لا الوسائطُ الفارغة.
    /// </summary>
    [Fact]
    public async Task AnAdministrator_IsNotRefusedForSecurity_SoTheRefusalIsAboutTheRole()
    {
        var service = typeof(Brqosh.Infrastructure.Sales.SalesInvoicePostingService);
        var instance = TryConstruct(service)!;
        var operation = PublicOperations(service).First(m => m.Name == "PostAsync");

        TestActor.Viewer();
        var asViewer = await InvokeAndCaptureAsync(instance, operation);
        Assert.True(IsAuthorizationRefusal(asViewer), "المُطّلِع لم يُرفض أمنياً — القياس مكسور.");

        TestActor.Administrator();
        var asAdmin = await InvokeAndCaptureAsync(instance, operation);
        Assert.False(IsAuthorizationRefusal(asAdmin),
            "المديرُ رُفض أمنياً أيضاً — فالرفضُ ليس عن الدور، والاختبارُ لا يقيس الصلاحية.");
    }

    /// <summary>
    /// <b>وحارسٌ ثالث:</b> لا متغيّرَ بيئةٍ يرفع «المُطّلِع». هذا هو العطلُ الذي أُغلق في م2-١
    /// (<c>AutomatedTestBypass</c>)، وهذه البوّابة تمنع عودتَه بأيّ اسمٍ آخر — بخلاف البحث
    /// النصّيّ عن <c>admin123</c> الذي لا يرى إلا اسماً واحداً بعينه.
    /// </summary>
    [Theory]
    [InlineData("BRQOSH_TEST")]
    [InlineData("BRQOSH_ENV")]
    [InlineData("BRQOSH_SKIP_STRESS")]
    [InlineData("BRQOSH_API_ROLE")]
    public void NoEnvironmentVariable_LiftsTheViewerRefusal(string variable)
    {
        var original = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "1");
            TestActor.Viewer();

            Assert.Throws<AccountingException>(SecurityContext.EnsureCanPost);
            Assert.Throws<AccountingException>(SecurityContext.EnsureCanPostAccounting);
            Assert.Throws<AccountingException>(SecurityContext.EnsureAdministrator);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }

    // ── أدواتُ القياس ─────────────────────────────────────────────────────

    private object? TryConstruct(Type service)
    {
        var ctor = service.GetConstructors()
            .FirstOrDefault(c => c.GetParameters().Length == 1
                                 && c.GetParameters()[0].ParameterType == typeof(BrqoshDbContext));
        return ctor?.Invoke([_db]);
    }

    /// <summary>يستدعي العملية بوسائطَ افتراضية ويُعيد ما رُمي — أو <c>null</c> إن لم يُرمَ شيء.</summary>
    private static async Task<Exception?> InvokeAndCaptureAsync(object instance, MethodInfo operation)
    {
        var arguments = operation.GetParameters()
            .Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
            .ToArray();

        try
        {
            if (operation.Invoke(instance, arguments) is Task task) await task;
            return null;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return ex.InnerException;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>هل هذا رفضٌ صادرٌ عن طبقة الصلاحيات — لا انكسارٌ عرضيّ؟</summary>
    private static bool IsAuthorizationRefusal(Exception? thrown) =>
        thrown is AccountingException accounting
        && (accounting.Message.Contains("صلاحية", StringComparison.Ordinal)
            || accounting.Message.Contains("مستخدم مسجّل", StringComparison.Ordinal));

    private static string Shorten(string message) =>
        message.Length <= 90 ? message : message[..90] + "…";
}
