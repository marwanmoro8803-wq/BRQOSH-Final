using System.Windows;
using Brqosh.Application.Setup;
using Brqosh.Domain.Modules;

namespace Brqosh.Desktop.Views;

public partial class SetupWizardWindow : Window
{
    private readonly ISetupStateStore _store;

    public SetupWizardWindow(ISetupStateStore store)
    {
        _store = store;
        InitializeComponent();
    }

    private void OnStartClicked(object sender, RoutedEventArgs e)
    {
        BusinessProfile? profile =
            ConstructionCard.IsChecked == true ? BusinessProfile.Construction :
            ManufacturingCard.IsChecked == true ? BusinessProfile.Manufacturing :
            RetailCard.IsChecked == true ? BusinessProfile.Retail :
            GeneralCard.IsChecked == true ? BusinessProfile.General :
            null;

        if (profile is null)
        {
            ShowError("اختر نظام العمل أولاً.");
            return;
        }

        var companyName = CompanyNameBox.Text.Trim();
        if (companyName.Length < 2)
        {
            ShowError("اسم المنشأة مطلوب.");
            return;
        }

        var vatNumber = VatNumberBox.Text.Trim();
        if (vatNumber.Length > 0 && (vatNumber.Length != 15 || !vatNumber.All(char.IsDigit)))
        {
            ShowError("الرقم الضريبي يجب أن يتكون من 15 رقماً (أو اتركه فارغاً الآن).");
            return;
        }

        _store.Save(new SetupState(
            IsCompleted: true,
            Profile: profile.Value,
            CompanyName: companyName,
            VatRegistrationNumber: vatNumber,
            CompletedAtUtc: DateTime.UtcNow));

        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
