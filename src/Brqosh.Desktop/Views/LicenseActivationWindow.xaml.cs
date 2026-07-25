using System.Windows;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Desktop.Views;

public partial class LicenseActivationWindow : Window
{
    public bool LicenseActivated { get; private set; }

    public LicenseActivationWindow(string companyName)
    {
        InitializeComponent();
        MachineIdBox.Text = LicenseService.MachineId();
        CompanyBox.Text = companyName;
        KeyBox.Text = LicenseStore.Load()?.SignedLicenseJson ?? "";
    }

    private void OnActivate(object sender, RoutedEventArgs e)
    {
        var company = CompanyBox.Text.Trim();
        var machineId = MachineIdBox.Text.Trim();
        var signedJson = KeyBox.Text.Trim();

        if (!RsaLicenseValidator.TryValidate(company, machineId, signedJson, out _))
        {
            ErrorText.Text = "ترخيص غير صالح لهذا الجهاز أو اسم المنشأة.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        LicenseStore.SaveSignedLicense(company, signedJson);
        LicenseActivated = true;
        DialogResult = true;
        Close();
    }
}
