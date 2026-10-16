using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.RealEstate;
using Brqosh.Infrastructure.RealEstate;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط3-١ب) <b>أوامرُ الصيانة — والوحدةُ التي تأكل ربحَ المحفظة.</b>
///
/// <para><b>ما كان:</b> المؤجِّرُ يُصلح ويدفع، <b>والمبلغُ مصروفُ صيانةٍ عامٌّ بلا رابطٍ
/// بوحدة</b>. فمحفظةٌ بأربعين وحدةً تُظهر ربحاً إجمالياً <b>ولا تقول أيَّ وحدةٍ تخسر</b> —
/// وثلاثُ وحداتٍ قد تأكل ربحَ السبعةِ والثلاثين، <b>فيُجدَّد عقدُ الخاسرة بالسعر نفسِه</b>.</para>
///
/// <para><b>وجدولُ الوحدات مُرتَّبٌ بالأعلى تكلفةً</b>: الآكلةُ في الأعلى — <b>ورقمٌ لا
/// يُرى أوّلاً لا يُعالَج</b>.</para>
///
/// <para><b>وعمودُ «على المستأجر» هو سندُ المصادرة</b>: ما يُحمَّل عليه يصير فاتورةً عند
/// الترحيل، <b>وبها وحدَها يجوز أن يُصادَر تأمينُه</b> — فمصادرةٌ بلا دَينٍ دعوى يخسرها
/// المؤجِّر.</para>
/// </summary>
public partial class MaintenanceOrdersPage : UserControl
{
    public record Row(long Id, string OrderNumber, string UnitLabel, string KindText,
        string Description, string StatusText, decimal TotalCost, decimal TenantCharge,
        decimal OwnerCost, string PostedText, MaintenanceStatus Status);

    public MaintenanceOrdersPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromBox.SelectedDate = new DateTime(today.Year, 1, 1);
        ToBox.SelectedDate = today;

        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (PostBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        if (FromBox.SelectedDate is null || ToBox.SelectedDate is null) return;

        try
        {
            var from = DateOnly.FromDateTime(FromBox.SelectedDate.Value);
            var to = DateOnly.FromDateTime(ToBox.SelectedDate.Value);

            await using var db = AppServices.CreateDb();

            var orders = await db.MaintenanceOrders
                .Where(m => m.CompanyId == AppServices.CompanyId
                            && m.RequestDate >= from && m.RequestDate <= to)
                .OrderByDescending(m => m.RequestDate)
                .ToListAsync();

            var unitIds = orders.Where(o => o.UnitId != null).Select(o => o.UnitId!.Value)
                .Distinct().ToList();
            var units = await db.PropertyUnits.AsNoTracking()
                .Where(u => unitIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UnitNumber);

            var rows = orders.Select(o => new Row(
                o.Id, o.OrderNumber,
                o.UnitId is long id && units.TryGetValue(id, out var n) ? n : "مرافق مشتركة",
                KindText(o.Kind), o.Description, StatusText(o.Status),
                o.TotalCost, o.TenantChargeAmount, o.OwnerCost,
                o.JournalEntryId is null ? "لم يُرحَّل" : "مُرحَّل",
                o.Status)).ToList();

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            using var facade = new DesktopAccountingFacade();
            var byUnit = await facade.GetService<MaintenanceOrderService>()
                .CostByUnitAsync(AppServices.CompanyId, from, to);

            UnitGrid.ItemsSource = byUnit;

            var unposted = rows.Count(r => r.Status == MaintenanceStatus.Completed
                                           && r.PostedText == "لم يُرحَّل");

            SummaryText.Text = rows.Count == 0
                ? "لا أوامرَ في هذه المدة — والصيانةُ بلا أمرٍ تُقيَّد مصروفاً عامّاً لا يقول أيَّ وحدةٍ أكلته."
                : $"أوامر: {rows.Count:N0} · التكلفة: {rows.Sum(r => r.TotalCost):N2} · "
                  + $"على المالك: {rows.Sum(r => r.OwnerCost):N2} · "
                  + $"على المستأجرين: {rows.Sum(r => r.TenantCharge):N2}"
                  + (unposted > 0
                      ? $" · ⚠️ منتهيةٌ بلا ترحيل: {unposted:N0} — وتكلفتُها ليست في القوائم بعد."
                      : "");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        var dialog = new MaintenanceOrderDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) await ReloadAsync();
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر أمراً أولاً.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<MaintenanceOrderService>().PostAsync(
                AppServices.CompanyId, row.Id, DateOnly.FromDateTime(DateTime.Today));

            SummaryText.Text = result.TenantInvoiceId is null
                ? $"رُحّل الأمر: {result.TotalCost:N2} على المالك كاملةً."
                : $"رُحّل الأمر: {result.OwnerCost:N2} على المالك، و{result.TenantCharge:N2} "
                  + "فاتورةً على المستأجر — وهي سندُ مصادرة تأمينه.";

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string KindText(MaintenanceKind kind) => kind switch
    {
        MaintenanceKind.Preventive => LocaleManager.T("دورية", "Preventive"),
        MaintenanceKind.Corrective => LocaleManager.T("إصلاحية", "Corrective"),
        // ‏(CEEF-232 · ع-٢) **والتجديدُ الرأسماليُّ يُسمّى صراحةً** — فصنفٌ يُرحَّل خلافاً
        // ‏لبقيّة الأصناف يجب أن يُرى في الجدول، **وإلّا رُسمِل ما ظُنّ أنّه صيانة**.
        MaintenanceKind.CapitalImprovement => LocaleManager.T("تجديد رأسمالي", "Capital improvement"),
        _ => LocaleManager.T("تلف مستأجر", "Tenant damage"),
    };

    private static string StatusText(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.Open => LocaleManager.T("مفتوح", "Open"),
        MaintenanceStatus.InProgress => LocaleManager.T("قيد التنفيذ", "In progress"),
        MaintenanceStatus.Completed => LocaleManager.T("منتهٍ", "Completed"),
        _ => LocaleManager.T("ملغى", "Cancelled"),
    };
}
