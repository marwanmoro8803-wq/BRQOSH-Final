using System.Windows;
using Brqosh.Domain.Construction;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class SubcontractDialog : Window
{
    private record ProjectOption(long Id, string Name);
    private record SupplierOption(long Id, string Name);

    public SubcontractDialog()
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

        SupplierBox.ItemsSource = await db.Suppliers
            .Where(s => s.CompanyId == AppServices.CompanyId && s.IsActive)
            .OrderBy(s => s.NameAr)
            .Select(s => new SupplierOption(s.Id, s.NameAr))
            .ToListAsync();
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ProjectBox.SelectedItem is not ProjectOption project)
                throw new InvalidOperationException("اختر المشروع.");
            if (SupplierBox.SelectedItem is not SupplierOption supplier)
                throw new InvalidOperationException("اختر مقاول الباطن.");
            if (!NumericInput.TryParse(ValueBox.Text, out var value) || value <= 0)
                throw new InvalidOperationException("قيمة العقد غير صالحة.");
            if (!NumericInput.TryParse(RetentionBox.Text, out var retention))
                throw new InvalidOperationException("نسبة المحتجز غير صالحة.");

            var date = DatePicker.SelectedDate is DateTime dt
                ? DateOnly.FromDateTime(dt)
                : DateOnly.FromDateTime(DateTime.Today);

            var agreement = SubcontractAgreement.Create(
                AppServices.CompanyId, project.Id, supplier.Id,
                NumberBox.Text, date, value, retention, ScopeBox.Text);
            agreement.Activate();

            await using var db = AppServices.CreateDb();
            db.SubcontractAgreements.Add(agreement);
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
