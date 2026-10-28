using Brqosh.Desktop.Ui;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق شاشة فواتير المبيعات خارج الشاشة: التحميل والمؤشرات والعرض والإلغاء هنا، والشاشة تعرض وتسأل المستخدم فقط.
/// </summary>
public sealed class SalesInvoicesViewModel
{
    public IReadOnlyList<SalesInvoicesPage.InvoiceRow> Rows { get; private set; } = [];
    public int MonthCount { get; private set; }
    public decimal MonthTotal { get; private set; }
    public decimal VatTotal { get; private set; }

    internal async Task LoadAsync(PagingState paging)
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.SalesInvoices.Where(i => i.CompanyId == AppServices.CompanyId);
        paging.SetTotal(await baseQuery.CountAsync());

        var invoices = await baseQuery
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync();

        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        Rows = invoices.Select(i => new SalesInvoicesPage.InvoiceRow(
            i.Id, i.InvoiceNumber, i.InvoiceDate,
            customers.GetValueOrDefault(i.CustomerId, "—"),
            i.Subtotal, i.VatAmount, i.GrandTotal(),
            i.Status switch
            {
                SalesInvoiceStatus.Posted => LocaleManager.T("مرحّلة", "Posted"),
                SalesInvoiceStatus.Cancelled => LocaleManager.T("ملغاة", "Cancelled"),
                _ => LocaleManager.T("مسودة", "Draft"),
            }, i.Status, i.JournalEntryId)).ToList();

        await LoadKpisAsync(baseQuery);
    }

    private async Task LoadKpisAsync(IQueryable<SalesInvoice> baseQuery)
    {
        var monthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var postedQuery = baseQuery.Where(i =>
            i.Status == SalesInvoiceStatus.Posted && i.InvoiceDate >= monthStart);

        MonthCount = await postedQuery.CountAsync();
        MonthTotal = await postedQuery.SumGrandTotalAsync();
        VatTotal = await postedQuery.SumVatAsync();
    }

    public async Task<SalesInvoice?> FindAsync(long id)
    {
        await using var db = AppServices.CreateDb();
        return await db.SalesInvoices.SingleOrDefaultAsync(i => i.Id == id);
    }

    public async Task CancelAsync(long id, DateOnly cancelDate)
    {
        using var facade = new DesktopAccountingFacade();
        var username = SecurityContext.ResolveUsername();
        await facade.GetService<SalesInvoiceCancellationService>().CancelAsync(
            AppServices.CompanyId, id, cancelDate, username);
    }
}
