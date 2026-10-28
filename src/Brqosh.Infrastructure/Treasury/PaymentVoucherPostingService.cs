using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// ترحيل سند الصرف مع تخصيص اختياري على فواتير مشتريات.
/// </summary>
public class PaymentVoucherPostingService(BrqoshDbContext db) : Brqosh.Application.Accounting.IPaymentVoucherPostingService
{
    public Task<PaymentVoucher> PostAsync(PaymentVoucher voucher) =>
        PostAsync(voucher, Array.Empty<(PaymentAllocationDocumentType, long, string, decimal)>());

    /// <summary>
    /// <b>يحسب ما يُحرَّر من ذمّة المورّد، ويُحضر حسابَي فرقِ العملة.</b>
    ///
    /// <para><b>وسندٌ بالريال يُعيد <c>null</c></b> — فيسلك الترحيلُ مسارَه القديم حرفاً،
    /// ولا يتغيّر رقمٌ في قاعدةٍ قائمة. <b>وهو الشرطُ الذي يحمي كلَّ منشأةٍ قائمة.</b></para>
    ///
    /// <para><b>ولا يُخلط سندٌ واحدٌ بعملتين</b> — كجانب العملاء حرفاً: تخصيصُ سندٍ
    /// دولاريٍّ على فاتورةٍ بالريال يُنتج «فرقَ عملة» هو في الحقيقة خطأُ إدخال.</para>
    /// </summary>
    private async Task<(decimal? Released, Account? Gain, Account? Loss)> ResolveSettlementAsync(
        PaymentVoucher voucher,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> allocations,
        CancellationToken ct)
    {
        if (voucher.ExchangeRate == 1m || allocations.Count == 0)
            return (null, null, null);

        var invoiceIds = allocations
            .Where(a => a.Type == PaymentAllocationDocumentType.PurchaseInvoice)
            .Select(a => a.DocumentId)
            .ToList();

        var invoices = await db.PurchaseInvoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        decimal released = 0m;
        foreach (var allocation in allocations)
        {
            if (!invoices.TryGetValue(allocation.DocumentId, out var invoice))
                throw new AccountingException(
                    $"سندٌ بعملة {voucher.CurrencyCode} خُصّص على مستندٍ لا تُقرأ عملتُه: {allocation.Number}.");

            if (invoice.CurrencyCode != voucher.CurrencyCode)
                throw new AccountingException(
                    $"عملةُ السند ({voucher.CurrencyCode}) تخالف عملةَ الفاتورة {allocation.Number} "
                    + $"({invoice.CurrencyCode}) — ولا يُخلط سندٌ واحدٌ بعملتين.");

            released += Math.Round(allocation.Amount * invoice.ExchangeRate, 2,
                MidpointRounding.AwayFromZero);
        }

        var gain = await SystemAccountProvisioning.EnsureAsync(
            db, voucher.CompanyId, SaudiChartOfAccounts.SystemCodes.RealizedFxGain, ct);
        var loss = await SystemAccountProvisioning.EnsureAsync(
            db, voucher.CompanyId, SaudiChartOfAccounts.SystemCodes.RealizedFxLoss, ct);

        return (released, gain, loss);
    }

    public async Task<PaymentVoucher> PostAsync(
        PaymentVoucher voucher,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> allocations)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
        {
            if (voucher.BranchId is null)
                voucher.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(voucher.CompanyId, BranchSessionContext.PreferredBranchId, ct));

            await FinancialPostingSupport.ValidateAsync(
                db, voucher.CompanyId, voucher.VoucherDate, PostingDocumentKind.PaymentVoucher, ct: ct);

            await new PaymentVoucherWorkflowService(db, new WorkflowEngine(db))
                .EnsureWorkflowApprovedAsync(voucher, ct);

            if (allocations.Count > 0)
            {
                var sum = allocations.Sum(a => a.Amount);
                if (Math.Abs(sum - voucher.Amount) > 0.01m)
                    throw new AccountingException(
                        $"مجموع التخصيصات {sum:N2} يجب أن يساوي مبلغ السند {voucher.Amount:N2}.");
            }

            var payable = await db.Accounts.SingleAsync(a =>
                a.CompanyId == voucher.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsPayable, ct);
            var cashOrBank = await ResolveCashOrBankAccountAsync(voucher.CompanyId, voucher.Method, voucher.BankAccountId);

