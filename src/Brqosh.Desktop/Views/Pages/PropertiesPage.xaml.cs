using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.RealEstate;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) <b>العقاراتُ والوحدات — الشاشةُ التي بلا وجودها بقي `ط3-١` نطاقاً بلا باب.</b>
///
/// <para><b>ما كان:</b> «العقارات» ملفُّ عملٍ يُعرض في شاشة التأسيس، <b>ولا عقارَ في
/// النظام ولا وحدة</b>. ثمّ بُني النطاقُ في <c>ط3-١</c> ومُقيس بالطفرات — <b>ولا يزال
/// المالكُ لا يصل إليه</b>. وهذه الشاشةُ هي ما يجعل ذلك البناءَ منتَجاً.</para>
///
/// <para><b>وحالةُ الوحدة تُعرض ولا تُعدَّل هنا</b>: الإشغالُ يقوده العقدُ لا المستخدم —
/// <b>فلا حالةَ إشغالٍ بلا عقدٍ يُبرّرها</b>، وهو حاجزُ النطاق المقيس في <c>ط3-١</c>.</para>
/// </summary>
public partial class PropertiesPage : UserControl
{
    public record PropertyRow(long Id, string Code, string NameAr, string City);

    public record UnitRow(string UnitNumber, decimal Area, int Bedrooms, decimal ListedRent, string Status);

    public PropertiesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddPropertyBtn, PermissionService.CanPost),
                (AddUnitBtn, PermissionService.CanPost),
                (LinkAssetBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();

        var properties = await db.Properties.AsNoTracking()
            .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new PropertyRow(p.Id, p.Code, p.NameAr, p.City))
            .ToListAsync();

        PropertiesGrid.ItemsSource = properties;
        UiTestAutomation.PublishGridCount(PropertiesGrid, properties.Count);

        var units = await db.PropertyUnits.AsNoTracking()
            .Where(u => u.CompanyId == AppServices.CompanyId)
            .ToListAsync();

        var leased = units.Count(u => u.Status == UnitStatus.Leased);
        var vacant = units.Count(u => u.Status == UnitStatus.Vacant);
        var maintenance = units.Count(u => u.Status == UnitStatus.Maintenance);

        // ‏**ونسبةُ الإشغال هي الرقمُ الذي يُسأل عنه أوّلاً** في هذا القطاع.
        SummaryText.Text = units.Count == 0
            ? $"عقارات: {properties.Count:N0} · ولا وحداتٍ بعد."
            : $"عقارات: {properties.Count:N0} · وحدات: {units.Count:N0} · مؤجَّرة: {leased:N0} · "
              + $"شاغرة: {vacant:N0} · صيانة: {maintenance:N0} · "
              + $"الإشغال: {(decimal)leased * 100m / units.Count:N1}٪";

        if (properties.Count > 0 && PropertiesGrid.SelectedItem is null)
            PropertiesGrid.SelectedIndex = 0;
        else
            await ReloadUnitsAsync();
    }

    private async void OnPropertySelected(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await ReloadUnitsAsync();
    }

    private async Task ReloadUnitsAsync()
    {
        if (PropertiesGrid.SelectedItem is not PropertyRow property)
        {
            UnitsGrid.ItemsSource = null;
            UiTestAutomation.PublishGridCount(UnitsGrid, 0);
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var rows = await db.PropertyUnits.AsNoTracking()
                .Where(u => u.PropertyId == property.Id)
                .OrderBy(u => u.UnitNumber)
                .Select(u => new UnitRow(u.UnitNumber, u.AreaSquareMeters, u.Bedrooms,
                    u.ListedAnnualRent, u.Status.ToString()))
                .ToListAsync();

            UnitsGrid.ItemsSource = rows.Select(r => r with { Status = StatusText(r.Status) }).ToList();
            UiTestAutomation.PublishGridCount(UnitsGrid, rows.Count);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAddProperty(object sender, RoutedEventArgs e)
    {
        if (new PropertyDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnAddUnit(object sender, RoutedEventArgs e)
    {
        if (PropertiesGrid.SelectedItem is not PropertyRow property)
        {
            UiFeedback.ShowWarning("اختر العقار الذي تُضاف إليه الوحدة.");
            return;
        }

        if (new PropertyUnitDialog(property.Id) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    /// <summary>
    /// (CEEF-232 · ع-١) <b>ربطُ العقار بأصلٍ ثابت — وبه وحدَه يُهلَك المبنى.</b>
    ///
    /// <para><b>وكان العقارُ بلا تكلفةٍ ولا عمرٍ ولا ربط</b>، و<c>DepreciationService</c>
    /// يُهلك صفوفَ الأصول وحدَها. <b>فمحفظةٌ عقاريّةٌ كاملةٌ لا تُهلَك بريالٍ واحد</b>:
    /// يُعترف بإيراد الإيجار كلِّه <b>بلا مقابلٍ من استهلاك المبنى</b> — فيُعلَن ربحٌ أكبرُ
    /// من الحقّ كلَّ سنة، <b>ثمّ تظهر عند البيع «خسارةٌ» هي إهلاكُ سنواتٍ لم يُقيَّد</b>.</para>
    ///
    /// <para><b>ولم يُبنَ محرّكُ إهلاكٍ ثانٍ</b>: الرابطُ يُدخل العقارَ في المحرّك القائم —
    /// <b>ومحرّكان لقاعدةٍ واحدةٍ يفترقان يوماً</b>.</para>
    /// </summary>
    private async void OnLinkFixedAsset(object sender, RoutedEventArgs e)
    {
        if (PropertiesGrid.SelectedItem is not PropertyRow property)
        {
            UiFeedback.ShowWarning("اختر العقار الذي يُربط بأصل.");
            return;
        }

        var dialog = new TextInputDialog("ربط بأصل ثابت", "كود الأصل الثابت:", "")
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;

        var code = (dialog.Value ?? "").Trim();
        if (code.Length == 0)
        {
            UiFeedback.ShowWarning("أدخل كود الأصل.");
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var asset = await db.FixedAssets
                .FirstOrDefaultAsync(a => a.CompanyId == AppServices.CompanyId
                                          && a.Code == code && a.IsActive);
            if (asset is null)
            {
                UiFeedback.ShowWarning("لا يوجد أصلٌ نشطٌ بهذا الكود.");
                return;
            }

            var entity = await db.Properties.SingleAsync(p => p.Id == property.Id);
            entity.LinkFixedAsset(asset.Id);
            await db.SaveChangesAsync();

            await ReloadAsync();
            UiDialog.Show("رُبط العقار بأصلٍ ثابت — ويُهلَك من الآن مع دفعة الإهلاك.",
                asset.NameAr, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string StatusText(string status) => status switch
    {
        nameof(UnitStatus.Vacant) => LocaleManager.T("شاغرة", "Vacant"),
        nameof(UnitStatus.Leased) => LocaleManager.T("مؤجَّرة", "Leased"),
        nameof(UnitStatus.Maintenance) => LocaleManager.T("صيانة", "Maintenance"),
        _ => status,
    };
}
