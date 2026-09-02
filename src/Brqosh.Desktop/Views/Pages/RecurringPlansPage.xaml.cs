using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٣) <b>الفوترةُ الدورية — الشاشةُ التي بلا وجودها بقي `ط1-٥` وعداً.</b>
///
/// <para><b>ما كان:</b> الخطةُ الدورية و<c>GenerateDueAsync</c> مبنيّتان ومقيستان في
/// <c>ط1-٥</c>، <b>ولا شاشةَ تُنشئ خطةً ولا تُطلق التوليد</b>. فعقودُ الإيجار في
/// <c>ط3-١</c> تُنشئ خططاً <b>لا يستطيع المالكُ رؤيتَها ولا تشغيلَها</b> — والإيجارُ
/// يبقى بلا فاتورةٍ شهراً بعد شهر.</para>
///
/// <para><b>ولا مُجدوِلَ عملٍ في هذا التطبيق</b> — والتوليدُ دفعةٌ يُطلقها المستخدم،
/// وهو أصدقُ لتطبيقٍ مكتبيّ: <b>خدمةٌ خلفيةٌ في تطبيقٍ مغلقٍ لا تعمل</b>. ولذلك يُعرض
/// «الاستحقاق القادم» في الجدول: <b>فالمالكُ يرى ما لم يُفوتَر قبل أن يُشغّل</b>.</para>
/// </summary>
public partial class RecurringPlansPage : UserControl
{
    public record Row(long Id, string Code, string Description, string CustomerName,
        string Frequency, string StartDate, string EndDate, string NextDue, string Status);

    public RecurringPlansPage()
    {
        InitializeComponent();
        AsOfBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (GenerateBtn, PermissionService.CanPost),
                (PauseBtn, PermissionService.CanPost));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();

        var plans = await db.RecurringInvoicePlans
            .Where(p => p.CompanyId == AppServices.CompanyId)
            .OrderBy(p => p.Code)
            .ToListAsync();

        var customerNames = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.CompanyId == AppServices.CompanyId)
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var today = DateOnly.FromDateTime(DateTime.Today);

        var rows = plans.Select(p => new Row(
            p.Id, p.Code, p.Description,
            customerNames.TryGetValue(p.CustomerId, out var name) ? name : $"#{p.CustomerId}",
            FrequencyText(p.Frequency),
            p.StartDate.ToString("yyyy-MM-dd"),
            p.EndDate is DateOnly end ? end.ToString("yyyy-MM-dd") : "—",
            p.NextOccurrence() is DateOnly due ? due.ToString("yyyy-MM-dd") : "—",
            StatusText(p.Status))).ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var due = plans.Count(p => p.Status == RecurringPlanStatus.Active
                                   && p.NextOccurrence() is DateOnly d && d <= today);

        SummaryText.Text = due == 0
            ? $"خطط: {plans.Count:N0} · لا مستحقّاتٍ حتى اليوم."
            : $"خطط: {plans.Count:N0} · ⚠️ {due:N0} خطةً استحقّت ولم تُفوتَر — والتوليد لا يجري إلا بطلبك.";
    }

    private async void OnGenerate(object sender, RoutedEventArgs e)
    {
        if (AsOfBox.SelectedDate is null)
        {
            UiFeedback.ShowWarning("اختر التاريخ الذي يُولَّد المستحقُّ حتّاه.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<RecurringInvoiceService>()
                .GenerateDueAsync(AppServices.CompanyId, DateOnly.FromDateTime(AsOfBox.SelectedDate.Value));

            await ReloadAsync();

            // ‏**وما تعذّر يُسمّى ولا يُبتلع**: خطةٌ تعثر لا تُسقط غيرَها، **وصمتٌ عمّا سقط
            // ‏يجعل المالكَ يظنّ أنّ كلَّ عقوده فُوتِرت**.
            SummaryText.Text = result.Refusals.Count == 0
                ? $"وُلِّدت {result.InvoicesCreated:N0} فاتورة من {result.PlansAdvanced:N0} خطة."
                : $"وُلِّدت {result.InvoicesCreated:N0} فاتورة · وتعذّر: "
                  + string.Join(" · ", result.Refusals);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnPause(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;

        try
        {
            await using var db = AppServices.CreateDb();
            var plan = await db.RecurringInvoicePlans.SingleAsync(p => p.Id == row.Id);

            if (plan.Status == RecurringPlanStatus.Paused) plan.Resume();
            else plan.Pause();

            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string FrequencyText(RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Weekly => "أسبوعية",
        RecurrenceFrequency.Monthly => "شهرية",
        RecurrenceFrequency.Quarterly => "ربع سنوية",
        RecurrenceFrequency.SemiAnnual => "نصف سنوية",
        RecurrenceFrequency.Annual => "سنوية",
        _ => frequency.ToString(),
    };

    private static string StatusText(RecurringPlanStatus status) => status switch
    {
        RecurringPlanStatus.Active => "نشطة",
        RecurringPlanStatus.Paused => "موقوفة",
        RecurringPlanStatus.Ended => "منتهية",
        _ => status.ToString(),
    };
}
