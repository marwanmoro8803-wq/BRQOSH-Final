using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class JournalEntryDialog : Window
{
    public record AccountOption(long Id, string Display);

    public class LineRow
    {
        public long? AccountId { get; set; }
        public string? Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    private readonly ObservableCollection<LineRow> _rows = [];
    private List<Account> _postableAccounts = [];

    public JournalEntryDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
        await using var db = AppServices.CreateDb();
        _postableAccounts = await db.Accounts
            .Where(a => a.CompanyId == AppServices.CompanyId && !a.IsGroup && a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync();

        AccountColumn.ItemsSource = _postableAccounts
            .Select(a => new AccountOption(a.Id, $"{a.Code} — {a.NameAr}"))
            .ToList();
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        // تحديث الإجماليات بعد إتمام التحرير الجاري
        Dispatcher.BeginInvoke(UpdateTotals);
    }

    private void UpdateTotals()
    {
        var debit = _rows.Sum(r => r.Debit);
        var credit = _rows.Sum(r => r.Credit);
        TotalDebitText.Text = debit.ToString("N2");
        TotalCreditText.Text = credit.ToString("N2");

        if (debit == credit && debit > 0)
        {
            BalanceText.Text = "✓ متوازن";
            BalanceText.Foreground = System.Windows.Media.Brushes.Green;
        }
        else
        {
            BalanceText.Text = $"الفرق: {Math.Abs(debit - credit):N2}";
            BalanceText.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (DatePickerBox.SelectedDate is null)
                throw new AccountingException("تاريخ القيد مطلوب.");

            var entryDate = DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value);
            var entry = JournalEntry.CreateDraft(AppServices.CompanyId, entryDate, DescriptionBox.Text);

            var accountById = _postableAccounts.ToDictionary(a => a.Id);
            foreach (var row in _rows.Where(r => r.AccountId is not null || r.Debit != 0 || r.Credit != 0))
            {
                if (row.AccountId is null || !accountById.TryGetValue(row.AccountId.Value, out var account))
                    throw new AccountingException("اختر الحساب لكل سطر في القيد.");
                entry.AddLine(account, row.Debit, row.Credit, row.Description);
            }

            await using var db = AppServices.CreateDb();
            // بوابة الترحيل الموحدة: تتحقق من الفترة المالية وتمنح الرقم التسلسلي
            await new Brqosh.Infrastructure.Accounting.JournalPostingService(db).PostAsync(entry);

            DialogResult = true;
            Close();
        }
        catch (AccountingException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
