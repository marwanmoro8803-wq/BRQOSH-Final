using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Shared;

namespace Brqosh.Domain.Companies;

/// <summary>المنشأة — الكيان الأعلى في النظام؛ كل البيانات معزولة بـ CompanyId (multi-company).</summary>
public class Company
{
    public long Id { get; private set; }
    public string NameAr { get; private set; }

    /// <summary>اسم المنشأة بالإنجليزية — يظهر في رأس المطبوعات.</summary>
    public string? NameEn { get; private set; }

    /// <summary>عنوان المنشأة — يظهر في رأس المطبوعات (نصّ حرّ، لا يصلح للفوترة الإلكترونية).</summary>
    public string? AddressAr { get; private set; }

    /// <summary>
    /// (CEEF-229 · Z-10) الاسم المسجَّل في السجل التجاريّ — <c>cbc:RegistrationName</c>.
    /// قد يختلف عن الاسم التجاريّ المعروض، والفاتورة الإلكترونية تلزم المسجَّل.
    /// </summary>
    public string? RegistrationName { get; private set; }

    /// <summary>رقم السجل التجاريّ — <c>cac:PartyIdentification/cbc:ID schemeID="CRN"</c>.</summary>
    public string? CommercialRegistrationNumber { get; private set; }

    /// <summary>
    /// (CEEF-229 · Z-10) العنوان الوطنيّ بحقوله المفكَّكة — <c>cac:PostalAddress</c>.
    /// كان غائباً من النموذج كلياً، فيستحيل توليد الفاتورة الإلكترونية مهما صحّ المولّد.
    /// </summary>
    public NationalAddress NationalAddress { get; private set; } = NationalAddress.Empty;

    /// <summary>مسار ملف الشعار المحلي (AppData) — يُطبَّق على كل المخرجات.</summary>
    public string? LogoFilePath { get; private set; }

    /// <summary>خط الطباعة العربي — QuestPDF FontFamily.</summary>
    public string PrintFontFamily { get; private set; } = "Tahoma";

    /// <summary>الرقم الضريبي (15 خانة) — مطلوب لتفعيل ZATCA.</summary>
    public string VatRegistrationNumber { get; private set; }

    public BusinessProfile Profile { get; private set; }
    public InventoryCostingMethod CostingMethod { get; private set; } = InventoryCostingMethod.WeightedAverage;

    /// <summary>عملة العرض الافتراضية (ISO 4217) — SAR للسوق السعودي.</summary>
    public string BaseCurrencyCode { get; private set; } = "SAR";

    /// <summary>عملة القياس المحاسبي — تُستخدم في التقارير الموحدة.</summary>
    public string FunctionalCurrencyCode { get; private set; } = "SAR";

    /// <summary>معرّف المستأجر — أساس multi-tenant (مرحلة 7).</summary>
    public Guid TenantId { get; private set; }

    /// <summary>مجموعة التقارير الموحّدة — اختياري.</summary>
    public long? CompanyGroupId { get; private set; }

    public CompanyOperationalMode OperationalMode { get; private set; } = CompanyOperationalMode.Active;

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · <c>accounting-core#6</c> · قرارُ المالك 2026-10-18) <b>حدُّ اعتماد القيد اليدويّ</b> — قيدٌ يدويٌّ
    /// ‏مجموعُ مدينه فوقه لا يُرحَّل إلّا بسلسلة موافقاتٍ مكتملة، ومعتمدُها غيرُ منشئه. والافتراضُ ١٠٬٠٠٠ ريال يغيّره المدير.
    /// </summary>
    public decimal ManualJournalApprovalThreshold { get; private set; } = DefaultManualJournalApprovalThreshold;

    public const decimal DefaultManualJournalApprovalThreshold = 10_000m;

    public DateTime CreatedAtUtc { get; private set; }

    // EF Core
    private Company()
    {
        NameAr = null!;
        VatRegistrationNumber = null!;
        BaseCurrencyCode = "SAR";
        FunctionalCurrencyCode = "SAR";
        TenantId = Guid.NewGuid();
    }

