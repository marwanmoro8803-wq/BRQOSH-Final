using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) إنشاءُ صنفِ قائمةٍ بوصفته.
///
/// <para><b>والمكوّنُ يُختار من الأصناف المخزنية لا يُكتب نصّاً</b>: الوصفةُ تصرف من
/// المخزون، <b>ونصٌّ حرٌّ لا يُطابق صنفاً لا يصرف شيئاً</b> — فتعود المشكلةُ التي بُنيت
/// الوصفةُ لإغلاقها.</para>
/// </summary>
public partial class MenuItemDialog : Window
{
    public class ComponentRow
    {
        public long ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    public record ProductOption(long Id, string Label);

    private readonly ObservableCollection<ComponentRow> _rows = [];

    public MenuItemDialog()
    {
        InitializeComponent();
        ComponentsGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var products = await db.Products.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId)
                .OrderBy(p => p.NameAr)
                .Select(p => new { p.Id, p.Code, p.NameAr })
                .ToListAsync();

            ProductColumn.ItemsSource = products
                .Select(p => new ProductOption(p.Id, $"{p.Code} — {p.NameAr}"))
                .ToList();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            ComponentsGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (!decimal.TryParse((PriceBox.Text ?? "").Trim(), out var price))
                throw new AccountingException($"سعر البيع غير صالح — ووردَ «{PriceBox.Text}».");

            var item = Brqosh.Domain.Hospitality.MenuItem.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                (MenuSection)Math.Max(0, SectionBox.SelectedIndex), price);

            foreach (var row in _rows.Where(r => r.ProductId > 0 && r.Quantity > 0m))
                item.AddComponent(row.ProductId, row.Quantity);

            await using var db = AppServices.CreateDb();
            db.MenuItems.Add(item);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
