using Brqosh.Application.Intelligence;
using Brqosh.Domain.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Intelligence;

/// <summary>
/// مساعد مالي تفاعلي قائم على قواعد وKPIs — يجيب عن أسئلة شائعة بدون نماذج لغوية خارجية.
/// </summary>
public class FinancialAssistantService(
    BrqoshDbContext db,
    IFinancialIntelligenceService intelligence) : IFinancialAssistantService
{
    public async Task<FinancialAssistantReply> AskAsync(
        long companyId, string question, CancellationToken ct = default)
    {
        var q = question.Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            return new FinancialAssistantReply(
                "اكتب سؤالك عن التدفق النقدي، الضريبة، أو الذمم.",
                "Type a question about cash flow, VAT, or receivables.",
                FinancialIntelligenceService.EngineId);
        }

        var kpi = await new DashboardQuery(db).ExecuteAsync(companyId);
        var report = await intelligence.AnalyzeAsync(companyId, ct: ct);
        var topic = ClassifyTopic(q);

        // ‏(أسئلةُ المالك 2026-10-24) ‏الرمزُ بجوار الأرقام من عملة دفاتر المنشأة لا ريالٌ مكتوب.
        var code = await db.Companies.Where(c => c.Id == companyId).Select(c => c.FunctionalCurrencyCode).FirstOrDefaultAsync(ct);
        var cur = new Cur(CurrencyCatalog.Short(code, english: false), CurrencyCatalog.Short(code, english: true));

        return topic switch
        {
            AssistantTopic.CashFlow => CashFlowReply(kpi, cur),
            AssistantTopic.Vat => VatReply(kpi, report, cur),
            AssistantTopic.Receivables => ReceivablesReply(kpi, report, cur),
            AssistantTopic.Payables => PayablesReply(kpi, report, cur),
            AssistantTopic.Sales => SalesReply(kpi, report, cur),
            AssistantTopic.Inventory => InventoryReply(kpi, cur),
            AssistantTopic.Anomaly => FilteredReply(report, "Anomaly", "FraudRisk"),
            AssistantTopic.Help => HelpReply(),
            _ => SummaryReply(kpi, report, cur),
        };
    }

    private static AssistantTopic ClassifyTopic(string q)
    {
        var lower = q.ToLowerInvariant();
        if (ContainsAny(lower, q, "تدفق", "نقد", "سيولة", "cash", "liquidity", "تحصيل", "سداد"))
            return AssistantTopic.CashFlow;
        if (ContainsAny(lower, q, "ضريبة", "vat", "زاتكا", "zatca", "إقرار"))
            return AssistantTopic.Vat;
        if (ContainsAny(lower, q, "عملاء", "مدينة", "receivable", "ar", "ذمم"))
            return AssistantTopic.Receivables;
        if (ContainsAny(lower, q, "مورد", "دائنة", "payable", "ap"))
            return AssistantTopic.Payables;
        if (ContainsAny(lower, q, "مبيعات", "إيراد", "sales", "revenue"))
            return AssistantTopic.Sales;
        if (ContainsAny(lower, q, "مخزون", "inventory", "stock"))
            return AssistantTopic.Inventory;
        if (ContainsAny(lower, q, "شذوذ", "احتيال", "anomaly", "fraud", "تنبيه"))
            return AssistantTopic.Anomaly;
        if (ContainsAny(lower, q, "مساعدة", "help", "ماذا", "what can"))
            return AssistantTopic.Help;
        return AssistantTopic.General;
    }

    private static bool ContainsAny(string lower, string original, params string[] needles) =>
        needles.Any(n => lower.Contains(n, StringComparison.OrdinalIgnoreCase)
                         || original.Contains(n, StringComparison.Ordinal));

    private static FinancialAssistantReply CashFlowReply(DashboardKpis k, Cur cur)
    {
        var net = k.CollectedThisMonth - k.PaidThisMonth;
        return new FinancialAssistantReply(
            $"التدفق النقدي هذا الشهر: تحصيلات {k.CollectedThisMonth:N2} {cur.Ar} · مدفوعات {k.PaidThisMonth:N2} {cur.Ar} · صافي {net:N2} {cur.Ar}.",
            $"Cash flow this month: collections {cur.En} {k.CollectedThisMonth:N2} · payments {cur.En} {k.PaidThisMonth:N2} · net {cur.En} {net:N2}.",
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply VatReply(DashboardKpis k, FinancialIntelligenceReport report, Cur cur)
    {
        var extra = report.Insights
            .Where(i => i.Category.Contains("tax", StringComparison.OrdinalIgnoreCase)
                        || i.TitleAr.Contains("ضريبة", StringComparison.Ordinal))
            .Take(2)
            .ToList();
        var tailAr = extra.Count > 0 ? " " + string.Join(" ", extra.Select(i => i.DetailAr)) : "";
        var tailEn = extra.Count > 0 ? " " + string.Join(" ", extra.Select(i => i.DetailEn)) : "";
        return new FinancialAssistantReply(
            $"صافي ضريبة الشهر التقريبي: {k.NetVatThisMonth:N2} {cur.Ar}.{tailAr}",
            $"Estimated net VAT this month: {cur.En} {k.NetVatThisMonth:N2}.{tailEn}",
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply ReceivablesReply(DashboardKpis k, FinancialIntelligenceReport report, Cur cur)
    {
        var liq = report.Insights.FirstOrDefault(i => i.Code.StartsWith("LIQ_", StringComparison.Ordinal));
        var detailAr = liq?.DetailAr ?? "راجع كشف أعمار الديون من تقارير العملاء.";
        var detailEn = liq?.DetailEn ?? "Review the AR ageing report under customer reports.";
        return new FinancialAssistantReply(
            $"ذمم العملاء التقريبية: {k.Receivables:N2} {cur.Ar}. {detailAr}",
            $"Approximate AR: {cur.En} {k.Receivables:N2}. {detailEn}",
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply PayablesReply(DashboardKpis k, FinancialIntelligenceReport report, Cur cur)
    {
        var liq = report.Insights.FirstOrDefault(i => i.Code == "LIQ_TIGHT" || i.Code == "LIQ_CASH_PROXY");
        var detailAr = liq?.DetailAr ?? "راجع استحقاقات الموردين قبل مواعيد السداد.";
        var detailEn = liq?.DetailEn ?? "Review supplier obligations ahead of their due dates.";
        return new FinancialAssistantReply(
            $"ذمم الموردين التقريبية: {k.Payables:N2} {cur.Ar}. {detailAr}",
            $"Approximate AP: {cur.En} {k.Payables:N2}. {detailEn}",
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply SalesReply(DashboardKpis k, FinancialIntelligenceReport report, Cur cur)
    {
        var perf = report.Insights.FirstOrDefault(i => i.Code.StartsWith("PERF_SALES", StringComparison.Ordinal));
        return new FinancialAssistantReply(
            perf?.DetailAr ?? $"مبيعات الشهر المرحّلة: {k.SalesThisMonth:N2} {cur.Ar}.",
            perf?.DetailEn ?? $"Posted sales this month: {cur.En} {k.SalesThisMonth:N2}.",
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply InventoryReply(DashboardKpis k, Cur cur) =>
        new(
            $"قيمة المخزون التقريبية: {k.InventoryValue:N2} {cur.Ar} — راجع بطء الحركة والفروقات الدورية.",
            $"Approximate inventory value: {cur.En} {k.InventoryValue:N2} — review slow movers and cycle counts.",
            FinancialIntelligenceService.EngineId);

    private static FinancialAssistantReply FilteredReply(
        FinancialIntelligenceReport report, params string[] categories)
    {
        var hits = report.Insights
            .Where(i => categories.Any(c => i.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
                        || i.Severity is "Warning" or "Error")
            .Take(4)
            .ToList();
        if (hits.Count == 0)
        {
            return new FinancialAssistantReply(
                "لا تنبيهات حرجة في المحرك القاعدي حالياً.",
                "No critical flags from the rules engine right now.",
                FinancialIntelligenceService.EngineId);
        }

        return new FinancialAssistantReply(
            string.Join(" · ", hits.Select(i => $"{i.TitleAr}: {i.DetailAr}")),
            string.Join(" · ", hits.Select(i => $"{i.TitleEn}: {i.DetailEn}")),
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply SummaryReply(DashboardKpis k, FinancialIntelligenceReport report, Cur cur)
    {
        var top = report.Insights.Take(3).ToList();
        var bodyAr = top.Count > 0
            ? string.Join(" · ", top.Select(i => $"{i.TitleAr}: {i.DetailAr}"))
            : "لا رؤى إضافية.";
        var bodyEn = top.Count > 0
            ? string.Join(" · ", top.Select(i => $"{i.TitleEn}: {i.DetailEn}"))
            : "No further insights.";
        return new FinancialAssistantReply(
            $"{SummaryPrefixAr(k, cur)} {bodyAr}",
            $"{SummaryPrefixEn(k, cur)} {bodyEn}",
            FinancialIntelligenceService.EngineId);
    }

    private static string SummaryPrefixAr(DashboardKpis k, Cur cur) =>
        $"مبيعات {k.SalesThisMonth:N0} · صافي ضريبة {k.NetVatThisMonth:N0} · ذمم {k.Receivables:N0} {cur.Ar}.";

    private static string SummaryPrefixEn(DashboardKpis k, Cur cur) =>
        $"Sales {k.SalesThisMonth:N0} · net VAT {k.NetVatThisMonth:N0} · receivables {k.Receivables:N0} {cur.En}.";

    private static FinancialAssistantReply HelpReply() =>
        new(
            "يمكنك السؤال عن: التدفق النقدي، VAT، ذمم العملاء/الموردين، المبيعات، المخزون، أو التنبيهات.",
            "Ask about: cash flow, VAT, AR/AP, sales, inventory, or risk alerts.",
            FinancialIntelligenceService.EngineId);

    private sealed record Cur(string Ar, string En);

    private enum AssistantTopic
    {
        General,
        CashFlow,
        Vat,
        Receivables,
        Payables,
        Sales,
        Inventory,
        Anomaly,
        Help,
    }
}
