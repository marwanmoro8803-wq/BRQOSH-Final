using System.IO;
using System.Reflection;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-230 · ط6-٥) <b>خدمةٌ مبنيّةٌ لا يستدعيها زرّ.</b>
///
/// <para><b>ما كان:</b> <c>NavigationReachabilityTests</c> يُثبت أنّ كلَّ بندٍ في القائمة
/// يفتح شاشةً — <b>ولا يستطيع أن يرى خدمةً بلا زرٍّ داخل شاشةٍ قائمة</b>. فبقيت
/// <c>SecurityDepositService</c> مبنيّةً ومقيسةً بالطفرات <b>ولا شيءَ في الواجهة
/// يستدعيها</b>، بعد مرحلةٍ كاملةٍ بُنيت لإغلاق هذا الصنف من العطل بعينه.</para>
///
/// <para><b>والقياسُ نصّيٌّ عمداً</b>: يبحث عن اسم النوع في مصادر الواجهة. <b>وهو لا
/// يُثبت أنّ الزرَّ يعمل</b> — يُثبت أنّ للخدمة مستدعياً في طبقة العرض. <b>وحدُّه
/// مُعلَن</b>: ما وراءه يقيسه تشغيلٌ حيّ لا اختبارُ وحدة.</para>
///
/// <para><b>ولماذا لا يكفي «سيُلاحَظ»</b>: لُوحظ أربعَ عشرةَ مرّةً ثمّ فات في الخامسة
/// عشرة — <b>في البند نفسِه الذي بُني الحارسُ السابق لأجله</b>. فما يُترك للانتباه
/// يسقط، وما يُقاس لا يسقط.</para>
/// </summary>
public class ServiceReachabilityTests
{
    /// <summary>
    /// خدماتٌ <b>لا يُنتظر</b> أن تستدعيها الواجهةُ — ولكلٍّ سببٌ مكتوب.
    ///
    /// <para><b>وإضافةُ سطرٍ هنا قرارٌ يُوقَّع</b>: إمّا الخدمةُ بنيةٌ تحتيةٌ يستدعيها
    /// غيرُها، وإمّا واجهةُ برمجةٍ لا شاشة. <b>وسطرٌ يُكتب لتسكيت الحارس هو العطلُ
    /// نفسُه بثوبٍ جديد.</b></para>
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredNotUiFacing = new(StringComparer.Ordinal)
    {
        // ── (أ) بنيةٌ تحتيةٌ تستدعيها خدمةٌ أخرى، لا زرّ ────────────────────
        // ‏وهذه ليست نقصاً: الواجهةُ تصل إليها **عبر مستدعيها**.
        ["InventoryCostingService"] = "طبقاتُ التكلفة — تستدعيها خدماتُ الترحيل.",
        ["BranchAssignmentService"] = "إسنادُ الفرع — يجري داخل كلّ ترحيل.",
        ["ExchangeRateService"] = "أسعارُ الصرف — تستدعيها خدماتُ التقييم والترجمة.",
        ["UnitConversionService"] = "تحويلُ الوحدات — يجري في ترحيل البيع والشراء وبناء المستند الإلكترونيّ.",
        ["LoginAuditService"] = "سجلُّ الدخول — يكتبه UserAuthService في كلّ محاولة.",
        ["DocumentVersionService"] = "نسخُ المستندات — يكتبها الترحيلُ نفسُه.",
        ["ThreeWayMatchService"] = "المطابقةُ الثلاثية — تجري ضمن ترحيل فاتورة المشتريات.",
        ["CashFlowStatementService"] = "بناءُ قائمة التدفّقات — يستدعيها استعلامُ التقرير، والشاشةُ تفتحه.",
        ["ManualJournalEntryWorkflowService"] = "سيرُ اعتماد القيد اليدويّ — تستدعيه خدمةُ ترحيل القيود.",
        ["DatabaseSchemaService"] = "تهيئةُ المخطّط — تجري عند الإقلاع قبل أيّ شاشة.",
        ["FxClosingTranslationService"] = "ترجمةُ إقفال الفترة — تستدعيها خدمةُ الإقفال، ولا تعمل بعملةٍ واحدة.",

        // ── (ب) واجهةُ برمجةٍ أو أداةٌ خارجية — لا شاشةَ لها بحكم طبيعتها ──
        ["TenantProvisioningService"] = "تهيئةُ المستأجرين — واجهةُ برمجةٍ للمنصّة لا شاشةَ مالك.",
        ["ReportSnapshotService"] = "لقطاتُ التقارير — واجهةُ برمجةٍ يستدعيها التكامل.",
        ["MasterDataGovernanceService"] = "حوكمةُ البيانات الأساسية — واجهةُ برمجة.",
        ["PaymentRunService"] = "دفعاتُ السداد المجمَّعة — واجهةُ برمجة، ولا شاشةَ لها بعد.",
        ["FinancialAssistantService"] = "المساعدُ الماليّ — تصل إليه الواجهةُ عبر طبقة التطبيق لا بالنوع.",

        // ── (ج) **نقصٌ مُقَرٌّ به: مبنيٌّ ومقيسٌ ولا زرَّ يستدعيه** ──────────
        // ‏وهذه ليست تصنيفاً بل **اعترافاً**: المالكُ لا يستطيع تشغيلَ هذه الميزات.
        // ‏وحذفُ السطر هو ما يُثبت أنّ العمل تمّ — لا وصفٌ في تقرير.
        ["BankStatementImportService"] =
            "استيرادُ كشف الحساب البنكيّ — **لا زرَّ يستدعيه**، وشاشةُ المطابقة تعرض ما في "
            + "القاعدة ولا تستورد. فالمطابقةُ تعمل على بياناتٍ لا سبيلَ لإدخالها.",
        ["BankStatementMatcherService"] = "مطابقةُ كشف الحساب آلياً — كسابقتها، بلا زرّ.",
        ["BankStatementDifferencePostingService"] = "ترحيلُ فروق المطابقة — كسابقتها، بلا زرّ.",
        ["CashMovementReportService"] = "تقريرُ حركة النقد — مبنيٌّ ولا شاشةَ تعرضه.",
        ["CsvDataMigrationService"] =
            "ترحيلُ البيانات من ملفّات CSV — **وهو أوّلُ ما يحتاجه عميلٌ ينتقل من نظامٍ آخر**، "
            + "ولا شاشةَ له. فالمنشأةُ تُدخل تاريخَها يدوياً أو لا تنتقل.",
    };

