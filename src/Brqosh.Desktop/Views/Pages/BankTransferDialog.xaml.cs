using System.Globalization;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class BankTransferDialog : Window
{
    public record BankOption(long Id, string Display);

    public BankTransferDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadBanksAsync();
    }

    private async Task LoadBanksAsync()
    {
        await using var db = AppServices.CreateDb();
        var banks = await db.BankAccounts
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.Code)
            .Select(b => new BankOption(b.Id, $"{b.Code} — {b.NameAr} ({b.CurrencyCode})"))
            .ToListAsync();

        FromBankCombo.ItemsSource = banks;
        ToBankCombo.ItemsSource = banks;
        if (banks.Count > 0)
        {
            FromBankCombo.SelectedIndex = 0;
            ToBankCombo.SelectedIndex = banks.Count > 1 ? 1 : 0;
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            if (FromBankCombo.SelectedValue is not long fromId || ToBankCombo.SelectedValue is not long toId)
                throw new InvalidOperationException("اختر حسابي البنك.");

            if (!decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                && !decimal.TryParse(AmountBox.Text.Trim(), out amount))
                throw new InvalidOperationException("أدخل مبلغاً صحيحاً.");

            var date = DatePickerBox.SelectedDate is DateTime dt
                ? DateOnly.FromDateTime(dt)
                : DateOnly.FromDateTime(DateTime.Today);

            using var facade = new DesktopAccountingFacade();
            var entry = await facade.PostBankTransferAsync(fromId, toId, amount, date,
                string.IsNullOrWhiteSpace(ReferenceBox.Text) ? null : ReferenceBox.Text.Trim());

            MessageBox.Show(this, $"تم التحويل وترحيل القيد {entry.EntryNumber}.", "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
