using System.Text.Json;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Licensing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>‏بياناتُ طرفٍ في الاتفاقية.</summary>
public sealed record LicenseAgreementPartyInfo(string Name, string? CommercialRegistration, string? VatNumber, string? Address);

/// <summary>
/// ‏الطرفُ الأول (المورّد) من إعدادات التطبيق — الملفُّ نفسُه الذي تكتبه شاشةُ الإعدادات في الواجهة:
/// ‏<c>%LOCALAPPDATA%/Brqosh/vendor-identity.json</c> (‏Name, CommercialRegistration, VatNumber, Address). قراءةٌ فقط.
/// </summary>
public static class LicenseAgreementVendorSettings
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Brqosh", "vendor-identity.json");

    public static LicenseAgreementPartyInfo? Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return null;
        try
        {
            var v = JsonSerializer.Deserialize<LicenseAgreementPartyInfo>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return v is null || string.IsNullOrWhiteSpace(v.Name) ? null : v;
        }
        catch (JsonException ex)
        {
            throw new AccountingException($"ملفّ بيانات المورّد تالف ({path}): {ex.Message}");
        }
    }
}

/// <summary>
/// ‏(Phase 7) <b>توليدُ اتفاقية الترخيص وحفظُها</b> — الجانبُ المحاسبيّ/القاعديّ. الواجهةُ (BRQOSH UI) تولّد PDF من النصّ المحفوظ
/// ‏ثمّ تُرفقه بـ<see cref="AttachPdfAsync"/>. كلُّ حفظٍ وإرفاقٍ يُدقَّق، والسطرُ لا يُعدَّل ولا يُحذف (حارسُ القاعدة + الصلاحيات).
/// </summary>
public sealed class LicenseAgreementService(BrqoshDbContext db, Func<LicenseAgreementPartyInfo?>? vendor = null, TimeProvider? clock = null)
{
    public const string CreateAction = "LicenseAgreement.Create";
    public const string AttachPdfAction = "LicenseAgreement.AttachPdf";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>
    /// ‏يُنشئ الاتفاقيةَ للمنشأة <paramref name="companyId"/> على آخر تفعيلٍ مسجَّل (<c>license_activations</c>) ويحفظها.
    /// ‏الطرفُ الأول من إعدادات المورّد؛ والسجلُّ التجاريُّ والعنوانُ للطرف الثاني من بيانات المنشأة.
    /// </summary>
    public async Task<LicenseAgreement> GenerateAndSaveAsync(long companyId, string secondPartyName, string secondPartyVatNumber,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct)
                      ?? throw new AccountingException($"المنشأة {companyId} غير موجودة.");
        var activation = await db.LicenseActivations.OrderByDescending(a => a.ActivatedAtUtc).ThenByDescending(a => a.Id)
                             .FirstOrDefaultAsync(ct)
                         ?? throw new AccountingException("لا تفعيلَ ترخيصٍ مسجَّلاً — فعِّل البرنامج أولاً ثمّ ولِّد الاتفاقية.");
        var first = (vendor ?? (() => LicenseAgreementVendorSettings.Load()))()
                    ?? throw new AccountingException("بيانات المورّد (الطرف الأول) غير مُعدّة — أدخِلها في الإعدادات.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var effective = DateOnly.FromDateTime(now);
        var count = await db.LicenseAgreements.CountAsync(a => a.CompanyId == companyId, ct);
        var number = $"LA-{companyId}-{effective:yyyyMMdd}-{count + 1:000}";
        var secondCr = company.CommercialRegistrationNumber;
        var a = company.NationalAddress;
        var secondAddress = a is null || a == Brqosh.Domain.Companies.NationalAddress.Empty
            ? null
            : string.Join("، ", new[] { $"{a.BuildingNumber} {a.StreetName}".Trim(), a.DistrictName, $"{a.CityName} {a.PostalZone}".Trim() }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        var body = LicenseAgreementTemplate.Render(new LicenseAgreementTemplate.Fields(
            first, new LicenseAgreementPartyInfo(secondPartyName?.Trim() ?? "", secondCr, secondPartyVatNumber?.Trim(), secondAddress),
            number, effective, activation.Edition, activation.MaxCompanies));

        var agreement = LicenseAgreement.Create(companyId, activation, number, effective,
            first.Name, first.VatNumber, first.CommercialRegistration, first.Address,
            secondPartyName ?? "", secondPartyVatNumber ?? "", secondCr, secondAddress,
            body, SecurityContext.CurrentUsername ?? "system", now);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            db.LicenseAgreements.Add(agreement);
            await db.SaveChangesAsync(token);
            await new AuditService(db).LogAsync(companyId, SecurityContext.CurrentUsername ?? "system", CreateAction,
                nameof(LicenseAgreement), agreement.Id.ToString(),
                $"{number} · activation {activation.Id} · {agreement.SecondPartyVatNumber} · sha256 {agreement.BodySha256}", ct: token);
        }, ct);
        return agreement;
    }

    /// <summary>‏يُرفق PDF الاتفاقية (بايتاتٍ أو مساراً) — مرّةً واحدة، ويُدقَّق.</summary>
    public async Task AttachPdfAsync(long agreementId, byte[]? pdfBytes, string? pdfPath, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        var agreement = await db.LicenseAgreements.FirstOrDefaultAsync(a => a.Id == agreementId, ct)
                        ?? throw new AccountingException($"اتفاقية الترخيص {agreementId} غير موجودة.");
        agreement.AttachPdf(pdfBytes, pdfPath);
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await db.SaveChangesAsync(token);
            await new AuditService(db).LogAsync(agreement.CompanyId, SecurityContext.CurrentUsername ?? "system", AttachPdfAction,
                nameof(LicenseAgreement), agreement.Id.ToString(),
                pdfPath is { Length: > 0 } ? $"path {pdfPath}" : $"bytes {pdfBytes?.Length ?? 0}", ct: token);
        }, ct);
    }
}

