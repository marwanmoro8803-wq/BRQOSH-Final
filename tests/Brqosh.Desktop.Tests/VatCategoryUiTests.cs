using System.IO;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Tax;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-208) الفئة الضريبية في شاشات إدخال المستندات.
///
/// أُضيفت الفئات في النموذج والحساب وXML (CEEF-207)، لكن بلا عمود في الواجهة يظل
/// المستخدم عاجزاً عن اختيار «صفرية» أو «معفاة» — فالميزة مبنيّة غير موصولة.
/// </summary>
public class VatCategoryUiTests
{
    private static readonly string[] TaxDocumentDialogs =
    [
        @"Views\Pages\SalesInvoiceDialog.xaml",
        @"Views\Pages\PurchaseInvoiceDialog.xaml",
        @"Views\Pages\PurchaseOrderDialog.xaml",
        @"Views\Pages\CreditNoteDialog.xaml",
    ];

    private static string DesktopRoot()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"Desktop source root not found: {root}");
        return root;
    }

    private static string Read(string relativePath)
    {
        var path = Path.Combine(DesktopRoot(), relativePath);
        Assert.True(File.Exists(path), $"Expected file missing: {relativePath}");
        return File.ReadAllText(path);
    }

    public static TheoryData<string> Dialogs()
    {
        var data = new TheoryData<string>();
        foreach (var d in TaxDocumentDialogs) data.Add(d);
        return data;
    }

    /// <summary>كل حوار مستند ضريبيّ يعرض عمود الفئة مربوطاً بالمصدر المشترك.</summary>
    [Theory]
    [MemberData(nameof(Dialogs))]
    public void TaxDocumentDialog_ExposesVatCategoryColumn(string dialog)
    {
        var xaml = Read(dialog);

        Assert.Contains("الفئة الضريبية", xaml);
        Assert.Contains("SelectedValueBinding=\"{Binding VatCategory}\"", xaml);
        // مصدر واحد للخيارات — إضافة فئة مستقبلاً تظهر في كل الشاشات دون تعديلها.
        Assert.Contains("ui:VatCategoryOptions.All", xaml);
    }

    /// <summary>واجهة بلا تمرير للقيمة زخرفة — يجب أن تصل الفئة إلى AddLine في كل حوار.</summary>
    [Theory]
    [MemberData(nameof(Dialogs))]
    public void TaxDocumentDialog_PassesCategoryToDomain(string dialog)
    {
        var codeBehind = Read(dialog + ".cs");
        Assert.Contains("row.VatCategory", codeBehind);
    }

    /// <summary>
    /// صف الشبكة مشترك: الحوارات الأربعة تعيد استخدام SalesInvoiceDialog.LineRow،
    /// فالخاصية تُعرَّف مرة واحدة — وهذا ما يجعل الفئة تسري عليها جميعاً.
    /// </summary>
    [Fact]
    public void SharedLineRow_DeclaresVatCategory_Once()
    {
        var owner = Read(@"Views\Pages\SalesInvoiceDialog.xaml.cs");
        Assert.Contains("VatCategory { get; set; }", owner);

        foreach (var dialog in TaxDocumentDialogs.Where(d => !d.Contains("SalesInvoiceDialog")))
            Assert.Contains("SalesInvoiceDialog.LineRow", Read(dialog + ".cs"));
    }

    /// <summary>الخيارات تغطي كل فئات الدومين — فلا فئة تبقى غير قابلة للاختيار.</summary>
    [Fact]
    public void Options_CoverEveryDomainCategory()
    {
        var offered = VatCategoryOptions.All.Select(o => o.Value).ToHashSet();
        foreach (var category in Enum.GetValues<VatCategory>())
            Assert.Contains(category, offered);

        Assert.Equal(VatCategory.Standard, VatCategoryOptions.All[0].Value);   // الافتراضي أولاً
        Assert.All(VatCategoryOptions.All, o => Assert.False(string.IsNullOrWhiteSpace(o.Display)));
    }
}
