using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop;

/// <summary>
/// واجهة ترحيل Golden Path — DI scope بدلاً من الإنشاء اليدوي المباشر للخدمات.
/// CEEF-122: الخدمات تُحلّ من الحاوية (نفس DbContext ضمن النطاق).
/// </summary>
public sealed class DesktopAccountingFacade : IDisposable
{
    private readonly IServiceScope _scope;

    public DesktopAccountingFacade()
    {
        _scope = AppServices.CreateInfrastructureScope();
    }

    /// <summary>DbContext النطاق — للصفحات التي تحتاج قراءة ضمن نفس وحدة العمل.</summary>
    public BrqoshDbContext Db => _scope.ServiceProvider.GetRequiredService<BrqoshDbContext>();

    /// <summary>حل أي خدمة مسجّلة من نطاق الواجهة — بديل الإنشاء اليدوي للخدمات في الصفحات.</summary>
    public T GetService<T>() where T : notnull =>
        _scope.ServiceProvider.GetRequiredService<T>();

    public Task<SalesInvoice> PostSalesInvoiceAsync(SalesInvoice invoice) =>
        GetService<SalesInvoicePostingService>().PostAsync(invoice);

    public Task<PurchaseInvoice> PostPurchaseInvoiceAsync(PurchaseInvoice invoice) =>
        GetService<PurchaseInvoicePostingService>().PostAsync(invoice);

    public Task<PaymentVoucher> PostPaymentVoucherAsync(
        PaymentVoucher voucher,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> allocations) =>
        GetService<PaymentVoucherPostingService>().PostAsync(voucher, allocations);

    public Task<JournalEntry> PostJournalEntryAsync(JournalEntry entry) =>
        GetService<JournalPostingService>().PostAsync(entry);

    public Task<JournalEntry> PostBankTransferAsync(
        long fromBankAccountId, long toBankAccountId, decimal amount, DateOnly transferDate, string? reference = null) =>
        GetService<BankTransferService>().TransferAsync(
            AppServices.CompanyId, fromBankAccountId, toBankAccountId, amount, transferDate, reference);

    public void Dispose() => _scope.Dispose();
}
