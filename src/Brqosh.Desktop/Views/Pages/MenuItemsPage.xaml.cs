using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Hospitality;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) <b>قائمةُ الطعام ووصفاتُها — الشاشةُ التي بلا وجودها بقي `ط3-٣أ` نطاقاً بلا باب.</b>
///
/// <para><b>وصنفٌ بلا وصفةٍ يُعرض صريحاً</b>: بيعُه لا يصرف شيئاً، <b>فتكلفتُه صفرٌ
/// وربحُه مئةٌ بالمئة</b> — ويظهر الفرقُ عجزَ جردٍ آخرَ السنة. <b>والعدُّ في العنوان هو
/// ما يجعل النقصَ مرئياً قبل ذلك بسنة.</b></para>
/// </summary>
public partial class MenuItemsPage : UserControl
{
    public record Row(long Id, string Code, string NameAr, string Section, decimal SalePrice, int Components);

    public record ComponentRow(string ProductName, decimal Quantity);

    public MenuItemsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();

        var items = await db.MenuItems.AsNoTracking()
            .Where(m => m.CompanyId == AppServices.CompanyId && m.IsActive)
            .OrderBy(m => m.Code)
            .ToListAsync();

        var rows = items.Select(m => new Row(m.Id, m.Code, m.NameAr, SectionText(m.Section),
            m.SalePrice, m.Components.Count)).ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var withoutRecipe = rows.Count(r => r.Components == 0);
        SummaryText.Text = withoutRecipe == 0
            ? $"أصناف القائمة: {rows.Count:N0} · كلُّها بوصفات."
            : $"أصناف القائمة: {rows.Count:N0} · ⚠️ بلا وصفة: {withoutRecipe:N0} — "
              + "وبيعُها لا يصرف مخزوناً ولا يُقيّد تكلفة، فيظهر الفرق عجزَ جردٍ آخرَ السنة.";

        if (rows.Count > 0 && Grid.SelectedItem is null) Grid.SelectedIndex = 0;
        else await ReloadComponentsAsync();
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await ReloadComponentsAsync();
    }

    private async Task ReloadComponentsAsync()
    {
        if (Grid.SelectedItem is not Row item)
        {
            ComponentsGrid.ItemsSource = null;
            UiTestAutomation.PublishGridCount(ComponentsGrid, 0);
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();

            var menuItem = await db.MenuItems.AsNoTracking().SingleAsync(m => m.Id == item.Id);
            var productIds = menuItem.Components.Select(c => c.ProductId).ToList();

            var products = await db.Products.AsNoTracking().IgnoreQueryFilters()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.NameAr);

            var rows = menuItem.Components.Select(c => new ComponentRow(
                products.TryGetValue(c.ProductId, out var name) ? name : $"#{c.ProductId}",
                c.QuantityPerServing)).ToList();

            ComponentsGrid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(ComponentsGrid, rows.Count);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new MenuItemDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private static string SectionText(MenuSection section) => section switch
    {
        MenuSection.Appetizer => "مقبلات",
        MenuSection.Main => "أطباق رئيسية",
        MenuSection.Dessert => "حلويات",
        MenuSection.Beverage => "مشروبات",
        _ => "أخرى",
    };
}
