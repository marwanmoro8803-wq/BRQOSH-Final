namespace Brqosh.Application.Intelligence;

/// <summary>خيارٌ سريعٌ في لوحة المساعد: <c>Label*</c> ما يظهر على الزرّ، و<c>Question*</c> ما يُسأل فعلاً (يُصاغ ليصيب موضوعاً يجيب عنه المساعدُ ببيانات المنشأة).</summary>
public sealed record AssistantPrompt(string LabelAr, string LabelEn, string QuestionAr, string QuestionEn);

/// <summary>
/// (1.0.32) <b>الخياراتُ الأربعةُ في لوحة المساعد تتبع الشاشةَ المفتوحة.</b> كانت أربعةً ثابتةً على كلّ صفحة
/// («كيف أرحّل؟» · «صلاحياتي» · «التدفق النقدي» · «ZATCA / OTP») فلا تعني شيئاً على شاشة الرواتب أو المخزون.
///
/// <para>تُشتقّ من <b>مجموعةِ الشاشة</b> (مجالُها) وعنوانِها: الأوّلُ دائماً «اشرح هذه الشاشة»، والثاني «كيف أعمل فيها» باسمها،
/// والثالثُ والرابعُ سؤالان من مجالها <b>يجيب عنهما المساعدُ ببياناتِ المنشأة الحيّة</b> (مبيعات · ذمم · مخزون · سيولة · ضريبة · تنبيهات)
/// أو بدليل الترحيل/الهيئة/الصلاحيات حيث لا أرقامَ للمجال. وكلُّ مجموعةٍ في الكتالوج مغطّاةٌ باختبار.</para>
/// </summary>
public static class PageAssistantPrompts
{
    public const int Count = 4;

    private enum Domain
    {
        Dashboard, Sales, Crm, Purchasing, Inventory, Treasury, Accounting, FixedAssets, Payroll, EInvoicing, Reports, Oversight,
        Projects, Manufacturing, Pos, RealEstate, Healthcare, Hospitality, Logistics, Program,
    }

    private static readonly Dictionary<string, Domain> DomainOfGroup = new(StringComparer.Ordinal)
    {
        ["المركز التنفيذي"] = Domain.Dashboard,
        ["المشاريع والمقاولات"] = Domain.Projects,
        ["الإنتاج والتصنيع"] = Domain.Manufacturing,
        ["نقاط البيع"] = Domain.Pos,
        ["العقارات والإيجارات"] = Domain.RealEstate,
        ["الرعاية الصحية"] = Domain.Healthcare,
        ["الضيافة"] = Domain.Hospitality,
        ["اللوجستيات"] = Domain.Logistics,
        ["المبيعات والعملاء"] = Domain.Sales,
        ["إدارة علاقات العملاء"] = Domain.Crm,
        ["المشتريات والموردون"] = Domain.Purchasing,
        ["المخزون والمستودعات"] = Domain.Inventory,
        ["الخزينة والبنوك"] = Domain.Treasury,
        ["المحاسبة العامة"] = Domain.Accounting,
        ["الأصول الثابتة"] = Domain.FixedAssets,
        ["الرواتب"] = Domain.Payroll,
        ["الفوترة الإلكترونية"] = Domain.EInvoicing,
        ["التقارير المالية"] = Domain.Reports,
        ["الرقابة والتدقيق"] = Domain.Oversight,
        ["البرنامج"] = Domain.Program,
        ["الإعدادات"] = Domain.Program,
    };

    /// <summary>مجموعاتٌ يعرفها المساعدُ — يقيس الاختبارُ أنّ كلَّ مجموعةٍ في الكتالوج منها.</summary>
    public static IReadOnlyCollection<string> KnownGroups => DomainOfGroup.Keys;

