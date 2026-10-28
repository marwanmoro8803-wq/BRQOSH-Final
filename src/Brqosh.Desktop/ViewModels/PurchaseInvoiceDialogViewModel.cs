using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق حوار فاتورة المشتريات خارج الحوار: تحميل القوائم والترحيل هنا، والحوار يبني المستند من الشبكة ويعرض فقط.
/// </summary>
public sealed class PurchaseInvoiceDialogViewModel
{
    /// <summary>المشاريع التي تقبل تكلفة (بلا الخيار الأول «بلا مشروع» — يضيفه الحوار).</summary>
    public async Task<List<SalesInvoiceDialog.WarehouseOption>> LoadProjectsAsync()
    {
        await using var db = AppServices.CreateDb();
        return await db.ConstructionProjects
            .Where(p => p.CompanyId == AppServices.CompanyId
                        && p.Status != Domain.Construction.ConstructionProjectStatus.Completed
                        && p.Status != Domain.Construction.ConstructionProjectStatus.Cancelled)
            .OrderBy(p => p.Code)
            .Select(p => new SalesInvoiceDialog.WarehouseOption(p.Id, p.Code + " — " + p.NameAr))
            .ToListAsync();
    }

    /// <summary>الأصول التي تنتظر فاتورة مورد (بلا الخيار الأول «بلا أصل» — يضيفه الحوار).</summary>
    public async Task<List<SalesInvoiceDialog.WarehouseOption>> LoadAssetsAsync()
    {
        await using var db = AppServices.CreateDb();
        return await db.FixedAssets
            .Where(a => a.CompanyId == AppServices.CompanyId && a.AwaitingInvoice && a.CancelledOn == null)
            .OrderBy(a => a.Code)
            .Select(a => new SalesInvoiceDialog.WarehouseOption(a.Id, a.Code + " — " + a.NameAr))
            .ToListAsync();
    }

    public async Task<List<SalesInvoiceDialog.ProductOption>> LoadProductsAsync()
    {
        await using var db = AppServices.CreateDb();
        return await db.Products.Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new SalesInvoiceDialog.ProductOption(p.Id, $"{p.Code} — {p.NameAr}", p.SalePrice, p.NameAr))
            .ToListAsync();
    }

    public async Task<List<PurchaseInvoiceDialog.SupplierOption>> LoadSuppliersAsync()
    {
        await using var db = AppServices.CreateDb();
        var suppliers = await db.Suppliers
            .Where(s => s.CompanyId == AppServices.CompanyId && s.IsActive)
            .OrderBy(s => s.NameAr)
            .ToListAsync();
        return suppliers
            .Select(s => new PurchaseInvoiceDialog.SupplierOption(s.Id, s.IsVatRegistered, s.NameAr))
            .ToList();
    }

    /// <summary>
    /// عملاتُ المنشأة المستعملة (لها سعرُ صرفٍ مسجَّل) لتتقدّم في قائمة العملة بعد عملة الأساس.
    /// <b>ترتيبٌ لا قيد</b>: فإن تعذّرت القراءةُ بقيت القائمةُ كاملةً من <c>CurrencyCatalog</c>.
    /// </summary>
    public async Task<List<string>> LoadCurrenciesAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var rates = db.ExchangeRates.Where(r => r.CompanyId == AppServices.CompanyId);
            return await rates.Select(r => r.FromCurrencyCode)
                .Union(rates.Select(r => r.ToCurrencyCode))
                .ToListAsync();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task PostAsync(PurchaseInvoice invoice)
    {
        using var facade = new DesktopAccountingFacade();
        await facade.PostPurchaseInvoiceAsync(invoice);
    }
}
