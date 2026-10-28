using Brqosh.Application.Reporting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public class DataQualityService(BrqoshDbContext db) : IDataQualityService
{
    public async Task<DataQualityReport> RunAsync(long companyId, CancellationToken ct = default)
    {
        var findings = new List<DataQualityFinding>();

        var unbalancedCount = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.Status == JournalEntryStatus.Posted)
            .Select(e => new
            {
                Debit = e.Lines.Sum(l => l.Debit),
                Credit = e.Lines.Sum(l => l.Credit),
            })
            .Where(x => x.Debit != x.Credit)
            .CountAsync(ct);

        if (unbalancedCount > 0)
        {
            findings.Add(new DataQualityFinding(
                "JE_UNBALANCED_POSTED",
                "Error",
                $"{unbalancedCount} قيد يوميّة مرحّل مجموع مدينه لا يساوي مجموع دائنه.",
                $"{unbalancedCount} posted journal entr{(unbalancedCount == 1 ? "y" : "ies")} with TotalDebit != TotalCredit."));
        }

        var orphanInvoices = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.JournalEntryId == null)
            .CountAsync(ct);

        if (orphanInvoices > 0)
        {
            findings.Add(new DataQualityFinding(
                "SI_POSTED_NO_JE",
                "Warning",
                $"{orphanInvoices} فاتورة مبيعات مرحّلة بلا قيد يوميّة مرتبط.",
                $"{orphanInvoices} posted sales invoice(s) without JournalEntryId."));
        }

        var negativeStock = await (
            from balance in db.InventoryBalances
            join product in db.Products on balance.ProductId equals product.Id
            where balance.CompanyId == companyId
                  && product.CompanyId == companyId
                  && product.IsActive
                  && product.Type == ProductType.Stockable
                  && balance.QuantityOnHand < 0
            select balance.Id).CountAsync(ct);

        if (negativeStock > 0)
        {
            findings.Add(new DataQualityFinding(
                "INV_NEGATIVE_ON_HAND",
                "Warning",
                $"{negativeStock} رصيد صنف مخزنيّ بكمية سالبة.",
                $"{negativeStock} stockable product balance(s) with negative on-hand quantity."));
        }

        var hasHq = await db.Branches
            .AnyAsync(b => b.CompanyId == companyId && b.IsHeadquarters && b.IsActive, ct);

        if (!hasHq)
        {
            findings.Add(new DataQualityFinding(
                "BRANCH_HQ_MISSING",
                "Error",
                "لا يوجد فرع رئيسيّ (مقرّ) نشط للمنشأة.",
                "Company has no active headquarters branch."));
        }

        var since = DateTime.UtcNow.AddHours(-24);
        var eventCount = await db.StoredDomainEvents
            .CountAsync(e => e.CompanyId == companyId && e.OccurredAtUtc >= since, ct);

        findings.Add(new DataQualityFinding(
            "DOMAIN_EVENTS_24H",
            "Info",
            $"{eventCount} حدث نطاق مسجّل خلال آخر 24 ساعة.",
            $"{eventCount} domain event(s) recorded in the last 24 hours."));

        // ‏**هويّاتُ الدفاتر المساعدة تُقاس في المنتَج لا في الاختبار وحدَه.**
        //
        // ‏كانت هـ-١ وهـ-٢ وهـ-٣ **مُعرَّفةً في `LedgerIdentities` داخل شجرة الاختبارات**
        // ‏فقط. **فلا سبيلَ للمالك أن يعرف أنّ حسابَ المراقبة فارق دفترَه المساعد** —
        // ‏وذلك بالضبط ما يقع حين يُرحَّل قيدٌ يدويٌّ إلى `1104` أو `2101` أو `1107`:
        // ‏**الأستاذُ يتحرّك والدفترُ المساعدُ لا يستطيع أن يتبعه، ولا شيءَ يشكو**
        // ‏(القيدُ متوازن، فميزانُ المراجعة سليم).
        //
        // ‏**والكشفُ هنا لا الحظر**: القيدُ اليدويُّ إلى حساب مراقبةٍ **هو اليومَ
        // ‏المسارُ الوحيدُ للأرصدة الافتتاحيّة** (بندٌ مفتوحٌ مُسمًّى)، **فمنعُه يُغلق
        // ‏التأسيسَ نفسَه**. فيُجعَل الانحرافُ **مرئيّاً ومقيساً** بدل أن يبقى دائماً صامتاً.
        //
        // ‏**والأدواتُ كلُّها قائمةٌ في البنية التحتيّة** — استعلاماتُ الأرصدة وميزانُ
        // ‏المراجعة — **والناقصُ كان سطحاً يستدعيها**.
        await AddSubledgerIdentityAsync(findings, companyId, "AR_SUBLEDGER_DRIFT",
            SaudiChartOfAccounts.SystemCodes.AccountsReceivable, "الذمم المدينة", "receivables",
            (await new CustomerBalanceQuery(db).AllBalancesAsync(companyId)).Values.Sum(),
            creditNature: false, ct);

        await AddSubledgerIdentityAsync(findings, companyId, "AP_SUBLEDGER_DRIFT",
            SaudiChartOfAccounts.SystemCodes.AccountsPayable, "الذمم الدائنة", "payables",
            (await new SupplierBalanceQuery(db).AllBalancesAsync(companyId)).Values.Sum(),
            creditNature: true, ct);

        await AddSubledgerIdentityAsync(findings, companyId, "INVENTORY_SUBLEDGER_DRIFT",
            SaudiChartOfAccounts.SystemCodes.Inventory, "المخزون", "inventory",
            (await new StockBalanceQuery(db).ExecuteAsync(companyId, includeZeroBalances: false))
                .Sum(r => r.TotalValue),
            creditNature: false, ct);

        // ‏**سلسلةُ تجزئة سجلّ التدقيق تُفحَص في المنتَج.**
        //
        // ‏`AuditChainVerifier` يقول عن نفسه: «لا يمنع العبث — يجعله **غير قابل
        // ‏للإخفاء**». **ولم يكن له مستدعٍ واحدٌ في `src/`** — في الاختبارات وحدَها.
        // ‏**فالعبثُ كان قابلاً للإخفاء تماماً**: لا أحدَ ينظر.
        //
        // ‏ومعترضُ EF يمنع التعديلَ والحذفَ **من داخل التطبيق**، والوصولُ المباشر
        // ‏للقاعدة يتجاوزه — **وهو المسارُ الذي تُوضع السلسلةُ لأجله أصلاً**.
        var chain = await new AuditChainVerifier(db).VerifyAsync(companyId, ct);

        findings.Add(chain.IsIntact
            ? new DataQualityFinding("AUDIT_CHAIN", "Info",
                $"سلسلة سجلّ التدقيق سليمة — فُحص {chain.EntriesChecked} إدخالاً.",
                $"Audit log hash chain intact — {chain.EntriesChecked} entr"
                + $"{(chain.EntriesChecked == 1 ? "y" : "ies")} checked.")
            : new DataQualityFinding("AUDIT_CHAIN", "Error",
                $"سلسلة سجلّ التدقيق مكسورة في {chain.Breaks.Count} موضعاً "
                + $"(أوّلُها عند التسلسل {chain.Breaks[0].Sequence}: {chain.Breaks[0].Description}) — "
                + "عُبث بالسجلّ من خارج التطبيق، والسجلُّ حجّةٌ نظاميّة.",
                $"Audit log hash chain broken at {chain.Breaks.Count} point(s) "
                + $"(first at sequence {chain.Breaks[0].Sequence}: {chain.Breaks[0].Kind}) — "
                + "the log was altered outside the application."));

        return new DataQualityReport(companyId, DateTime.UtcNow, findings);
    }

    /// <summary>
    /// يُقارن دفتراً مساعداً بحساب مراقبته، <b>ويُبلّغ الفرقَ
    /// بمقداره</b> لا بوجوده — فالمبلغُ هو ما يُبحث عنه في الدفاتر.
    ///
    /// <para><b>ويُبلَّغ السليمُ كذلك بدرجة <c>Info</c></b>: غيابُ السطر يُقرأ «لم يُفحص»،
    /// <b>ووجودُه بصفرٍ يقول «فُحص ولم ينحرف»</b> — وهو عرفُ هذا المستودع في
    /// عرض الاستقطاعات وغيرِه.</para>
    /// </summary>
    private async Task AddSubledgerIdentityAsync(
        List<DataQualityFinding> findings, long companyId, string code,
        string accountCode, string nameAr, string nameEn,
        decimal subsidiary, bool creditNature, CancellationToken ct)
    {
        var rows = await new TrialBalanceQuery(db).ExecuteAsync(
            companyId, new DateOnly(1900, 1, 1), new DateOnly(2999, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        var signed = row is null ? 0m : row.TotalDebit - row.TotalCredit;

        // ‏حسابٌ دائنُ الطبيعة رصيدُه الموجب دائن، فتُعكس إشارتُه ليُقارَن بدفترٍ موجب.
        var ledger = Math.Round(creditNature ? -signed : signed, 2, MidpointRounding.AwayFromZero);
        var drift = Math.Round(Math.Round(subsidiary, 2, MidpointRounding.AwayFromZero) - ledger,
            2, MidpointRounding.AwayFromZero);

        findings.Add(drift == 0m
            ? new DataQualityFinding(code, "Info",
                $"{nameAr} ({accountCode}): الدفتر المساعد يطابق الأستاذ.",
                $"{nameEn} ({accountCode}): subledger agrees with the ledger.")
            : new DataQualityFinding(code, "Error",
                $"{nameAr} ({accountCode}): الدفتر المساعد {subsidiary:N2} · الأستاذ {ledger:N2} · "
                + $"الفرق {drift:N2} — مبلغٌ في الميزانية لا يقابله طرفٌ في الدفتر المساعد.",
                $"{nameEn} ({accountCode}): subledger {subsidiary:N2} vs ledger {ledger:N2}, "
                + $"difference {drift:N2} — a balance-sheet amount with no counterparty behind it."));
    }
}
