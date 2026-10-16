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
/// (CEEF-230 · ط3-٢ب) <b>دورةُ المطالبات — والذمّةُ الوهمية تُرى قبل أن تُصلَح.</b>
///
/// <para><b>ما كان:</b> فاتورةُ الشركة تبقى بكامل قيمتها إلى الأبد. عيادةٌ تُطالب بمئة
/// ألفٍ شهرياً تُعتمد لها اثنان وثمانون، <b>وثمانيةَ عشرَ تُرفض وتبقى في الذمم</b> —
/// فبعد سنةٍ <b>مئتان وستّةَ عشرَ ألفاً وهميّة</b> تُقرأ سيولةً وليست شيئاً.</para>
///
/// <para><b>والعمودان «المرفوض» و«بلا قرار» هما الشاشة كلُّها</b>: الأوّلُ ما قالت الشركةُ
/// إنّها لا تدفعه، <b>والثاني ما لم يُقرَّر بعدُ أيُحمَّل على المريض أم يُشطب</b> — وهو
/// ما يمنع الإقفال. <b>ورقمٌ لا يُرى لا يُعالَج.</b></para>
/// </summary>
public partial class InsuranceClaimsPage : UserControl
{
    public record Row(long Id, string ClaimNumber, string PayerName, string Period,
        string StatusText, int LineCount, decimal Claimed, decimal Approved, decimal Rejected,
        decimal Undecided, ClaimStatus Status);

    public record LineRow(string VisitNumber, decimal Claimed, decimal Approved, decimal Rejected,
        string Reason, string DispositionText);

    public InsuranceClaimsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (BuildBtn, PermissionService.CanPost),
                (SubmitBtn, PermissionService.CanPost),
                (AdjudicateBtn, PermissionService.CanPost),
                (SettleBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var claims = await db.InsuranceClaims
                .Where(c => c.CompanyId == AppServices.CompanyId)
                .OrderByDescending(c => c.PeriodStart)
                .ToListAsync();

            var payers = await db.InsurancePayers.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId)
                .ToDictionaryAsync(p => p.Id, p => p.NameAr);

            var rows = claims.Select(c => new Row(
                c.Id, c.ClaimNumber,
                payers.TryGetValue(c.PayerId, out var name) ? name : $"#{c.PayerId}",
                $"{c.PeriodStart:yyyy-MM-dd} — {c.PeriodEnd:yyyy-MM-dd}",
                StatusText(c.Status), c.Lines.Count,
                c.ClaimedTotal, c.ApprovedTotal, c.RejectedTotal, c.UndecidedRejected,
                c.Status)).ToList();

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            var undecided = rows.Sum(r => r.Undecided);
            var open = rows.Where(r => r.Status != ClaimStatus.Closed).Sum(r => r.Claimed - r.Approved);

            SummaryText.Text = rows.Count == 0
                ? "لا مطالباتٍ بعد — والدفعةُ تُبنى ممّا فُوتر على الشركات في مدةٍ محدّدة."
                : $"دفعات: {rows.Count:N0} · المطالَب: {rows.Sum(r => r.Claimed):N2} · "
                  + $"المعتمَد: {rows.Sum(r => r.Approved):N2} · المرفوض: {rows.Sum(r => r.Rejected):N2}"
                  + (undecided > 0
                      ? $" · ⚠️ مرفوضٌ بلا قرار: {undecided:N2} — وهو باقٍ في ذمّة الشركة وقد رفضته."
                      : "")
                  + (open > 0 && undecided == 0
                      ? $" · معلّقٌ بلا فصل: {open:N2}"
                      : "");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnClaimSelected(object sender, SelectionChangedEventArgs e)
    {
        // ‏**والحدثُ يقع أثناء تحليل XAML قبل `Loaded`** — ولا قاعدةَ حينها. وهو ما أسقط
        // ‏حزمةَ اختباراتٍ كاملةً من قبل: `async void` يرمي فيُجهض المُشغّل.
        if (!IsLoaded) return;

        try
        {
            if (Grid.SelectedItem is not Row row)
            {
                LinesGrid.ItemsSource = null;
                return;
            }

            await using var db = AppServices.CreateDb();

            var claim = await db.InsuranceClaims.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == row.Id);
            if (claim is null) return;

            var visitIds = claim.Lines.Select(l => l.VisitId).ToList();
            var visits = await db.Visits.AsNoTracking()
                .Where(v => visitIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v.VisitNumber);

            LinesGrid.ItemsSource = claim.Lines.Select(l => new LineRow(
                visits.TryGetValue(l.VisitId, out var number) ? number : $"#{l.VisitId}",
                l.ClaimedAmount, l.ApprovedAmount, l.RejectedAmount,
                l.RejectionReason ?? "",
                DispositionText(l))).ToList();

            LinesCaption.Text = $"سطور الدفعة {row.ClaimNumber}";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnBuild(object sender, RoutedEventArgs e)
    {
        var dialog = new ClaimBuildDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) await ReloadAsync();
    }

    private async void OnSubmit(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر دفعةً أولاً.");
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var claim = await db.InsuranceClaims.SingleAsync(c => c.Id == row.Id);
            claim.Submit(DateOnly.FromDateTime(DateTime.Today));
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAdjudicate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر دفعةً أولاً.");
            return;
        }

        var dialog = new ClaimAdjudicationDialog(row.Id) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) await ReloadAsync();
    }

    private async void OnSettle(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowWarning("اختر دفعةً أولاً.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<ClaimSettlementService>().SettleAsync(
                AppServices.CompanyId, row.Id, DateOnly.FromDateTime(DateTime.Today));

            // ‏**والمصيران يُعرضان منفصلين**: المُعاد تحميلُه دَينٌ انتقل، والمشطوبُ إيرادٌ
            // ‏ضاع. **وجمعُهما في رقمٍ واحدٍ يُخفي أيَّهما وقع**.
            SummaryText.Text = $"عُولجت {result.CreditNoteIds.Count:N0} حالةَ رفض · "
                + $"أُعيد تحميلُ {result.RebilledToPatients:N2} على المرضى · "
                + $"وشُطب {result.WrittenOff:N2}.";

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string StatusText(ClaimStatus status) => status switch
    {
        ClaimStatus.Draft => LocaleManager.T("مسودة", "Draft"),
        ClaimStatus.Submitted => LocaleManager.T("مُقدَّمة", "Submitted"),
        ClaimStatus.Adjudicated => LocaleManager.T("مفصولٌ فيها", "Adjudicated"),
        _ => LocaleManager.T("مُقفلة", "Closed"),
    };

    private static string DispositionText(InsuranceClaimLine line)
    {
        if (line.RejectedAmount <= 0m) return line.IsAdjudicated ? "معتمَدٌ كاملاً" : "بانتظار القرار";

        return line.Disposition switch
        {
            RejectionDisposition.RebillPatient => LocaleManager.T("على المريض", "Rebilled to patient"),
            RejectionDisposition.WriteOff => LocaleManager.T("شطب", "Written off"),
            _ => LocaleManager.T("⚠️ بلا قرار", "⚠️ No decision"),
        };
    }
}
