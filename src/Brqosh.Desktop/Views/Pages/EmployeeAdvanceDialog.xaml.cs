using System.Globalization;
using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط2-٥) تسجيلُ سُلفةِ موظّف.
///
/// <para><b>والحفظُ ليس صرفاً</b>: السُّلفةُ تُسجَّل «بانتظار الصرف»، <b>ولا تصير ذمّةً
/// على الموظّف حتى يخرج المال</b>. واستقطاعُ ما لم يُصرف يأخذ منه ما لم يأخذه.</para>
/// </summary>
public partial class EmployeeAdvanceDialog : Window
{
    public record EmployeeOption(long Id, string Label);

    public EmployeeAdvanceDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
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

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (EmployeeBox.SelectedItem is not EmployeeOption employee)
                throw new AccountingException("اختر الموظّف.");
            if (DateBox.SelectedDate is null)
                throw new AccountingException("حدّد تاريخ الطلب.");

            var advance = EmployeeAdvance.Create(AppServices.CompanyId, employee.Id,
                NumberBox.Text ?? "", DateOnly.FromDateTime(DateBox.SelectedDate.Value),
                Parse(AmountBox.Text, "المبلغ"), Parse(InstallmentBox.Text, "القسط الشهري"),
                ReasonBox.Text);

            await using var db = AppServices.CreateDb();
            db.EmployeeAdvances.Add(advance);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal Parse(string? text, string field)
    {
        if (decimal.TryParse((text ?? "").Trim(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var value))
            return value;

        throw new AccountingException($"قيمة «{field}» غير صالحة.");
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
