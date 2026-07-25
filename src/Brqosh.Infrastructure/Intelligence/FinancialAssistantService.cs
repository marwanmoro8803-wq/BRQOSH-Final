using Brqosh.Application.Intelligence;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;

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

        return topic switch
        {
            AssistantTopic.CashFlow => CashFlowReply(kpi),
            AssistantTopic.Vat => VatReply(kpi, report),
            AssistantTopic.Receivables => ReceivablesReply(kpi, report),
            AssistantTopic.Payables => PayablesReply(kpi, report),
            AssistantTopic.Sales => SalesReply(kpi, report),
            AssistantTopic.Inventory => InventoryReply(kpi),
            AssistantTopic.Anomaly => FilteredReply(report, "Anomaly", "FraudRisk"),
            AssistantTopic.Help => HelpReply(),
            _ => SummaryReply(kpi, report),
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

    private static FinancialAssistantReply CashFlowReply(DashboardKpis k)
    {
        var net = k.CollectedThisMonth - k.PaidThisMonth;
        return new FinancialAssistantReply(
            $"التدفق النقدي هذا الشهر: تحصيلات {k.CollectedThisMonth:N2} ر.س · مدفوعات {k.PaidThisMonth:N2} ر.س · صافي {net:N2} ر.س.",
            $"Cash flow this month: collections SAR {k.CollectedThisMonth:N2} · payments SAR {k.PaidThisMonth:N2} · net SAR {net:N2}.",
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply VatReply(DashboardKpis k, FinancialIntelligenceReport report)
    {
        var extra = report.Insights
            .Where(i => i.Category.Contains("tax", StringComparison.OrdinalIgnoreCase)
                        || i.TitleAr.Contains("ضريبة", StringComparison.Ordinal))
            .Take(2)
            .Select(i => i.DetailAr)
            .ToList();
        var tail = extra.Count > 0 ? " " + string.Join(" ", extra) : "";
        return new FinancialAssistantReply(
            $"صافي ضريبة الشهر التقريبي: {k.NetVatThisMonth:N2} ر.س.{tail}",
            $"Estimated net VAT this month: SAR {k.NetVatThisMonth:N2}.{tail}",
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply ReceivablesReply(DashboardKpis k, FinancialIntelligenceReport report)
    {
        var liq = report.Insights.FirstOrDefault(i => i.Code.StartsWith("LIQ_", StringComparison.Ordinal));
        var detail = liq?.DetailAr ?? "راجع كشف أعمار الديون من تقارير العملاء.";
        return new FinancialAssistantReply(
            $"ذمم العملاء التقريبية: {k.Receivables:N2} ر.س. {detail}",
            $"Approximate AR: SAR {k.Receivables:N2}. {detail}",
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply PayablesReply(DashboardKpis k, FinancialIntelligenceReport report)
    {
        var liq = report.Insights.FirstOrDefault(i => i.Code == "LIQ_TIGHT" || i.Code == "LIQ_CASH_PROXY");
        var detail = liq?.DetailAr ?? "راجع استحقاقات الموردين قبل مواعيد السداد.";
        return new FinancialAssistantReply(
            $"ذمم الموردين التقريبية: {k.Payables:N2} ر.س. {detail}",
            $"Approximate AP: SAR {k.Payables:N2}. {detail}",
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply SalesReply(DashboardKpis k, FinancialIntelligenceReport report)
    {
        var perf = report.Insights.FirstOrDefault(i => i.Code.StartsWith("PERF_SALES", StringComparison.Ordinal));
        var detail = perf?.DetailAr ?? $"مبيعات الشهر المرحّلة: {k.SalesThisMonth:N2} ر.س.";
        return new FinancialAssistantReply(
            detail,
            detail,
            FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply InventoryReply(DashboardKpis k) =>
        new(
            $"قيمة المخزون التقريبية: {k.InventoryValue:N2} ر.س — راجع بطء الحركة والفروقات الدورية.",
            $"Approximate inventory value: SAR {k.InventoryValue:N2} — review slow movers and cycle counts.",
            FinancialIntelligenceService.EngineId);

    private static FinancialAssistantReply FilteredReply(
        FinancialIntelligenceReport report, params string[] categories)
    {
        var hits = report.Insights
            .Where(i => categories.Any(c => i.Category.Contains(c, StringComparison.OrdinalIgnoreCase))
                        || i.Severity is "Warning" or "Error")
            .Take(4)
            .Select(i => $"{i.TitleAr}: {i.DetailAr}")
            .ToList();
        if (hits.Count == 0)
        {
            return new FinancialAssistantReply(
                "لا تنبيهات حرجة في المحرك القاعدي حالياً.",
                "No critical flags from the rules engine right now.",
                FinancialIntelligenceService.EngineId);
        }

        var joined = string.Join(" · ", hits);
        return new FinancialAssistantReply(joined, joined, FinancialIntelligenceService.EngineId);
    }

    private static FinancialAssistantReply SummaryReply(DashboardKpis k, FinancialIntelligenceReport report)
    {
        var top = report.Insights.Take(3).Select(i => $"{i.TitleAr}: {i.DetailAr}").ToList();
        var prefix = LocaleSummary(k);
        var body = top.Count > 0 ? string.Join(" · ", top) : "لا رؤى إضافية.";
        return new FinancialAssistantReply(
            $"{prefix} {body}",
            $"{prefix} {body}",
            FinancialIntelligenceService.EngineId);
    }

    private static string LocaleSummary(DashboardKpis k) =>
        $"مبيعات {k.SalesThisMonth:N0} · صافي ضريبة {k.NetVatThisMonth:N0} · ذمم {k.Receivables:N0} ر.س.";

    private static FinancialAssistantReply HelpReply() =>
        new(
            "يمكنك السؤال عن: التدفق النقدي، VAT، ذمم العملاء/الموردين، المبيعات، المخزون، أو التنبيهات.",
            "Ask about: cash flow, VAT, AR/AP, sales, inventory, or risk alerts.",
            FinancialIntelligenceService.EngineId);

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
