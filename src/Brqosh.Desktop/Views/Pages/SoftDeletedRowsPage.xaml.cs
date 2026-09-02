using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;

using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٥) <b>المحذوفات — «لا شاشةَ تُظهره اليوم» كُتب في الخدمة نفسِها.</b>
///
/// <para><b>ما كان:</b> <c>SoftDeletedRowService</c> مبنيّةٌ ومسجَّلةٌ في الحقن، <b>وتعليقُها
/// يقول صراحةً «وهو أوّلُ ما ينقص: لا شاشةَ تُظهره اليوم»</b> — وبقي كذلك. فالحذفُ في هذا
/// النظام <b>إخفاءٌ لا إعدام</b>، والصفُّ المخفيُّ يحجز كودَه ويمنع إنشاءَ بديلٍ باسمه،
/// <b>ولا يرى المالكُ شيئاً يُفسّر الرفض</b>.</para>
///
/// <para><b>والاستعادةُ لا تتصادم</b> — والسببُ مقيسٌ في النطاق: الفهرسُ الفريد الذي يحجز
/// الكودَ يمنع أن يوجد صفٌّ حيٌّ يحمله.</para>
/// </summary>
public partial class SoftDeletedRowsPage : UserControl
{
    public record Row(long Id, string Code, string Name, string DeletedAt);

    private static readonly CodeAvailabilityScope[] Scopes =
    [
        CodeAvailabilityScope.Product,
        CodeAvailabilityScope.Account,
        CodeAvailabilityScope.Warehouse,
        CodeAvailabilityScope.CostCenter,
    ];

    public SoftDeletedRowsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            // ‏**والاستعادةُ للمدير وحدَه** — والنطاقُ يفرضه، والشاشةُ لا تعرض ما سيُرفض.
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (RestoreBtn, PermissionService.CanManageUsers));
            await ReloadAsync();
        };
    }

    private CodeAvailabilityScope SelectedScope =>
        Scopes[Math.Clamp(ScopeBox.SelectedIndex, 0, Scopes.Length - 1)];

    private async void OnScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            using var facade = new DesktopAccountingFacade();
            var rows = await facade.GetService<SoftDeletedRowService>()
                .ListAsync(SelectedScope, AppServices.CompanyId);

            var display = rows
                .OrderByDescending(r => r.DeletedAtUtc)
                .Select(r => new Row(r.Id, r.Code, r.Name,
                    r.DeletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")))
                .ToList();

            Grid.ItemsSource = display;
            UiTestAutomation.PublishGridCount(Grid, display.Count);

            SummaryText.Text = display.Count == 0
                ? "لا محذوفاتٍ من هذا النوع."
                : $"محذوفات: {display.Count:N0} — وكلٌّ منها يحجز كوده، فلا يُنشأ بديلٌ باسمه.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;

        if (UiDialog.Show("استعادة الصف المحذوف إلى الدفاتر؟", row.Code,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<SoftDeletedRowService>().RestoreAsync(
                SelectedScope, AppServices.CompanyId, row.Id, SecurityContext.ResolveUsername());

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
