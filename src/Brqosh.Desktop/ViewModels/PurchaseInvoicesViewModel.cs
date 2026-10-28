using Brqosh.Desktop.Ui;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق شاشة فواتير المشتريات خارج الشاشة: التحميل والمؤشرات والعرض والإلغاء هنا، والشاشة تعرض وتسأل المستخدم فقط.
/// </summary>
public sealed class PurchaseInvoicesViewModel
{
    public IReadOnlyList<PurchaseInvoicesPage.InvoiceRow> Rows { get; private set; } = [];
    public int MonthCount { get; private set; }
    public decimal MonthTotal { get; private set; }
    public decimal VatTotal { get; private set; }

    /// <summary>معايير شريط البحث والفلترة؛ تُطبَّق على القائمة والترقيم فقط، والمؤشرات تبقى على كل الفواتير.</summary>
    public string? Search { get; set; }
    public PurchaseInvoiceStatus? StatusFilter { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    internal async Task LoadAsync(PagingState paging)
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.PurchaseInvoices.Where(i => i.CompanyId == AppServices.CompanyId);
        var listQuery = baseQuery;
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            listQuery = listQuery.Where(i => (i.InvoiceNumber != null && i.InvoiceNumber.Contains(term)) || i.SupplierInvoiceReference.Contains(term)
                || db.Suppliers.Any(p => p.Id == i.SupplierId && p.NameAr.Contains(term)));
        }
        if (StatusFilter is { } status) listQuery = listQuery.Where(i => i.Status == status);
        if (FromDate is { } from) listQuery = listQuery.Where(i => i.InvoiceDate >= from);
        if (ToDate is { } to) listQuery = listQuery.Where(i => i.InvoiceDate <= to);
        paging.SetTotal(await listQuery.CountAsync());

        var invoices = await listQuery
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync();

        var supplierIds = invoices.Select(i => i.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        Rows = invoices.Select(i => new PurchaseInvoicesPage.InvoiceRow(
            i.Id, i.InvoiceNumber, i.SupplierInvoiceReference, i.InvoiceDate,
            suppliers.GetValueOrDefault(i.SupplierId, "—"),
            i.Subtotal, i.VatAmount, i.GrandTotal(),
            i.VatDeductible ? "قابل للخصم" : "غير قابل",
            i.Status switch
            {
                PurchaseInvoiceStatus.Posted => LocaleManager.T("مرحّلة", "Posted"),
                PurchaseInvoiceStatus.Cancelled => LocaleManager.T("ملغاة", "Cancelled"),
                _ => LocaleManager.T("مسودة", "Draft"),
            }, i.Status, i.JournalEntryId)).ToList();

        await LoadKpisAsync(baseQuery);
    }

    private async Task LoadKpisAsync(IQueryable<PurchaseInvoice> baseQuery)
    {
        var monthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var postedQuery = baseQuery.Where(i =>
            i.Status == PurchaseInvoiceStatus.Posted && i.InvoiceDate >= monthStart);

        MonthCount = await postedQuery.CountAsync();
        MonthTotal = await postedQuery.SumGrandTotalAsync();
        VatTotal = await postedQuery.SumVatAsync();
    }

    public async Task<PurchaseInvoice?> FindAsync(long id)
    {
        await using var db = AppServices.CreateDb();
        return await db.PurchaseInvoices.SingleOrDefaultAsync(i => i.Id == id);
    }

    public async Task CancelAsync(long id, DateOnly cancelDate)
    {
        using var facade = new DesktopAccountingFacade();
        var username = SecurityContext.ResolveUsername();
        await facade.GetService<PurchaseInvoiceCancellationService>().CancelAsync(
            AppServices.CompanyId, id, cancelDate, username);
    }
}
