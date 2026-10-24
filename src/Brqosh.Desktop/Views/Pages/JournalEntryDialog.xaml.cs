using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class JournalEntryDialog : Window
{
    public record AccountOption(long Id, string Display);
    public record CostCenterOption(long Id, string Display);
    public record CompanyOption(long Id, string Display);

    public class LineRow
    {
        public long? AccountId { get; set; }
        public long? CostCenterId { get; set; }
        public string? Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    private readonly ObservableCollection<LineRow> _rows = [];
    private List<Account> _postableAccounts = [];
    private List<Domain.Accounting.CostCenter> _costCenters = [];

    public JournalEntryDialog()
    {
        InitializeComponent();
        CurrencyBox.Text = Brqosh.Desktop.Ui.CurrencyLabel.Code;
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) =>
        {
            await LoadAccountsAsync();
            await LoadCostCentersAsync();
            await LoadCounterpartyCompaniesAsync();
        };
    }

    private async Task LoadCounterpartyCompaniesAsync()
    {
        await using var db = AppServices.CreateDb();
        var company = await db.Companies.AsNoTracking()
            .SingleAsync(c => c.Id == AppServices.CompanyId);
        if (company.CompanyGroupId is null)
            return;

        var sisters = await db.Companies.AsNoTracking()
            .Where(c => c.CompanyGroupId == company.CompanyGroupId && c.Id != AppServices.CompanyId)
            .OrderBy(c => c.NameAr)
            .Select(c => new CompanyOption(c.Id, c.NameAr))
            .ToListAsync();
        if (sisters.Count == 0)
            return;

        CounterpartyCombo.ItemsSource = sisters;
        CounterpartyCombo.SelectedIndex = 0;
        IntercompanyPanel.Visibility = Visibility.Visible;
    }

    private void OnIntercompanyToggled(object sender, RoutedEventArgs e)
    {
        CounterpartyCombo.IsEnabled = IntercompanyCheck.IsChecked == true;
    }

    private async Task LoadCostCentersAsync()
    {
        await using var db = AppServices.CreateDb();
        _costCenters = await db.CostCenters
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.Code)
            .ToListAsync();

        CostCenterColumn.ItemsSource = _costCenters
            .Select(c => new CostCenterOption(c.Id, $"{c.Code} — {c.NameAr}"))
            .ToList();
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

    /// <summary>
    /// (CEEF-231 · O-34) يقرأ عملةَ المعاملة وسعرَها — <b>والريالُ يعني «لا مذكرة»</b>.
    /// </summary>
    /// <remarks>
    /// <b>وسعرٌ لا يُقرأ يُرفض ولا يُقرأ واحداً</b>: صفرٌ أو نصٌّ يُبتلع صامتاً يُرحّل
    /// القيدَ بأرقام العملة الأجنبية كأنّها ريالات — <b>وهو خطأٌ لا يظهر في ميزانٍ لأنّه
    /// متوازن</b>، ولا يُكتشف إلا بمطابقةِ حسابٍ بنكيٍّ بعد شهور.
    /// </remarks>
    private (string Code, decimal Rate)? ReadCurrency()
    {
        var code = (CurrencyBox.Text ?? Brqosh.Desktop.Ui.CurrencyLabel.Code).Trim().ToUpperInvariant();
        if (code.Length == 0) code = Brqosh.Desktop.Ui.CurrencyLabel.Code;
        if (code == Brqosh.Desktop.Ui.CurrencyLabel.Code) return null;

        if (!NumericInput.TryParsePositive(RateBox.Text, out var rate))
            throw new AccountingException($"سعر الصرف غير صالح — ووردَ «{RateBox.Text}».");

        return (code, rate);
    }

    /// <summary>
    /// يُحدّث الإجماليات عند تغيّر العملة أو السعر.
    /// </summary>
    /// <remarks>
    /// <b>والحارسُ ليس زينة</b>: <c>TextChanged</c> يقع أثناء <c>InitializeComponent</c>
    /// حين يُضبط <c>Text="SAR"</c> في الوسم، <b>وعناصرُ الإجماليات تُبنى بعد الحقل</b> —
    /// فتُقرأ فارغةً ويسقط الحوارُ قبل أن يُعرض.
    /// </remarks>
    private void OnCurrencyChanged(object sender, TextChangedEventArgs e)
    {
        if (TranslationText is null || TotalDebitText is null) return;
        UpdateTotals();
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

        // ‏**والمترجَمُ يُعرض قبل الترحيل لا بعده** — فمن يُدخل بعملةٍ أجنبيةٍ يرى
        // ‏بالريال ما سيدخل الدفتر، **ولا يكتشف السعرَ الخاطئ من كشف حسابٍ بعد شهر**.
        try
        {
            if (ReadCurrency() is (string code, decimal rate))
            {
                TranslationText.Text =
                    $"= {Math.Round(debit * rate, 2, MidpointRounding.AwayFromZero):N2} ريال "
                    + $"({code} × {rate:N4})";
                TranslationText.Foreground = System.Windows.Media.Brushes.DimGray;
                TranslationText.Visibility = Visibility.Visible;
            }
            else
            {
                TranslationText.Visibility = Visibility.Collapsed;
            }
        }
        catch (AccountingException ex)
        {
            TranslationText.Text = ex.Message;
            TranslationText.Foreground = System.Windows.Media.Brushes.Red;
            TranslationText.Visibility = Visibility.Visible;
        }
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        // (CEEF-229 · م0-١٦) ضغطة مزدوجة كانت تُنشئ مستندين — الزرّ لا يُعطَّل أثناء الانتظار.
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (DatePickerBox.SelectedDate is null)
                throw new AccountingException("تاريخ القيد مطلوب.");

            var entryDate = DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value);
            var entry = JournalEntry.CreateDraft(AppServices.CompanyId, entryDate, DescriptionBox.Text);

            var accountById = _postableAccounts.ToDictionary(a => a.Id);
            var currency = ReadCurrency();

            foreach (var row in _rows.Where(r => r.AccountId is not null || r.Debit != 0 || r.Credit != 0))
            {
                if (row.AccountId is null || !accountById.TryGetValue(row.AccountId.Value, out var account))
                    throw new AccountingException("اختر الحساب لكل سطر في القيد.");

                if (currency is not (string code, decimal rate))
                {
                    entry.AddLine(account, row.Debit, row.Credit, row.Description, row.CostCenterId);
                    continue;
                }

                // ‏**المُدخَلُ بعملة المعاملة، والمُرحَّلُ بالريال** — والمذكرةُ تحفظ الأصلَ والسعر.
                var foreign = row.Debit + row.Credit;
                var functional = Math.Round(foreign * rate, 2, MidpointRounding.AwayFromZero);

                entry.AddLine(account,
                    row.Debit > 0 ? functional : 0m,
                    row.Credit > 0 ? functional : 0m,
                    row.Description, row.CostCenterId, code, rate, foreign);
            }

            if (IntercompanyCheck.IsChecked == true)
            {
                if (CounterpartyCombo.SelectedValue is not long counterpartyId)
                    throw new AccountingException("اختر الشركة المقابلة للمعاملة بين الشركات.");
                entry.MarkIntercompany(counterpartyId);
            }

            using var facade = new DesktopAccountingFacade();
            await facade.PostJournalEntryAsync(entry);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
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
