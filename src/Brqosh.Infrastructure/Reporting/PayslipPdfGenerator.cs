using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>قسيمةُ راتبٍ واحدة — ما يستحقّه الموظّف وما استُقطع منه.</summary>
public record PayslipData(
    string PeriodCode,
    DateOnly PayDate,
    string EmployeeName,
    string EmployeeCode,
    string NationalId,
    string JobTitle,
    string? Iban,
    decimal BasicSalary,
    decimal HousingAllowance,
    decimal TransportAllowance,
    decimal OtherAllowance,
    decimal GosiEmployee,
    // (جاهزيّةُ الإطلاق · ح-٢٤) ‏الغيابُ والسلفة — وكانا غائبَين عن القسيمة وحدَها.
    decimal AbsenceDeduction = 0m,
    decimal AdvanceDeduction = 0m)
{
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٢٤) <b>الاستحقاقُ بعد الغياب — كما يحسبه <c>PayrollLine</c>.</b>
    ///
    /// <para>كان يجمع مكوّناتِ الراتب <b>خامّاً</b>، و<c>PayrollLine.GrossPay</c> محسوبٌ
    /// <b>بعد خصم الغياب</b>. <b>فشهرٌ فيه إجازةٌ بلا أجرٍ يُخرج قسيمةً باستحقاقٍ أعلى
    /// ممّا رُحِّل.</b></para>
    /// </summary>
    public decimal GrossPay =>
        BasicSalary + HousingAllowance + TransportAllowance + OtherAllowance - AbsenceDeduction;

    /// <summary>
    /// <b>والسلفةُ استقطاعٌ كالتأمينات</b> — <c>PayrollLine.NetPay</c> يطرحها،
    /// <b>والقسيمةُ كانت لا تعرفها</b>.
    /// </summary>
    public decimal TotalDeductions => GosiEmployee + AdvanceDeduction;

    /// <summary>
    /// <b>والصافي هنا هو الصافي المُرحَّل والمُحوَّل حرفاً</b> —
    /// <c>GrossPay − GosiEmployee − AdvanceDeduction</c>، وهو نصُّ
    /// <c>PayrollLine.NetPay</c>. <b>واختلافُهما يعني ورقةً تخالف ما يصل الحساب.</b>
    /// </summary>
    public decimal NetPay => GrossPay - TotalDeductions;
}

/// <summary>
/// (CEEF-230 · ط2-٦) <b>قسيمةُ الراتب.</b>
///
/// <para><b>ما كان:</b> لا مستندَ لموظّفٍ من أيّ نوع — لا قسيمةَ ولا تقريرَ ولا سجلّاً
/// فردياً. <b>وشاشةُ الرواتب تعرض المجاميعَ وحدها بعد الترحيل</b>، فلا يستطيع المالكُ أن
/// يُعطي موظّفاً ورقةً تشرح راتبَه.</para>
///
/// <para><b>وهي ليست ترفاً</b>: نظامُ العمل يُلزم صاحبَ العمل ببيانٍ للأجر، والموظّفُ
/// يحتاجها لبنكه وتأشيرته وقرضه. <b>ومنشأةٌ بلا قسيمةٍ تكتبها في وورد كلَّ شهر</b> —
/// وأرقامُها لا تُطابق الدفاتر بالضرورة.</para>
///
/// <para><b>وتُظهر الاستقطاعَ صريحاً لا مطويّاً في الصافي</b>: موظّفٌ يرى «الصافي» وحده
/// لا يعرف كم اقتُطع ولماذا، <b>وأكثرُ خلافات الرواتب يبدأ من رقمٍ لم يُشرَح</b>.</para>
/// </summary>
public static class PayslipPdfGenerator
{
    static PayslipPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Build(PrintBranding branding, PayslipData data) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                // A4: بطاقتا الاستحقاق والاستقطاع جنباً إلى جنب، وفي A5 كانت القسيمةُ تفيض صفحةً ثانية.
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle(10));

                page.Header().Element(h => branding.RenderHeader(h, "قسيمة راتب", $"الفترة {PrintText.Code(data.PeriodCode)}"));

                page.Content().PaddingTop(10).ContentFromRightToLeft().Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Card().Column(info =>
                        {
                            info.Item().Element(c => PrintTheme.LabelValue(c, "الموظف", data.EmployeeName, emphasizeValue: true));
                            info.Item().Element(c => PrintTheme.LabelValue(c, "الرقم الوظيفي", PrintText.Code(data.EmployeeCode)));
                            info.Item().Element(c => PrintTheme.LabelValue(c, "المسمى الوظيفي", data.JobTitle));
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Card().Column(info =>
                        {
                            info.Item().Element(c => PrintTheme.LabelValue(c, "الهوية / الإقامة", PrintText.Code(data.NationalId)));
                            info.Item().Element(c => PrintTheme.LabelValue(c, "تاريخ الصرف", PrintText.Date(data.PayDate)));
                            if (!string.IsNullOrWhiteSpace(data.Iban))
                                info.Item().Element(c => PrintTheme.LabelValue(c, "الآيبان", PrintText.Code(data.Iban)));
                        });
                    });

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Card().Column(earn =>
                        {
                            earn.Item().Element(c => PrintTheme.CardTitle(c, "الاستحقاق"));
                            Line(earn, "الراتب الأساسي", data.BasicSalary);
                            Line(earn, "بدل السكن", data.HousingAllowance);
                            Line(earn, "بدل النقل", data.TransportAllowance);
                            Line(earn, "بدلات أخرى", data.OtherAllowance);
                            // ‏**والغيابُ يُعرَض طرحاً لا يُطوى في الإجمالي**: موظّفٌ يرى أساسيّاً
                            // ‏أعلى من إجماليه يسأل عن الفرق، **وحقُّه أن يقرأه لا أن يُحسبه**.
                            if (data.AbsenceDeduction != 0m)
                                Line(earn, "خصم غياب / إجازة بلا أجر", -data.AbsenceDeduction);
                            Line(earn, "إجمالي الاستحقاق", data.GrossPay, bold: true);
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Card().Column(cut =>
                        {
                            // ‏**والاستقطاعُ يُعرض ولو كان صفراً**: غيابُ السطر يُقرأ «لم يُحسب»،
                            // ‏ووجودُه بصفرٍ يقول «حُسب ولم يُستقطع». والوافدُ حصّتُه صفرٌ نظاماً.
                            cut.Item().Element(c => PrintTheme.CardTitle(c, "الاستقطاعات"));
                            Line(cut, "التأمينات الاجتماعية", data.GosiEmployee);
                            Line(cut, "سداد سلفة", data.AdvanceDeduction);
                            Line(cut, "إجمالي الاستقطاعات", data.TotalDeductions, bold: true);
                        });
                    });

                    col.Item().Element(c => PrintTheme.SummaryBand(c, [("صافي المستحق (ر.س)", PrintText.Money(data.NetPay))]));
                    col.Item().Element(c => PrintTheme.AmountInWords(c, data.NetPay));

                    col.Item().PaddingTop(6).Row(sign =>
                    {
                        // فيقع المحاسبُ أقصى اليمين كما تُقرأ الورقة (المحتوى يُبنى من اليمين).
                        sign.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "المحاسب"));
                        sign.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "الموظف"));
                    });
                });

                page.Footer().Element(f => branding.RenderFooterMeta(f));
            });
        }).GeneratePdf();

    private static void Line(ColumnDescriptor col, string label, decimal amount, bool bold = false) =>
        col.Item().Element(c => PrintTheme.LabelValue(c, label, PrintText.Money(amount), emphasizeValue: bold, labelWidth: 120));
}
