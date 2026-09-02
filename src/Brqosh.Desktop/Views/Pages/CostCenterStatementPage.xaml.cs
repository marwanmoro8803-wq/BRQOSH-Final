using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٣) <b>كشفُ مراكز التكلفة — الشاشةُ التي بلا وجودها بقي التقريرُ بلا قارئ.</b>
///
/// <para><b>ما كان:</b> مراكزُ التكلفة تُنشأ وتُسنَد إلى القيود، <b>ولا تقريرَ يعرض ما
/// جُمع فيها</b>. فالتصنيفُ عملٌ يُبذل شهراً بشهر <b>ولا يُقرأ منه شيء</b>.</para>
///
/// <para><b>وغيرُ المصنّف يُعرض صريحاً</b>: قيدٌ بلا مركزٍ يظهر في سطرٍ باسمه، <b>وإخفاؤه
/// يجعل مجموعَ المراكز أقلَّ من قائمة الدخل بلا تفسير</b> — ويظنّ المالكُ أنّ الفرقَ خطأٌ
/// في التقرير وهو نقصٌ في الإدخال.</para>
/// </summary>
public partial class CostCenterStatementPage : UserControl
{
    public record Row(string CostCenter, string AccountCode, string AccountName, string TypeText,
        decimal Debit, decimal Credit, decimal Net);

    public CostCenterStatementPage()
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
        if (FromBox.SelectedDate is null || ToBox.SelectedDate is null)
        {
            UiFeedback.ShowWarning("حدّد المدة أولاً.");
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var rows = await new CostCenterStatementQuery(db).ExecuteAsync(
                AppServices.CompanyId,
                DateOnly.FromDateTime(FromBox.SelectedDate.Value),
                DateOnly.FromDateTime(ToBox.SelectedDate.Value));

            var display = rows.Select(r => new Row(
                $"{r.CostCenterCode} — {r.CostCenterNameAr}", r.AccountCode, r.AccountNameAr,
                r.AccountType == AccountType.Revenue ? "إيراد" : "مصروف",
                r.Debit, r.Credit, r.Net)).ToList();

            Grid.ItemsSource = display;
            UiTestAutomation.PublishGridCount(Grid, display.Count);

            var unclassified = rows.Where(r => r.CostCenterId is null).Sum(r => Math.Abs(r.Net));
            var revenue = rows.Where(r => r.AccountType == AccountType.Revenue).Sum(r => r.Net);
            var expense = rows.Where(r => r.AccountType != AccountType.Revenue).Sum(r => r.Net);

            SummaryText.Text = unclassified == 0m
                ? $"إيراد: {revenue:N2} · مصروف: {expense:N2} · الصافي: {revenue - expense:N2}"
                : $"إيراد: {revenue:N2} · مصروف: {expense:N2} · الصافي: {revenue - expense:N2} · "
                  + $"⚠️ غير مصنَّف على مركز: {unclassified:N2} — والتصنيف يُدخل في القيد.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
