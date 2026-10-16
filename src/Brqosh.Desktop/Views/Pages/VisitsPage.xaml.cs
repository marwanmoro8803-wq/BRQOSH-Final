using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Healthcare;
using Brqosh.Infrastructure.Healthcare;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) <b>الزياراتُ وفوترتُها — والقسمةُ تُعرض بعد وقوعها لا قبلها.</b>
///
/// <para><b>ما كان:</b> <c>VisitBillingService</c> مبنيٌّ ومقيسٌ في <c>ط3-٢</c> — يقسم
/// الزيارةَ بين المريض وشركة التأمين ويُصدر فاتورتين — <b>ولا زرَّ يستدعيه</b>.</para>
///
/// <para><b>وتُعرض القسمةُ بأرقامها بعد الفوترة</b>: كم على المريض، وكم يُطالَب به التأمين،
/// <b>وما الذي حدّ حصّتَه</b> — نسبةٌ أم سقفٌ أم لا وثيقة. فالسببُ يُقرأ يومَ يقع، <b>لا
/// بعد أشهرٍ حين تُرفض المطالبة</b>.</para>
/// </summary>
public partial class VisitsPage : UserControl
{
    public record Row(long Id, string VisitNumber, string VisitDate, string PatientName,
        string DoctorName, decimal Total, string Covered, string Status, bool IsOpen);

    public VisitsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (BillBtn, PermissionService.CanPost));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();

        var visits = await db.Visits
            .Where(v => v.CompanyId == AppServices.CompanyId)
            .OrderByDescending(v => v.VisitDate)
            .ToListAsync();

        var patients = await db.Patients.AsNoTracking()
            .Where(p => p.CompanyId == AppServices.CompanyId)
            .ToDictionaryAsync(p => p.Id, p => p.NameAr);

        var rows = visits.Select(v => new Row(
            v.Id, v.VisitNumber, v.VisitDate.ToString("yyyy-MM-dd"),
            patients.TryGetValue(v.PatientId, out var name) ? name : $"#{v.PatientId}",
            v.DoctorName, v.Total,
            v.PolicyId is null ? "حساب خاص" : "بوثيقة",
            StatusText(v.Status), v.Status == VisitStatus.Open)).ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var open = visits.Count(v => v.Status == VisitStatus.Open);

        // ‏**وزيارةٌ مفتوحةٌ خدمةٌ قُدِّمت بلا فاتورة** — إيرادٌ مؤدًّى لم يُطالَب به،
        // ‏ويسقط من الدفاتر إن نُسي.
        SummaryText.Text = open == 0
            ? $"زيارات: {rows.Count:N0} · كلُّها مفوترة."
            : $"زيارات: {rows.Count:N0} · ⚠️ مفتوحة لم تُفوتَر: {open:N0} — خدمةٌ قُدِّمت ولم يُطالَب بها.";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new VisitDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnBill(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (!row.IsOpen)
        {
            UiFeedback.ShowWarning("لا تُفوتَر إلا زيارةٌ مفتوحة — ولا تُفوتَر مرّتين.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<VisitBillingService>()
                .BillAsync(AppServices.CompanyId, row.Id);
            await ReloadAsync();

            SummaryText.Text = $"فُوتِرت {row.VisitNumber}: إجمالي {result.Total:N2} · "
                + $"على المريض {result.PatientShare:N2} · مطالبة التأمين {result.PayerShare:N2} · "
                + $"والحدّ: {BasisText(result.Basis)}";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string StatusText(VisitStatus status) => status switch
    {
        VisitStatus.Open => LocaleManager.T("مفتوحة", "Open"),
        VisitStatus.Billed => LocaleManager.T("مفوترة", "Billed"),
        VisitStatus.Cancelled => LocaleManager.T("ملغاة", "Cancelled"),
        _ => status.ToString(),
    };

    private static string BasisText(CoverageBasis basis) => basis switch
    {
        CoverageBasis.SelfPay => LocaleManager.T("لا وثيقة سارية — على الحساب الخاص", "No active policy — self-pay"),
        CoverageBasis.CoveragePercent => LocaleManager.T("نسبة التغطية", "Coverage percentage"),
        CoverageBasis.AnnualLimit => LocaleManager.T("سقف سنة الوثيقة — وما جاوزه على المريض", "Policy-year limit — excess billed to patient"),
        _ => basis.ToString(),
    };
}
