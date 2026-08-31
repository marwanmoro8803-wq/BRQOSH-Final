using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class BankAccountsPage : UserControl
{
    public record Row(long Id, string Code, string NameAr, string BankName, string AccountNumber, string Iban,
        string GlCode, string CurrencyCode, string DefaultAr, bool IsForeign);

    private List<BankAccount> _banks = [];
    private string _functionalCurrency = "SAR";

    public BankAccountsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (FxRevalBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var company = await db.Companies.SingleAsync(c => c.Id == AppServices.CompanyId);
        _functionalCurrency = company.FunctionalCurrencyCode;

        _banks = await db.BankAccounts
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderBy(b => b.Code)
            .ToListAsync();

        var glIds = _banks.Select(b => b.GlAccountId).Distinct().ToList();
        var glCodes = await db.Accounts
            .Where(a => glIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Code);

        var rows = _banks.Select(b =>
        {
            var isForeign = !string.Equals(b.CurrencyCode, _functionalCurrency, StringComparison.OrdinalIgnoreCase);
            return new Row(
                b.Id, b.Code, b.NameAr, b.BankName, b.AccountNumber, b.Iban,
                glCodes.GetValueOrDefault(b.GlAccountId, "—"),
                b.CurrencyCode,
                b.IsDefault ? "نعم" : "—",
                isForeign);
        }).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var foreignCount = rows.Count(r => r.IsForeign);
        BankSummaryText.Text =
            $"حسابات نشطة: {rows.Count:N0} · أجنبية: {foreignCount:N0} · عملة وظيفية: {_functionalCurrency}";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new BankAccountDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        var bank = _banks.Single(b => b.Id == row.Id);
        if (new BankAccountDialog(bank) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private void OnFxReval(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر حساباً بنكياً أولاً.");
            return;
        }

        if (!row.IsForeign)
        {
            UiFeedback.ShowWarning($"الحساب بعملة {_functionalCurrency} — إعادة التقييم للحسابات الأجنبية فقط.");
            return;
        }

        var bank = _banks.Single(b => b.Id == row.Id);
        new FxRevaluationDialog(bank.Id, bank.NameAr, bank.CurrencyCode, _functionalCurrency)
        {
            Owner = Window.GetWindow(this),
        }.ShowDialog();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (UiDialog.Show($"إيقاف الحساب البنكي {row.NameAr}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var bank = await db.BankAccounts.SingleAsync(b => b.Id == row.Id);
            bank.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
