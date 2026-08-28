using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Zatca;

/// <summary>إعدادات ZATCA على مستوى الشركة — الشهادة والمفتاح يُخزَّنان كمسارات ملفات فقط (لا تُحفظ في قاعدة البيانات).</summary>
public class ZatcaSettings
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public bool IsEnabled { get; private set; }
    public ZatcaEnvironment Environment { get; private set; } = ZatcaEnvironment.Simulation;

    /// <summary>عنوان API (مثل https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal).</summary>
    public string ApiBaseUrl { get; private set; }

    public string CertificateFilePath { get; private set; }
    public string PrivateKeyFilePath { get; private set; }

    /// <summary>رمز المصادقة — يُخزَّن محلياً؛ في الإنتاج يُفضَّل DPAPI لاحقاً.</summary>
    public string ApiToken { get; private set; }

    /// <summary>معرّف طلب CSID من بيئة الامتثال — مطلوب لطلب شهادة الإنتاج.</summary>
    public string ComplianceRequestId { get; private set; }

    /// <summary>السرّ المُرجَع مع شهادة CSID — يُستخدم في Basic Auth مع الشهادة.</summary>
    public string CsidSecret { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    // EF Core
    private ZatcaSettings()
    {
        ApiBaseUrl = null!;
        CertificateFilePath = null!;
        PrivateKeyFilePath = null!;
        ApiToken = null!;
        ComplianceRequestId = null!;
        CsidSecret = null!;
    }

    public static ZatcaSettings CreateDefault(long companyId) => new()
    {
        CompanyId = companyId,
        IsEnabled = false,
        Environment = ZatcaEnvironment.Simulation,
        ApiBaseUrl = "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal",
        CertificateFilePath = "",
        PrivateKeyFilePath = "",
        ApiToken = "",
        ComplianceRequestId = "",
        CsidSecret = "",
        UpdatedAtUtc = DateTime.UtcNow,
    };

    public void Update(bool isEnabled, ZatcaEnvironment environment, string apiBaseUrl,
        string certificatePath, string privateKeyPath, string apiToken)
    {
        IsEnabled = isEnabled;
        Environment = environment;
        ApiBaseUrl = (apiBaseUrl ?? "").Trim().TrimEnd('/');
        CertificateFilePath = (certificatePath ?? "").Trim();
        PrivateKeyFilePath = (privateKeyPath ?? "").Trim();
        ApiToken = (apiToken ?? "").Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ApplyCsid(string certificatePath, string privateKeyFilePath, string apiToken,
        string complianceRequestId, string csidSecret)
    {
        CertificateFilePath = certificatePath.Trim();
        PrivateKeyFilePath = privateKeyFilePath.Trim();
        ApiToken = apiToken.Trim();
        ComplianceRequestId = (complianceRequestId ?? "").Trim();
        CsidSecret = (csidSecret ?? "").Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>تطبيق قيم افتراضية من قالب الدولة عند التهيئة الأولى.</summary>
    public void ApplyTemplateDefaults(bool enabled, string apiBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(apiBaseUrl))
            ApiBaseUrl = apiBaseUrl.Trim().TrimEnd('/');
        // (CEEF-229 · م0-١٠) لا يُفرض وضع المحاكاة عند التفعيل.
        // كان كلّ تفعيلٍ من القالب يُرجِع البيئة إلى المحاكاة — حتى لو ضبطها المالك إنتاجاً —
        // فتُعلَّم الفواتير مقبولةً بلا إرسال. البيئة قرارُ المنشأة لا أثرٌ جانبيّ للتفعيل.
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
