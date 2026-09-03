using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط1-٦ب) <b>إدخالُ الموازنة — و<c>Budget.SetLine</c> كان بلا مستدعٍ في الواجهة.</b>
///
/// <para><b>ما كان:</b> بُنيت الموازناتُ في <c>ط1-٦</c> ومعها <c>BudgetVarianceQuery</c>
/// وشاشةُ الانحراف. <b>وتلك الشاشةُ تقرأ ولا تكتب</b> — ولا موضعَ في المستودع كلِّه
/// يُنشئ موازنةً أو يضع لها سطراً إلا الاختبارات. <b>فتقريرُ الانحراف يفتح على قائمةٍ
/// فارغةٍ إلى الأبد</b>، والمنشأةُ ترى ميزةً تُعلَن ولا تُستعمل.</para>
///
/// <para><b>والحسابُ يُختار من الدليل لا يُكتب</b>: رمزٌ يُكتب بيدٍ يُخزَّن كما كُتب،
/// <b>وحرفٌ زائدٌ يُنشئ سطرَ موازنةٍ لحسابٍ لا وجودَ له</b> — فلا يُقابَل بشيءٍ في التقرير
/// ويظهر «مُخطَّطاً بلا إنفاق» إلى الأبد.</para>
///
/// <para><b>والمجموعاتُ لا تُخطَّط</b>: الحسابُ التجميعيُّ لا يستقبل قيداً، <b>فموازنةٌ
/// عليه تُقابَل بصفرٍ دائماً</b>.</para>
/// </summary>
public partial class BudgetDialog : Window
{
    /// <summary>خيارُ حسابٍ في القائمة — <b>ورقمُه هو ما يُخزَّن</b>.</summary>
    public sealed record AccountOption(string Code, string Label);

    /// <summary>سطرُ موازنةٍ يُحرَّر.</summary>
    public class LineRow
    {
        public string? AccountCode { get; set; }
        public int Month { get; set; } = 1;
        public decimal Amount { get; set; }
    }

    private readonly long? _budgetId;
    private readonly ObservableCollection<LineRow> _rows = [];

    public BudgetDialog()
    {
        InitializeComponent();
        YearBox.Text = DateTime.Today.Year.ToString();
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadAccountsAsync();
    }

    public BudgetDialog(Budget budget) : this()
    {
        _budgetId = budget.Id;
        Title = "تعديل موازنة";

        CodeBox.Text = budget.Code;
        CodeBox.IsEnabled = false;
        NameBox.Text = budget.NameAr;
        YearBox.Text = budget.Year.ToString();
        YearBox.IsEnabled = false;

        foreach (var line in budget.Lines)
        {
            // ‏رمزُ الفترة `YYYY-MM` — والشهرُ آخرُ مقطعيه.
            var month = int.TryParse(line.PeriodCode.Split('-').Last(), out var m) ? m : 1;
            _rows.Add(new LineRow { AccountCode = line.AccountCode, Month = month, Amount = line.Amount });
        }
    }

    /// <summary>
    /// حساباتُ الدليل غيرُ التجميعية — <b>وهي وحدَها ما يُخطَّط</b>.
    /// </summary>
    private async Task LoadAccountsAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var accounts = await db.Accounts.AsNoTracking()
                .Where(a => a.CompanyId == AppServices.CompanyId && !a.IsGroup)
                .OrderBy(a => a.Code)
                .Select(a => new { a.Code, a.NameAr })
                .ToListAsync();

            AccountColumn.ItemsSource = accounts
                .Select(a => new AccountOption(a.Code, $"{a.Code} — {a.NameAr}"))
                .ToList();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            ErrorText.Visibility = Visibility.Collapsed;

            if (!int.TryParse(YearBox.Text, out var year) || year < 2000)
                throw new AccountingException("السنة غير صالحة.");

            var lines = _rows.Where(r => !string.IsNullOrWhiteSpace(r.AccountCode)).ToList();
            if (lines.Count == 0)
                throw new AccountingException("أضف سطراً واحداً على الأقل — وموازنةٌ بلا سطور لا تُقابَل بشيء.");

            await using var db = AppServices.CreateDb();

            var budget = _budgetId is long id
                ? await db.Budgets.SingleAsync(b => b.Id == id)
                : Budget.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text, year);

            // ‏**والمعتمدةُ تُعاد مفتوحةً قبل التعديل** — والنطاقُ يرفض تعديلَها،
            // ‏**ورفضٌ بعد ملء الشاشة أسوأُ من فتحٍ صريح**. والفتحُ قرارُ المستخدم
            // ‏لأنّه يُبطل اعتماداً وُقّع.
            if (_budgetId is not null && budget.Status != BudgetStatus.Draft)
                throw new AccountingException(
                    "الموازنة معتمدةٌ أو مقفلة — أعِدْ فتحَها من شاشة الموازنات قبل تعديلها.");

            foreach (var row in lines)
                budget.SetLine(row.AccountCode!, row.Month, row.Amount);

            if (_budgetId is null)
                db.Budgets.Add(budget);

            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex is AccountingException ae ? ae.Message : ex.Message);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>صفوفُ الشبكة — للاختبار وحده، كي يملأها كما يملؤها المستخدم.</summary>
    internal ObservableCollection<LineRow> RowsForTests => _rows;

    /// <summary>بانِي الموازنة — <b>وهو ما يستدعيه زرُّ الحفظ نفسُه</b>.</summary>
    internal Budget BuildBudgetFromRows(long companyId, string code, string nameAr, int year)
    {
        var budget = Budget.Create(companyId, code, nameAr, year);

        foreach (var row in _rows.Where(r => !string.IsNullOrWhiteSpace(r.AccountCode)))
            budget.SetLine(row.AccountCode!, row.Month, row.Amount);

        return budget;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
