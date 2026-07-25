using System.Windows;
using Brqosh.Domain.Construction;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class ContractDialog : Window
{
    private record ProjectOption(long Id, string Name);

    public ContractDialog()
    {
        InitializeComponent();
        DatePicker.SelectedDate = DateTime.Today;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await using var db = AppServices.CreateDb();
        ProjectBox.ItemsSource = await db.ConstructionProjects
            .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new ProjectOption(p.Id, $"{p.Code} — {p.NameAr}"))
            .ToListAsync();
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ProjectBox.SelectedItem is not ProjectOption project)
                throw new InvalidOperationException("اختر المشروع.");
            if (!decimal.TryParse(ValueBox.Text, out var value) || value <= 0)
                throw new InvalidOperationException("قيمة العقد غير صالحة.");

            var date = DatePicker.SelectedDate is DateTime dt
                ? DateOnly.FromDateTime(dt)
                : DateOnly.FromDateTime(DateTime.Today);

            var contract = ConstructionContract.Create(
                AppServices.CompanyId, project.Id, NumberBox.Text, date, value, DescBox.Text);
            contract.Activate();

            await using var db = AppServices.CreateDb();
            db.ConstructionContracts.Add(contract);
            await db.SaveChangesAsync();

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