    public static IReadOnlyList<AssistantPrompt> For(string? pageKey, string? groupTitleAr, string? titleAr, string? titleEn)
    {
        var domain = groupTitleAr is not null && DomainOfGroup.TryGetValue(groupTitleAr, out var d) ? d : Domain.Program;
        var nameAr = Pick(titleAr, "هذه الشاشة");
        var nameEn = Pick(titleEn ?? titleAr, "this screen");

        var list = new List<AssistantPrompt>(Count)
        {
            // ‏ملاحظة صياغة: محرّكُ الأسئلة المالي يطابق «ar» و«ذمم» بالاحتواء، فالأسئلةُ تُصاغ بلا «are» ولا «ذمم» حيث يُراد غيرُ الذمم المدينة (يقيسه الاختبار).
            // ‏١) الشاشةُ نفسُها — يطابق «هذه الشاشة» فيُجاب بدليل الشاشة الحاليّة.
            new("اشرح هذه الشاشة", "Explain this screen", "ماذا أفعل في هذه الشاشة؟", "What do I do on this screen?"),
            // ‏٢) كيف أعمل فيها — «كيف» + اسمُها فيُجاب بخطوات العمل فيها.
            new($"كيف أعمل في «{nameAr}»؟", $"How do I work in \"{nameEn}\"?",
                $"كيف أعمل في {nameAr}؟", $"How do I work in {nameEn}?"),
        };
        list.AddRange(Specific(domain));
        return list;
    }

    private static string Pick(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static AssistantPrompt Sales => new("مبيعات الشهر", "This month's sales", "كم مبيعات هذا الشهر؟", "This month's sales total");
    private static AssistantPrompt Receivables => new("ذمم العملاء", "Receivables", "ما ذمم العملاء المتأخرة؟", "What are the overdue customer receivables?");
    private static AssistantPrompt Payables => new("ذمم الموردين", "Payables", "كم أدين للموردين؟", "Which payables fall due soon?");
    private static AssistantPrompt Inventory => new("قيمة المخزون", "Inventory value", "كم قيمة المخزون الآن؟", "What is the inventory value now?");
    private static AssistantPrompt Cash => new("التدفق النقدي", "Cash flow", "ما التدفق النقدي هذا الشهر؟", "What is this month's cash flow?");
    private static AssistantPrompt Vat => new("صافي الضريبة", "Net VAT", "كم صافي ضريبة القيمة المضافة هذا الشهر؟", "What is this month's net VAT?");
    private static AssistantPrompt Alerts => new("التنبيهات", "Alerts", "هل توجد تنبيهات أو شذوذ في الحسابات؟", "Show alerts and any anomaly flags");
    private static AssistantPrompt Summary => new("ملخّص الوضع", "Overview", "أعطني نظرة عامة سريعة", "Give me a quick overview");
    private static AssistantPrompt Posting => new("كيف أرحّل؟", "How to post?", "كيف أرحّل المستند؟", "How do I post a document?");
    private static AssistantPrompt Zatca => new("الفوترة الإلكترونية", "E-invoicing", "كيف أربط ZATCA وأحصل على OTP؟", "How do I connect ZATCA and get an OTP?");
    private static AssistantPrompt Permissions => new("صلاحياتي", "My permissions", "ما صلاحياتي في البرنامج؟", "What are my permissions?");

    private static IEnumerable<AssistantPrompt> Specific(Domain domain) => domain switch
    {
        Domain.Dashboard => [Summary, Alerts],
        Domain.Sales => [Sales, Receivables],
        Domain.Crm => [Sales, Summary],
        Domain.Purchasing => [Payables, Posting],
        Domain.Inventory => [Inventory, Alerts],
        Domain.Treasury => [Cash, Posting],
        Domain.Accounting => [Posting, Alerts],
        Domain.FixedAssets => [Posting, Summary],
        Domain.Payroll => [Permissions, Cash],
        Domain.EInvoicing => [Zatca, Vat],
        Domain.Reports => [Vat, Summary],
        Domain.Oversight => [Permissions, Alerts],
        Domain.Projects => [Receivables, Summary],
        Domain.Manufacturing => [Inventory, Posting],
        Domain.Pos => [Sales, Cash],
        Domain.RealEstate => [Receivables, Cash],
        Domain.Healthcare => [Sales, Receivables],
        Domain.Hospitality => [Sales, Cash],
        Domain.Logistics => [Inventory, Summary],
        _ => [Permissions, Summary],
    };
}