    /// <summary>
    /// كلُّ نوعٍ اسمُه ينتهي بـ<c>Service</c> في طبقة البنية التحتية.
    ///
    /// <para><b>والاسمُ هو المعيار</b> — لا وسمٌ ولا واجهةٌ مشتركة. وهو اصطلاحُ المستودع
    /// نفسِه، <b>وحارسُ الحقن يعتمد عليه كذلك</b>.</para>
    /// </summary>
    private static IReadOnlyList<Type> InfrastructureServices() =>
        [.. typeof(Brqosh.Infrastructure.Persistence.BrqoshDbContext).Assembly
            .GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && !t.IsInterface
                        && t.Name.EndsWith("Service", StringComparison.Ordinal)
                        && t.Namespace is not null
                        && t.Namespace.StartsWith("Brqosh.Infrastructure", StringComparison.Ordinal))
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    /// <summary>
    /// <b>الأسماءُ التي قد تُستدعى بها الخدمة — نوعُها وكلُّ واجهةٍ تُحقّقها.</b>
    ///
    /// <para><b>وهذا ما يمنع اتّهاماً باطلاً</b>: شاشةٌ تطلب <c>IPaymentRunQuery</c> من
    /// الحقن لا تذكر النوعَ المحقِّق أبداً — <b>والبحثُ عن الاسم وحدَه يقول «لا مستدعيَ
    /// لها» وهي مستدعاة</b>. وحارسٌ يتّهم بالباطل يُدرَّب على تجاهله.</para>
    /// </summary>
    private static IEnumerable<string> CallableNames(Type service) =>
        new[] { service.Name }.Concat(service.GetInterfaces()
            .Where(i => i.Namespace is not null
                        && i.Namespace.StartsWith("Brqosh.", StringComparison.Ordinal))
            .Select(i => i.Name));

