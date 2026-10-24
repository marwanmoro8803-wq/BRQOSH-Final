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
        TextStyle.Default.FontSize(fontSize).FontFamily(EffectiveFontFamily).DirectionFromRightToLeft();

    /// <summary>
    /// ترويسةٌ موحّدة: اسمُ المنشأة وبياناتُها يميناً (والشعارُ إن وُجد أقصى اليمين)، وشارةُ نوع المستند كحليّةً
    /// يساراً، ثمّ خطٌّ كحليٌّ سميكٌ يتبعه خطٌّ ذهبيٌّ رفيع. يستعملها كلُّ مولِّدٍ فيتّسق الشكلُ.
    /// </summary>
    public void RenderHeader(IContainer container, string documentTitleAr, string? documentSubtitleAr = null)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(150).Column(badge =>
                {
                    badge.Item().Element(c => PrintTheme.TitleBadge(c, documentTitleAr));
                    if (!string.IsNullOrWhiteSpace(documentSubtitleAr))
                        badge.Item().PaddingTop(4).AlignCenter().Text(documentSubtitleAr).FontSize(9)
                            .FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily);
                });

                row.RelativeItem().PaddingHorizontal(12).Column(info =>
                {
                    info.Item().AlignRight().Text(CompanyNameAr).Bold().FontSize(17).FontColor(PrintTheme.Navy)
                        .FontFamily(EffectiveFontFamily);
                    if (!string.IsNullOrWhiteSpace(CompanyNameEn))
                        info.Item().AlignRight().Text(CompanyNameEn).FontSize(9).FontColor(PrintTheme.Muted)
                            .FontFamily(EffectiveFontFamily);
                    if (!string.IsNullOrWhiteSpace(VatRegistrationNumber))
                        info.Item().PaddingTop(2).AlignRight()
                            .Text($"الرقم الضريبي: {PrintText.Code(VatRegistrationNumber)}").FontSize(9)
                            .FontColor(PrintTheme.Ink).FontFamily(EffectiveFontFamily);
                    if (!string.IsNullOrWhiteSpace(AddressAr))
                        info.Item().AlignRight().Text(AddressAr).FontSize(9).FontColor(PrintTheme.Muted)
                            .FontFamily(EffectiveFontFamily);
                });

                if (LogoImageBytes is { Length: > 0 })
                    row.ConstantItem(72).AlignRight().AlignTop().MaxHeight(64).Image(LogoImageBytes).FitArea();
            });

            col.Item().PaddingTop(8).LineHorizontal(3).LineColor(PrintTheme.Navy);
            col.Item().PaddingTop(1.5f).LineHorizontal(1).LineColor(PrintTheme.Gold);
        });
    }

    /// <summary>
    /// تذييلٌ موحّد: خطٌّ رفيع، ثمّ اسمُ المنشأة يميناً وختمُ الطباعة وسطاً ورقمُ الصفحة يساراً.
    /// سطرُ الامتثال (إن وُجد) فوقه.
    /// </summary>
    public void RenderFooterMeta(IContainer container, string? complianceLine = null)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(complianceLine))
                col.Item().PaddingBottom(3).AlignCenter().Text(complianceLine).FontSize(8)
                    .FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily);

            col.Item().LineHorizontal(0.75f).LineColor(PrintTheme.Line);
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily));
                    text.Span("صفحة ");
                    text.CurrentPageNumber();
                    text.Span(" من ");
                    text.TotalPages();
                });
                row.RelativeItem(2).AlignCenter().Text($"BRQOSH · {PrintText.Timestamp(DateTime.Now)}").FontSize(7)
                    .FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily);
                row.RelativeItem().AlignRight().Text(CompanyNameAr).FontSize(8)
                    .FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily);
            });
        });
    }
}
