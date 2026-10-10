using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١١٥ · ‏`order-to-cash-procure-to-pay-inventory#8`) ‏**شاشتا أمر البيع وعرض السعر شبكتا سطور،
/// وصفحةُ العروض تقبل وترفض وتحوّل.**
///
/// ‏<para>كانت الشاشتان سطراً نصّيّاً واحداً (<c>AddLine(description, qty, price)</c>) بلا صنفٍ ولا فئةٍ ضريبيّة،
/// ‏وكانت <c>SalesQuotationsPage</c> لا تقبل ولا ترفض ولا تحوّل. القياسُ بنيويٌّ على المصدر: حلقةٌ على
/// ‏الصفوف تمرّر <c>ProductId</c> والثلاثيَّ الضريبيّ إلى <c>AddLine</c>، وأزرارٌ ثلاثةٌ موصولةٌ بالخدمة.</para>
/// </summary>
public class SalesOrderAndQuotationEntryTests
{
    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        return File.ReadAllText(Path.Combine(root, relativePath));
    }

    private static readonly Regex LoopedAddLine = new(
        @"foreach\s*\(\s*var\s+row\s+in\s+_rows[\s\S]*?\.AddLine\([^;]*row\.ProductId[^;]*row\.VatCategory[^;]*row\.TaxExemptionReasonCode[^;]*row\.TaxExemptionReason\b[^;]*;",
        RegexOptions.CultureInvariant);

    [Theory]
    [InlineData(@"Views\Pages\SalesOrderDialog")]
    [InlineData(@"Views\Pages\SalesQuotationDialog")]
    public void The_dialog_passes_every_rows_product_and_vat_triple_to_AddLine(string dialog)
    {
        Assert.Matches(LoopedAddLine, Read(dialog + ".xaml.cs"));
    }

    [Theory]
    [InlineData(@"Views\Pages\SalesOrderDialog")]
    [InlineData(@"Views\Pages\SalesQuotationDialog")]
    public void The_dialog_is_a_lines_grid_with_product_and_vat_category_columns(string dialog)
    {
        var xaml = Read(dialog + ".xaml");
        Assert.Contains("x:Name=\"LinesGrid\"", xaml);
        Assert.Contains("x:Name=\"ProductColumn\"", xaml);
        Assert.Contains("{Binding ProductId}", xaml);
        Assert.Contains("VatCategoryOptions.All", xaml);
        Assert.Contains("{Binding TaxExemptionReasonCode}", xaml);
        Assert.DoesNotContain("x:Name=\"DescriptionBox\"", xaml);
    }

    [Fact]
    public void The_quotations_page_wires_accept_reject_and_convert()
    {
        var xaml = Read(@"Views\Pages\SalesQuotationsPage.xaml");
        Assert.Contains("Click=\"OnAccept\"", xaml);
        Assert.Contains("Click=\"OnReject\"", xaml);
        Assert.Contains("Click=\"OnConvert\"", xaml);

        var code = Read(@"Views\Pages\SalesQuotationsPage.xaml.cs");
        Assert.Contains(".AcceptAsync(", code);
        Assert.Contains(".RejectAsync(", code);
        Assert.Contains(".CreateFromQuotationAsync(", code);
    }
}
