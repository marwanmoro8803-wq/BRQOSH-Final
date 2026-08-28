using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Retail;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class PosShiftsPage : UserControl
{
    public record ShiftRow(long Id, string CashierName, string OpenedAt, string ClosedAt,
        decimal OpeningCash, decimal TotalSales, int TransactionCount, string StatusAr, PosShiftStatus Status);

    public PosShiftsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (OpenBtn, PermissionService.CanPost),
                (CloseBtn, PermissionService.CanPost));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var shifts = await db.PosShifts
            .Where(s => s.CompanyId == AppServices.CompanyId)
            .OrderByDescending(s => s.OpenedAtUtc)
            .Take(100)
            .ToListAsync();

        var rows = shifts.Select(s => new ShiftRow(
            s.Id, s.CashierName,
            s.OpenedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            s.ClosedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—",
            s.OpeningCash, s.TotalSales, s.TransactionCount,
            s.Status == PosShiftStatus.Open ? "مفتوحة" : "مغلقة", s.Status)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        var open = shifts.Count(s => s.Status == PosShiftStatus.Open);
        var sales = shifts.Sum(s => s.TotalSales);
        ShiftSummaryText.Text = $"ورديات: {shifts.Count} · مفتوحة: {open} · إجمالي مبيعات: {sales:N2} ر.س";
    }

    private async void OnOpenShift(object sender, RoutedEventArgs e)
    {
        try
        {
            SecurityContext.EnsureCanPost();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
            return;
        }

        var nameDialog = new TextInputDialog("فتح وردية", "اسم الكاشير:",
            AppServices.CurrentUser?.DisplayNameAr ?? "")
            { Owner = Window.GetWindow(this) };
        if (nameDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(nameDialog.Value))
            return;

        var cashDialog = new TextInputDialog("فتح وردية", "النقد الافتتاحي:", "0")
            { Owner = Window.GetWindow(this) };
        if (cashDialog.ShowDialog() != true)
            return;

        if (!NumericInput.TryParse(cashDialog.Value, out var cash) || cash < 0)
        {
            UiFeedback.ShowWarning("مبلغ النقد غير صالح.");
            return;
        }

        try
        {
            var shift = PosShift.Open(AppServices.CompanyId, nameDialog.Value.Trim(), cash);
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            db.PosShifts.Add(shift);
            await db.SaveChangesAsync();
            await facade.GetService<AuditService>().LogAsync(AppServices.CompanyId, SecurityContext.ResolveUsername(),
                "فتح وردية POS", "PosShift", shift.Id.ToString(), shift.CashierName);
            AppServices.CurrentShiftId = shift.Id;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }

    private async void OnCloseShift(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ShiftRow row || row.Status != PosShiftStatus.Open)
        {
            UiFeedback.ShowInfo("اختر وردية مفتوحة.");
            return;
        }

        try
        {
            SecurityContext.EnsureCanPost();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
            return;
        }

        var dialog = new TextInputDialog("إغلاق الوردية", "النقد عند الإغلاق:", row.OpeningCash.ToString("N2"))
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;

        if (!NumericInput.TryParse(dialog.Value, out var closing))
        {
            UiFeedback.ShowWarning("المبلغ غير صالح.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var shift = await db.PosShifts.SingleAsync(s => s.Id == row.Id);
            shift.Close(closing);
            await db.SaveChangesAsync();
            await facade.GetService<AuditService>().LogAsync(AppServices.CompanyId, SecurityContext.ResolveUsername(),
                "إغلاق وردية POS", "PosShift", shift.Id.ToString(), $"نقد إغلاق {closing:N2}");

            if (AppServices.CurrentShiftId == row.Id)
                AppServices.CurrentShiftId = null;

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }
}
