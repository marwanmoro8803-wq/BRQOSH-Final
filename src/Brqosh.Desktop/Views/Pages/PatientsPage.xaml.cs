using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) <b>المرضى ووثائقُهم — الشاشةُ التي بلا وجودها بقي `ط3-٢` نطاقاً بلا باب.</b>
///
/// <para><b>وتُعرض الوثيقةُ إلى جانب المريض</b> لأنّ الوثيقةَ هي ما يقسم الفاتورة:
/// نسبةُ التغطية والتحمّلُ والسقف. <b>ومريضٌ بلا وثيقةٍ سارية يُفوتَر على حسابه الخاصّ</b>
/// — وذلك قرارٌ يُرى قبل الزيارة لا بعدها.</para>
/// </summary>
public partial class PatientsPage : UserControl
{
    public record Row(long Id, string Mrn, string NameAr, string NationalId, int Age, string Phone);

    public record PolicyRow(string PayerName, string PolicyNumber, string Coverage,
        decimal Copay, string Validity);

    public PatientsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (AddPolicyBtn, PermissionService.CanPost));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var patients = await db.Patients.AsNoTracking()
            .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.MedicalRecordNumber)
            .ToListAsync();

        var rows = patients
            .Select(p => new Row(p.Id, p.MedicalRecordNumber, p.NameAr, p.NationalId,
                p.AgeOn(today), p.Phone))
            .ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var insured = await db.InsurancePolicies.AsNoTracking()
            .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive
                        && p.StartDate <= today && today <= p.EndDate)
            .Select(p => p.PatientId)
            .Distinct()
            .CountAsync();

        SummaryText.Text = rows.Count == 0
            ? "لا مرضى بعد."
            : $"مرضى: {rows.Count:N0} · لهم وثائقُ تأمينٍ سارية: {insured:N0} · "
              + $"على الحساب الخاصّ: {rows.Count - insured:N0}";

        if (rows.Count > 0 && Grid.SelectedItem is null) Grid.SelectedIndex = 0;
        else await ReloadPoliciesAsync();
    }

    private async void OnPatientSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await ReloadPoliciesAsync();
    }

    private async Task ReloadPoliciesAsync()
    {
        if (Grid.SelectedItem is not Row patient)
        {
            PoliciesGrid.ItemsSource = null;
            UiTestAutomation.PublishGridCount(PoliciesGrid, 0);
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();

            var payers = await db.InsurancePayers.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId)
                .ToDictionaryAsync(p => p.Id, p => p.NameAr);

            var rows = await db.InsurancePolicies.AsNoTracking()
                .Where(p => p.PatientId == patient.Id)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            PoliciesGrid.ItemsSource = rows.Select(p => new PolicyRow(
                payers.TryGetValue(p.PayerId, out var payer) ? payer : $"#{p.PayerId}",
                p.PolicyNumber,
                $"{p.CoveragePercent:N0}٪",
                p.CopayFixed,
                $"{p.StartDate:yyyy-MM-dd} — {p.EndDate:yyyy-MM-dd}")).ToList();

            UiTestAutomation.PublishGridCount(PoliciesGrid, rows.Count);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new PatientDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnAddPolicy(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row patient)
        {
            UiFeedback.ShowWarning("اختر المريض الذي تُضاف له الوثيقة.");
            return;
        }

        if (new InsurancePolicyDialog(patient.Id) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }
}
