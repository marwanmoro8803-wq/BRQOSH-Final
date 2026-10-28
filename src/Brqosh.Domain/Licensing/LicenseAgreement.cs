using System.Security.Cryptography;
using System.Text;
using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Licensing;

/// <summary>
/// ‏(Phase 7) <b>اتفاقيةُ ترخيصٍ محفوظة</b> — مربوطةٌ بالمنشأة (الطرف الثاني) وبسجلّ التفعيل الذي تُرخِّصه.
/// ‏تحمل اسمَ كلّ طرفٍ ورقمَه الضريبيَّ وسجلَّه التجاريّ، ونصَّ الاتفاقية وبصمتَه، وملفَّ PDF (بايتاتٍ أو مساراً) تولّده الواجهة.
/// ‏<b>لا تُعدَّل بعد حفظها</b>: الحقلُ الوحيدُ الذي يُكتب لاحقاً هو PDF — مرّةً واحدة. ولا سعرَ فيها.
/// </summary>
public class LicenseAgreement
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long LicenseActivationId { get; private set; }
    public string AgreementNumber { get; private set; } = null!;
    public string Edition { get; private set; } = null!;
    public int? MaxCompanies { get; private set; }
    public DateOnly EffectiveDate { get; private set; }

    public string FirstPartyName { get; private set; } = null!;
    public string? FirstPartyVatNumber { get; private set; }
    public string? FirstPartyCommercialRegistration { get; private set; }
    public string? FirstPartyAddress { get; private set; }

    public string SecondPartyName { get; private set; } = null!;
    public string SecondPartyVatNumber { get; private set; } = null!;
    public string? SecondPartyCommercialRegistration { get; private set; }
    public string? SecondPartyAddress { get; private set; }

    public string BodyText { get; private set; } = null!;
    public string BodySha256 { get; private set; } = null!;
    public byte[]? PdfBytes { get; private set; }
    public string? PdfPath { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; } = null!;

    private LicenseAgreement()
    {
    }

    /// <summary>‏رقمٌ ضريبيٌّ سعوديّ: ١٥ رقماً يبدأ بـ3 وينتهي بـ3.</summary>
    public static bool IsSaudiVat(string? vat) =>
        vat is { Length: 15 } && vat.All(char.IsAsciiDigit) && vat[0] == '3' && vat[^1] == '3';

    public static LicenseAgreement Create(long companyId, LicenseActivation activation, string agreementNumber, DateOnly effectiveDate,
        string firstPartyName, string? firstPartyVat, string? firstPartyCr, string? firstPartyAddress,
        string secondPartyName, string secondPartyVat, string? secondPartyCr, string? secondPartyAddress,
        string bodyText, string createdBy, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(firstPartyName))
            throw new AccountingException("بيانات المورّد (الطرف الأول) غير مُعدّة — أدخِل اسم المورّد في الإعدادات.");
        if (!string.IsNullOrWhiteSpace(firstPartyVat) && !IsSaudiVat(firstPartyVat.Trim()))
            throw new AccountingException("الرقم الضريبي للمورّد يجب أن يكون 15 رقماً يبدأ وينتهي بـ3.");
        if (string.IsNullOrWhiteSpace(secondPartyName))
            throw new AccountingException("اسم الطرف الثاني مطلوب لاتفاقية الترخيص.");
        if (!IsSaudiVat(secondPartyVat?.Trim()))
            throw new AccountingException("الرقم الضريبي للطرف الثاني يجب أن يكون 15 رقماً يبدأ وينتهي بـ3.");
        if (string.IsNullOrWhiteSpace(bodyText))
            throw new AccountingException("نصّ الاتفاقية فارغ.");

        return new LicenseAgreement
        {
            CompanyId = companyId,
            LicenseActivationId = activation.Id,
            AgreementNumber = agreementNumber,
            Edition = activation.Edition,
            MaxCompanies = activation.MaxCompanies,
            EffectiveDate = effectiveDate,
            FirstPartyName = firstPartyName.Trim(),
            FirstPartyVatNumber = Clean(firstPartyVat),
            FirstPartyCommercialRegistration = Clean(firstPartyCr),
            FirstPartyAddress = Clean(firstPartyAddress),
            SecondPartyName = secondPartyName.Trim(),
            SecondPartyVatNumber = secondPartyVat!.Trim(),
            SecondPartyCommercialRegistration = Clean(secondPartyCr),
            SecondPartyAddress = Clean(secondPartyAddress),
            BodyText = bodyText,
            BodySha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bodyText))).ToLowerInvariant(),
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy,
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>‏يُرفق PDF الذي ولّدته الواجهة — <b>مرّةً واحدة</b>؛ وبعدها الاتفاقيةُ مختومةٌ كلُّها.</summary>
    public void AttachPdf(byte[]? pdfBytes, string? pdfPath)
    {
        if (PdfBytes is not null || PdfPath is not null)
            throw new AccountingException("اتفاقية الترخيص محفوظة ولا تُعدَّل — ملفّها مُرفقٌ مسبقاً.");
        if ((pdfBytes is null || pdfBytes.Length == 0) && string.IsNullOrWhiteSpace(pdfPath))
            throw new AccountingException("لا ملفّ PDF لإرفاقه باتفاقية الترخيص.");
        PdfBytes = pdfBytes is { Length: > 0 } ? pdfBytes : null;
        PdfPath = string.IsNullOrWhiteSpace(pdfPath) ? null : pdfPath.Trim();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
