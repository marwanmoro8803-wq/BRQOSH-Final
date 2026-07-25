using System.Globalization;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class WorkOrderDialog : Window
{
    public record BomOption(long Id, string Display);

    public WorkOrderDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadBomsAsync();
    }

    private async Task LoadBomsAsync()
    {
        await using var db = AppServices.CreateDb();
        BomCombo.ItemsSource = await db.BillsOfMaterials
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderBy(b => b.Code)
            .Select(b => new BomOption(b.Id, $"{b.Code} — {b.NameAr}"))
            .ToListAsync();
        if (BomCombo.Items.Count > 0)
            BomCombo.SelectedIndex = 0;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (BomCombo.SelectedValue is not long bomId)
                throw new AccountingException("اختر قائمة مواد.");
            if (DateBox.SelectedDate is null)
                throw new AccountingException("التاريخ مطلوب.");
            if (!decimal.TryParse(QtyBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                throw new AccountingException("الكمية المخططة غير صالحة.");

            var order = WorkOrder.CreateDraft(AppServices.CompanyId, bomId,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value), qty, NotesBox.Text);

            await using var db = AppServices.CreateDb();
            db.WorkOrders.Add(order);
            await db.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex is AccountingException ae ? ae.Message : ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
