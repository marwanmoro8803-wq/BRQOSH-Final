using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق شاشة العملاء خارج الشاشة: التحميل والإيقاف وإعادة التفعيل والحفظ هنا، والشاشة تعرض وتسأل المستخدم فقط.
/// أول خطوة في نقل الواجهة إلى MVVM تدريجياً.
/// </summary>
public sealed class CustomersViewModel
{
    private List<Customer> _customers = [];

    public IReadOnlyList<CustomersPage.CustomerRow> Rows { get; private set; } = [];
    public int TotalCount { get; private set; }
    public decimal TotalBalance { get; private set; }

    public bool CanDeactivate => PermissionService.CanPostAccounting(AppServices.CurrentUser);
    public bool CanReactivate => PermissionService.CanPostAccounting(AppServices.CurrentUser);

    public Customer? Find(long id) => _customers.SingleOrDefault(c => c.Id == id);

    public async Task LoadAsync(string filter, bool showInactive)
    {
        await using var db = AppServices.CreateDb();
        var query = db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && (c.IsActive || showInactive));
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(c => c.NameAr.Contains(filter) || c.VatRegistrationNumber.Contains(filter));

        _customers = await query.OrderBy(c => c.NameAr).Take(200).ToListAsync();
        TotalCount = await db.Customers.CountAsync(c => c.CompanyId == AppServices.CompanyId && (c.IsActive || showInactive));
        var balances = await new Brqosh.Infrastructure.Sales.CustomerBalanceQuery(db)
            .AllBalancesAsync(AppServices.CompanyId);
        TotalBalance = balances.Values.Sum();
        Rows = _customers.Select(c => new CustomersPage.CustomerRow(
            c.Id, c.NameAr, c.VatRegistrationNumber, c.Phone, c.Address,
            c.SupportsStandardTaxInvoice ? "ضريبية (B2B)" : "مبسطة (B2C)",
            balances.GetValueOrDefault(c.Id))).ToList();
    }

    /// <summary>الحارس عند نقطة الكتابة لا في إخفاء الزر وحده.</summary>
    public async Task DeactivateAsync(long id)
    {
        if (!CanDeactivate)
            throw new UnauthorizedAccessException(PermissionService.DeniedActionMessage("الإيقاف", "deactivate"));
        await using var db = AppServices.CreateDb();
        var customer = await db.Customers.SingleAsync(c => c.Id == id);
        customer.Deactivate();
        await db.SaveChangesAsync();
    }

    public async Task ReactivateAsync(long id)
    {
        if (!CanReactivate)
            throw new UnauthorizedAccessException(PermissionService.DeniedActionMessage("إعادة التفعيل", "reactivate"));
        await using var db = AppServices.CreateDb();
        var customer = await db.Customers.SingleAsync(c => c.Id == id);
        customer.Reactivate();
        await db.SaveChangesAsync();
    }
}
