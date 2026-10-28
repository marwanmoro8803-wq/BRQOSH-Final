using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق حوار فاتورة المبيعات خارج الحوار: تحميل القوائم والترحيل هنا، والحوار يبني المستند من الشبكة ويعرض فقط.
/// </summary>
public sealed class SalesInvoiceDialogViewModel
{
    /// <summary>بيئة ZATCA إن كانت مفعّلة — و<c>null</c> إن لم تكن.</summary>
    public async Task<string?> LoadZatcaEnvironmentAsync()
    {
        await using var db = AppServices.CreateDb();
        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == AppServices.CompanyId);
        return settings is { IsEnabled: true } ? $"{settings.Environment}" : null;
    }

    public async Task<List<SalesInvoiceDialog.ProductOption>> LoadProductsAsync()
    {
        await using var db = AppServices.CreateDb();
        return await db.Products.Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new SalesInvoiceDialog.ProductOption(p.Id, $"{p.Code} — {p.NameAr}", p.SalePrice, p.NameAr))
            .ToListAsync();
    }

    public async Task<List<SalesInvoiceDialog.CustomerOption>> LoadCustomersAsync()
    {
        await using var db = AppServices.CreateDb();
        return await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.NameAr)
            .Select(c => new SalesInvoiceDialog.CustomerOption(c.Id, c.NameAr))
            .ToListAsync();
    }

    public async Task PostAsync(SalesInvoice invoice)
    {
        using var facade = new DesktopAccountingFacade();
        await facade.PostSalesInvoiceAsync(invoice);
    }
}
