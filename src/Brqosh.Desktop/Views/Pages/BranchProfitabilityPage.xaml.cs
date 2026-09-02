using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط4-٢) <b>ربحيّةُ الفروع — والبيانات كانت كلُّها موجودةً ولا يقرؤها تقرير.</b>
///
/// <para>كلُّ مستندٍ يحمل <c>BranchId</c> وكلُّ قيدٍ كذلك، ومنتقي الفرع مبنيٌّ منذ
/// <c>CEEF-024</c>. <b>وصفرُ تقريرٍ في المستودع كلِّه كان يُرشِّح بالفرع.</b> فمنشأةٌ
/// بثلاثة فروعٍ تعرف ربحَها مجموعاً <b>ولا تعرف أيَّ فرعٍ يكسبه</b> — وقد يكون فرعٌ
/// يخسر ويُخفيه مجموعُ إخوته.</para>
/// </summary>
public partial class BranchProfitabilityPage : UserControl
{
    public BranchProfitabilityPage()
    {
        InitializeComponent();

        var today = DateTime.Today;
        FromBox.SelectedDate = new DateTime(today.Year, 1, 1);
        ToBox.SelectedDate = today;

        Loaded += async (_, _) => await RunAsync();
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        try
        {
            if (FromBox.SelectedDate is null || ToBox.SelectedDate is null)
                throw new AccountingException("حدّد الفترة أولاً.");

            var from = DateOnly.FromDateTime(FromBox.SelectedDate.Value);
            var to = DateOnly.FromDateTime(ToBox.SelectedDate.Value);

            await using var db = AppServices.CreateDb();
            var result = await new BranchProfitabilityQuery(db)
                .ExecuteAsync(AppServices.CompanyId, from, to);

            Grid.ItemsSource = result.Rows;
            UiTestAutomation.PublishGridCount(Grid, result.Rows.Count);

            // ‏**والخاسرُ هو الخبر** — لا المجموع. فمجموعٌ موجبٌ يُخفي فرعاً يأكل ربحَ
            // ‏إخوته، **وهو القرارُ الذي بُني هذا التقريرُ لأجله**.
            var losing = result.Rows.Where(r => r.NetIncome < 0m).ToList();

            SummaryText.Text = result.Rows.Count == 0
                ? "لا حركةَ إيراداتٍ أو مصروفاتٍ في هذه الفترة."
                : $"فروع: {result.Rows.Count:N0} · صافي الدخل: {result.NetIncome:N2}"
                  + (losing.Count > 0
                      ? $" — وفروعٌ خاسرة: {string.Join("، ", losing.Select(r => $"{r.BranchNameAr} ({r.NetIncome:N2})"))}"
                      : " — ولا فرعَ خاسر.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
