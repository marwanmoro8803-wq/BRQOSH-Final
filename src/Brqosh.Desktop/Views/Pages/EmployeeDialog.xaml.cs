using System.Globalization;
using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٢) إنشاءُ موظفٍ وتعديلُه.
///
/// <para><b>ولا يُتحقّق من شيءٍ هنا</b>: الهويّةُ عشرةُ أرقام، والآيبانُ سعوديٌّ صحيح،
/// والأجرُ غيرُ سالب — <b>كلُّها في النطاق</b>. والشاشةُ تُمرّر وتعرض ما رُفض. <b>وتكرارُ
/// الفحص هنا يُنشئ حقيقتين تختلفان يوماً</b>، ويُغري بإضعاف إحداهما «لأنّ الأخرى تحرس».</para>
/// </summary>
public partial class EmployeeDialog : Window
{
    private readonly long? _employeeId;

    public EmployeeDialog()
    {
        InitializeComponent();
        HireDateBox.SelectedDate = DateTime.Today;
    }

    public EmployeeDialog(Employee employee) : this()
    {
        ArgumentNullException.ThrowIfNull(employee);

        _employeeId = employee.Id;
        HeaderText.Text = $"تعديل الموظف {employee.Code}";

        CodeBox.Text = employee.Code;
        CodeBox.IsEnabled = false;
        NameBox.Text = employee.NameAr;
        NationalIdBox.Text = employee.NationalId;
        NationalIdBox.IsEnabled = false;
        NationalityBox.SelectedIndex = employee.Nationality == EmployeeNationality.Saudi ? 0 : 1;
        JobTitleBox.Text = employee.JobTitle;
        HireDateBox.SelectedDate = employee.HireDate.ToDateTime(TimeOnly.MinValue);
        HireDateBox.IsEnabled = false;
        BasicBox.Text = employee.BasicSalary.ToString(CultureInfo.InvariantCulture);
        HousingBox.Text = employee.HousingAllowance.ToString(CultureInfo.InvariantCulture);
        TransportBox.Text = employee.TransportAllowance.ToString(CultureInfo.InvariantCulture);
        OtherBox.Text = employee.OtherAllowance.ToString(CultureInfo.InvariantCulture);
        IbanBox.Text = employee.Iban ?? string.Empty;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var basic = Money(BasicBox.Text, "الأساسي");
            var housing = Money(HousingBox.Text, "بدل السكن");
            var transport = Money(TransportBox.Text, "بدل النقل");
            var other = Money(OtherBox.Text, "البدلات الأخرى");
            var iban = string.IsNullOrWhiteSpace(IbanBox.Text) ? null : IbanBox.Text.Trim();

            if (_employeeId is long id)
            {
                var existing = await db.Employees.SingleAsync(x => x.Id == id);
                existing.Rename(NameBox.Text, JobTitleBox.Text);
                existing.SetPay(basic, housing, transport, other);
                existing.SetIban(iban);
            }
            else
            {
                if (HireDateBox.SelectedDate is null)
                    throw new AccountingException("تاريخ التعيين مطلوب.");

                var employee = Employee.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                    NationalIdBox.Text,
                    NationalityBox.SelectedIndex == 0 ? EmployeeNationality.Saudi : EmployeeNationality.NonSaudi,
                    JobTitleBox.Text, DateOnly.FromDateTime(HireDateBox.SelectedDate.Value));

                employee.SetPay(basic, housing, transport, other);
                employee.SetIban(iban);
                db.Employees.Add(employee);
            }

            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary><b>ورقمٌ لا يُقرأ يُسمّى حقلَه</b> — فلا يبحث المستخدم عن الخطأ في أربعة حقول.</summary>
    private static decimal Money(string text, string field) =>
        decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new AccountingException($"قيمة «{field}» غير صالحة — ووردت «{text}».");

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
