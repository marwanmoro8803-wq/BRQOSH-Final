using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق شاشة سندات القبض خارج الشاشة: التحميل والعكس هنا، والشاشة تعرض وتسأل المستخدم فقط.
/// </summary>
public sealed class ReceiptVouchersViewModel
{
    public IReadOnlyList<ReceiptVouchersPage.VoucherRow> Rows { get; private set; } = [];
    public int PostedCount { get; private set; }
    public decimal PostedTotal { get; private set; }

    internal async Task LoadAsync(PagingState paging)
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.ReceiptVouchers.Where(v => v.CompanyId == AppServices.CompanyId);
        paging.SetTotal(await baseQuery.CountAsync());

        var vouchers = await baseQuery
            .OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync();

        var customerIds = vouchers.Select(v => v.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        PostedTotal = await baseQuery
            .Where(v => v.Status == ReceiptVoucherStatus.Posted)
            .SumAsync(v => (decimal?)v.Amount) ?? 0m;

        Rows = vouchers.Select(v => new ReceiptVouchersPage.VoucherRow(
            v.Id, v.VoucherNumber, v.VoucherDate,
            customers.GetValueOrDefault(v.CustomerId, "—"),
            v.Amount,
            v.Method == ReceiptMethod.Cash ? "نقداً" : "بنك",
            v.Reference, v.Description,
            ReceiptVouchersPage.StatusLabel(v.Status), v.Status, v.JournalEntryId)).ToList();

        PostedCount = await baseQuery.CountAsync(v => v.Status == ReceiptVoucherStatus.Posted);
    }

    public async Task ReverseAsync(long id, DateOnly reversalDate)
    {
        using var facade = new DesktopAccountingFacade();
        var username = SecurityContext.ResolveUsername();
        await facade.GetService<VoucherReversalService>().ReverseReceiptAsync(
            AppServices.CompanyId, id, reversalDate, username);
    }
}
