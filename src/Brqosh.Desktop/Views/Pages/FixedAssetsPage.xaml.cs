using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Domain.Assets;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Assets;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class FixedAssetsPage : UserControl
{
    public record AssetRow(long Id, string Code, string NameAr, string AcquisitionDate,
        decimal AcquisitionCost, decimal AccumulatedDepreciation, decimal BookValue, decimal MonthlyDepreciation,
        long? AcquisitionJournalEntryId, long? DisposalJournalEntryId);

    private List<FixedAsset> _assets = [];

    public FixedAssetsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPostAccounting),
                (RunDepreciationBtn, PermissionService.CanPostAccounting),
                (DisposeBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        _assets = await db.FixedAssets
            .Where(a => a.CompanyId == AppServices.CompanyId && a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync();

        Grid.ItemsSource = _assets.Select(a => new AssetRow(
            a.Id, a.Code, a.NameAr, a.AcquisitionDate.ToString("yyyy-MM-dd"),
            a.AcquisitionCost, a.AccumulatedDepreciation, a.BookValue, a.MonthlyDepreciation,
            a.AcquisitionJournalEntryId, a.DisposalJournalEntryId)).ToList();
        UiTestAutomation.PublishGridCount(Grid, _assets.Count);
        RenderKpis();
    }

    private void RenderKpis()
    {
        KpiGrid.Children.Clear();
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("أصول نشطة", "Active assets"), "FA", _assets.Count, "#0D9488", KpiValueKind.Count));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("تكلفة الاقتناء", "Acquisition cost"), "AC",
            _assets.Sum(a => a.AcquisitionCost), "#2563EB", KpiValueKind.Currency));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("قيمة دفترية", "Book value"), "BV",
            _assets.Sum(a => a.BookValue), "#7C3AED", KpiValueKind.Currency));
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new FixedAssetDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not AssetRow row) return;
        var asset = _assets.Single(a => a.Id == row.Id);
        if (new FixedAssetDialog(asset) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private void OnViewJournal(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not AssetRow row)
        {
            MessageBox.Show("اختر أصلاً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.AcquisitionJournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            MessageBox.Show("لا يوجد قيد اقتناء مرتبط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnViewDisposalJournal(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not AssetRow row)
        {
            MessageBox.Show("اختر أصلاً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.DisposalJournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            MessageBox.Show("لا يوجد قيد استبعاد مرتبط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnDispose(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not AssetRow row) return;

        var dialog = new TextInputDialog("استبعاد أصل ثابت",
            $"عائد البيع/الخردة للأصل {row.NameAr} (0 إن لم يوجد):", "0")
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;

        if (!decimal.TryParse(dialog.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var proceeds) || proceeds < 0)
        {
            MessageBox.Show("أدخل مبلغاً صحيحاً (صفر أو أكثر).", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"استبعاد الأصل {row.NameAr} بعائد {proceeds:N2}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var asset = await db.FixedAssets.SingleAsync(a => a.Id == row.Id);
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<FixedAssetDisposalPostingService>().PostDisposalAsync(
                asset, proceeds, DateOnly.FromDateTime(DateTime.Today), username);
            await ReloadAsync();
            MessageBox.Show("تم ترحيل قيد الاستبعاد.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnRunDepreciation(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("تشغيل إهلاك شهري لجميع الأصول النشطة؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            var count = await facade.GetService<DepreciationService>().RunMonthlyAsync(
                AppServices.CompanyId, DateOnly.FromDateTime(DateTime.Today), username);

            MessageBox.Show(count > 0
                    ? $"تم ترحيل إهلاك {count} أصل."
                    : "لا توجد أصول تحتاج إهلاكاً.",
                "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
