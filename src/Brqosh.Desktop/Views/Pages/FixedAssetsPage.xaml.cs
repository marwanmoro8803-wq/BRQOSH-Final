using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Domain.Accounting;
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
                // ‏(ح-٩٢) وعكسُ التشغيل بحارس العكس.
                (ReverseDepreciationBtn, PermissionService.CanCancelOrReverse),
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
            UiDialog.Show("اختر أصلاً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.AcquisitionJournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            UiDialog.Show("لا يوجد قيد اقتناء مرتبط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnViewDisposalJournal(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not AssetRow row)
        {
            UiDialog.Show("اختر أصلاً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.DisposalJournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            UiDialog.Show("لا يوجد قيد استبعاد مرتبط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnDispose(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not AssetRow row) return;

        // (جاهزيّةُ الإطلاق · ح-١٦) ‏**ومربّعُ نصٍّ واحدٌ لا يكفي لتوريدٍ خاضع.**
        //
        // ‏كانت الشاشةُ تسأل عن المبلغ وحدَه، **فينقص أمران**: الفئةُ الضريبيّة — تقبلها
        // ‏الخدمةُ منذ CEEF-232 · ض-٢ **ولا شاشةَ تُمرّرها**، فتُفرض الأساسيّةُ على كلّ
        // ‏بيعٍ ولو كان تصديراً صفريَّ النسبة — **والمشتري**، ولم يكن للأصل مشترٍ في
        // ‏النموذج أصلاً، فيُقيَّد التزامُ ضريبةٍ بلا فاتورةٍ ولا طرفٍ ثانٍ.
        //
        // ‏(CEEF-229 · م0-١٥ · CEEF-232 · ض-٢/ض-٤) **والمعاينةُ تبقى وتُثرى**: الناتجُ
        // ‏والمقبوضُ والضريبةُ — **والنسبةُ من حال المنشأة لا مكتوبةً ثابتة** — وصارت
        // ‏تتجدّد مع كلّ تغييرٍ في الشاشة بدل رسالةِ تأكيدٍ واحدة.
        var customers = await FixedAssetDisposalDialog.LoadCustomersAsync();
        var isVatRegistered = await ResolveDisposalVatRateAsync() > 0m;

        var dialog = new FixedAssetDisposalDialog(
            row.NameAr, row.AcquisitionCost, row.AccumulatedDepreciation, row.BookValue,
            isVatRegistered, customers)
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var asset = await db.FixedAssets.SingleAsync(a => a.Id == row.Id);
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<FixedAssetDisposalPostingService>().PostDisposalAsync(
                asset, dialog.Proceeds, DateOnly.FromDateTime(DateTime.Today), username,
                dialog.Treatment.Category, dialog.BuyerCustomerId, dialog.Treatment.Code);
            await ReloadAsync();
            UiDialog.Show("تم ترحيل قيد الاستبعاد.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnRunDepreciation(object sender, RoutedEventArgs e)
    {
        if (UiDialog.Show("تشغيل إهلاك شهري لجميع الأصول النشطة؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            var count = await facade.GetService<DepreciationService>().RunMonthlyAsync(
                AppServices.CompanyId, DateOnly.FromDateTime(DateTime.Today), username);

            UiDialog.Show(count > 0
                    ? $"تم ترحيل إهلاك {count} أصل."
                    : "لا توجد أصول تحتاج إهلاكاً.",
                "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٩٢) <b>عكسُ تشغيل الإهلاك بتاريخه</b> — آخرُه لكلّ أصل؛ وكان تشغيلٌ بتاريخٍ خطأٍ يبقى، والتشغيلُ يأخذ تاريخَ اليوم
    /// فلا يُصحَّح بتشغيلٍ آخر.
    /// </summary>
    private async void OnReverseDepreciation(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this)!;
        var runDate = UiDialogs.PromptAccountingDate(owner, "عكس تشغيل الإهلاك", "تاريخ التشغيل المراد عكسه:");
        if (runDate is null)
            return;
        var reversalDate = UiDialogs.PromptAccountingDate(owner, "تاريخ العكس", "تاريخ قيد عكس الإهلاك:");
        if (reversalDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var count = await facade.GetService<DepreciationService>().ReverseRunAsync(
                AppServices.CompanyId, runDate.Value, reversalDate.Value, SecurityContext.ResolveUsername());

            UiDialog.Show("عُكس تشغيل الإهلاك وعاد مجمَّع الأصول إلى ما قبله.", $"{count:N0}",
                MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// (CEEF-232 · ض-٤) نسبةُ ضريبة الاستبعاد كما ستُقيَّد فعلاً — <b>لا كما يُفترض</b>.
    ///
    /// <para><b>والمصدرُ واحدٌ مع خدمة الترحيل:</b> رقمٌ ضريبيٌّ من خمسةَ عشرَ رقماً.
    /// <b>وقاعدتان لنسبةٍ واحدة تفترقان يوماً</b> — فتُعرض نسبةٌ ويُقيَّد غيرُها.</para>
    /// </summary>
    private static async Task<decimal> ResolveDisposalVatRateAsync()
    {
        using var facade = new DesktopAccountingFacade();
        var vatNumber = await facade.Db.Companies
            .Select(c => c.VatRegistrationNumber)
            .FirstOrDefaultAsync();

        return (vatNumber ?? "").Trim().Length == 15 ? VatCalculator.SaudiVatRate : 0m;
    }
}