    /// <summary>
    /// <b>القياسُ المركزيّ: لكلّ خدمةٍ مستدعٍ في طبقة العرض — أو إقرارٌ بسببٍ مكتوب.</b>
    /// </summary>
    [Fact]
    public void EveryInfrastructureService_IsCalledFromTheDesktop()
    {
        var sources = DesktopSourceText();
        if (sources is null) return;   // ‏المصادرُ غيرُ متاحةٍ في هذا التشغيل.

        var unreachable = InfrastructureServices()
            .Where(t => !DeclaredNotUiFacing.ContainsKey(t.Name))
            .Where(t => !CallableNames(t).Any(n => sources.Contains(n, StringComparison.Ordinal)))
            .Select(t => t.Name)
            .ToList();

        Assert.True(unreachable.Count == 0,
            "خدماتٌ مبنيّةٌ لا يستدعيها شيءٌ في الواجهة — والمالكُ لا يصل إليها:\n  "
            + string.Join("\n  ", unreachable));
    }

    /// <summary>
    /// <b>وحارسٌ للحارس: كلُّ إقرارٍ لخدمةٍ قائمةٍ وغيرِ مستدعاة.</b>
    ///
    /// <para>فإقرارٌ يبقى بعد أن صار للخدمة زرٌّ <b>يُخفي أنّ العمل تمّ</b>؛ وإقرارٌ
    /// لخدمةٍ حُذفت <b>أثرُ تنظيفٍ نُسي</b>.</para>
    /// </summary>
    [Fact]
    public void TheDeclaredList_HasNoStaleEntries()
    {
        var sources = DesktopSourceText();
        if (sources is null) return;

        var services = InfrastructureServices().ToDictionary(t => t.Name, StringComparer.Ordinal);

        var stale = DeclaredNotUiFacing.Keys
            .Where(name => !services.TryGetValue(name, out var type)
                           || CallableNames(type).Any(n => sources.Contains(n, StringComparison.Ordinal)))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            "إقراراتٌ بائدة — إمّا صار للخدمة مستدعٍ وإمّا حُذفت:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>نصُّ كلّ مصادر الواجهة — يُقرأ مرّةً ويُبحث فيه.</summary>
    private static string? DesktopSourceText()
    {
        var root = FindRepoDirectory("src/Brqosh.Desktop");
        if (root is null) return null;

        var text = new System.Text.StringBuilder();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            text.Append(StripComments(File.ReadAllText(file)));
        }

        return text.ToString();
    }

    /// <summary>
    /// <b>يُسقط التعليقاتِ قبل البحث — وهذا ليس تجميلاً.</b>
    ///
    /// <para><b>وأخرجته طفرةٌ نجت مرّتين</b>: نُزع كلُّ استدعاءٍ لـ<c>SoftDeletedRowService</c>
    /// من الشاشة <b>وبقي الحارسُ أخضر</b> — لأنّ تعليقَ الشاشة يذكر اسمَ الخدمة.
    /// <b>فكان الحارسُ يقبل ذِكراً في تعليقٍ مكانَ استدعاءٍ في شيفرة</b>: خدمةٌ يُكتب عنها
    /// ولا تُستدعى تمرّ خضراء، وهو بعينه العطلُ الذي بُني لأجله.</para>
    /// </summary>
    private static string StripComments(string source)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(
            source, @"/\*.*?\*/", " ", System.Text.RegularExpressions.RegexOptions.Singleline);

        return System.Text.RegularExpressions.Regex.Replace(
            text, @"//.*?$", " ", System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    private static string? FindRepoDirectory(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}
