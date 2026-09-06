using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>(CEEF-230 · ط6-٤) إنشاءُ بوليصة — والتحصيلُ يُثبت بعد التسليم لا هنا.</summary>
public partial class ShipmentDialog : Window
{
    private sealed record Option(long Id, string Label)
    {
        public override string ToString() => Label;
    }

    public ShipmentDialog()
    {
        InitializeComponent();
        ShipDateBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadShippersAsync();
    }

    private async Task LoadShippersAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var shippers = await db.Customers.AsNoTracking()
                .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new { c.Id, c.NameAr })
                .ToListAsync();

            ShipperBox.ItemsSource = shippers.Select(s => new Option(s.Id, s.NameAr)).ToList();
            if (shippers.Count > 0) ShipperBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ShipperBox.SelectedItem is not Option shipper)
                throw new AccountingException("اختر المرسِل.");
            if (ShipDateBox.SelectedDate is null)
                throw new AccountingException("تاريخ الشحن مطلوب.");

            var shipment = Shipment.Create(AppServices.CompanyId, WaybillBox.Text, shipper.Id,
                ConsigneeBox.Text, CityBox.Text,
                DateOnly.FromDateTime(ShipDateBox.SelectedDate.Value),
                Number(FreightBox.Text, "أجرة الشحن"),
                Number(CodBox.Text, "مبلغ التحصيل"),
                (int)Number(PiecesBox.Text, "عدد الطرود"),
                Number(WeightBox.Text, "الوزن"),
                PhoneBox.Text);

            await using var db = AppServices.CreateDb();
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal Number(string text, string field) =>
        NumericInput.TryParse(text, out var value)
            ? value
            : throw new AccountingException($"قيمة «{field}» غير صالحة — ووردت «{text}».");

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
