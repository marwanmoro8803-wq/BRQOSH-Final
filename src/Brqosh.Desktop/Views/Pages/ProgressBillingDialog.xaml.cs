using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Construction;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProgressBillingDialog : Window
{
    private record ProjectOption(long Id, string Name, long? CustomerId, decimal RetentionPercent);
    private record CustomerOption(long Id, string Name);

    public ProgressBillingDialog()
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
            .Select(p => new ProjectOption(p.Id, $"{p.Code} — {p.NameAr}", p.CustomerId, p.RetentionPercent))
            .ToListAsync();

        CustomerBox.ItemsSource = await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.NameAr)
            .Select(c => new CustomerOption(c.Id, c.NameAr))
            .ToListAsync();
    }

    private void OnProjectChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectBox.SelectedItem is ProjectOption project)
        {
            RetentionBox.Text = project.RetentionPercent.ToString("0.##");
            if (project.CustomerId is long cid)
            {
                foreach (CustomerOption opt in CustomerBox.Items)
                {
                    if (opt.Id == cid)
                    {
                        CustomerBox.SelectedItem = opt;
                        break;
                    }
                }
            }
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ProjectBox.SelectedItem is not ProjectOption project)
                throw new InvalidOperationException("اختر المشروع.");
            if (CustomerBox.SelectedItem is not CustomerOption customer)
                throw new InvalidOperationException("اختر العميل.");
            if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
                throw new InvalidOperationException("قيمة الأعمال غير صالحة.");
            if (!decimal.TryParse(RetentionBox.Text, out var retention))
                throw new InvalidOperationException("نسبة المحتجز غير صالحة.");

            var date = DatePicker.SelectedDate is DateTime dt
                ? DateOnly.FromDateTime(dt)
                : DateOnly.FromDateTime(DateTime.Today);

            var billing = ProgressBilling.CreateDraft(
                AppServices.CompanyId, project.Id, customer.Id, date,
                PeriodBox.Text, amount, retention);

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<ProgressBillingPostingService>().PostAsync(billing);

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
