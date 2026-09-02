using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط3-٤ب) إنشاءُ رحلةٍ وتحميلُ شحناتها.
///
/// <para><b>والشحناتُ تُختار من المفتوحة وحدَها</b>: المسلَّمةُ وصلت مستلمَها، <b>وتحميلُها
/// يُنسب إلى الرحلة إيراداً لم تُنتجه</b> — فيظهر المسارُ رابحاً بشحناتِ غيره. <b>والحصرُ
/// في القائمة يمنع الخطأ قبل أن يُرفض</b>، والحارسُ في النطاق باقٍ.</para>
/// </summary>
public partial class DeliveryTripDialog : Window
{
    public class ShipmentRow
    {
        public long Id { get; set; }
        public bool Selected { get; set; }
        public string WaybillNumber { get; set; } = "";
        public string DestinationCity { get; set; } = "";
        public decimal FreightCharge { get; set; }
        public string StatusText { get; set; } = "";
        public ShipmentStatus Status { get; set; }
    }

    private readonly ObservableCollection<ShipmentRow> _rows = [];

    public DeliveryTripDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        ShipmentGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadShipmentsAsync();
    }

    private async Task LoadShipmentsAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            // ‏**والمحمَّلُ على رحلةٍ أخرى لا يُعرض**: الشحنةُ الواحدة على رحلةٍ واحدة،
            // ‏**ورحلتان تحملانها تحسبان أجرتَها مرّتين** في ربحيّة مسارين.
            var loaded = await db.DeliveryTrips
                .Where(t => t.CompanyId == AppServices.CompanyId)
                .SelectMany(t => t.Shipments)
                .Select(s => s.ShipmentId)
                .ToListAsync();

            var shipments = await db.Shipments.AsNoTracking()
                .Where(s => s.CompanyId == AppServices.CompanyId
                            && s.Status != ShipmentStatus.Delivered
                            && s.Status != ShipmentStatus.Returned
                            && s.Status != ShipmentStatus.Cancelled
                            && !loaded.Contains(s.Id))
                .OrderBy(s => s.ShipDate)
                .ToListAsync();

            _rows.Clear();
            foreach (var s in shipments)
                _rows.Add(new ShipmentRow
                {
                    Id = s.Id,
                    WaybillNumber = s.WaybillNumber,
                    DestinationCity = s.DestinationCity,
                    FreightCharge = s.FreightCharge,
                    Status = s.Status,
                    StatusText = s.Status == ShipmentStatus.InTransit ? "في الطريق" : "منشأة",
                });
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ShipmentGrid.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            if (DateBox.SelectedDate is null)
                throw new AccountingException("تاريخ المغادرة مطلوب.");

            var trip = DeliveryTrip.Create(AppServices.CompanyId,
                NumberBox.Text ?? "", RouteBox.Text ?? "", PlateBox.Text ?? "",
                DateOnly.FromDateTime(DateBox.SelectedDate.Value));

            foreach (var row in _rows.Where(r => r.Selected))
                trip.LoadShipment(row.Id, row.Status);

            await using var db = AppServices.CreateDb();
            db.DeliveryTrips.Add(trip);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
