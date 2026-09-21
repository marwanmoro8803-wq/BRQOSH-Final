using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class BomDialog : Window
{
    public record ProductOption(long Id, string Display, string NameAr);

    public class LineRow
    {
        public long ProductId { get; set; }
        public decimal Quantity { get; set; } = 1m;
    }

    private readonly ObservableCollection<LineRow> _rows = [];
    private readonly long? _editBomId;

    public BomDialog()
    {
        InitializeComponent();
        Title = "BOM جديد";
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadProductsAsync();
    }

    public BomDialog(long bomId) : this()
    {
        _editBomId = bomId;
        Title = "تعديل BOM";
        CodeBox.IsReadOnly = true;
        Loaded += async (_, _) => await LoadBomAsync(bomId);
    }

    private async Task LoadBomAsync(long bomId)
    {
        await using var db = AppServices.CreateDb();
        var bom = await db.BillsOfMaterials.SingleAsync(b => b.Id == bomId);
        CodeBox.Text = bom.Code;
        NameBox.Text = bom.NameAr;
        LabourBox.Text = bom.LabourCostPerUnit.ToString(CultureInfo.InvariantCulture);
        OverheadBox.Text = bom.OverheadRatePercent.ToString(CultureInfo.InvariantCulture);
        ProductCombo.SelectedValue = bom.FinishedProductId;
        foreach (var line in bom.Lines)
            _rows.Add(new LineRow { ProductId = line.ComponentProductId, Quantity = line.Quantity });
    }

    private async Task LoadProductsAsync()
    {
        await using var db = AppServices.CreateDb();
        var products = await db.Products
            .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive && p.Type == ProductType.Stockable)
            .OrderBy(p => p.Code)
            .Select(p => new ProductOption(p.Id, $"{p.Code} — {p.NameAr}", p.NameAr))
            .ToListAsync();

        ProductCombo.ItemsSource = products;
        if (products.Count > 0 && ProductCombo.SelectedIndex < 0)
            ProductCombo.SelectedIndex = 0;

        ProductColumn.ItemsSource = products.Select(p => new { p.Id, p.NameAr }).ToList();
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (ProductCombo.SelectedValue is not long productId)
                throw new AccountingException("اختر المنتج النهائي.");

            var lines = _rows.Where(r => r.ProductId > 0 && r.Quantity > 0)
                .Select(r => (r.ProductId, r.Quantity)).ToList();
            if (lines.Count == 0)
                throw new AccountingException("أضف مكوّناً واحداً على الأقل.");

            await using var db = AppServices.CreateDb();

            if (_editBomId is long bomId)
            {
                var bom = await db.BillsOfMaterials.SingleAsync(b => b.Id == bomId);
                bom.Rename(NameBox.Text);
                bom.ReplaceLines(lines);
                bom.SetConversionCosts(ConversionNumber(LabourBox.Text, "أجر العمالة المباشرة"),
                    ConversionNumber(OverheadBox.Text, "نسبة التكاليف الصناعية"));
            }
            else
            {
                var bom = BillOfMaterials.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text, productId);
                foreach (var (pid, qty) in lines)
                    bom.AddLine(pid, qty);
                bom.SetConversionCosts(ConversionNumber(LabourBox.Text, "أجر العمالة المباشرة"),
                    ConversionNumber(OverheadBox.Text, "نسبة التكاليف الصناعية"));
                db.BillsOfMaterials.Add(bom);
            }

            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex is AccountingException ae ? ae.Message : ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٣) يقرأ رقمَ تكلفةِ تحويل — <b>وفارغٌ يعني صفراً</b>،
    /// أمّا نصٌّ ليس رقماً فيُرمى باسمه بدل أن يُقرأ صفراً صامتاً.
    ///
    /// <para><b>وعبر <c>NumericInput</c> لا <c>decimal.TryParse</c> عارياً</b>: أوّلُ
    /// صياغةٍ كتبته عارياً بـ<c>NumberStyles.Any</c>، <b>فأسقطها حارسُ المُحلِّل الواحد</b>
    /// — و«1,5» تُقرأ هناك <b>خمسةَ عشر</b>. فأجرُ وحدةٍ يُكتب بفاصلةٍ عشريّةٍ يصير عشرةَ
    /// أضعافه، <b>ويدخل تكلفةَ كلّ وحدةٍ تُنتَج</b>.</para>
    /// </summary>
    private static decimal ConversionNumber(string? text, string field)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;

        return NumericInput.TryParse(text, out var value)
            ? value
            : throw new AccountingException($"قيمة «{field}» ليست رقماً صحيحاً.");
    }
}
