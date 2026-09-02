using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٣) <b>وحداتُ القياس — الشاشةُ التي بلا وجودها بقي `ط1-١` وعداً.</b>
///
/// <para><b>ما كان:</b> الوحدةُ نصٌّ حرٌّ على الصنف، <b>وكلُّ سطرِ فاتورةٍ يُصدَّر للهيئة
/// برمز «PCE» — «حبّة»</b> مهما كان المُباع. فمَن باع كرتوناً أقرّ بحبّة، ومَن باع كيلو
/// أقرّ بحبّة. <b>وثلاثةُ عيوبٍ نظاميةٍ كانت تُشحن في كلّ فاتورة</b>، وهذا أحدُها.</para>
///
/// <para><b>والأصلُ معاملُه واحدٌ بالضرورة</b> — والحارسُ في النطاق لا هنا، فوحدةٌ تُعلن
/// نفسَها أصلاً بمعاملٍ غيرِ واحدٍ تُفسد كلَّ تحويلٍ في أسرتها بصمت.</para>
/// </summary>
public partial class UnitsOfMeasurePage : UserControl
{
    public record Row(string Code, string NameAr, string UnCode, string BaseCode, decimal Factor);

    public UnitsOfMeasurePage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();

        var units = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.CompanyId == AppServices.CompanyId)
            .OrderBy(u => u.BaseCode).ThenBy(u => u.Factor)
            .ToListAsync();

        var rows = units
            .Select(u => new Row(u.Code, u.NameAr, u.UnCode, u.BaseCode, u.Factor))
            .ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var families = rows.Select(r => r.BaseCode).Distinct().Count();
        SummaryText.Text = rows.Count == 0
            ? "لا وحداتِ قياسٍ بعد — والأصناف تُصدَّر برمز «PCE» حتى تُضاف وحداتُها."
            : $"وحدات: {rows.Count:N0} · أُسَر: {families:N0} — والتحويل لا يعبر أسرةً إلى أخرى.";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new UnitOfMeasureDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }
}
