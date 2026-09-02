using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٦) <b>حركةُ النقد والمركز النقديّ — والخدمةُ كانت بلا شاشة.</b>
///
/// <para><b>ما كان:</b> <c>CashMovementReportService</c> مبنيّةٌ ومسجَّلة، <b>ولا شاشةَ
/// تعرضها</b>. فالمالكُ يرى أرصدةَ الحسابات في اللوحة <b>ولا يرى ما دخل وما خرج</b> —
/// وهما سؤالان مختلفان: <b>رصيدٌ ثابتٌ قد يُخفي دخولَ مليونٍ وخروجَه</b>.</para>
///
/// <para><b>والمركزُ الموحَّد في العنوان</b>: مجموعُ ما في الحسابات كلِّها حتّى التاريخ —
/// وهو الرقمُ الذي يُسأل عنه قبل أيّ التزام.</para>
/// </summary>
public partial class CashPositionPage : UserControl
{
    public record Row(string Name, decimal Inflows, decimal Outflows, decimal Net, int Count);

    public CashPositionPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromBox.SelectedDate = new DateTime(today.Year, today.Month, 1);
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

        var from = DateOnly.FromDateTime(FromBox.SelectedDate.Value);
        var to = DateOnly.FromDateTime(ToBox.SelectedDate.Value);

        try
        {
            using var facade = new DesktopAccountingFacade();
            var service = facade.GetService<CashMovementReportService>();

            var banks = await facade.Db.BankAccounts.AsNoTracking()
                .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
                .OrderBy(b => b.NameAr)
                .Select(b => new { b.Id, b.NameAr })
                .ToListAsync();

            var rows = new List<Row>();
            foreach (var bank in banks)
            {
                var movement = await service.GetBankMovementAsync(
                    AppServices.CompanyId, bank.Id, from, to);

                rows.Add(new Row(bank.NameAr, movement.TotalInflows, movement.TotalOutflows,
                    movement.NetMovement, movement.TransactionCount));
            }

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            var position = await service.GetConsolidatedCashPositionAsync(AppServices.CompanyId, to);

            SummaryText.Text = rows.Count == 0
                ? "لا حساباتٍ بنكيةً نشطة."
                : $"الداخل: {rows.Sum(r => r.Inflows):N2} · الخارج: {rows.Sum(r => r.Outflows):N2} · "
                  + $"الصافي: {rows.Sum(r => r.Net):N2} · والمركز النقديّ الموحّد: {position:N2}";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
