using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// ترحيل سند القبض: رقم RV-{سنة}-{تسلسل}، القيد: صندوق/بنك (مدين) ← عملاء (دائن).
/// </summary>
public class ReceiptVoucherPostingService(BrqoshDbContext db)
{
    public async Task<ReceiptVoucher> PostAsync(ReceiptVoucher voucher)
    {
        var posting = new JournalPostingService(db);
        await posting.EnsurePeriodOpenAsync(voucher.CompanyId, voucher.VoucherDate);

        var cashCode = voucher.Method == ReceiptMethod.Cash
            ? SaudiChartOfAccounts.SystemCodes.CashOnHand
            : SaudiChartOfAccounts.SystemCodes.CashAtBanks;

        var cashOrBank = await db.Accounts.SingleAsync(a => a.CompanyId == voucher.CompanyId && a.Code == cashCode);
        var receivable = await db.Accounts.SingleAsync(a =>
            a.CompanyId == voucher.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsReceivable);

        var voucherNumber = await DocumentNumberGenerator.NextAsync(
            db.ReceiptVouchers.Where(v => v.CompanyId == voucher.CompanyId).Select(v => v.VoucherNumber),
            "RV", voucher.VoucherDate.Year);

        var entry = voucher.Post(voucherNumber, cashOrBank, receivable);
        var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(voucher.CompanyId, voucher.VoucherDate);
        entry.Post(entryNumber);

        db.JournalEntries.Add(entry);
        db.ReceiptVouchers.Add(voucher);
        await db.SaveChangesAsync();

        voucher.LinkJournalEntry(entry.Id);
        await db.SaveChangesAsync();
        return voucher;
    }
}
