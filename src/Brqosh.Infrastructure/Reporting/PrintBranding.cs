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
    string PrintFontFamily = "Tahoma",
    string CurrencyCode = "SAR",
    // ‏(أسئلةُ المالك 2026-10-24) ‏المدقِّق/المراجعُ القانونيّ للمنشأة (من إعداداتها)، ومَن طبع الورقة (مستخدمُ الجلسة).
    string? AuditorName = null,
    string? PreparedBy = null,
    // ‏(أسئلةُ المالك · طباعةٌ بالإنجليزيّة) ‏لغةُ المطبوع: الإنجليزيّةُ LTR. والفاتورةُ الضريبيّةُ تبقى عربيّةً أياً كانت.
    bool English = false)
{
    /// <summary>
    /// ما يُكتب بجوار المبالغ في المطبوع — رمزُ عملة دفاتر المنشأة (ر.س للريال). كان «ر.س» مكتوباً في كلّ مولِّد،
    /// فمنشأةٌ بعملةٍ أخرى كانت تطبع مبالغَها موسومةً بالريال.
    /// </summary>
    public string CurrencyAr => CurrencyCatalog.Short(CurrencyCode, english: PrintLang.English);

    public string EffectiveFontFamily =>
        string.IsNullOrWhiteSpace(PrintFontFamily) ? "Tahoma" : PrintFontFamily;

    public static PrintBranding From(string companyNameAr, string vatNumber) =>
        new(companyNameAr, vatNumber, English: PrintLang.Preferred);

    public static PrintBranding FromCompany(Company company, string? preparedBy = null, bool? english = null)
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
            CompanyBrandingStorage.NormalizePrintFont(company.PrintFontFamily),
            company.FunctionalCurrencyCode,
            company.AuditorName,
            string.IsNullOrWhiteSpace(preparedBy) ? null : preparedBy.Trim(),
            english ?? PrintLang.Preferred);
    }

    /// <summary>سطرُ «أعدّه · المراجع» — <c>null</c> إن لم يُعرف أيٌّ منهما. والمراجعُ يظهر حيث طُلب (<paramref name="withAuditor"/>) وعُرف.</summary>
    public string? PeopleLine(bool withAuditor)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(PreparedBy))
            parts.Add(PrintLang.TF("أعدّه: {0}", PreparedBy));
        if (withAuditor && !string.IsNullOrWhiteSpace(AuditorName))
            parts.Add(PrintLang.TF("المراجع: {0}", AuditorName));
        return parts.Count == 0 ? null : string.Join("   ·   ", parts);
    }

    public TextStyle PageTextStyle(float fontSize = 11) =>
        English
            ? TextStyle.Default.FontSize(fontSize).FontFamily(EffectiveFontFamily).DirectionFromLeftToRight()
            : TextStyle.Default.FontSize(fontSize).FontFamily(EffectiveFontFamily).DirectionFromRightToLeft();

    /// <summary>
    /// اسمُ المنشأة المعروض: بالإنجليزيّة اسمُها الإنجليزيّ إن وُجد (وإلّا العربيّ)، وبالعربيّة العربيّ يعلوه الإنجليزيّ صغيراً.
    /// </summary>
    private string PrimaryName => English && !string.IsNullOrWhiteSpace(CompanyNameEn) ? CompanyNameEn! : CompanyNameAr;

    /// <summary>
    /// ترويسةٌ موحّدة: اسمُ المنشأة وبياناتُها يميناً (والشعارُ إن وُجد أقصى اليمين)، وشارةُ نوع المستند كحليّةً
    /// يساراً، ثمّ خطٌّ كحليٌّ سميكٌ يتبعه خطٌّ ذهبيٌّ رفيع. يستعملها كلُّ مولِّدٍ فيتّسق الشكلُ.
    /// </summary>
    public void RenderHeader(IContainer container, string documentTitleAr, string? documentSubtitleAr = null)
    {
        container.Column(col =>
        {
            // ‏الصفُّ يُبنى من اليسار: للعربيّة (شارةٌ · بياناتٌ · شعارٌ أقصى اليمين) وللإنجليزيّة بالعكس (شعارٌ · بياناتٌ · شارة).
            // ‏لا نعكس الحاويةَ بـ`ContentFromRightToLeft` — فهي تقلب اتجاهَ النصوص داخلها وتُزيح النقطةَ الأخيرة إلى أوّل السطر.
            col.Item().Row(row =>
            {
                void Badge() => row.ConstantItem(150).Column(badge =>
                {
                    badge.Item().Element(c => PrintTheme.TitleBadge(c, documentTitleAr));
                    if (!string.IsNullOrWhiteSpace(documentSubtitleAr))
                        badge.Item().PaddingTop(4).AlignCenter().Text(documentSubtitleAr).FontSize(9)
                            .FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily);
                });

                void Info() => row.RelativeItem().PaddingHorizontal(12).Column(info =>
                {
                    info.Item().AlignStart().Text(PrimaryName).Bold().FontSize(17).FontColor(PrintTheme.Navy)
                        .FontFamily(EffectiveFontFamily);
                    if (!English && !string.IsNullOrWhiteSpace(CompanyNameEn))
                        info.Item().AlignStart().Text(CompanyNameEn).FontSize(9).FontColor(PrintTheme.Muted)
                            .FontFamily(EffectiveFontFamily);
                    if (!string.IsNullOrWhiteSpace(VatRegistrationNumber))
                        info.Item().PaddingTop(2).AlignStart()
                            .Text(PrintLang.TF("الرقم الضريبي: {0}", PrintText.Code(VatRegistrationNumber))).FontSize(9)
                            .FontColor(PrintTheme.Ink).FontFamily(EffectiveFontFamily);
                    if (!string.IsNullOrWhiteSpace(AddressAr))
                        info.Item().AlignStart().Text(AddressAr).FontSize(9).FontColor(PrintTheme.Muted)
                            .FontFamily(EffectiveFontFamily);
                });

                void Logo()
                {
                    if (LogoImageBytes is { Length: > 0 })
                        row.ConstantItem(72).AlignStart().AlignTop().MaxHeight(64).Image(LogoImageBytes).FitArea();
                }

                if (English)
                {
                    Logo();
                    Info();
                    Badge();
                }
                else
                {
                    Badge();
                    Info();
                    Logo();
                }
            });

            col.Item().PaddingTop(8).LineHorizontal(3).LineColor(PrintTheme.Navy);
            col.Item().PaddingTop(1.5f).LineHorizontal(1).LineColor(PrintTheme.Gold);
        });
    }

    /// <summary>
    /// تذييلٌ موحّد: خطٌّ رفيع، ثمّ اسمُ المنشأة يميناً وختمُ الطباعة وسطاً ورقمُ الصفحة يساراً.
    /// سطرُ الامتثال (إن وُجد) فوقه.
    /// </summary>
    /// <param name="showAuditor">
    /// ‏للقوائم والكشوف والتقارير المالية: يُضاف سطرٌ بـ«أعدّه: … · المراجع: …». أمّا الفاتورةُ والسند فلا مراجعَ لهما، ويظهر فيهما
    /// ‏«أعدّه» وحدَه إن عُرف.
    /// </param>
    public void RenderFooterMeta(IContainer container, string? complianceLine = null, bool showAuditor = false)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(complianceLine))
                col.Item().PaddingBottom(3).AlignCenter().Text(complianceLine).FontSize(8)
                    .FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily);

            var people = PeopleLine(showAuditor);
            if (people is not null)
                col.Item().PaddingBottom(3).AlignStart().Text(people).FontSize(8)
                    .FontColor(PrintTheme.Ink).FontFamily(EffectiveFontFamily);

            col.Item().LineHorizontal(0.75f).LineColor(PrintTheme.Line);
            col.Item().PaddingTop(3).Row(row =>
            {
                void Page() => row.RelativeItem().AlignEnd().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily));
                    text.Span(PrintLang.T("صفحة "));
                    text.CurrentPageNumber();
                    text.Span(PrintLang.T(" من "));
                    text.TotalPages();
                });
                void Stamp() => row.RelativeItem(2).AlignCenter().Text($"BRQOSH · {PrintText.Timestamp(DateTime.Now)}").FontSize(7)
                    .FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily);
                void Name() => row.RelativeItem().AlignStart().Text(PrimaryName).FontSize(8)
                    .FontColor(PrintTheme.Muted).FontFamily(EffectiveFontFamily);

                // ‏العربيّةُ: الصفحةُ يساراً والاسمُ يميناً؛ والإنجليزيّةُ بالعكس.
                if (English)
                {
                    Name();
                    Stamp();
                    Page();
                }
                else
                {
                    Page();
                    Stamp();
                    Name();
                }
            });
        });
    }
}
