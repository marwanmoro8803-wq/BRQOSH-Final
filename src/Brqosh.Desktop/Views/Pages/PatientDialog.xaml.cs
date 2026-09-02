using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Sales;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) إنشاءُ مريض — <b>ومعه حسابُه في دفتر العملاء</b>.
///
/// <para><b>والاثنان في حفظةٍ واحدة</b>: المريضُ لا يوجد بلا ذمّة، <b>وذمّةٌ تُنشأ ثمّ
/// يفشل حفظُ المريض تترك عميلاً يتيماً</b> في دفتر العملاء لا يعرف أحدٌ ما هو.</para>
/// </summary>
public partial class PatientDialog : Window
{
    public PatientDialog()
    {
        InitializeComponent();
        BirthBox.SelectedDate = DateTime.Today.AddYears(-30);
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (BirthBox.SelectedDate is null)
                throw new AccountingException("تاريخ الميلاد مطلوب.");

            await using var db = AppServices.CreateDb();
            await using var transaction = await db.Database.BeginTransactionAsync();

            var customer = Customer.Create(AppServices.CompanyId, (NameBox.Text ?? "").Trim(),
                phone: (PhoneBox.Text ?? "").Trim());
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            var patient = Patient.Create(AppServices.CompanyId, MrnBox.Text, NameBox.Text,
                NationalIdBox.Text, DateOnly.FromDateTime(BirthBox.SelectedDate.Value),
                GenderBox.SelectedIndex == 0 ? PatientGender.Male : PatientGender.Female,
                customer.Id, PhoneBox.Text);

            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
