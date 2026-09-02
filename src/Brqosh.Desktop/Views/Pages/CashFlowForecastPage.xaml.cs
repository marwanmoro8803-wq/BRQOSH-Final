using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Treasury;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط4-٣ج) <b>التدفّقُ النقديُّ المتوقَّع — خدمةٌ مبنيّةٌ بلا شاشةٍ تفتحها.</b>
///
/// <para><b>ما كان:</b> <c>CashFlowForecastService</c> مبنيّةٌ ومختبَرة، <b>ولا تظهر إلا
/// رقماً واحداً في زاوية لوحة التحكّم</b> — لا مفتاحَ في القائمة ولا شاشة. <b>وتوقّعُ
/// ثلاثةَ عشرَ أسبوعاً مضغوطاً في رقمٍ واحدٍ ليس توقّعاً</b>: العجزُ يقع في أسبوعٍ بعينه،
/// ومجموعُ ثلاثةَ عشرَ أسبوعاً موجباً <b>يُخفي أسبوعاً لا يُدفع فيه راتب</b>.</para>
///
/// <para><b>وهو ما تُدار به السيولةُ فعلاً</b>: متى يُسدَّد المورّد، ومتى يُطلب تسهيل،
/// ومتى تُؤجَّل شحنة. <b>وبغيره تُتَّخذ هذه القراراتُ من رصيد اليوم</b> — وهو أسوأُ
/// مؤشّرٍ ممكن، لأنّه لا يعرف ما استُحقّ ولم يُقبَض بعد.</para>
/// </summary>
public partial class CashFlowForecastPage : UserControl
{
    public CashFlowForecastPage()
    {
        InitializeComponent();
        AsOfBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await RunAsync();
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        try
        {
            var asOf = AsOfBox.SelectedDate is DateTime d ? DateOnly.FromDateTime(d) : (DateOnly?)null;

            // ‏**والخدمةُ من الحقن لا بالبناء المباشر** — واصطلاحُ هذا المستودع مقيسٌ
            // ‏بحارسٍ (`DesktopDiGuardTests`): شاشةٌ تبني خدمةً بيدها تتجاوز كلَّ ما
            // ‏يُلبَس عليها في التركيب — من سياقٍ ومعاملةٍ وتتبّع.
            using var scope = AppServices.CreateInfrastructureScope();
            var result = await scope.ServiceProvider.GetRequiredService<ICashFlowForecastService>()
                .ForecastAsync(AppServices.CompanyId, asOf);

            Grid.ItemsSource = result.Weeks;
            UiTestAutomation.PublishGridCount(Grid, result.Weeks.Count);

            // ‏**والأسبوعُ السالبُ هو الخبر** — لا المجموع. فمجموعٌ موجبٌ يُخفي أسبوعاً
            // ‏لا يُدفع فيه راتب، **والعجزُ يقع في أسبوعٍ بعينه لا في المتوسّط**.
            var worst = result.Weeks.OrderBy(w => w.NetCashFlow).FirstOrDefault();
            var net = result.TotalProjectedInflows - result.TotalProjectedOutflows;

            SummaryText.Text = worst is null
                ? "لا ذممَ مستحقةٌ في الأفق — ولا توقّعَ بلا مستندات."
                : $"صافي ثلاثة عشر أسبوعاً: {net:N2} · "
                  + $"وأضعفُ أسبوعٍ هو {worst.WeekIndex} ({worst.WeekStart:yyyy-MM-dd}) بصافي {worst.NetCashFlow:N2}"
                  + (worst.NetCashFlow < 0m ? " — وهو عجزٌ يقع فيه لا في المتوسط." : string.Empty);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