            var voucherNumber = await DocumentNumberGenerator.NextAsync(db, 
                db.PaymentVouchers.Where(v => v.CompanyId == voucher.CompanyId).Select(v => v.VoucherNumber),
                "PV", voucher.VoucherDate, voucher.CompanyId);

            // ‏**قيمةُ الذمّة كما نشأت** — بسعرِ يوم كلّ فاتورة، لا
            // ‏بسعر يوم السداد. ومن فرقِ السعرين يقع الربحُ أو الخسارةُ المحقَّقة.
            var (releasedPayable, fxGain, fxLoss) =
                await ResolveSettlementAsync(voucher, allocations, ct);

            // ‏حسابُ خصم السداد — **ولا يُنشأ إلا حين يوجد خصم**،
            // ‏فقاعدةٌ لا تمنح خصماً لا يظهر فيها حسابٌ بلا حركة.
            var discountAccount = voucher.SettlementDiscount > 0m
                ? await SystemAccountProvisioning.EnsureAsync(db, voucher.CompanyId, await Brqosh.Infrastructure.Accounting.PostingRoleService.ResolveAsync(db, voucher.CompanyId, SaudiChartOfAccounts.PostingRoleCodes.PurchaseDiscountsEarned, ct), ct)
                : null;

            var entry = voucher.Post(voucherNumber, payable, cashOrBank,
                releasedPayable, fxGain, fxLoss, discountAccount);
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(voucher.CompanyId, voucher.VoucherDate);

            // ‏مركزُ تكلفة المستند يُختم على سطور المصروف — **وقبل
            // ‏الترحيل**، فالقيدُ المرحَّل لا يُعدَّل. ويُتحقَّق من المركز أوّلاً: مرجعٌ
            // ‏لمركزٍ محذوفٍ يُنتج تقريراً يعرض «غير مُصنَّف» ويُقرأ نقصَ تصنيف.
            await FinancialPostingSupport.EnsureCostCenterAsync(db, voucher.CompanyId, voucher.CostCenterId, ct);
            entry.StampCostCenter(voucher.CostCenterId);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);
            if (voucher.Id == 0)
                // ‏يُضاف إن لم يكن محفوظاً — واصطلاحُ `JournalPostingService` نفسُه.
                // ‏وكان الإضافةُ بلا شرطٍ في تسع خدمات، **وأسقطت شاشةَ الرواتب بـ`23505`** حين
                // ‏حفظت مسودّتَها ثمّ رحّلتها. والشرطُ يجعل المسارين مقبولين معاً، **فيُغلق
                // ‏صنفُ العطل لا حالتُه**: شاشةٌ تُكتب غداً بالاصطلاح الآخر لا تسقط.
                if (voucher.Id == 0)
                    db.PaymentVouchers.Add(voucher);
            await db.SaveChangesAsync(ct);

            voucher.LinkJournalEntry(entry.Id);
            await db.SaveChangesAsync(ct);

            if (allocations.Count > 0)
            {
                await new PaymentAllocationService(db).ApplyPaymentAllocationsAsync(
                    voucher.CompanyId, voucher.Id, voucher.VoucherDate, allocations, ct);
                await db.SaveChangesAsync(ct);
            }

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                voucher.CompanyId, "PaymentVoucher", voucher.Id, voucher.VoucherNumber!), ct);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                voucher.CompanyId, entry.Id, entry.EntryNumber!, voucher.VoucherDate), ct);

            await FinancialPostingSupport.AuditPostAsync(
                db, voucher.CompanyId, "PaymentVoucher", voucher.VoucherNumber!,
                reason: "ترحيل سند صرف", ct: ct);

            return voucher;
        });
    }

    private async Task<Account> ResolveCashOrBankAccountAsync(long companyId, ReceiptMethod method, long? bankAccountId)
    {
        // الحلّ من مكانٍ واحد — وكان السقوط هنا يُقيّد على الأب 1102
        // بينما البنوك تُقيَّد على أبنائه، فينشقّ نقد البنوك. انظر BankGlAccountResolver.
        if (method == ReceiptMethod.Cash)
            return await db.Accounts.SingleAsync(
                a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);

        return await BankGlAccountResolver.ResolveAsync(db, companyId, bankAccountId);
    }
}