    private Company(string nameAr, string vatRegistrationNumber, BusinessProfile profile)
    {
        NameAr = nameAr;
        VatRegistrationNumber = vatRegistrationNumber;
        Profile = profile;
        CostingMethod = InventoryCostingMethod.WeightedAverage;
        BaseCurrencyCode = "SAR";
        FunctionalCurrencyCode = "SAR";
        TenantId = Guid.NewGuid();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Company Create(string nameAr, string vatRegistrationNumber, BusinessProfile profile)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("اسم المنشأة مطلوب.", nameof(nameAr));

        var vat = (vatRegistrationNumber ?? string.Empty).Trim();
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(AsciiDigits.Is)))
            throw new ArgumentException("الرقم الضريبي يجب أن يتكون من 15 رقماً.", nameof(vatRegistrationNumber));

        return new Company(nameAr.Trim(), vat, profile);
    }

    /// <summary>إنشاء شركة لمستأجر SaaS معرف مسبقاً (O-09 stub).</summary>
    public static Company CreateForTenant(Guid tenantId, string nameAr, string vatRegistrationNumber, BusinessProfile profile)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("معرّف المستأجر مطلوب.", nameof(tenantId));

        var company = Create(nameAr, vatRegistrationNumber, profile);
        company.SetTenantIdForProvisioning(tenantId);
        return company;
    }

    internal void SetTenantIdForProvisioning(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("معرّف المستأجر غير صالح.", nameof(tenantId));
        TenantId = tenantId;
    }

    public void Update(string nameAr, string vatRegistrationNumber)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("اسم المنشأة مطلوب.", nameof(nameAr));

        var vat = (vatRegistrationNumber ?? string.Empty).Trim();
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(AsciiDigits.Is)))
            throw new ArgumentException("الرقم الضريبي يجب أن يتكون من 15 رقماً.", nameof(vatRegistrationNumber));

        NameAr = nameAr.Trim();
        VatRegistrationNumber = vat;
    }

    public void UpdateBranding(string? nameEn, string? addressAr, string printFontFamily)
    {
        NameEn = TrimOrNull(nameEn);
        AddressAr = TrimOrNull(addressAr);
        var font = (printFontFamily ?? string.Empty).Trim();
        PrintFontFamily = font.Length > 0 ? font : "Tahoma";
    }

    /// <summary>
    /// (CEEF-229 · Z-10) بيانات المنشأة النظامية للفوترة الإلكترونية.
    /// تُحفَظ كما هي؛ اكتمالُها يُفحَص عند تفعيل التكامل لا عند الإدخال — فالمستخدم يملأ
    /// على دفعات، ومنعُ الحفظ الجزئيّ يعني إعادة الإدخال من الصفر في كل مرّة.
    /// </summary>
    public void UpdateRegulatoryIdentity(
        string? registrationName, string? commercialRegistrationNumber, NationalAddress address)
    {
        RegistrationName = string.IsNullOrWhiteSpace(registrationName) ? null : registrationName.Trim();
        CommercialRegistrationNumber = string.IsNullOrWhiteSpace(commercialRegistrationNumber)
            ? null : commercialRegistrationNumber.Trim();
        NationalAddress = address;
    }

    /// <summary>
    /// ما ينقص المنشأة لتُصدر فاتورة إلكترونية مقبولة — رسائل عربية تُعرض كما هي.
    /// فارغةٌ تعني جاهزيةً من ناحية بيانات البائع.
    /// </summary>
    public IReadOnlyList<string> RegulatoryGaps()
    {
        var gaps = new List<string>();

        if (string.IsNullOrWhiteSpace(VatRegistrationNumber))
            gaps.Add("الرقم الضريبي مطلوب للفاتورة الإلكترونية.");
        if (string.IsNullOrWhiteSpace(RegistrationName))
            gaps.Add("الاسم المسجَّل في السجل التجاري مطلوب للفاتورة الإلكترونية.");
        if (string.IsNullOrWhiteSpace(CommercialRegistrationNumber))
            gaps.Add("رقم السجل التجاري مطلوب للفاتورة الإلكترونية.");

        gaps.AddRange(NationalAddress.Problems);
        return gaps;
    }

    /// <summary>هل بيانات البائع مكتملة للفوترة الإلكترونية؟</summary>
    public bool IsReadyForEInvoicing => RegulatoryGaps().Count == 0;

    public void SetLogoFilePath(string? path) =>
        LogoFilePath = TrimOrNull(path);

    /// <summary>
    /// (أسئلةُ المالك 2026-10-24) <b>اسمُ المدقِّق/المراجع القانونيّ</b> للمنشأة — اختياريّ. يُطبع في تذييل القوائم والكشوف
    /// والتقارير («المراجع: …») فتحمل الورقةُ مَن يراجعها، لا مَن طبعها وحدَه.
    /// </summary>
    public string? AuditorName { get; private set; }

    public const int AuditorNameMaxLength = 120;

    public void SetAuditor(string? name)
    {
        var value = TrimOrNull(name);
        if (value is not null && value.Length > AuditorNameMaxLength)
            throw new ArgumentException($"اسم المدقق لا يتجاوز {AuditorNameMaxLength} حرفاً.", nameof(name));
        if (value is not null && value.Any(char.IsControl))
            throw new ArgumentException("اسم المدقق لا يحوي محارف تحكّم.", nameof(name));
        AuditorName = value;
    }

    public void SetCostingMethod(InventoryCostingMethod method) => CostingMethod = method;

    public void SetCurrencies(string baseCurrencyCode, string functionalCurrencyCode)
    {
        BaseCurrencyCode = NormalizeCurrency(baseCurrencyCode);
        FunctionalCurrencyCode = NormalizeCurrency(functionalCurrencyCode);
    }

    public void AssignCompanyGroup(long? groupId) =>
        CompanyGroupId = groupId is > 0 ? groupId : null;

    public void SetOperationalMode(CompanyOperationalMode mode) => OperationalMode = mode;

    /// <summary>حدُّ اعتماد القيد اليدويّ — صفرٌ يعني أنّ كلَّ قيدٍ يدويٍّ يُعتمد، ولا سالب.</summary>
    public void SetManualJournalApprovalThreshold(decimal threshold)
    {
        if (threshold < 0m)
            throw new Accounting.AccountingException("حد اعتماد القيد اليدوي لا يكون سالباً.");
        ManualJournalApprovalThreshold = Math.Round(threshold, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>تغيير ملف العمل — يُستخدم في تدقيق UiAudit متعدد الملفات.</summary>
    public void UpdateBusinessProfile(BusinessProfile profile) => Profile = profile;

    public void EnsureCanPost()
    {
        if (OperationalMode == CompanyOperationalMode.Archived)
            throw new AccountingException("المنشأة مؤرشفة — الترحيل محظور.");
        if (OperationalMode == CompanyOperationalMode.ReadOnly)
            throw new AccountingException("المنشأة في وضع القراءة فقط — الترحيل محظور.");
    }

    /// <summary>رمزُ العملة مطبَّعاً (ثلاثةُ أحرفٍ كبيرة) ومن الكتالوج — يرمي إن لم يكن عملةً معروفة.</summary>
    public static string NormalizeCurrency(string code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (c.Length != 3 || !c.All(char.IsLetter))
            throw new ArgumentException("رمز العملة يجب أن يكون ISO 4217 من 3 أحرف.", nameof(code));
        // ‏ثلاثةُ أحرفٍ لا تكفي: «ABC» ليست عملة، وتُكتب بجوار كلِّ مبلغٍ في الشاشات والمطبوعات.
        if (!CurrencyCatalog.IsKnown(c))
            throw new ArgumentException($"رمز العملة «{c}» غير معروف — اختر عملةً من قائمة ISO 4217.", nameof(code));
        return c;
    }

    private static string? TrimOrNull(string? value)
    {
        var t = (value ?? string.Empty).Trim();
        return t.Length > 0 ? t : null;
    }
}
