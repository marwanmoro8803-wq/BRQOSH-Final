using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.RealEstate;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط3-١ب) إنشاءُ أمرِ صيانةٍ بسطور تكلفته.
///
/// <para><b>والتكلفةُ سطورٌ لا رقمٌ واحد</b>: «إصلاحٌ — ٣٢٠٠» لا يُراجَع ولا يُطعَن فيه.
/// <b>وهو ما يجعل المصادرةَ سنداً</b>: المستأجرُ يرى قطعَ الغيار والأجرةَ مفصّلة.</para>
///
/// <para><b>والتحميلُ على المستأجر لا يُقبل إلا في التلف</b> — والحارسُ في النطاق يرفضه
/// باسمه، <b>فالاستهلاكُ العاديُّ ثمنُ التأجير لا ذنبُ الساكن</b>.</para>
/// </summary>
public partial class MaintenanceOrderDialog : Window
{
    public record UnitOption(long Id, long PropertyId, long? LeaseId, string Label);

    public class CostRow
    {
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
    }

    private readonly ObservableCollection<CostRow> _rows = [];
    private long _firstPropertyId;

    public MaintenanceOrderDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        CostGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadUnitsAsync();
    }

    private async Task LoadUnitsAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var properties = await db.Properties.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
                .Select(p => new { p.Id, p.NameAr })
                .ToListAsync();

            _firstPropertyId = properties.Count > 0 ? properties[0].Id : 0;

            var units = await db.PropertyUnits.AsNoTracking()
                .Where(u => u.CompanyId == AppServices.CompanyId)
                .Select(u => new { u.Id, u.PropertyId, u.UnitNumber })
                .ToListAsync();

            // ‏**والعقدُ النشط للوحدة هو ما يُحمَّل عليه** — ولا تحميلَ بلا عقد.
            var leases = await db.LeaseContracts.AsNoTracking()
                .Where(l => l.CompanyId == AppServices.CompanyId
                            && l.Status == LeaseStatus.Active)
                .Select(l => new { l.Id, l.PropertyUnitId })
                .ToListAsync();

            var byUnit = leases.GroupBy(l => l.PropertyUnitId)
                .ToDictionary(g => g.Key, g => g.First().Id);

            var names = properties.ToDictionary(p => p.Id, p => p.NameAr);

            UnitBox.ItemsSource = units
                .Select(u => new UnitOption(u.Id, u.PropertyId,
                    byUnit.TryGetValue(u.Id, out var lid) ? lid : null,
                    $"{(names.TryGetValue(u.PropertyId, out var pn) ? pn : "")} — {u.UnitNumber}"))
                .ToList();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        CostGrid.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            if (DateBox.SelectedDate is null)
                throw new AccountingException("تاريخ الطلب مطلوب.");

            var unit = UnitBox.SelectedItem as UnitOption;
            var propertyId = unit?.PropertyId ?? _firstPropertyId;
            if (propertyId <= 0)
                throw new AccountingException("لا عقاراتٍ بعد — أنشئ عقاراً أولاً.");

            var order = MaintenanceOrder.Create(AppServices.CompanyId, propertyId,
                NumberBox.Text ?? "", (MaintenanceKind)Math.Max(0, KindBox.SelectedIndex),
                DateOnly.FromDateTime(DateBox.SelectedDate.Value),
                DescriptionBox.Text ?? "", unit?.Id, VendorBox.Text);

            foreach (var row in _rows.Where(r => !string.IsNullOrWhiteSpace(r.Description) && r.Amount > 0m))
                order.AddCost(row.Description, row.Amount);

            var tenantCharge = ParseAmount(TenantChargeBox.Text, "المحمَّل على المستأجر");
            if (tenantCharge > 0m)
            {
                if (unit?.LeaseId is not long leaseId)
                    throw new AccountingException(
                        "لا عقدَ نشطاً على هذه الوحدة — ولا يُحمَّل على مستأجرٍ لا وجودَ له.");

                order.ChargeTenant(tenantCharge, leaseId);
            }

            order.Complete(DateOnly.FromDateTime(DateBox.SelectedDate.Value));

            await using var db = AppServices.CreateDb();
            db.MaintenanceOrders.Add(order);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal ParseAmount(string? text, string field)
    {
        if (decimal.TryParse((text ?? "").Trim(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var value))
            return value;

        throw new AccountingException($"مبلغ «{field}» غير صالح.");
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
