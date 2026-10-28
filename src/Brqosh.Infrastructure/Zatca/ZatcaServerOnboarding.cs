using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>‏بيئاتُ بوّابة فاتورة الثلاث — ولكلٍّ عنوانُها وقالبُ شهادتها، والشهاداتُ لا تنتقل بين البيئات.</summary>
public enum ZatcaPortal
{
    /// <summary>‏البيئةُ التجريبيّة للمطوّرين — رمزُ OTP ثابتٌ منشور (<see cref="ZatcaServerOnboarding.SandboxTestOtp"/>)، بلا تسجيلٍ لدى الهيئة.</summary>
    Sandbox,
    /// <summary>‏المحاكاة — رقمٌ ضريبيٌّ حقيقيّ وOTP من بوّابة فاتورة للمحاكاة.</summary>
    Simulation,
    /// <summary>‏الإنتاج — OTP من بوّابة فاتورة.</summary>
    Production,
}

public static class ZatcaPortals
{
    public const string GatewayRoot = "https://gw-fatoora.zatca.gov.sa/e-invoicing";

    public static string BaseUrl(ZatcaPortal portal) => portal switch
    {
        ZatcaPortal.Sandbox => GatewayRoot + "/developer-portal",
        ZatcaPortal.Simulation => GatewayRoot + "/simulation",
        _ => GatewayRoot + "/core",
    };

    public static string CertificateTemplate(ZatcaPortal portal) => portal switch
    {
        ZatcaPortal.Sandbox => "TSTZATCA-Code-Signing",
        ZatcaPortal.Simulation => "PREZATCA-Code-Signing",
        _ => "ZATCA-Code-Signing",
    };

    public static ZatcaEnvironment Environment(ZatcaPortal portal) =>
        portal == ZatcaPortal.Production ? ZatcaEnvironment.Production : ZatcaEnvironment.Simulation;

    public static ZatcaPortal? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "sandbox" or "developer-portal" or "developer" or "dev" => ZatcaPortal.Sandbox,
        "simulation" or "sim" => ZatcaPortal.Simulation,
        "production" or "prod" or "core" => ZatcaPortal.Production,
        _ => null,
    };
}

