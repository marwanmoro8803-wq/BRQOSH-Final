using Brqosh.Desktop.Ui;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق حوار سند القبض خارج الحوار: تحميل القوائم والرصيد والمستندات المفتوحة والترحيل هنا، والحوار يبني السند ويعرض فقط.
/// </summary>
public sealed class ReceiptVoucherDialogViewModel
{
    /// <summary>(م2-٢٢) «من عليه رصيدٌ يُحصَّل منه» لا «النشط» — والموقوف يُعلَّم.</summary>
    public async Task<List<ReceiptVoucherDialog.CustomerOption>> LoadCustomersAsync()
    {
        await using var db = AppServices.CreateDb();
        return (await new CollectableCustomersQuery(db).ExecuteAsync(AppServices.CompanyId))
            .Select(c => new ReceiptVoucherDialog.CustomerOption(c.Id, c.IsActive ? c.NameAr : $"{c.NameAr} (موقوف)"))
            .ToList();
    }

    public async Task<List<ReceiptVoucherDialog.BankOption>> LoadBanksAsync()
    {
        await using var db = AppServices.CreateDb();
        return await db.BankAccounts
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.Code)
            .Select(b => new ReceiptVoucherDialog.BankOption(b.Id, $"{b.Code} — {b.NameAr} ({b.BankName})"))
            .ToListAsync();
    }

    /// <summary>رصيد العميل ومستنداته المفتوحة للتخصيص.</summary>
    public async Task<(decimal Balance, List<ReceiptVoucherDialog.AllocRow> Open)> LoadCustomerDocumentsAsync(long customerId)
    {
        using var facade = new DesktopAccountingFacade();
        var db = facade.Db;
        var balance = await new CustomerBalanceQuery(db).ForCustomerAsync(AppServices.CompanyId, customerId);

        var open = await facade.GetService<PaymentAllocationService>().OpenSalesDocumentsAsync(AppServices.CompanyId, customerId);
        var rows = open.Select(d => new ReceiptVoucherDialog.AllocRow
        {
            DocumentType = d.DocumentType,
            DocumentId = d.DocumentId,
            DocumentNumber = d.DocumentNumber,
            TypeLabel = d.DocumentType switch
            {
                PaymentAllocationDocumentType.SalesInvoice => LocaleManager.T("فاتورة", "Invoice"),
                PaymentAllocationDocumentType.ProgressBilling => LocaleManager.T("مستخلص", "Progress billing"),
                _ => d.DocumentType.ToString(),
            },
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
        ReceiptVoucher voucher,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> allocations)
    {
        using var facade = new DesktopAccountingFacade();
        await facade.GetService<ReceiptVoucherPostingService>().PostAsync(voucher, allocations);
    }
}