/// <summary>‏نصُّ الاتفاقية (مسودةٌ للمراجعة القانونية — صفحةُ Notion «مسودة اتفاقية الترخيص مع بيانات المورّد»). بلا أسعار.</summary>
public static class LicenseAgreementTemplate
{
    public sealed record Fields(LicenseAgreementPartyInfo First, LicenseAgreementPartyInfo Second, string Number,
        DateOnly EffectiveDate, string Edition, int? MaxCompanies);

    public static string Render(Fields f)
    {
        static string V(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();
        var edition = string.IsNullOrWhiteSpace(f.Edition) ? "—" : char.ToUpperInvariant(f.Edition[0]) + f.Edition[1..];
        return $"""
            اتفاقية ترخيص استخدام برنامج BRQOSH

            الطرف الأول (المورّد): {V(f.First.Name)}، سجل تجاري رقم {V(f.First.CommercialRegistration)}، رقم ضريبي {V(f.First.VatNumber)}، العنوان الوطني {V(f.First.Address)}.
            الطرف الثاني (العميل): {V(f.Second.Name)}، سجل تجاري رقم {V(f.Second.CommercialRegistration)}، رقم ضريبي {V(f.Second.VatNumber)}، العنوان {V(f.Second.Address)}.
            تاريخ النفاذ: {f.EffectiveDate:yyyy-MM-dd}  رقم الترخيص: {f.Number}  الفئة: {edition}  عدد الشركات: {(f.MaxCompanies?.ToString() ?? "غير محدود")}

            1. منح الترخيص
            يمنح المورّد العميلَ ترخيصاً غير حصري وغير قابل للتحويل لاستخدام البرنامج داخل منشأته ضمن الفئة وعدد الشركات المحددة أعلاه.

            2. القيود
            لا يحق للعميل نسخ البرنامج لغير النسخ الاحتياطي، أو تفكيكه أو الهندسة العكسية له، أو تأجيره أو إعادة بيعه، أو تجاوز حدود الترخيص أو آلية التفعيل.

            3. الملكية
            يبقى البرنامج وحقوق الملكية الفكرية للمورّد. بيانات العميل ملك للعميل وحده.

            4. الرسوم والدفع
            حسب ما يتفق عليه الطرفان كتابةً خارج هذه الاتفاقية. تُضاف ضريبة القيمة المضافة حسب النظام، وتُصدر فاتورة ضريبية إلكترونية لكل دفعة.

            5. الدعم والتحديثات
            حسب ملحق الدعم. التحديثات التنظيمية (ZATCA وضريبة القيمة المضافة) مشمولة طوال سريان الدعم.

            6. حماية البيانات
            يلتزم الطرفان بنظام حماية البيانات الشخصية (PDPL). لا يصل المورّد لبيانات العميل إلا بإذنه ولغرض الدعم فقط. البرنامج يتيح تصدير بيانات الشخص وإخفاءها.

            7. الالتزام التنظيمي
            يوفّر البرنامج أدوات الفوترة الإلكترونية ومتطلبات هيئة الزكاة والضريبة والجمارك، ويبقى العميل مسؤولاً عن صحة بياناته وإقراراته.

            8. الضمان وحدود المسؤولية
            يضمن المورّد أن البرنامج يعمل جوهرياً وفق وثائقه للمدة المتفق عليها من التسليم. لا تتجاوز مسؤولية المورّد مجموع ما دفعه العميل خلال الاثني عشر شهراً السابقة، ولا يُسأل عن الأضرار غير المباشرة أو فوات الربح.

            9. المدة والإنهاء
            يحق لأي طرف الإنهاء عند الإخلال الجوهري إذا لم يُصحَّح خلال 30 يوماً من الإشعار. عند الإنهاء يتوقف الاستخدام ويحق للعميل تصدير بياناته.

            10. القانون الواجب التطبيق
            تخضع الاتفاقية لأنظمة المملكة العربية السعودية.

            التوقيعات
            المورّد: ____________________    العميل: ____________________

            (مسودة — تُراجع قانونياً قبل الاستخدام.)
            """;
    }
}
