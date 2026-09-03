using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-233 · ز-٢) تقديمُ طلبِ إجازةٍ أو غياب.
///
/// <para><b>والرصيدُ يُعرض قبل الحفظ لا بعد الرفض</b>: مقدّمُ الطلب يرى ما يملكه
/// <b>فلا يطلب ما ليس له</b> — وطلبٌ يُرفض بعد تقديمه احتكاكٌ بلا فائدة.</para>
///
/// <para><b>والحفظُ ليس اعتماداً</b>: الطلبُ يُسجَّل «معلَّقاً»، <b>ولا يُخصم من رصيدٍ
/// ولا يمسّ راتباً حتى يُعتمد</b>.</para>
/// </summary>
public partial class LeaveRequestDialog : Window
{
    public record EmployeeOption(long Id, string Label);

    public LeaveRequestDialog()
    {
        InitializeComponent();
        FromBox.SelectedDate = DateTime.Today;
        ToBox.SelectedDate = DateTime.Today;
        HintText.Text = "الحفظ يسجل الطلب معلقا — ولا يخصم من رصيد ولا يمس راتبا حتى يعتمد.";

        EmployeeBox.SelectionChanged += async (_, _) => await ShowBalanceAsync();
        Loaded += async (_, _) => await LoadEmployeesAsync();
    }

    private async Task LoadEmployeesAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var employees = await db.Employees.AsNoTracking()
                .Where(e => e.CompanyId == AppServices.CompanyId && e.IsActive)
                .OrderBy(e => e.NameAr)
                .Select(e => new { e.Id, e.Code, e.NameAr })
                .ToListAsync();

            EmployeeBox.ItemsSource = employees
                .Select(e => new EmployeeOption(e.Id, $"{e.Code} — {e.NameAr}"))
                .ToList();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary><b>ورصيدُ المختار يُعرض فوراً</b> — فالطلبُ يُبنى على رقمٍ مرئيّ.</summary>
    private async Task ShowBalanceAsync()
    {
        if (EmployeeBox.SelectedItem is not EmployeeOption employee) return;

        try
        {
            await using var db = AppServices.CreateDb();
            var balance = await new LeaveBalanceQuery(db).ForEmployeeAsync(
                AppServices.CompanyId, employee.Id, DateOnly.FromDateTime(DateTime.Today));

            HintText.Text =
                $"الرصيد المتاح: {balance.BalanceDays:N2} يوما "
                + $"— المستحق {balance.AccruedDays:N2} والمستهلك {balance.TakenDays:N2}.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (EmployeeBox.SelectedItem is not EmployeeOption employee)
                throw new AccountingException("اختر الموظّف.");
            if (FromBox.SelectedDate is null || ToBox.SelectedDate is null)
                throw new AccountingException("حدّد تاريخي بداية الإجازة ونهايتها.");

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<LeaveRequestService>().SubmitAsync(
                AppServices.CompanyId, employee.Id,
                (LeaveType)Math.Max(0, TypeBox.SelectedIndex),
                DateOnly.FromDateTime(FromBox.SelectedDate.Value),
                DateOnly.FromDateTime(ToBox.SelectedDate.Value),
                ReasonBox.Text);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
