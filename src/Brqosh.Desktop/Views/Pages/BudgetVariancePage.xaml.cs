using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٣) <b>الموازنةُ والانحراف — الشاشةُ التي بلا وجودها بقيت الموازنةُ جدولاً.</b>
///
/// <para><b>ما كان:</b> الموازنةُ وتقريرُ انحرافها مبنيّان في <c>ط2</c>، <b>ولا شاشةَ
/// تعرضهما</b>. فالمنشأةُ تُخطّط ولا تعرف أين خالفت خطّتَها.</para>
///
/// <para><b>والانحرافُ الموجب صالحٌ للمنشأة في الحالتين</b>: المصروفُ أقلَّ من مُخطَّطه
/// <b>وفرٌ</b>، والإيرادُ أكثرَ من مُخطَّطه <b>زيادة</b>. <b>وتوحيدُ الإشارة بالطرح المجرَّد
/// يجعل نصفَ الجدول يُقرأ بالمقلوب</b>.</para>
/// </summary>
public partial class BudgetVariancePage : UserControl
{
    public record Row(string AccountCode, string AccountName, string CostCenter,
        decimal Planned, decimal Actual, decimal Variance);

    private sealed record BudgetOption(long Id, string Label)
    {
        public override string ToString() => Label;
    }

    public BudgetVariancePage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromBox.SelectedDate = new DateTime(today.Year, 1, 1);
        ToBox.SelectedDate = today;
        Loaded += async (_, _) => await LoadBudgetsAsync();
    }

    private async Task LoadBudgetsAsync()
    {
        await using var db = AppServices.CreateDb();
        var budgets = await db.Budgets.AsNoTracking()
            .Where(b => b.CompanyId == AppServices.CompanyId)
            .OrderByDescending(b => b.Year)
            .Select(b => new { b.Id, b.Code, b.NameAr, b.Year })
            .ToListAsync();

        BudgetBox.ItemsSource = budgets
            .Select(b => new BudgetOption(b.Id, $"{b.Code} — {b.NameAr} ({b.Year})"))
            .ToList();

        if (budgets.Count == 0)
        {
            SummaryText.Text = "لا موازناتٍ بعد — والانحراف لا يُحسب بلا خطة.";
            RunBtn.IsEnabled = false;
            return;
        }

        BudgetBox.SelectedIndex = 0;
        await RunAsync();
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        if (BudgetBox.SelectedItem is not BudgetOption option) return;
        if (FromBox.SelectedDate is null || ToBox.SelectedDate is null)
        {
            UiFeedback.ShowWarning("حدّد المدة أولاً.");
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var rows = await new BudgetVarianceQuery(db).ExecuteAsync(
                AppServices.CompanyId, option.Id,
                DateOnly.FromDateTime(FromBox.SelectedDate.Value),
                DateOnly.FromDateTime(ToBox.SelectedDate.Value));

            var display = rows.Select(r => new Row(r.AccountCode, r.AccountNameAr,
                r.CostCenterNameAr, r.Planned, r.Actual, r.Variance)).ToList();

            Grid.ItemsSource = display;
            UiTestAutomation.PublishGridCount(Grid, display.Count);

            // ‏**وما أُنفق بلا خطةٍ يُعرض** — سطرٌ مُخطَّطُه صفرٌ وفعليُّه رقم. **وإخفاؤه
            // ‏يُظهر التزامَ المنشأة بخطّتها وهي أنفقت خارجَها.**
            var unplanned = display.Count(r => r.Planned == 0m && r.Actual != 0m);
            var overrun = display.Count(r => r.Variance < 0m);

            SummaryText.Text = $"بنود: {display.Count:N0} · تجاوزت خطّتها: {overrun:N0}"
                + (unplanned > 0 ? $" · ⚠️ أُنفقت بلا خطة: {unplanned:N0}" : string.Empty);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
