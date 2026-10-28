using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// عملةُ المستند تُختار من قائمة (<c>ComboBox</c>) لا تُكتب نصّاً حرّاً — في فواتير البيع والشراء وسندات القبض والصرف.
/// </summary>
public class DocumentCurrencyIsAPickerTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }

    [Theory]
    [InlineData("SalesInvoiceDialog")]
    [InlineData("PurchaseInvoiceDialog")]
    [InlineData("PaymentVoucherDialog")]
    [InlineData("ReceiptVoucherDialog")]
    public void TheCurrencyField_IsAComboBox_NotFreeText(string dialog)
    {
        var dir = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages");
        var markup = File.ReadAllText(Path.Combine(dir, dialog + ".xaml"));
        var source = File.ReadAllText(Path.Combine(dir, dialog + ".xaml.cs"));

        Assert.Contains("<ComboBox x:Name=\"CurrencyBox\"", markup);
        Assert.DoesNotContain("<TextBox x:Name=\"CurrencyBox\"", markup);
        Assert.DoesNotContain("CurrencyBox.Text", source);
        Assert.Contains("CurrencyChoices.Fill(CurrencyBox", source);
    }
}