/// <summary>
/// ‏<b>إعدادُ تسجيل خادم الختم</b> — من الإعدادات لا من الكود: <c>Zatca:Onboarding:&lt;Key&gt;</c> (appsettings) أو
/// ‏<c>BRQOSH_ZATCA_&lt;KEY&gt;</c> (متغيّرُ بيئة). المفاتيح: Environment, Otp, CompanyId, Vat, Cr, OrganizationName, Branch,
/// ‏Address, BusinessCategory, InvoiceType, SolutionName, ApiBaseUrl (اختياريّ يتجاوز عنوانَ البيئة).
/// </summary>
public sealed record ZatcaOnboardingOptions(
    long CompanyId,
    ZatcaPortal Portal,
    string? Otp,
    string Vat,
    string? Cr,
    string OrganizationName,
    string Branch,
    string Address,
    string BusinessCategory,
    string InvoiceType = "1100",
    string SolutionName = "BRQOSH-ERP",
    string? ApiBaseUrl = null)
{
    public string EffectiveBaseUrl => string.IsNullOrWhiteSpace(ApiBaseUrl) ? ZatcaPortals.BaseUrl(Portal) : ApiBaseUrl.TrimEnd('/');

    public bool IssuesStandard => InvoiceType.Length == 4 && InvoiceType[0] == '1';
    public bool IssuesSimplified => InvoiceType.Length == 4 && InvoiceType[1] == '1';

    public static ZatcaOnboardingOptions FromConfiguration(Func<string, string?> config)
    {
        string? Get(string key) =>
            NonEmpty(config("Zatca:Onboarding:" + key)) ?? NonEmpty(config("BRQOSH_ZATCA_" + ToEnv(key)));

        var portal = ZatcaPortals.Parse(Get("Environment"))
                     ?? throw new AccountingException("بيئة التسجيل (Zatca:Onboarding:Environment) مطلوبة: sandbox أو simulation أو production.");
        var companyId = long.TryParse(Get("CompanyId"), out var id) && id > 0 ? id : 1;
        var options = new ZatcaOnboardingOptions(
            companyId, portal, Get("Otp"),
            Get("Vat") ?? "", Get("Cr"),
            Get("OrganizationName") ?? "",
            Get("Branch") ?? Get("Address") ?? "",
            Get("Address") ?? "",
            Get("BusinessCategory") ?? "",
            Get("InvoiceType") ?? "1100",
            Get("SolutionName") ?? "BRQOSH-ERP",
            Get("ApiBaseUrl"));
        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (Vat.Length != 15 || !Vat.All(char.IsDigit) || Vat[0] != '3' || Vat[^1] != '3')
            throw new AccountingException("الرقم الضريبي للتسجيل يجب أن يكون 15 رقماً يبدأ وينتهي بـ3.");
        if (!string.IsNullOrWhiteSpace(Cr) && (Cr.Trim().Length != 10 || !Cr.Trim().All(char.IsDigit)))
            throw new AccountingException("السجل التجاري للتسجيل يجب أن يكون 10 أرقام.");
        if (string.IsNullOrWhiteSpace(OrganizationName))
            throw new AccountingException("اسم المنشأة (Zatca:Onboarding:OrganizationName) مطلوب للتسجيل.");
        if (string.IsNullOrWhiteSpace(Address) || string.IsNullOrWhiteSpace(BusinessCategory))
            throw new AccountingException("العنوان المسجّل ونشاط المنشأة مطلوبان للتسجيل.");
        if (InvoiceType.Length != 4 || InvoiceType.Any(c => c is not ('0' or '1')) || !(IssuesStandard || IssuesSimplified))
            throw new AccountingException("نوع الفواتير (InvoiceType) أربعةُ أرقام من 0/1 — مثل 1100 (ضريبية ومبسّطة) أو 0100 (مبسّطة).");
    }

    private static string? NonEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string ToEnv(string key) =>
        string.Concat(key.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToUpperInvariant();
}

public enum ZatcaOnboardingStatus
{
    /// <summary>‏لا OTP في الإعداد لبيئةٍ تشترطه — الخطوةُ الباقية: «أدخِل OTP الحقيقيّ عند التسجيل».</summary>
    AwaitingOtp,
    /// <summary>‏شهادةُ الإنتاج صدرت وحُفظت — خادمُ الختم جاهز.</summary>
    Onboarded,
    /// <summary>‏فحصُ امتثالٍ لم ينجح — شهادةُ الامتثال محفوظة، والتفاصيلُ في الرسالة.</summary>
    ComplianceFailed,
}

public sealed record ZatcaOnboardingResult(
    ZatcaOnboardingStatus Status,
    string Message,
    string? ComplianceRequestId = null,
    int ComplianceChecksPassed = 0,
    int ComplianceChecksTotal = 0,
    string? CertificatePath = null);

/// <summary>
/// ‏(ADR 0003 · Phase 7) <b>تسجيلُ خادم الختم المركزيّ من أوّله لآخره، بالإعداد وحده</b>:
/// ‏مفتاحٌ وCSR ← شهادةُ امتثال (OTP) ← فحوصُ الامتثال لكلّ نوع مستندٍ أعلنه الـCSR ← شهادةُ إنتاج ← حفظُها في إعدادات المنشأة
/// ‏التي يقرؤها <see cref="ZatcaCentralSigner"/>.
///
/// <para>‏<b>ولا ينتظر شيئاً:</b> البيئةُ التجريبيّة تأخذ رمزَها المنشور؛ وبيئةٌ تشترط OTP بلا OTP في الإعداد تردّ
/// ‏<see cref="ZatcaOnboardingStatus.AwaitingOtp"/> — «أدخِل OTP الحقيقيّ عند التسجيل» — بلا طلبٍ ولا استثناء.</para>
/// </summary>
public sealed class ZatcaServerOnboarding(
    BrqoshDbContext db,
    ZatcaCsidOnboardingService? service = null,
    Func<ZatcaOnboardingOptions, ZatcaKeyMaterial>? generateKey = null,
    Func<string, string>? readKeyPem = null)
{
    /// <summary>‏رمزُ OTP الثابتُ للبيئة التجريبيّة للمطوّرين كما في دليل بوّابة المطوّرين.</summary>
    public const string SandboxTestOtp = "123345";

    public const string AwaitingOtpMessage =
        "أدخِل OTP الحقيقيّ عند التسجيل — يُولَّد من بوّابة فاتورة (fatoora.zatca.gov.sa) ويُوضع في Zatca:Onboarding:Otp أو BRQOSH_ZATCA_OTP. (enter real OTP on registration)";

    private readonly ZatcaCsidOnboardingService _service = service ?? new ZatcaCsidOnboardingService();

    public async Task<ZatcaOnboardingResult> RunAsync(ZatcaOnboardingOptions options, CancellationToken ct = default)
    {
        options.Validate();
        var otp = string.IsNullOrWhiteSpace(options.Otp)
            ? options.Portal == ZatcaPortal.Sandbox ? SandboxTestOtp : null
            : options.Otp.Trim();
        if (otp is null)
            return new ZatcaOnboardingResult(ZatcaOnboardingStatus.AwaitingOtp, AwaitingOtpMessage);

        var baseUrl = options.EffectiveBaseUrl;
        var key = (generateKey ?? GenerateKey)(options);
        var compliance = await _service.RequestComplianceCsidAsync(baseUrl, otp, key.CsrBase64, options.CompanyId, ct);

        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == options.CompanyId, ct);
        if (settings is null)
        {
            settings = ZatcaSettings.CreateDefault(options.CompanyId);
            db.ZatcaSettings.Add(settings);
        }
        ZatcaSettingsSecrets.ApplyProtectedCsid(settings, compliance.CertificatePemPath, compliance.PrivateKeyPemPath,
            ZatcaCsidOnboardingService.BuildApiToken(compliance.BinarySecurityToken, compliance.Secret),
            compliance.RequestId, compliance.Secret);
        await db.SaveChangesAsync(ct);

        // ‏فحوصُ الامتثال — بشهادة الامتثال ومفتاحها.
        var certPem = File.ReadAllText(compliance.CertificatePemPath);
        var keyPem = (readKeyPem ?? ZatcaKeyFile.ReadPem)(compliance.PrivateKeyPemPath);
        var samples = ComplianceSamples(options).ToList();
        var passed = 0;
        var failures = new List<string>();
        var previousHash = ZatcaHashHelper.GenesisPreviousInvoiceHash();
        var icv = 1;
        foreach (var (label, data) in samples)
        {
            var unsigned = ZatcaUblInvoiceBuilder.BuildXml(data with
            {
                Icv = icv.ToString(),
                PreviousInvoiceHash = previousHash,
            });
            var signed = ZatcaXmlSigner.SignPemWithResult(unsigned, certPem, keyPem);
            var check = await _service.CheckComplianceInvoiceAsync(baseUrl, compliance.BinarySecurityToken,
                compliance.Secret, signed.InvoiceHashBase64, data.Uuid, signed.SignedXml, ct);
            if (check.Passed) passed++;
            else failures.Add($"{label}: {check.HttpStatus} {check.ValidationStatus} {check.Body}");
            previousHash = signed.InvoiceHashBase64;
            icv++;
        }

        if (failures.Count > 0)
            return new ZatcaOnboardingResult(ZatcaOnboardingStatus.ComplianceFailed,
                "فحوصُ الامتثال لم تكتمل — " + string.Join(" | ", failures),
                compliance.RequestId, passed, samples.Count, compliance.CertificatePemPath);

        var production = await _service.RequestProductionCsidAsync(baseUrl, compliance.RequestId,
            compliance.BinarySecurityToken, compliance.Secret, options.CompanyId, ct);
        var token = ZatcaCsidOnboardingService.BuildApiToken(production.BinarySecurityToken, production.Secret);
        ZatcaSettingsSecrets.ApplyProtectedCsid(settings, production.CertificatePemPath, production.PrivateKeyPemPath,
            token, production.RequestId, production.Secret);
        ZatcaSettingsSecrets.ApplyProtectedUpdate(settings, true, ZatcaPortals.Environment(options.Portal), baseUrl,
            production.CertificatePemPath, production.PrivateKeyPemPath, token);
        await db.SaveChangesAsync(ct);

        return new ZatcaOnboardingResult(ZatcaOnboardingStatus.Onboarded,
            $"صدرت شهادةُ الإنتاج ({options.Portal}) وحُفظت — خادمُ الختم جاهز.",
            compliance.RequestId, passed, samples.Count, production.CertificatePemPath);
    }

    private ZatcaKeyMaterial GenerateKey(ZatcaOnboardingOptions o) =>
        _service.GenerateKeyPairAndCsr(o.CompanyId, o.OrganizationName, o.Vat, o.SolutionName,
            ZatcaPortals.Environment(o.Portal), o.InvoiceType, o.Address, o.BusinessCategory,
            organizationUnit: o.Branch, commonName: $"{o.SolutionName}-{o.Vat}",
            certificateTemplate: ZatcaPortals.CertificateTemplate(o.Portal));

    /// <summary>‏مستندُ عيّنةٍ لكلّ نوعٍ أعلنه الـCSR: فاتورةٌ وإشعارا دائنٍ ومدينٍ — ضريبيّةً (1xxx) ومبسّطةً (x1xx).</summary>
    public static IEnumerable<(string Label, ZatcaUblInvoiceBuilder.InvoiceData Data)> ComplianceSamples(ZatcaOnboardingOptions o)
    {
        var address = new NationalAddress("1234", "King Fahd Road", "Al Olaya", "Riyadh", "12211", "Riyadh", "5678");
        var seller = new ZatcaUblInvoiceBuilder.SellerIdentity(o.OrganizationName, string.IsNullOrWhiteSpace(o.Cr) ? null : o.Cr.Trim(), address);
        var buyer = new ZatcaUblInvoiceBuilder.BuyerIdentity("Compliance Test Buyer", address);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var kinds = new List<(string Prefix, bool Simplified)>();
        if (o.IssuesStandard) kinds.Add(("STD", false));
        if (o.IssuesSimplified) kinds.Add(("SIM", true));

        foreach (var (prefix, simplified) in kinds)
        {
            var invoiceNumber = $"{prefix}-COMPLIANCE-1";
            var invoiceUuid = Guid.NewGuid();
            ZatcaUblInvoiceBuilder.InvoiceData Doc(string number, Guid uuid, bool credit, bool debit) => new(
                number, uuid, today, new TimeOnly(10, 0, 0),
                o.OrganizationName, o.Vat,
                simplified ? null : "Compliance Test Buyer",
                simplified ? null : "399999999900003",
                100m, 15m, 115m,
                [new SalesInvoiceLine("Compliance sample item", 1m, 100m)],
                "1", "", "",
                IsCreditNote: credit, IsDebitNote: debit, IsSimplified: simplified,
                BillingReferenceInvoiceNumber: credit || debit ? invoiceNumber : null,
                BillingReferenceUuid: credit || debit ? invoiceUuid : null,
                IssueReason: credit ? "Returned goods" : debit ? "Price adjustment" : null,
                Seller: seller,
                Buyer: simplified ? null : buyer,
                SupplyDate: today);

            yield return ($"{prefix} invoice", Doc(invoiceNumber, invoiceUuid, false, false));
            yield return ($"{prefix} credit note", Doc($"{prefix}-COMPLIANCE-2", Guid.NewGuid(), true, false));
            yield return ($"{prefix} debit note", Doc($"{prefix}-COMPLIANCE-3", Guid.NewGuid(), false, true));
        }
    }
}
