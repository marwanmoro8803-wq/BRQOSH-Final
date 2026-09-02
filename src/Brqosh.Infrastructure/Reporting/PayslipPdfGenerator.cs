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
    decimal GosiEmployee)
{
    public decimal GrossPay =>
        BasicSalary + HousingAllowance + TransportAllowance + OtherAllowance;

    public decimal TotalDeductions => GosiEmployee;

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
                page.Size(PageSizes.A5);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle(11));

                page.Header().Element(h => branding.RenderHeader(h, "قسيمة راتب"));

                page.Content().Column(col =>
                {
                    col.Item().Text($"الفترة: {data.PeriodCode}");
                    col.Item().Text($"تاريخ الصرف: {PrintText.Date(data.PayDate)}");

                    col.Item().PaddingTop(10).Text($"الموظف: {data.EmployeeName}").Bold();
                    col.Item().Text($"الرقم الوظيفي: {PrintText.Code(data.EmployeeCode)}");
                    col.Item().Text($"الهوية / الإقامة: {PrintText.Code(data.NationalId)}");
                    col.Item().Text($"المسمى الوظيفي: {data.JobTitle}");
                    if (!string.IsNullOrWhiteSpace(data.Iban))
                        col.Item().Text($"الآيبان: {PrintText.Code(data.Iban)}");

                    col.Item().PaddingTop(12).Text("الاستحقاق").Bold();
                    Line(col, "الراتب الأساسي", data.BasicSalary);
                    Line(col, "بدل السكن", data.HousingAllowance);
                    Line(col, "بدل النقل", data.TransportAllowance);
                    Line(col, "بدلات أخرى", data.OtherAllowance);
                    Line(col, "إجمالي الاستحقاق", data.GrossPay, bold: true);

                    // ‏**والاستقطاعُ يُعرض ولو كان صفراً**: غيابُ السطر يُقرأ «لم يُحسب»،
                    // ‏ووجودُه بصفرٍ يقول «حُسب ولم يُستقطع». والوافدُ حصّتُه صفرٌ نظاماً.
                    col.Item().PaddingTop(10).Text("الاستقطاعات").Bold();
                    Line(col, "التأمينات الاجتماعية", data.GosiEmployee);
                    Line(col, "إجمالي الاستقطاعات", data.TotalDeductions, bold: true);

                    col.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Text("صافي المستحق").Bold().FontSize(13);
                        row.ConstantItem(120).AlignLeft()
                            .Text($"{data.NetPay:N2} ر.س").Bold().FontSize(13);
                    });
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Text("المحاسب").FontSize(10);
                        row.RelativeItem().AlignCenter().Text("الموظف").FontSize(10);
                    });
                    col.Item().Element(f => branding.RenderFooterMeta(f));
                });
            });
        }).GeneratePdf();

    private static void Line(ColumnDescriptor col, string label, decimal amount, bool bold = false) =>
        col.Item().Row(row =>
        {
            var name = row.RelativeItem().Text(label);
            var value = row.ConstantItem(110).AlignLeft().Text($"{amount:N2}");
            if (!bold) return;

            name.Bold();
            value.Bold();
        });
}
