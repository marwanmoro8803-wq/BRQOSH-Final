using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-233 · ز) <b>الإجازاتُ والغياب — الشاشةُ التي بغيرها بقي البندُ نطاقاً بلا باب.</b>
///
/// <para><b>ما كان:</b> لا كيانَ ولا خدمةَ ولا شاشة. <b>ورصيدُ الإجازات دَينٌ نقديٌّ
/// بنصّ المادة 111</b> يُدفع عند انتهاء الخدمة — <b>ولا يظهر في الميزانيّة بريال</b>.</para>
///
/// <para><b>وعمودُ «الرصيد المتاح» هو الشاشة كلُّها</b>: يُرى قبل الاعتماد لا بعده.
/// <b>ورصيدٌ لا يُرى يُمنَح منه ما ليس فيه</b> — ثمّ يُطالَب الموظّفُ عند المغادرة بردّ
/// أيّامٍ أُذن له فيها.</para>
/// </summary>
public partial class LeaveRequestsPage : UserControl
{
    public record Row(long Id, string RequestNumber, string EmployeeName, string TypeText,
        string FromDate, string ToDate, int Days, decimal BalanceDays,
        string StatusText, LeaveStatus Status);

    public LeaveRequestsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (ApproveBtn, PermissionService.CanPost),
                (RejectBtn, PermissionService.CanPost),
                (AccrueBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var today = DateOnly.FromDateTime(DateTime.Today);

            var employees = await db.Employees
                .Where(e => e.CompanyId == AppServices.CompanyId)
                .ToDictionaryAsync(e => e.Id, e => e.NameAr);

            var balances = (await new LeaveBalanceQuery(db)
                .ForAllAsync(AppServices.CompanyId, today))
                .ToDictionary(b => b.EmployeeId);

            var requests = await db.LeaveRequests
                .Where(r => r.CompanyId == AppServices.CompanyId)
                .OrderByDescending(r => r.FromDate)
                .Take(300)
                .ToListAsync();

            var rows = requests.Select(r => new Row(
                r.Id, r.RequestNumber,
                employees.TryGetValue(r.EmployeeId, out var name) ? name : $"#{r.EmployeeId}",
                TypeText(r.Type),
                r.FromDate.ToString("yyyy-MM-dd"), r.ToDate.ToString("yyyy-MM-dd"), r.Days,
                balances.TryGetValue(r.EmployeeId, out var b) ? b.BalanceDays : 0m,
                StatusText(r.Status), r.Status)).ToList();

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            // ‏**والالتزامُ يُعرض بالريال لا بالأيّام وحدَها** — فالأيّامُ لا تُقرأ في
            // ‏ميزانيّة، **والمالكُ يسأل «كم عليّ؟»** لا «كم يوماً؟».
            var liabilityDays = balances.Values.Sum(b => Math.Max(0m, b.BalanceDays));
            var pending = requests.Count(r => r.Status == LeaveStatus.Pending);

            SummaryText.Text =
                $"طلبات: {rows.Count} · معلّقة: {pending} · "
                + $"رصيد الإجازات القائم: {liabilityDays:N2} يوماً";
            UiTestAutomation.PublishSummary(SummaryText, SummaryText.Text);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new LeaveRequestDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnApprove(object sender, RoutedEventArgs e) =>
        await DecideAsync(approve: true);

    private async void OnReject(object sender, RoutedEventArgs e) =>
        await DecideAsync(approve: false);

    /// <summary>
    /// <b>والاعتمادُ يُفحص فيه الرصيدُ والتداخل</b> — في الخدمة لا في الشاشة.
    /// </summary>
    /// <remarks>
    /// <b>وفحصٌ في الشاشة يُلتفّ عليه بأيّ مسارٍ آخر</b>، والشاشةُ تعرض ما رُفض.
    /// </remarks>
    private async Task DecideAsync(bool approve)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (row.Status != LeaveStatus.Pending)
        {
            UiFeedback.ShowInfo("اختر طلبا معلقا.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var service = facade.GetService<LeaveRequestService>();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var user = SecurityContext.ResolveUsername();

            if (approve)
                await service.ApproveAsync(AppServices.CompanyId, row.Id, today, user);
            else
                await service.RejectAsync(AppServices.CompanyId, row.Id, today, user);

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// <b>تخصيصُ الإجازات — والفرقُ وحدَه يُقيَّد.</b>
    ///
    /// <para><b>وبغيره يبقى الالتزامُ خارجَ الميزانيّة</b>: مئةُ موظّفٍ برصيدِ خمسةَ عشرَ
    /// يوماً وأجرِ عشرةِ آلافٍ ⟵ <b>خمسُمئة ألفٍ لا يراها أحد</b>.</para>
    /// </summary>
    private async void OnAccrue(object sender, RoutedEventArgs e)
    {
        if (UiDialog.Show("تخصيص الإجازات حتى اليوم؟ — ويقيد الفرق وحده.", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<LeaveAccrualService>().RunMonthlyAsync(
                AppServices.CompanyId, DateOnly.FromDateTime(DateTime.Today),
                SecurityContext.ResolveUsername());

            await ReloadAsync();

            if (result.Movement == 0m)
                UiDialog.Show("لا تغير في الالتزام — ولم يقيد شيء.", "BRQOSH",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            else
                UiDialog.Show("قيدت حركة تخصيص الإجازات.",
                    $"{result.Movement:N2} · {result.Employees:N0}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string TypeText(LeaveType type) => type switch
    {
        LeaveType.Annual => "سنوية",
        LeaveType.Sick => "مرضية",
        LeaveType.Unpaid => "بلا أجر",
        LeaveType.Maternity => "وضع",
        LeaveType.Marriage => "زواج",
        LeaveType.Bereavement => "وفاة",
        LeaveType.Hajj => "حج",
        _ => "امتحان",
    };

    private static string StatusText(LeaveStatus status) => status switch
    {
        LeaveStatus.Pending => "معلقة",
        LeaveStatus.Approved => "معتمدة",
        LeaveStatus.Rejected => "مرفوضة",
        _ => "ملغاة",
    };
}
