using System.Windows;
using Brqosh.Domain.Crm;
using Brqosh.Infrastructure.Crm;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class CrmActivityDialog : Window
{
    public CrmActivityDialog()
    {
        InitializeComponent();
        TypeBox.ItemsSource = new[]
        {
            (CrmActivityType.Call, "مكالمة"),
            (CrmActivityType.Meeting, "اجتماع"),
            (CrmActivityType.Note, "ملاحظة"),
            (CrmActivityType.Email, "بريد"),
        };
        TypeBox.DisplayMemberPath = "Item2";
        TypeBox.SelectedValuePath = "Item1";
        TypeBox.SelectedIndex = 0;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            var type = TypeBox.SelectedValue is CrmActivityType t ? t : CrmActivityType.Note;
            var activity = CrmActivity.Log(AppServices.CompanyId, type, SubjectBox.Text, NotesBox.Text);
            using var scope = AppServices.CreateInfrastructureScope();
            await scope.ServiceProvider.GetRequiredService<CrmService>().LogActivityAsync(activity);
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
