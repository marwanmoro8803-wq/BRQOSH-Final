using Brqosh.Domain.Companies;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>Shared company header/footer for all printable documents.</summary>
public sealed record PrintBranding(
    string CompanyNameAr,
    string VatRegistrationNumber,
    string? CompanyNameEn = null,
    string? AddressAr = null,
    byte[]? LogoImageBytes = null,
    string PrintFontFamily = "Tahoma")
{
    public string EffectiveFontFamily =>
        string.IsNullOrWhiteSpace(PrintFontFamily) ? "Tahoma" : PrintFontFamily;

    public static PrintBranding From(string companyNameAr, string vatNumber) =>
        new(companyNameAr, vatNumber);

    public static PrintBranding FromCompany(Company company)
    {
        var logo = CompanyBrandingStorage.TryReadLogo(company.LogoFilePath);

        // (CEEF-229 · Z-10) الترويسة تُفضّل العنوان الوطنيّ على النصّ الحرّ متى اكتمل.
        //
        // الفاتورة الضريبية تلزمها بيانات عنوان البائع، والنصّ الحرّ لا يفي بها. ومتى أدخل
        // المالك العنوان الوطنيّ صار هو المصدر — فلا يُطالَب بإدخاله مرّتين، ولا تختلف
        // الورقة عن XML المرسَل إلى الهيئة.
        var address = company.NationalAddress.IsComplete
            ? company.NationalAddress.ToPrintLine()
            : company.AddressAr;

        return new(
            company.NameAr,
            company.VatRegistrationNumber,
            company.NameEn,
            address,
            logo,
            CompanyBrandingStorage.NormalizePrintFont(company.PrintFontFamily));
    }

    public TextStyle PageTextStyle(float fontSize = 11) =>
        TextStyle.Default.FontSize(fontSize).FontFamily(EffectiveFontFamily);

    public void RenderHeader(IContainer container, string documentTitleAr, string? documentSubtitleAr = null)
    {
        container.Column(col =>
        {
            if (LogoImageBytes is { Length: > 0 })
                col.Item().AlignCenter().MaxHeight(64).Image(LogoImageBytes);

            col.Item().AlignCenter().Text(CompanyNameAr).Bold().FontSize(16).FontFamily(EffectiveFontFamily);
            if (!string.IsNullOrWhiteSpace(CompanyNameEn))
                col.Item().AlignCenter().Text(CompanyNameEn).FontSize(9).FontFamily(EffectiveFontFamily);
            if (!string.IsNullOrWhiteSpace(VatRegistrationNumber))
                col.Item().AlignCenter().Text($"الرقم الضريبي: {PrintText.Code(VatRegistrationNumber)}").FontSize(10)
                    .FontFamily(EffectiveFontFamily);
            if (!string.IsNullOrWhiteSpace(AddressAr))
                col.Item().AlignCenter().Text(AddressAr).FontSize(9).FontFamily(EffectiveFontFamily);
            col.Item().PaddingTop(6).AlignCenter().Text(documentTitleAr).Bold().FontSize(13)
                .FontFamily(EffectiveFontFamily);
            if (!string.IsNullOrWhiteSpace(documentSubtitleAr))
                col.Item().AlignCenter().Text(documentSubtitleAr).FontSize(9).FontFamily(EffectiveFontFamily);
        });
    }

    public void RenderFooterMeta(IContainer container, string? complianceLine = null)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(complianceLine))
                col.Item().AlignCenter().Text(complianceLine).FontSize(8).FontFamily(EffectiveFontFamily);
            col.Item().AlignCenter().Text($"BRQOSH · {PrintText.Timestamp(DateTime.Now)}").FontSize(7)
                .FontFamily(EffectiveFontFamily);
        });
    }
}
