using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط4-٣) <b>أعمارُ ديون العملاء — والنظامُ كان يُحيل إليها ولا وجودَ لها.</b>
///
/// <para><b>ما كان:</b> المساعدُ الماليّ يقول للمالك «راجعْ كشفَ أعمار الديون من تقارير
/// العملاء» — <b>ولا كشفَ ولا تقريرَ ولا استعلام</b>. فالنظامُ يُرسل مالكَه إلى بابٍ
/// لا وجودَ له.</para>
///
/// <para><b>والعمرُ من تاريخ المستند لا من تاريخ استحقاق</b>: لا تحمل الفاتورةُ في هذا
/// النظام مهلةَ سداد، <b>فالحديثُ عن «متأخّر» ادّعاءٌ لا سندَ له</b> — والاسمُ «أعمار
/// الديون» صادقٌ بما يُحسب.</para>
/// </summary>
public partial class ReceivableAgingPage : UserControl
{
    public ReceivableAgingPage()
    {
        InitializeComponent();
        AsOfBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await RunAsync();
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        if (AsOfBox.SelectedDate is null)
        {
            UiFeedback.ShowWarning("حدّد التاريخ أولاً.");
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var rows = await new ReceivableAgingQuery(db).ExecuteAsync(
                AppServices.CompanyId, DateOnly.FromDateTime(AsOfBox.SelectedDate.Value));

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            var total = rows.Sum(r => r.Total);
            var over90 = rows.Sum(r => r.Over90);

            // ‏**وما جاوز التسعين هو الرقمُ الذي يُتّخذ به قرار**: مخصَّصُ ديونٍ مشكوكٍ
            // ‏فيها، أو إيقافُ تعاملٍ، أو إحالةٌ للتحصيل. **ودفنُه في مجموعٍ واحدٍ يُخفيه.**
            SummaryText.Text = rows.Count == 0
                ? "لا أرصدةَ على العملاء حتى هذا التاريخ."
                : $"عملاء بأرصدة: {rows.Count:N0} · الإجمالي: {total:N2} · "
                  + $"وما جاوز ٩٠ يومًا: {over90:N2}"
                  + (total > 0m ? $" ({over90 * 100m / total:N1}٪ من الإجمالي)" : string.Empty);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
