using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٢) <b>قسائمُ الرواتب — الشاشةُ التي بلا وجودها بقي `ط2-٦` وعداً.</b>
///
/// <para><b>ما كان:</b> <c>PayslipQuery</c> و<c>PayslipPdfGenerator</c> مبنيّان ومختبَران
/// في <c>ط2-٦</c>، <b>ولا شاشةَ ولا حتّى مستدعٍ في خدمة الطباعة</b>. فالمالكُ لا يستطيع
/// إعطاءَ موظّفٍ ورقةً تشرح راتبَه — <b>وهي إلزامٌ نظاميّ</b>، ويحتاجها الموظّفُ لبنكه
/// وتأشيرته وقرضه.</para>
///
/// <para><b>ولا تُعرض إلا الدوراتُ المرحّلة</b>: المسودّةُ تتغيّر، <b>وقسيمةٌ سُلّمت ثمّ
/// تغيّر أصلُها خلافٌ لا يُحسم</b>. والقيدُ في <c>PayslipQuery</c> نفسِها، والشاشةُ لا
/// تعرض ما سيُرفض.</para>
/// </summary>
public partial class PayslipsPage : UserControl
{
    public record Row(int Index, string Code, string EmployeeName, string NationalId, string JobTitle,
        decimal Basic, decimal Housing, decimal Transport, decimal Other, decimal Gosi, decimal Net);

    private sealed record RunOption(long Id, string Label)
    {
        public override string ToString() => Label;
    }

    public PayslipsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadRunsAsync();
    }

    private async Task LoadRunsAsync()
    {
        await using var db = AppServices.CreateDb();

        var runs = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.CompanyId == AppServices.CompanyId && r.Status == PayrollRunStatus.Posted)
            .OrderByDescending(r => r.PayDate)
            .Select(r => new { r.Id, r.PeriodCode, r.PayDate })
            .ToListAsync();

        RunBox.ItemsSource = runs
            .Select(r => new RunOption(r.Id, $"{r.PeriodCode} — صُرفت في {r.PayDate:yyyy-MM-dd}"))
            .ToList();

        if (runs.Count == 0)
        {
            Grid.ItemsSource = null;
            UiTestAutomation.PublishGridCount(Grid, 0);
            SummaryText.Text = "لا دورةَ رواتبٍ مرحّلة بعد — والقسيمة لا تُصدَر من مسودّة.";
            PrintBtn.IsEnabled = false;
            return;
        }

        PrintBtn.IsEnabled = true;
        RunBox.SelectedIndex = 0;
    }

    private async void OnRunChanged(object sender, SelectionChangedEventArgs e) => await ReloadAsync();

    private async Task ReloadAsync()
    {
        if (RunBox.SelectedItem is not RunOption option) return;

        try
        {
            await using var db = AppServices.CreateDb();
            var slips = await new PayslipQuery(db).ExecuteAsync(AppServices.CompanyId, option.Id);

            var rows = slips.Select((s, i) => new Row(i, s.EmployeeCode, s.EmployeeName, s.NationalId,
                s.JobTitle, s.BasicSalary, s.HousingAllowance, s.TransportAllowance,
                s.OtherAllowance, s.GosiEmployee, s.NetPay)).ToList();

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            // ‏**وسطرٌ بلا سجلٍّ يخرج قسيمةً كذلك** — والناقصُ هويّةٌ ومسمّى، يُرى في
            // ‏الورقة ولا يُخفيها. **وحجبُها يحرم موظّفاً حقّاً لأنّ سجلَّه لم يُنشأ بعد.**
            var withoutRecord = rows.Count(r => r.Code == "—");
            SummaryText.Text = withoutRecord == 0
                ? $"قسائم: {rows.Count:N0} · إجمالي الصافي: {rows.Sum(r => r.Net):N2}"
                : $"قسائم: {rows.Count:N0} · إجمالي الصافي: {rows.Sum(r => r.Net):N2} · "
                  + $"{withoutRecord:N0} منها بلا سجل موظف — تخرج بأرقامها وينقصها التعريف.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnPrint(object sender, RoutedEventArgs e)
    {
        if (RunBox.SelectedItem is not RunOption option)
        {
            UiFeedback.ShowWarning("اختر دورةَ رواتبٍ أولاً.");
            return;
        }

        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر قسيمةَ موظفٍ من الجدول.");
            return;
        }

        try
        {
            await DocumentPrintHelper.ExportAndDeliverAsync(
                service => service.ExportPayslipAsync(
                    AppServices.CompanyId, option.Id, row.Index, AppServices.CurrentUser?.Username ?? "system"),
                Window.GetWindow(this));
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
