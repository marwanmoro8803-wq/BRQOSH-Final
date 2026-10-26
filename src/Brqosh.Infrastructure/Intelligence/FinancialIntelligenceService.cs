using Brqosh.Application.Intelligence;
using Brqosh.Application.Reporting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Intelligence;

/// <summary>
/// محرك دعم قرار مالي قائم على قواعد قابلة للشرح (Deterministic Decision Support).
/// لا يستدعي نماذج لغوية خارجية — أي ادعاء «AI حي/LLM» غير صحيح لهذا المسار.
/// </summary>
public class FinancialIntelligenceService(
    BrqoshDbContext db,
    IDataQualityService dataQuality) : IFinancialIntelligenceService
{
    public const string EngineId = "RulesEngine-v1";

    public async Task<FinancialIntelligenceReport> AnalyzeAsync(
        long companyId, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var day = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var insights = new List<FinancialInsight>();

        await AddPerformanceInsightsAsync(companyId, day, insights, ct);
        await AddLiquidityInsightsAsync(companyId, day, insights, ct);
        await AddAnomalyInsightsAsync(companyId, day, insights, ct);
        await AddFraudRiskInsightsAsync(companyId, day, insights, ct);
        await AddDataQualityBridgeAsync(companyId, insights, ct);
        await AddClassificationHintsAsync(companyId, day, insights, ct);

        if (insights.Count == 0)
        {
            insights.Add(new FinancialInsight(
                "OK_NO_FLAGS",
                "Performance",
                "Info",
                "لا تنبيهات حرجة",
                "لم يكتشف المحرك القاعدي مؤشرات تستدعي تدخلاً فورياً حتى تاريخ التحليل.",
                "No critical flags",
                "The rules engine found nothing requiring immediate action as of the analysis date."));
        }

        return new FinancialIntelligenceReport(companyId, DateTime.UtcNow, EngineId, insights);
    }

    private async Task AddPerformanceInsightsAsync(
        long companyId, DateOnly day, List<FinancialInsight> insights, CancellationToken ct)
    {
        var monthStart = new DateOnly(day.Year, day.Month, 1);
        var postedSalesQuery = db.SalesInvoices
            .Where(i => i.CompanyId == companyId
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.InvoiceDate >= monthStart
                        && i.InvoiceDate <= day);
        var salesTotal = await postedSalesQuery.SumGrandTotalAsync(ct);
        var salesCount = await postedSalesQuery.CountAsync(ct);

        insights.Add(new FinancialInsight(
            "PERF_SALES_MTD",
            "Performance",
            "Info",
            "مبيعات الشهر حتى التاريخ",
            $"إجمالي فواتير مبيعات مرحّلة من {monthStart:yyyy-MM-dd} إلى {day:yyyy-MM-dd}: {salesTotal:N2} · عدد الفواتير: {salesCount}.",
            "Sales month to date",
            $"Total posted sales invoices from {monthStart:yyyy-MM-dd} to {day:yyyy-MM-dd}: {salesTotal:N2} · invoice count: {salesCount}.",
            salesTotal));

        var prevMonthEnd = monthStart.AddDays(-1);
        var prevMonthStart = new DateOnly(prevMonthEnd.Year, prevMonthEnd.Month, 1);
        var prevSalesQuery = db.SalesInvoices
            .Where(i => i.CompanyId == companyId
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.InvoiceDate >= prevMonthStart
                        && i.InvoiceDate <= prevMonthEnd);
        var prevTotal = await prevSalesQuery.SumGrandTotalAsync(ct);
        if (prevTotal > 0)
        {
            var changePct = Math.Round((salesTotal - prevTotal) / prevTotal * 100m, 1, MidpointRounding.AwayFromZero);
            var sev = changePct <= -20m ? "Warning" : "Info";
            insights.Add(new FinancialInsight(
                "PERF_SALES_MOM",
                "Performance",
                sev,
                "تغير المبيعات عن الشهر السابق",
                $"النسبة التقريبية مقارنة بشهر {prevMonthStart:yyyy-MM}: {changePct:N1}% (الحالي {salesTotal:N2} ← السابق {prevTotal:N2}).",
                "Sales change vs previous month",
                $"Approximate change vs {prevMonthStart:yyyy-MM}: {changePct:N1}% (current {salesTotal:N2} ← previous {prevTotal:N2}).",
                changePct));
        }
    }

    private async Task AddLiquidityInsightsAsync(
        long companyId, DateOnly day, List<FinancialInsight> insights, CancellationToken ct)
    {
        // رصيد تقريبي للصندوق/البنوك من القيود المرحّلة (مدين − دائن) حتى التاريخ
        var liquidCodes = new[]
        {
            SaudiChartOfAccounts.SystemCodes.CashOnHand,
            SaudiChartOfAccounts.SystemCodes.CashAtBanks,
        };

        var liquidAccounts = await db.Accounts
            .Where(a => a.CompanyId == companyId && liquidCodes.Contains(a.Code))
            .Select(a => a.Id)
            .ToListAsync(ct);

        decimal cashApprox = 0;
        if (liquidAccounts.Count > 0)
        {
            var lines = await db.JournalEntries
                .Where(e => e.CompanyId == companyId
                            && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                            && e.EntryDate <= day)
                .SelectMany(e => e.Lines)
                .Where(l => liquidAccounts.Contains(l.AccountId))
                .Select(l => new { l.Debit, l.Credit })
                .ToListAsync(ct);
            cashApprox = lines.Sum(l => l.Debit - l.Credit);
        }

        var arCode = SaudiChartOfAccounts.SystemCodes.AccountsReceivable;
        var apCode = SaudiChartOfAccounts.SystemCodes.AccountsPayable;
        var arId = await db.Accounts.Where(a => a.CompanyId == companyId && a.Code == arCode)
            .Select(a => (long?)a.Id).FirstOrDefaultAsync(ct);
        var apId = await db.Accounts.Where(a => a.CompanyId == companyId && a.Code == apCode)
            .Select(a => (long?)a.Id).FirstOrDefaultAsync(ct);

        decimal ar = 0, ap = 0;
        if (arId is long arid)
        {
            var rows = await db.JournalEntries.Where(e => e.CompanyId == companyId && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed) && e.EntryDate <= day)
                .SelectMany(e => e.Lines).Where(l => l.AccountId == arid)
                .Select(l => new { l.Debit, l.Credit }).ToListAsync(ct);
            ar = rows.Sum(l => l.Debit - l.Credit);
        }
        if (apId is long apid)
        {
            var rows = await db.JournalEntries.Where(e => e.CompanyId == companyId && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed) && e.EntryDate <= day)
                .SelectMany(e => e.Lines).Where(l => l.AccountId == apid)
                .Select(l => new { l.Debit, l.Credit }).ToListAsync(ct);
            ap = rows.Sum(l => l.Credit - l.Debit);
        }

        insights.Add(new FinancialInsight(
            "LIQ_CASH_PROXY",
            "Liquidity",
            cashApprox < 0 ? "Warning" : "Info",
            "تقدير السيولة النقدية (صندوق+بنوك)",
            $"رصيد تقريبي من دفتر الأستاذ حتى {day:yyyy-MM-dd}: {cashApprox:N2}. ذمم مدينة ≈ {ar:N2} · دائنة ≈ {ap:N2}.",
            "Cash liquidity estimate (till + banks)",
            $"Approximate ledger balance as of {day:yyyy-MM-dd}: {cashApprox:N2}. Receivables ≈ {ar:N2} · payables ≈ {ap:N2}.",
            cashApprox));

        if (ap > 0 && cashApprox < ap * 0.25m)
        {
            insights.Add(new FinancialInsight(
                "LIQ_TIGHT",
                "Liquidity",
                "Warning",
                "ضغط سيولة محتمل",
                $"النقد التقديري أقل من 25% من الذمم الدائنة التقريبية ({cashApprox:N2} مقابل دائن {ap:N2}). راجع التحصيل والالتزامات.",
                "Possible liquidity pressure",
                $"Estimated cash is below 25% of approximate payables ({cashApprox:N2} against payables {ap:N2}). Review collections and obligations.",
                cashApprox / ap));
        }
    }

    private async Task AddAnomalyInsightsAsync(
        long companyId, DateOnly day, List<FinancialInsight> insights, CancellationToken ct)
    {
        var from = day.AddDays(-90);
        var entries = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                        && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                        && e.EntryDate >= from
                        && e.EntryDate <= day)
            .ToListAsync(ct);

        if (entries.Count == 0) return;

        var amounts = entries.Select(e => e.TotalDebit).Where(a => a > 0).ToList();
        if (amounts.Count < 5) return;

        var avg = amounts.Average();
        var threshold = avg * 5m;
        var outliers = entries.Where(e => e.TotalDebit >= threshold && e.TotalDebit >= 10_000m).Take(5).ToList();
        foreach (var o in outliers)
        {
            insights.Add(new FinancialInsight(
                "ANOM_LARGE_JE",
                "Anomaly",
                "Warning",
                "قيد بحجم شاذ نسبياً",
                $"القيد {o.EntryNumber} بتاريخ {o.EntryDate:yyyy-MM-dd} بمبلغ {o.TotalDebit:N2} (≥ 5× متوسط آخر 90 يوماً ≈ {avg:N2}). راجع المستند المصدر: {o.SourceDocument ?? "يدوي"}.",
                "Relatively outsized journal entry",
                $"Entry {o.EntryNumber} dated {o.EntryDate:yyyy-MM-dd} for {o.TotalDebit:N2} (≥ 5× the 90-day average ≈ {avg:N2}). Review the source document: {o.SourceDocument ?? "manual"}.",
                o.TotalDebit));
        }

        var roundHeavy = entries.Count(e => e.TotalDebit >= 1000m && e.TotalDebit % 1000m == 0);
        if (roundHeavy >= 10 && roundHeavy >= entries.Count * 0.4m)
        {
            insights.Add(new FinancialInsight(
                "ANOM_ROUND_AMOUNTS",
                "Anomaly",
                "Info",
                "كثافة مبالغ مستديرة",
                $"{roundHeavy} من {entries.Count} قيود (≥1000) مبالغها من مضاعفات 1000 — قد يكون طبيعياً أو يستحق مراجعة عيّنة.",
                "High density of round amounts",
                $"{roundHeavy} of {entries.Count} entries (≥1000) are multiples of 1000 — may be normal, or worth sampling for review.",
                roundHeavy));
        }
    }

    private async Task AddFraudRiskInsightsAsync(
        long companyId, DateOnly day, List<FinancialInsight> insights, CancellationToken ct)
    {
        var from = day.AddDays(-30);
        var posted = await db.SalesInvoices.CountAsync(i =>
            i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Posted
            && i.InvoiceDate >= from && i.InvoiceDate <= day, ct);
        var cancelled = await db.SalesInvoices.CountAsync(i =>
            i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Cancelled
            && i.InvoiceDate >= from && i.InvoiceDate <= day, ct);

        var denom = posted + cancelled;
        if (denom >= 5)
        {
            var rate = Math.Round((decimal)cancelled / denom * 100m, 1, MidpointRounding.AwayFromZero);
            if (rate >= 15m)
            {
                insights.Add(new FinancialInsight(
                    "FRAUD_CANCEL_RATE",
                    "FraudRisk",
                    "Warning",
                    "نسبة إلغاء فواتير مرتفعة",
                    $"خلال 30 يوماً: ملغى {cancelled} من أصل {denom} ({rate:N1}%). راقب صلاحيات الإلغاء ومبررات العمليات.",
                    "High invoice cancellation rate",
                    $"Over 30 days: {cancelled} cancelled out of {denom} ({rate:N1}%). Monitor cancellation permissions and operation justifications.",
                    rate));
            }
        }
    }

    private async Task AddDataQualityBridgeAsync(
        long companyId, List<FinancialInsight> insights, CancellationToken ct)
    {
        var dq = await dataQuality.RunAsync(companyId, ct);
        foreach (var f in dq.Findings.Where(x =>
                     x.Severity is "Error" or "Warning"
                     && x.Code is not "DOMAIN_EVENTS_24H"))
        {
            insights.Add(new FinancialInsight(
                "DQ_" + f.Code,
                "Risk",
                f.Severity,
                "تنبيه جودة بيانات",
                f.MessageAr,
                "Data quality alert",
                f.MessageEn));
        }
    }

    private async Task AddClassificationHintsAsync(
        long companyId, DateOnly day, List<FinancialInsight> insights, CancellationToken ct)
    {
        var recent = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                        && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                        && e.EntryDate >= day.AddDays(-14)
                        && e.EntryDate <= day
                        && e.SourceDocument == null)
            .OrderByDescending(e => e.Id)
            .Take(20)
            .ToListAsync(ct);

        foreach (var e in recent)
        {
            var hint = SuggestAccountFromDescription(e.Description);
            if (hint is null) continue;
            insights.Add(new FinancialInsight(
                "CLASSIFY_HINT",
                "DecisionSupport",
                "Info",
                "اقتراح تصنيف قيد يدوي",
                $"القيد {e.EntryNumber}: «{e.Description}» — اقتراح قاعدي: حساب {hint}.",
                "Manual entry classification hint",
                $"Entry {e.EntryNumber}: «{e.Description}» — rule-based suggestion: account {hint}.",
                null));
        }
    }

    /// <summary>قاموس كلمات بسيط — قابل للتوسع، شفاف، بدون تعلم آلي.</summary>
    internal static string? SuggestAccountFromDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var d = description.Trim();

        if (ContainsAny(d, "راتب", "رواتب", "أجور", "payroll", "salary"))
            return $"{SaudiChartOfAccounts.SystemCodes.SalariesExpense} (رواتب)";
        if (ContainsAny(d, "إيجار", "rent"))
            return "5204 (إيجارات)";
        if (ContainsAny(d, "كهرباء", "ماء", "اتصالات", "utilities"))
            return "5205 (مرافق)";
        if (ContainsAny(d, "مبيعات", "sales"))
            return $"{SaudiChartOfAccounts.SystemCodes.SalesRevenue} (إيراد مبيعات)";
        if (ContainsAny(d, "شراء", "مشتريات", "purchase"))
            return "5102 (مشتريات)";
        if (ContainsAny(d, "بنك", "عمولة", "bank"))
            return "5213 (رسوم بنكية)";
        return null;
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase));
}
