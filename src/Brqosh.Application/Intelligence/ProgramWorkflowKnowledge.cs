namespace Brqosh.Application.Intelligence;

/// <summary>مسارات عمل البرنامج — تُدمج في كل رد عند تطابق السؤال.</summary>
public static class ProgramWorkflowKnowledge
{
    public sealed record WorkflowHint(
        string Id,
        string TitleAr,
        string TitleEn,
        string StepsAr,
        string StepsEn,
        string? RelatedNavKey);

    private static readonly WorkflowHint[] Workflows =
    [
        new("sales-cycle", "دورة المبيعات", "Sales cycle",
            "1) العملاء → بيانات العميل · 2) عرض سعر/أمر بيع (اختياري) · 3) فاتورة مبيعات → سطور → ترحيل · 4) سند قبض للتحصيل · 5) ZATCA للفاتورة المرحّلة",
            "1) Customers master · 2) Quotation/SO (optional) · 3) Sales invoice → lines → Post · 4) Receipt voucher · 5) ZATCA for posted invoice",
            "sales_invoices"),
        new("purchase-cycle", "دورة المشتريات", "Purchase cycle",
            "1) الموردون · 2) طلب شراء → أمر شراء · 3) إذن استلام · 4) فاتورة مشتريات → ترحيل · 5) سند صرف",
            "1) Suppliers · 2) PR → PO · 3) Goods receipt · 4) Purchase invoice → Post · 5) Payment voucher",
            "purchase_invoices"),
        new("treasury", "الخزينة", "Treasury",
            "قبض يخفّض ذمم العملاء · صرف يخفّض ذمم الموردين · التسوية البنكية تطابق كشف البنك مع القيود",
            "Receipts reduce AR · Payments reduce AP · Bank reconciliation matches statement to entries",
            "bank_reconciliation"),
        new("construction", "مقاولات", "Construction",
            "مشروع → BOQ → عقد → مستخلصات → محتجزات → ربحية المشروع. كل مستند مرحّل يولّد قيوداً.",
            "Project → BOQ → contract → progress billings → retentions → profitability. Posted docs generate entries.",
            "projects"),
        new("manufacturing", "تصنيع", "Manufacturing",
            "BOM → أمر إنتاج (مسودة ← صادر بوصفةٍ مجمَّدة) → الإكمال: صرف الخامات وتحميل الأجر والتكاليف وإدخال التام بتكلفته في قيدٍ واحد — حساب الإنتاج تحت التشغيل يُحمَّل ويُصفَّر معاً",
            "BOM → work order (draft → released, recipe frozen) → completion: issue materials, load labour and overhead, receive finished goods at cost in one step — the production WIP account is loaded and cleared together",
            "work_orders"),
        new("pos", "نقطة البيع", "POS",
            "افتح وردية → شاشة البيع → بِع → أغلق الوردية. الفاتورة تُرحّل تلقائياً.",
            "Open shift → POS session → sell → close shift. Invoice posts automatically.",
            "pos_session"),
        new("month-end", "إقفال الشهر", "Month-end close",
            "ميزان مراجعة متوازن → قائمة دخل · مركز مالي · VAT · إقفال الفترة (لا ترحيل في فترة مغلقة)",
            "Balanced trial balance → P&L · balance sheet · VAT · period close (no posting in closed period)",
            "fiscal_periods"),
        new("zatca-flow", "ZATCA", "ZATCA flow",
            "إعدادات ZATCA → CSID → فاتورة مرحّلة → XML/QR. OTP حي ⏭ O-01",
            "ZATCA settings → CSID → posted invoice → XML/QR. Live OTP ⏭ O-01",
            "zatca_settings"),
        new("inventory", "المخزون", "Inventory",
            "أصناف · مستودعات · حركات · جرد · تكلفة إضافية. الفواتير المرحّلة تحرّك المخزون.",
            "Products · warehouses · movements · count · landed cost. Posted invoices move stock.",
            "stock_movements"),
        new("crm", "CRM", "CRM",
            "عميل محتمل → فرصة → أنشطة → تحويل لعميل → فاتورة مبيعات",
            "Lead → opportunity → activities → convert to customer → sales invoice",
            "crm_leads"),
        new("reporting", "التقارير", "Reporting",
            "اختر الفترة · شغّل التقرير · تحقق من الملخص. التقارير تقرأ القيود المرحّلة فقط.",
            "Pick period · run report · verify summary. Reports read posted entries only.",
            "trial_balance"),
    ];

    private static readonly (string[] Keys, string WorkflowId)[] Triggers =
    [
        (["فاتورة مبيع", "فاتورة بيع", "sales invoice", "أبيع", "بيع لعميل"], "sales-cycle"),
        (["فاتورة مشتري", "شراء", "مورد", "purchase", "أشتري"], "purchase-cycle"),
        (["قبض", "صرف", "خزينة", "بنك", "شيك", "treasury", "receipt", "payment voucher"], "treasury"),
        (["مشروع", "مقاول", "boq", "مستخلص", "construction", "عقد"], "construction"),
        (["إنتاج", "bom", "أمر إنتاج", "تصنيع", "work order", "wip"], "manufacturing"),
        (["pos", "نقطة بيع", "وردية", "كاشير"], "pos"),
        (["إقفال", "شهر", "فترة", "close", "month end"], "month-end"),
        (["زاتكا", "zatca", "csid", "فاتورة إلكترون", "e-invoice"], "zatca-flow"),
        (["مخزون", "جرد", "مستودع", "inventory", "stock"], "inventory"),
        (["crm", "عميل محتمل", "فرصة", "lead"], "crm"),
        (["تقرير", "ميزان", "قائمة دخل", "report", "trial balance"], "reporting"),
    ];

    public static IReadOnlyList<WorkflowHint> Match(string question, int max = 2)
    {
        if (string.IsNullOrWhiteSpace(question))
            return [];

        var lower = question.ToLowerInvariant();
        var hits = new List<(int Score, WorkflowHint Hint)>();

        foreach (var wf in Workflows)
        {
            var score = ScoreWorkflow(wf, lower, question);
            if (score > 0)
                hits.Add((score, wf));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .Take(max)
            .Select(h => h.Hint)
            .ToList();
    }

    private static int ScoreWorkflow(WorkflowHint wf, string lower, string original)
    {
        var trigger = Triggers.FirstOrDefault(t => t.WorkflowId == wf.Id);
        if (trigger.Keys.Length == 0)
            return 0;

        var score = 0;
        foreach (var key in trigger.Keys)
        {
            if (lower.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                original.Contains(key, StringComparison.OrdinalIgnoreCase))
                score += key.Length > 4 ? 3 : 1;
        }

        if (wf.RelatedNavKey is not null &&
            ProgramNavCatalog.TryGet(wf.RelatedNavKey, out var nav))
        {
            if (lower.Contains(nav.TitleAr, StringComparison.OrdinalIgnoreCase))
                score += 5;
        }

        return score;
    }
}
