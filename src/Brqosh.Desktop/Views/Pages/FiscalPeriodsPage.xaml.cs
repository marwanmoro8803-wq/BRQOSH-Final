using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Accounting;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class FiscalPeriodsPage : UserControl
{
    public record PeriodRow(long Id, string Code, DateOnly StartDate, DateOnly EndDate,
        string StatusAr, int EntryCount, string ClosedAt);

    public FiscalPeriodsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            // الخدمة تتطلب مدير نظام؛ إخفاء الأزرار عن المحاسب يمنع رفضاً متأخراً محيراً.
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (ClosePeriodBtn, PermissionService.CanManageUsers),
                (ReopenPeriodBtn, PermissionService.CanManageUsers),
                (YearEndBtn, PermissionService.CanManageUsers));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var periods = await db.FiscalPeriods
            .Where(p => p.CompanyId == AppServices.CompanyId)
            .OrderByDescending(p => p.Code)
            .ToListAsync();

        var rows = new List<PeriodRow>();
        foreach (var period in periods)
        {
            var count = await db.JournalEntries.CountAsync(e =>
                e.CompanyId == AppServices.CompanyId &&
                e.EntryDate >= period.StartDate && e.EntryDate <= period.EndDate &&
                e.Status == JournalEntryStatus.Posted);

            rows.Add(new PeriodRow(
                period.Id, period.Code, period.StartDate, period.EndDate,
                period.Status == FiscalPeriodStatus.Closed ? "مقفلة 🔒" : "مفتوحة",
                count,
                period.ClosedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—"));
        }

        PeriodsGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(PeriodsGrid, rows.Count);

        var closed = rows.Count(r => r.StatusAr.Contains("مقفلة"));
        var totalEntries = rows.Sum(r => r.EntryCount);
        PeriodSummaryText.Text =
            $"فترات: {rows.Count} · مقفلة: {closed} · قيود مرحّلة: {totalEntries:N0}";
    }

    private async void OnClose(object sender, RoutedEventArgs e) => await TransitionAsync(close: true);

    private async void OnReopen(object sender, RoutedEventArgs e) => await TransitionAsync(close: false);

    private async void OnYearEndClose(object sender, RoutedEventArgs e)
    {
        var yearText = UiDialogs.PromptText("إقفال سنوي", "أدخل السنة المالية (مثال: 2026):", DateTime.Today.Year.ToString());
        if (yearText is null) return;
        if (!int.TryParse(yearText.Trim(), out var year) || year < 2000 || year > 2100)
        {
            UiFeedback.ShowWarning("سنة غير صالحة.");
            return;
        }

        var confirm = MessageBox.Show(
            $"سيتم إقفال جميع فترات السنة {year} وإنشاء قيد إقفال الإيرادات/المصروفات.\nهل تريد المتابعة؟",
            "BRQOSH — إقفال سنوي", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<YearEndCloseService>().CloseYearAsync(
                AppServices.CompanyId, year, SecurityContext.ResolveUsername());
            MessageBox.Show(
                $"تم إقفال السنة {result.Year}.\nصافي الدخل: {result.NetIncome:N2}\nالقيد: {result.EntryNumber}",
                "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async Task TransitionAsync(bool close)
    {
        if (PeriodsGrid.SelectedItem is not PeriodRow row)
        {
            MessageBox.Show("اختر فترة من الجدول أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var username = SecurityContext.ResolveUsername();
            if (close)
            {
                using var scope = AppServices.CreateInfrastructureScope();
                var monthEnd = scope.ServiceProvider.GetRequiredService<IMonthEndCloseUseCase>();
                var result = await monthEnd.ClosePeriodAsync(AppServices.CompanyId, row.Id, username);
                MessageBox.Show(
                    $"تم إقفال {result.PeriodCode}.\nتسويات بنكية معلّقة: {result.UnreconciledBankLineCount}",
                    "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                using var facade = new DesktopAccountingFacade();
                await facade.GetService<FiscalPeriodService>().ReopenAsync(AppServices.CompanyId, row.Id, username);
            }

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
