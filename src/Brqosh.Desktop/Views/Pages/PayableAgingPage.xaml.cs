using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط4-٣ب) <b>أعمارُ ديون الموردين — والجانبُ الآخر كان وحدَه مبنياً.</b>
///
/// <para><b>وهو أخطرُ من جانب العملاء لا أهون</b>: تأخّرُ العميل يُكلّف مالاً لم يصل،
/// <b>وتأخّرُ السداد يُكلّف المنشأةَ علاقتَها بمورّدها</b> — يُوقف التوريد، وتُسحب مهلةُ
/// الائتمان، <b>وتتوقّف المنشأةُ لأنّها لم تعرف أنّ فاتورةً بلغت التسعين</b>.</para>
///
/// <para><b>والعمرُ من تاريخ المستند لا من تاريخ استحقاق</b> — كما في جانب العملاء
/// تماماً. <b>وتقريران بمنطقين مختلفين لجانبَي الميزان يجعلان الفرقَ بينهما غيرَ
/// مفهوم.</b></para>
/// </summary>
public partial class PayableAgingPage : UserControl
{
    public PayableAgingPage()
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
            var rows = await new PayableAgingQuery(db).ExecuteAsync(
                AppServices.CompanyId, DateOnly.FromDateTime(AsOfBox.SelectedDate.Value));

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            var total = rows.Sum(r => r.Total);
            var over90 = rows.Sum(r => r.Over90);

            // ‏**وما جاوز التسعين هو الرقمُ الذي يُتّخذ به قرار**: جدولةُ سداد، أو تفاوضٌ
            // ‏على مهلة، أو **استردادُ ضريبةِ مدخلاتٍ خُصمت عن فاتورةٍ لم تُسدَّد**.
            SummaryText.Text = rows.Count == 0
                ? "لا أرصدةَ للموردين حتى هذا التاريخ."
                : $"موردون بأرصدة: {rows.Count:N0} · الإجمالي: {total:N2} · "
                  + $"وما جاوز ٩٠ يومًا: {over90:N2}"
                  + (total > 0m ? $" ({over90 * 100m / total:N1}٪ من الإجمالي)" : string.Empty);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
