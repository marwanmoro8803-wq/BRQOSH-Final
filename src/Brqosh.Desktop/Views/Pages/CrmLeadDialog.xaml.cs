using System.Windows;
using Brqosh.Domain.Crm;
using Brqosh.Infrastructure.Crm;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class CrmLeadDialog : Window
{
    public CrmLeadDialog()
    {
        InitializeComponent();
        SourceBox.ItemsSource = new[] { "مباشر", "موقع", "إحالة", "معرض", "هاتف" };
        SourceBox.SelectedIndex = 0;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            var lead = CrmLead.Create(
                AppServices.CompanyId,
                NameBox.Text,
                PhoneBox.Text,
                EmailBox.Text,
                SourceBox.SelectedItem?.ToString() ?? "مباشر");
            using var scope = AppServices.CreateInfrastructureScope();
            await scope.ServiceProvider.GetRequiredService<CrmService>().CreateLeadAsync(lead);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
