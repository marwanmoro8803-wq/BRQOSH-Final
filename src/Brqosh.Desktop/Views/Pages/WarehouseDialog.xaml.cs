using Brqosh.Infrastructure.Platform;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class WarehouseDialog : Window
{
    private readonly long? _warehouseId;

    public WarehouseDialog() => InitializeComponent();

    public WarehouseDialog(Warehouse warehouse) : this()
    {
        _warehouseId = warehouse.Id;
        CodeBox.Text = warehouse.Code;
        CodeBox.IsEnabled = false;
        NameBox.Text = warehouse.NameAr;
        DefaultCheck.IsChecked = warehouse.IsDefault;
        Title = "تعديل مستودع";
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            await using var db = AppServices.CreateDb();
            if (_warehouseId is long id)
            {
                var wh = await db.Warehouses.SingleAsync(w => w.Id == id);
                wh.Update(NameBox.Text);
                if (DefaultCheck.IsChecked == true) wh.SetAsDefault();
            }
            else
            {
                // (CEEF-229 · م2-١٠ / D-6) ‏فحصُ الكود قبل الحفظ — وإلا تحدّثت قاعدةُ
                // ‏البيانات بلغتها: انتهاكُ تفرّدٍ يُعرض نصُّه الإنجليزيّ في حوارٍ عربيّ،
                // ‏والمالكُ لا يرى الصفَّ الحاجز في أيّ قائمة (قد يكون محذوفاً).
                var availability = await new CodeAvailabilityQuery(db)
                    .CheckAsync(CodeAvailabilityScope.Warehouse, AppServices.CompanyId, CodeBox.Text);
                if (CodeAvailabilityQuery.Explain(availability, CodeAvailabilityScope.Warehouse, CodeBox.Text.Trim())
                    is string reason)
                    throw new AccountingException(reason);

                var wh = Warehouse.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text, DefaultCheck.IsChecked == true);
                db.Warehouses.Add(wh);
            }

            if (DefaultCheck.IsChecked == true)
            {
                var others = await db.Warehouses
                    .Where(w => w.CompanyId == AppServices.CompanyId && w.IsDefault)
                    .ToListAsync();
                foreach (var o in others)
                {
                    if (_warehouseId is null || o.Id != _warehouseId)
                        o.ClearDefault();
                }
            }

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

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
