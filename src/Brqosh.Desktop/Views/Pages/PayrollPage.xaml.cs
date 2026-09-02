using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Payroll;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class PayrollPage : UserControl
{
    public record RunRow(long Id, string PeriodCode, string PayDate, int EmployeeCount,
        decimal TotalGross, decimal TotalNet, string StatusAr, PayrollRunStatus Status, long? JournalEntryId);

    public PayrollPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPostAccounting),
                (PostBtn, PermissionService.CanPostAccounting),
                (ReverseBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var runs = await db.PayrollRuns
            .Where(p => p.CompanyId == AppServices.CompanyId)
            .OrderByDescending(p => p.PayDate)
            .Take(100)
            .ToListAsync();

        Grid.ItemsSource = runs.Select(p => new RunRow(
            p.Id, p.PeriodCode, p.PayDate.ToString("yyyy-MM-dd"),
            p.Lines.Count, p.TotalGross, p.TotalNet,
            p.Status switch
            {
                PayrollRunStatus.Posted => "مرحّل",
                PayrollRunStatus.Reversed => "معكوس",
                _ => "مسودة",
            }, p.Status, p.JournalEntryId)).ToList();
        UiTestAutomation.PublishGridCount(Grid, runs.Count);
        RenderKpis(runs);
    }

    private void RenderKpis(IReadOnlyList<PayrollRun> runs)
    {
        KpiGrid.Children.Clear();
        var posted = runs.Count(r => r.Status == PayrollRunStatus.Posted);
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("دورات", "Payroll runs"), "PR", runs.Count, "#7C3AED", KpiValueKind.Count));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("إجمالي إجمالي", "Total gross"), "GR",
            runs.Sum(r => r.TotalGross), "#2563EB", KpiValueKind.Currency));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("مرحّل", "Posted"), "OK", posted, "#0D9488", KpiValueKind.Count));
    }

    private async void OnNew(object sender, RoutedEventArgs e)
    {
        if (new PayrollDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RunRow row || row.Status != PayrollRunStatus.Draft)
        {
            UiDialog.Show("اختر دورة رواتب بحالة مسودة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var run = await db.PayrollRuns.SingleAsync(p => p.Id == row.Id);
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<PayrollPostingService>().PostAsync(run, username);

            UiDialog.Show("تم ترحيل دورة الرواتب.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnViewJournal(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RunRow row)
        {
            UiDialog.Show("اختر دورة من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.JournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            UiDialog.Show("لا يوجد قيد محاسبي مرتبط بهذه الدورة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnReverse(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RunRow row || row.Status != PayrollRunStatus.Posted)
        {
            UiDialog.Show("اختر دورة رواتب مرحّلة للعكس.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (UiDialog.Show($"عكس دورة الرواتب {row.PeriodCode}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var reversalDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ العكس", "تاريخ قيد عكس الرواتب:");
        if (reversalDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<PayrollReversalService>().ReverseAsync(
                AppServices.CompanyId, row.Id, reversalDate.Value, username);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    /// <summary>
    /// (CEEF-230 · ط6-٥) <b>تخصيصُ مكافأة نهاية الخدمة — والخدمةُ لم تكن مسجَّلةً في الحقن أصلاً.</b>
    ///
    /// <para>بُنيت <c>EndOfServiceAccrualService</c> في <c>ط2-٣</c> بالمادتين 84 و85
    /// ومُقيست بالطفرات، <b>ولم تكن مسجَّلةً في الحقن ولا يستدعيها زرّ</b>. فحسابُ المخصّص
    /// <c>2202</c> — الذي كان بلا كاتبٍ منذ اليوم الأوّل — <b>بقي بلا كاتبٍ فعلاً</b>:
    /// المنشأةُ تحمل التزاماً نظامياً يتراكم <b>ولا يظهر في ميزانيّتها بريالٍ واحد</b>.</para>
    ///
    /// <para><b>وهو فرقٌ لا تراكم</b>: يُحسب الالتزامُ اليومَ ويُقارَن بما في الحساب،
    /// <b>ويُقيَّد الفرقُ وحده</b> — فتشغيلٌ ثانٍ بلا تغييرٍ لا يُقيّد شيئاً.</para>
    /// </summary>
    private async void OnAccrueEndOfService(object sender, RoutedEventArgs e)
    {
        if (UiDialog.Show("تخصيص مكافأة نهاية الخدمة حتى اليوم؟ — ويُقيَّد الفرق وحده.", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<EndOfServiceAccrualService>().RunMonthlyAsync(
                AppServices.CompanyId, DateOnly.FromDateTime(DateTime.Today),
                SecurityContext.ResolveUsername());

            await ReloadAsync();

            // ‏**والنصُّ حرفيٌّ والأرقامُ في العنوان**: نصٌّ مركَّبٌ لا يبلغه مترجِم،
            // ‏وحارسُ `DialogLocalizationTests` يعدّه. **والرقمُ لا يسقط** — يُحمل في
            // ‏عنوان الحوار.
            if (result.Movement == 0m)
                UiDialog.Show("لا تغيّرَ في الالتزام — ولم يُقيَّد شيء.", "BRQOSH",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            else
                UiDialog.Show("قُيّدت حركة تخصيص مكافأة نهاية الخدمة.",
                    $"{result.Movement:N2} · {result.Employees:N0}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
