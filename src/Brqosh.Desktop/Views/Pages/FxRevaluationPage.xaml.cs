using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٥) <b>إعادةُ تقييم الأرصدة بالعملات — والخدمةُ كانت بلا زرّ.</b>
///
/// <para><b>ما كان:</b> <c>FxRevaluationBatchService</c> مبنيّةٌ ومسجَّلةٌ في الحقن،
/// <b>ولا شيءَ يستدعيها</b>. فحسابٌ بنكيٌّ بالدولار يبقى في الدفاتر <b>بسعر يوم كلّ
/// إيداع</b> إلى الأبد — <b>وقائمةُ المركز المالي تُظهر رصيداً لا يساوي ما في البنك</b>.
/// وهو خطأٌ يكبر كلَّما تحرّك السعر.</para>
///
/// <para><b>والرصيدُ الأجنبيُّ يُدخَل يدوياً</b>: النظامُ لا يخزّنه — يخزّن الرياليَّ
/// وحدَه. <b>وهذا حدٌّ مُعلَنٌ في الخدمة نفسِها</b>، والشاشةُ تطلبه صراحةً بدل أن تخترعه.</para>
/// </summary>
public partial class FxRevaluationPage : UserControl
{
    public class Row
    {
        public long BankAccountId { get; set; }
        public string? Name { get; set; }
        public string? Currency { get; set; }
        public decimal ForeignBalance { get; set; }
    }

    private readonly ObservableCollection<Row> _rows = [];

    public FxRevaluationPage()
    {
        InitializeComponent();
        AsOfBox.SelectedDate = DateTime.Today;
        Grid.ItemsSource = _rows;

        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (RunBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var company = await db.Companies.AsNoTracking()
                .SingleAsync(c => c.Id == AppServices.CompanyId);

            var banks = await db.BankAccounts.AsNoTracking()
                .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive
                            && b.CurrencyCode != company.FunctionalCurrencyCode)
                .OrderBy(b => b.NameAr)
                .Select(b => new { b.Id, b.NameAr, b.CurrencyCode })
                .ToListAsync();

            _rows.Clear();
            foreach (var bank in banks)
                _rows.Add(new Row { BankAccountId = bank.Id, Name = bank.NameAr, Currency = bank.CurrencyCode });

            UiTestAutomation.PublishGridCount(Grid, _rows.Count);

            SummaryText.Text = _rows.Count == 0
                ? $"لا حساباتٍ بنكيةً بعملةٍ تخالف عملة الدفاتر ({company.FunctionalCurrencyCode}) — ولا شيءَ يُعاد تقييمه."
                : $"حسابات بعملةٍ أجنبية: {_rows.Count:N0} — أدخِلْ رصيدَ كلٍّ بعملته ثم أعِدْ التقييم.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        if (AsOfBox.SelectedDate is null)
        {
            UiFeedback.ShowWarning("حدّد التاريخ أولاً.");
            return;
        }

        Grid.CommitEdit(DataGridEditingUnit.Row, true);

        var balances = _rows
            .Where(r => r.ForeignBalance > 0m)
            .ToDictionary(r => r.BankAccountId, r => r.ForeignBalance);

        if (balances.Count == 0)
        {
            // ‏**ورصيدٌ صفرٌ ليس إذناً بالتقييم**: الخدمةُ ترفض رصيداً غيرَ موجب، وتشغيلٌ
            // ‏بلا أرصدةٍ يُخرج رسالةَ رفضٍ غامضة. **فالسببُ يُقال هنا.**
            UiFeedback.ShowWarning("أدخِلْ رصيدَ حسابٍ واحدٍ على الأقلّ بعملته الأجنبية.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var entries = await facade.GetService<FxRevaluationBatchService>()
                .RevalueAllForeignBanksAsync(
                    AppServices.CompanyId, DateOnly.FromDateTime(AsOfBox.SelectedDate.Value),
                    SecurityContext.ResolveUsername(), balances);

            SummaryText.Text = entries.Count == 0
                ? "لا فرقَ عند هذا السعر — ولم يُقيَّد شيء."
                : $"قُيّد {entries.Count:N0} قيدَ إعادة تقييم.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal Parse(string? text) =>
        NumericInput.TryParse(text, out var v)
            ? v
            : 0m;
}
