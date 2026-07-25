namespace Brqosh.Application.Intelligence;

/// <summary>يستنتج الشاشة الأنسب من نص السؤال — يغطي كل مفاتيح <see cref="ProgramNavCatalog"/>.</summary>
public static class ProgramQuestionMatcher
{
    private static readonly (string NavKey, string[] Synonyms)[] ManualIndex =
    [
        ("sales_invoices", ["فاتورة مبيع", "فاتورة بيع", "sales invoice", "invoice"]),
        ("purchase_invoices", ["فاتورة مشتري", "فاتورة شراء", "purchase invoice"]),
        ("journal_entries", ["قيد", "journal", "قيد يومي"]),
        ("customers", ["عميل", "عملاء", "customer"]),
        ("suppliers", ["مورد", "موردين", "supplier"]),
        ("products", ["صنف", "منتج", "أصناف", "product"]),
        ("receipt_vouchers", ["سند قبض", "تحصيل", "receipt"]),
        ("payment_vouchers", ["سند صرف", "سداد", "payment voucher"]),
        ("projects", ["مشروع", "مشاريع", "project"]),
        ("boq", ["boq", "جدول كميات", "كميات"]),
        ("contracts", ["عقد", "عقود", "contract"]),
        ("progress_billings", ["مستخلص", "مستخلصات", "progress billing"]),
        ("subcontractors", ["مقاول باطن", "subcontractor"]),
        ("retentions", ["محتجز", "محتجزات", "retention"]),
        ("pos_session", ["pos", "نقطة بيع", "كاشير", "شاشة البيع"]),
        ("pos_shifts", ["وردية", "ورديات", "shift"]),
        ("zatca_settings", ["zatca", "زاتكا", "csid", "otp"]),
        ("trial_balance", ["ميزان", "trial balance"]),
        ("bank_reconciliation", ["تسوية بنك", "bank recon"]),
        ("work_orders", ["أمر إنتاج", "work order"]),
        ("purchase_orders", ["أمر شراء", "purchase order"]),
        ("goods_receipts", ["إذن استلام", "استلام بضاعة", "goods receipt"]),
        ("crm_leads", ["عميل محتمل", "lead", "crm"]),
        ("company_settings", ["إعدادات", "settings", "ملف عمل"]),
        ("users", ["مستخدم", "صلاحية", "user", "role"]),
        ("fixed_assets", ["أصل ثابت", "أصول ثابتة", "fixed asset"]),
        ("payroll", ["رواتب", "راتب", "payroll"]),
        ("audit_log", ["سجل تدقيق", "audit log"]),
        ("general_ledger_report", ["دفتر أستاذ", "general ledger"]),
    ];

    private static readonly Lazy<(string NavKey, string[] Synonyms)[]> FullIndex = new(BuildFullIndex);

    public static IReadOnlyCollection<string> AllKeys() => ProgramNavCatalog.Keys;

    public static string? GuessNavKey(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var lower = question.ToLowerInvariant();
        string? bestKey = null;
        var bestScore = 0;

        foreach (var (navKey, synonyms) in FullIndex.Value)
        {
            var score = 0;
            foreach (var syn in synonyms)
            {
                if (syn.Length < 2)
                    continue;

                if (lower.Contains(syn, StringComparison.OrdinalIgnoreCase) ||
                    question.Contains(syn, StringComparison.OrdinalIgnoreCase))
                    score += syn.Length;
            }

            if (ProgramNavCatalog.TryGet(navKey, out var nav))
            {
                if (lower.Contains(nav.TitleAr, StringComparison.OrdinalIgnoreCase) ||
                    lower.Contains(nav.TitleEn, StringComparison.OrdinalIgnoreCase))
                    score += 12;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestKey = navKey;
            }
        }

        return bestScore >= 3 ? bestKey : null;
    }

    private static (string NavKey, string[] Synonyms)[] BuildFullIndex()
    {
        var byKey = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (navKey, synonyms) in ManualIndex)
        {
            if (!byKey.TryGetValue(navKey, out var set))
                byKey[navKey] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var syn in synonyms)
                set.Add(syn);
        }

        foreach (var key in ProgramNavCatalog.Keys)
        {
            if (!ProgramNavCatalog.TryGet(key, out var nav))
                continue;

            if (!byKey.TryGetValue(key, out var set))
                byKey[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            set.Add(key.Replace('_', ' '));
            AddTitleTokens(set, nav.TitleAr);
            AddTitleTokens(set, nav.TitleEn);
        }

        return byKey
            .Select(kv => (kv.Key, kv.Value.Where(s => s.Length >= 2).ToArray()))
            .ToArray();
    }

    private static void AddTitleTokens(ISet<string> set, string title)
    {
        foreach (var token in title.Split([' ', '·', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            if (t.Length >= 2)
                set.Add(t);
        }
    }
}
