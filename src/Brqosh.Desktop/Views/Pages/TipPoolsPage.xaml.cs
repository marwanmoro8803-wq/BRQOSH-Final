using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Hospitality;
using Brqosh.Infrastructure.Hospitality;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط3-٣د) <b>أوعيةُ الإكراميات — والالتزامُ الذي كان يُقيَّد إيراداً.</b>
///
/// <para><b>ما كان:</b> لا شيءَ في النظام يعرف الإكرامية. فالنقديّةُ تدخل الدرجَ
/// <b>فتظهر فائضَ صندوقٍ يبدو خطأً في العدّ</b>، وإكراميةُ البطاقة تصل البنكَ
/// <b>فلا يجد لها النظامُ إلا الإيرادَ موضعاً</b> — فتُدفع ضريبةٌ وزكاةٌ على مالٍ ليس
/// للمنشأة.</para>
///
/// <para><b>وعمودُ «بلا صاحب» هو الشاشةُ كلُّها</b>: مالٌ محصَّلٌ لم يُقرَّر لمن هو،
/// <b>وهو ما يمنع الإقفال</b>. ويبقى في الحساب سنواتٍ ثمّ يُشطب إيراداً — <b>وهو أخذُ
/// مالِ النُّدُل بمرور الوقت</b>.</para>
/// </summary>
public partial class TipPoolsPage : UserControl
{
    public record Row(long Id, string PoolDate, string StatusText, decimal Cash, decimal Card,
        decimal Collected, decimal Distributed, decimal Undistributed, int Shares,
        string PostedText, TipPoolStatus Status, bool IsPosted);

    public TipPoolsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (CloseBtn, PermissionService.CanPostAccounting),
                (PayBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var pools = await db.TipPools
                .Where(p => p.CompanyId == AppServices.CompanyId)
                .OrderByDescending(p => p.PoolDate)
                .ToListAsync();

            var rows = pools.Select(p => new Row(
                p.Id, p.PoolDate.ToString("yyyy-MM-dd"), StatusText(p.Status),
                p.CollectedCash, p.CollectedCard, p.TotalCollected, p.TotalDistributed,
                p.Undistributed, p.Distributions.Count,
                p.JournalEntryId is null ? "لم يُقيَّد" : "مقيَّد",
                p.Status, p.JournalEntryId is not null)).ToList();

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            var orphan = rows.Sum(r => r.Undistributed);
            SummaryText.Text = rows.Count == 0
                ? "لا أوعيةَ بعد — والوعاءُ يجمع إكرامياتِ ورديةٍ أو يوم، ثمّ تُوزَّع حصصاً."
                : $"أوعية: {rows.Count:N0} · المحصَّل: {rows.Sum(r => r.Collected):N2} · "
                  + $"الموزَّع: {rows.Sum(r => r.Distributed):N2}"
                  + (orphan > 0
                      ? $" · ⚠️ بلا صاحب: {orphan:N2} — والتزامٌ بلا صاحبٍ يُشطب بعد سنواتٍ إيراداً."
                      : " · ولا ريالَ بلا صاحب.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        var dialog = new TipPoolDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) await ReloadAsync();
    }

    private async void OnClose(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر وعاءً أولاً.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<TipPoolService>().CloseAndPostAsync(
                AppServices.CompanyId, row.Id, DateOnly.FromDateTime(DateTime.Today));

            SummaryText.Text = $"قُيّد الوعاء: نقدي {result.CollectedCash:N2} · "
                + $"بطاقة {result.CollectedCard:N2} — والمجموع التزامٌ على المنشأة لا إيراد.";

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnPayOut(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر وعاءً أولاً.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<TipPoolService>().PayOutAsync(
                AppServices.CompanyId, row.Id, DateOnly.FromDateTime(DateTime.Today));

            SummaryText.Text = "صُرفت الحصص — ورصيد الالتزام صار صفراً.";
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string StatusText(TipPoolStatus status) =>
        status == TipPoolStatus.Closed ? "مُقفل" : "مفتوح";
}
