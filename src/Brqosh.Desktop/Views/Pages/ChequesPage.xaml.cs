using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;

namespace Brqosh.Desktop.Views.Pages;

public partial class ChequesPage : UserControl
{
    public record ChequeRow(long Id, string ChequeNumber, string TypeAr, string ChequeDate,
        string PartyName, decimal Amount, string StatusAr, ChequeType Type, ChequeStatus Status,
        long? PortfolioJournalEntryId, long? SettlementJournalEntryId, long? SecondarySettlementJournalEntryId);

    private ChequeRow? _selected;

    public ChequesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewReceivedBtn, PermissionService.CanPostAccounting),
                (NewIssuedBtn, PermissionService.CanPostAccounting),
                (DepositBtn, PermissionService.CanPostAccounting),
                (ClearBtn, PermissionService.CanPostAccounting),
                (BounceBtn, PermissionService.CanCancelOrReverse),
                (CancelBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var cheques = await db.Cheques
            .Where(c => c.CompanyId == AppServices.CompanyId)
            .OrderByDescending(c => c.ChequeDate).ThenByDescending(c => c.Id)
            .Take(200)
            .ToListAsync();

        var customerIds = cheques.Where(c => c.CustomerId is not null).Select(c => c.CustomerId!.Value).Distinct();
        var supplierIds = cheques.Where(c => c.SupplierId is not null).Select(c => c.SupplierId!.Value).Distinct();

        var customers = await db.Customers.Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);
        var suppliers = await db.Suppliers.Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        var rows = cheques.Select(c => new ChequeRow(
            c.Id, c.ChequeNumber, Cheque.TypeLabel(c.Type), c.ChequeDate.ToString("yyyy-MM-dd"),
            c.CustomerId is long cid ? customers.GetValueOrDefault(cid, "—")
                : c.SupplierId is long sid ? suppliers.GetValueOrDefault(sid, "—") : "—",
            c.Amount, Cheque.StatusLabel(c.Status), c.Type, c.Status,
            c.PortfolioJournalEntryId, c.SettlementJournalEntryId, c.SecondarySettlementJournalEntryId)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var inPortfolio = cheques.Count(c => c.Status == ChequeStatus.InPortfolio);
        var totalAmount = cheques.Where(c => c.Status != ChequeStatus.Cancelled).Sum(c => c.Amount);
        ChequeSummaryText.Text =
            $"شيكات: {cheques.Count} · في المحفظة: {inPortfolio} · إجمالي نشط: {totalAmount:N2} ر.س";

        UpdateActionButtons();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = Grid.SelectedItem as ChequeRow;
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        DepositBtn.IsEnabled = _selected is { Type: ChequeType.Received, Status: ChequeStatus.InPortfolio };
        ClearBtn.IsEnabled = _selected is { Type: ChequeType.Received, Status: ChequeStatus.InPortfolio or ChequeStatus.Deposited }
            or { Type: ChequeType.Issued, Status: ChequeStatus.InPortfolio };
        BounceBtn.IsEnabled = _selected is { Type: ChequeType.Received, Status: ChequeStatus.InPortfolio or ChequeStatus.Deposited or ChequeStatus.Cleared };
        CancelBtn.IsEnabled = _selected is { Type: ChequeType.Issued, Status: ChequeStatus.InPortfolio };
    }

    private async void OnNewReceived(object sender, RoutedEventArgs e)
    {
        if (new ChequeDialog(ChequeType.Received) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnNewIssued(object sender, RoutedEventArgs e)
    {
        if (new ChequeDialog(ChequeType.Issued) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async Task RunDatedActionAsync(string prompt, Func<ChequePostingService, DateOnly, Task> action)
    {
        if (_selected is null) return;

        var date = UiDialogs.PromptAccountingDate(Window.GetWindow(this)!, "تاريخ العملية", prompt);
        if (date is null) return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await action(facade.GetService<ChequePostingService>(), date.Value);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnDeposit(object sender, RoutedEventArgs e) =>
        await RunDatedActionAsync("تاريخ إيداع الشيك:", (s, d) =>
            s.DepositAsync(AppServices.CompanyId, _selected!.Id, d));

    private async void OnClear(object sender, RoutedEventArgs e) =>
        await RunDatedActionAsync("تاريخ تحصيل الشيك:", (s, d) =>
            s.ClearAsync(AppServices.CompanyId, _selected!.Id, d));

    private async void OnBounce(object sender, RoutedEventArgs e) =>
        await RunDatedActionAsync("تاريخ ارتجاع الشيك:", (s, d) =>
            s.BounceReceivedAsync(AppServices.CompanyId, _selected!.Id, d));

    private async void OnCancelCheque(object sender, RoutedEventArgs e) =>
        await RunDatedActionAsync("تاريخ إلغاء الشيك:", (s, d) =>
            s.CancelIssuedAsync(AppServices.CompanyId, _selected!.Id, d));

    private void OnViewJournals(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            MessageBox.Show("اختر شيكاً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var owner = Window.GetWindow(this);
        var shown = false;

        if (_selected.PortfolioJournalEntryId is long portfolioId)
        {
            new JournalEntryViewDialog(portfolioId) { Owner = owner, Title = "قيد الحافظة — BRQOSH" }.ShowDialog();
            shown = true;
        }

        if (_selected.SettlementJournalEntryId is long settlementId)
        {
            new JournalEntryViewDialog(settlementId) { Owner = owner, Title = "قيد التسوية — BRQOSH" }.ShowDialog();
            shown = true;
        }

        if (_selected.SecondarySettlementJournalEntryId is long secondaryId)
        {
            new JournalEntryViewDialog(secondaryId) { Owner = owner, Title = "قيد تسوية إضافي — BRQOSH" }.ShowDialog();
            shown = true;
        }

        if (!shown)
            MessageBox.Show("لا توجد قيود مرتبطة بهذا الشيك.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
