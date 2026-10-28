using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق حوار سند الصرف خارج الحوار: تحميل القوائم والرصيد والمستندات المفتوحة والترحيل هنا، والحوار يبني السند ويعرض فقط.
/// </summary>
public sealed class PaymentVoucherDialogViewModel
{
    public async Task<List<PaymentVoucherDialog.SupplierOption>> LoadSuppliersAsync()
    {
        await using var db = AppServices.CreateDb();
        return await db.Suppliers
            .Where(s => s.CompanyId == AppServices.CompanyId && s.IsActive)
            .OrderBy(s => s.NameAr)
            .Select(s => new PaymentVoucherDialog.SupplierOption(s.Id, s.NameAr))
            .ToListAsync();
    }

    public async Task<List<PaymentVoucherDialog.BankOption>> LoadBanksAsync()
    {
        await using var db = AppServices.CreateDb();
        return await db.BankAccounts
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.Code)
            .Select(b => new PaymentVoucherDialog.BankOption(b.Id, $"{b.Code} — {b.NameAr} ({b.BankName})"))
            .ToListAsync();
    }

    /// <summary>رصيد المورد ومستنداته المفتوحة للتخصيص.</summary>
    public async Task<(decimal Balance, List<PaymentVoucherDialog.AllocRow> Open)> LoadSupplierDocumentsAsync(long supplierId)
    {
        using var facade = new DesktopAccountingFacade();
        var db = facade.Db;
        var balance = await new SupplierBalanceQuery(db).ForSupplierAsync(AppServices.CompanyId, supplierId);

        var open = await facade.GetService<PaymentAllocationService>().OpenPurchaseDocumentsAsync(AppServices.CompanyId, supplierId);
        var rows = open.Select(d => new PaymentVoucherDialog.AllocRow
        {
            DocumentType = d.DocumentType,
            DocumentId = d.DocumentId,
            DocumentNumber = d.DocumentNumber,
            RemainingAmount = d.RemainingAmount,
            AllocateAmount = 0m,
        }).ToList();
        return (balance.Balance, rows);
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

    public async Task PostAsync(
        PaymentVoucher voucher,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> allocations)
    {
        using var facade = new DesktopAccountingFacade();
        await facade.PostPaymentVoucherAsync(voucher, allocations);
    }
}
