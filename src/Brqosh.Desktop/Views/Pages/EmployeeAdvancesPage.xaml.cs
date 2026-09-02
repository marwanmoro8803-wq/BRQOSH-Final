using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط2-٥) <b>سُلَفُ الموظفين — والمتبقّي يُرى قبل أن يُنسى.</b>
///
/// <para><b>ما كان:</b> المنشأةُ تُسلّف موظّفَها ولا شيءَ في النظام يعرفها. <b>فالمالُ
/// يخرج ويُقيَّد مصروفَ رواتب</b>، ثمّ يُنسى الاستقطاع — ولا موضعَ له أصلاً.</para>
///
/// <para><b>وعمودُ «المتبقّي» هو الشاشة كلُّها</b>: هو ما في ذمّة الموظّف الآن. <b>ورقمٌ
/// لا يُرى لا يُستقطَع</b> — ولا يُكتشف إلا حين يستقيل صاحبُه.</para>
/// </summary>
public partial class EmployeeAdvancesPage : UserControl
{
    public record Row(long Id, string AdvanceNumber, string EmployeeName, string DisbursedOn,
        decimal Amount, decimal Deducted, decimal Outstanding, decimal Installment,
        string StatusText, AdvanceStatus Status);

    public EmployeeAdvancesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (DisburseBtn, PermissionService.CanPostAccounting),
                (RefreshBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var advances = await db.EmployeeAdvances
                .Where(a => a.CompanyId == AppServices.CompanyId)
                .OrderByDescending(a => a.RequestDate)
                .ToListAsync();

            var employees = await db.Employees.AsNoTracking()
                .Where(e => e.CompanyId == AppServices.CompanyId)
                .ToDictionaryAsync(e => e.Id, e => e.NameAr);

            using var facade = new DesktopAccountingFacade();
            var balances = (await facade.GetService<EmployeeAdvanceService>()
                .BalancesAsync(AppServices.CompanyId)).ToDictionary(b => b.AdvanceId);

            var rows = advances.Select(a =>
            {
                balances.TryGetValue(a.Id, out var balance);
                return new Row(a.Id, a.AdvanceNumber,
                    employees.TryGetValue(a.EmployeeId, out var name) ? name : $"#{a.EmployeeId}",
                    a.DisbursedOn?.ToString("yyyy-MM-dd") ?? "",
                    a.Amount, balance?.Deducted ?? 0m, balance?.Outstanding ?? 0m,
                    a.InstallmentAmount, StatusText(a.Status), a.Status);
            }).ToList();

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            var outstanding = rows.Sum(r => r.Outstanding);
            var pending = rows.Count(r => r.Status == AdvanceStatus.Approved);

            SummaryText.Text = rows.Count == 0
                ? "لا سُلَفَ بعد — والسُّلفةُ ذمّةٌ على الموظّف تُسدَّد بالاستقطاع الشهريّ."
                : $"سُلَف: {rows.Count:N0} · المتبقّي في ذمم الموظفين: {outstanding:N2}"
                  + (pending > 0 ? $" · بانتظار الصرف: {pending:N0}" : "");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        var dialog = new EmployeeAdvanceDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) await ReloadAsync();
    }

    private async void OnDisburse(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر سُلفةً أولاً.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<EmployeeAdvanceService>().DisburseAsync(
                AppServices.CompanyId, row.Id, DateOnly.FromDateTime(DateTime.Today),
                SaudiChartOfAccounts.SystemCodes.CashOnHand);

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnRefreshStatuses(object sender, RoutedEventArgs e)
    {
        try
        {
            using var facade = new DesktopAccountingFacade();
            var changed = await facade.GetService<EmployeeAdvanceService>()
                .RefreshStatusesAsync(AppServices.CompanyId);

            await ReloadAsync();

            if (changed > 0)
                SummaryText.Text = $"تغيّرت حالةُ {changed:N0} سُلفة." + " " + SummaryText.Text;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string StatusText(AdvanceStatus status) => status switch
    {
        AdvanceStatus.Approved => "بانتظار الصرف",
        AdvanceStatus.Outstanding => "قائمة",
        AdvanceStatus.Settled => "مسددة",
        _ => "ملغاة",
    };
}
