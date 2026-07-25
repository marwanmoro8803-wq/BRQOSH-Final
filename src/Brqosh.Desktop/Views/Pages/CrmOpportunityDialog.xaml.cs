using System.Globalization;
using System.Windows;
using Brqosh.Domain.Crm;
using Brqosh.Infrastructure.Crm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class CrmOpportunityDialog : Window
{
    public CrmOpportunityDialog()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await using var db = AppServices.CreateDb();
            CustomerBox.ItemsSource = await db.Customers
                .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .ToListAsync();
        };
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount))
                amount = 0m;
            long? customerId = CustomerBox.SelectedValue is long id ? id : null;
            var opp = CrmOpportunity.Create(AppServices.CompanyId, TitleBox.Text, amount, customerId);
            using var scope = AppServices.CreateInfrastructureScope();
            await scope.ServiceProvider.GetRequiredService<CrmService>().CreateOpportunityAsync(opp);
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
