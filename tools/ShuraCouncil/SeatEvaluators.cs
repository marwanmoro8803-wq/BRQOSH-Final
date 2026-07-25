namespace ShuraCouncil;

internal static class SeatEvaluators
{
    public static IReadOnlyList<SeatVerdict> EvaluateAll(EvidenceBundle e) =>
    [
        EvaluateS01(e),
        EvaluateS02(e),
        EvaluateS03(e),
        EvaluateS04(e),
        EvaluateS05(e),
        EvaluateS06(e),
        EvaluateS07(e),
        EvaluateS08(e),
        EvaluateS09(e),
        EvaluateS10(e),
        EvaluateS11(e),
        EvaluateS12(e),
    ];

    private static double UiPassRate(EvidenceBundle e) =>
        e.UiAuditTotal > 0 ? (double)e.UiAuditPassed / e.UiAuditTotal : 0;

    private static SeatVerdict Build(
        string id,
        string role,
        EvidenceBundle e,
        IReadOnlyList<SeatFinding> findings,
        Func<EvidenceBundle, IReadOnlyDictionary<string, VoteKind>> votes,
        string summary) =>
        new(id, role, findings, votes(e), summary);

    private static SeatVerdict EvaluateS01(EvidenceBundle e)
    {
        var rate = UiPassRate(e);
        var findings = new List<SeatFinding>
        {
            new("S01-UI-RATE", rate >= 0.90, $"نسبة نجاح FlaUI: {rate:P1} ({e.UiAuditPassed}/{e.UiAuditTotal})"),
            new("S01-TESTS", e.TestsFailed == 0, $"اختبارات فاشلة: {e.TestsFailed} من {e.TestsTotal}"),
            new("S01-SCENARIO", e.FullScenarioPass, "FullScenarioAudit SUCCESS"),
        };
        return Build("S01", "المؤيد (Product Champion)", e, findings, VotesChampion,
            rate >= 0.9 && e.TestsFailed == 0
                ? "النواة التشغيلية قوية — جاهزون لتجريب عملاء"
                : "تحفظ — الأدلة لا تدعم حماساً كاملاً");
    }

    private static SeatVerdict EvaluateS02(EvidenceBundle e)
    {
        var findings = new List<SeatFinding>
        {
            new("S02-UI-FAIL", e.UiAuditFailed == 0, $"فشل FlaUI: {e.UiAuditFailed}"),
            new("S02-SEC-P0", e.SecurityP0 == 0, $"ثغرات P0: {e.SecurityP0}"),
            new("S02-MARKET-ZERO", e.MarketZeroCode < 20, $"فجوات zero-code: {e.MarketZeroCode}"),
        };
        return Build("S02", "المعارض (Skeptic)", e, findings, VotesSkeptic,
            "لا نُعلن اكتمالاً — دائماً فجوة قابلة للإثبات");
    }

    private static SeatVerdict EvaluateS03(EvidenceBundle e)
    {
        var findings = new List<SeatFinding>
        {
            new("S03-TESTS", e.TestsFailed == 0 && e.TestsTotal >= 400, $"tests {e.TestsTotal} failed {e.TestsFailed}"),
            new("S03-UI-LOG", e.UiAuditLogFound, "سجل FlaUI موجود"),
            new("S03-NIGHTLY", e.NightlyUiAuditScript, "run-ui-audit-nightly.ps1"),
            new("S03-SHURA", e.ShuraToolExists, "tools/ShuraCouncil"),
        };
        return Build("S03", "الناقد (Quality Auditor)", e, findings, VotesQuality,
            e.NightlyUiAuditScript && e.UiAuditLogFound
                ? "خط التحقق آلي — مقبول بشروط تغطية ملفات تعريف"
                : "فجوة في الأتمتة أو الأدلة");
    }

    private static SeatVerdict EvaluateS04(EvidenceBundle e)
    {
        var findings = new List<SeatFinding>
        {
            new("S04-ZERO", e.MarketZeroCode <= 35, $"G-items zero: {e.MarketZeroCode}"),
            new("S04-GTM", !e.G31Zero || !e.G32Zero, "G-31/G-32 ليست كلاهما ZERO"),
        };
        return Build("S04", "المفكر الاستراتيجي", e, findings, VotesStrategy,
            e.G31Zero && e.G32Zero
                ? "منصة تقنية بدون GTM — خطر استراتيجي"
                : "مسار منصة معقول إن أُغلق الأمن أولاً");
    }

    private static SeatVerdict EvaluateS05(EvidenceBundle e)
    {
        var apiKeyEnforced = CheckApiKeyEnforcement(e.RepoRoot);
        var findings = new List<SeatFinding>
        {
            new("S05-P0-COUNT", e.SecurityP0 == 0, $"P0 في CSV: {e.SecurityP0}"),
            new("S05-API-ENFORCE", apiKeyEnforced, "BRQOSH_API_KEY إلزامي في Production بالكود"),
        };
        return Build("S05", "أمن سيبراني", e, findings, VotesSecurity,
            e.SecurityP0 > 0 || !apiKeyEnforced
                ? "لا إطلاق قبل إغلاق P0 API"
                : "وضع أمني مقبول in-repo");
    }

    private static SeatVerdict EvaluateS06(EvidenceBundle e)
    {
        var uiAudit = File.Exists(Path.Combine(e.RepoRoot, "tools", "UiAudit", "Program.cs"));
        var navMap = File.Exists(Path.Combine(e.RepoRoot, "tools", "UiAudit", "NavKeyMap.cs"));
        var findings = new List<SeatFinding>
        {
            new("S06-UIAUDIT", uiAudit, "tools/UiAudit"),
            new("S06-NAVKEY", navMap, "NavKeyMap.cs"),
            new("S06-SHURA", e.ShuraToolExists, "tools/ShuraCouncil"),
        };
        return Build("S06", "مهندس برمجيات", e, findings, VotesEngineer,
            uiAudit && navMap && e.ShuraToolExists
                ? "أدوات تحقق حقيقية في المستودع"
                : "نقص في خط الأتمتة");
    }

    private static SeatVerdict EvaluateS07(EvidenceBundle e) =>
        Build("S07", "محلل سوق", e,
        [
            new("S07-G31", !e.G31Zero, "دعم عملاء"),
            new("S07-G32", !e.G32Zero, "تسعير/باقات"),
            new("S07-ZERO", e.MarketZeroCode < 40, $"zero-code {e.MarketZeroCode}"),
        ], VotesMarket,
            e.G31Zero || e.G32Zero
                ? "غير قابل للبيع كـ SaaS اليوم"
                : "جاهزية سوق جزئية");

    private static SeatVerdict EvaluateS08(EvidenceBundle e) =>
        Build("S08", "محاسب قانوني / SOCPA", e,
        [
            new("S08-SCENARIO", e.FullScenarioPass, "ميزان متوازن في FullScenario"),
            new("S08-TESTS", e.TestsFailed == 0, "اختبارات خضراء"),
        ], VotesAccounting,
            e.FullScenarioPass && e.TestsFailed == 0
                ? "سلامة محاسبية in-repo مثبتة"
                : "تحفظ محاسبي");

    private static SeatVerdict EvaluateS09(EvidenceBundle e) =>
        Build("S09", "اقتصادي / مالي", e,
        [
            new("S09-ROI-TOOLS", e.NightlyUiAuditScript, "أتمتة = ROI صيانة"),
            new("S09-MARKET", e.MarketZeroCode < 50, "فجوات سوق محدودة"),
        ], VotesEconomist,
            "أولوية ROI: أمن P0 ثم OTP ثم GTM");

    private static SeatVerdict EvaluateS10(EvidenceBundle e) =>
        Build("S10", "مدير شركة (CEO)", e,
        [
            new("S10-FOCUS", e.SecurityP0 <= 4, $"P0 backlog {e.SecurityP0}"),
            new("S10-LAUNCH", !(e.G31Zero && e.G32Zero), "GTM"),
        ], VotesCeo,
            "تركيز الفريق: P0 أمن + O-01 — لا تشتيت CRM كامل");

    private static SeatVerdict EvaluateS11(EvidenceBundle e) =>
        Build("S11", "محصّي ضرائب (ZATCA)", e,
        [
            new("S11-O01", !e.O01Open, "O-01 مغلق"),
            new("S11-UI-ZATCA", e.UiAuditLogFound, "فحص شاشات ZATCA في FlaUI"),
        ], VotesTax,
            e.O01Open
                ? "لا امتثال حي بدون OTP"
                : "in-repo جاهز — انتظر OTP حي");

    private static SeatVerdict EvaluateS12(EvidenceBundle e)
    {
        var falseClosure = e.TestsFailed > 0 || (e.SecurityP0 > 0 && UiPassRate(e) >= 0.99);
        return Build("S12", "مدقق مالي خارجي", e,
        [
            new("S12-4LEVEL", e.TestsFailed == 0, "اختبار"),
            new("S12-EXTERNAL", e.O01Open, "O-01 مفتوح = ⏭"),
            new("S12-NO-FALSE", !falseClosure, "لا إغلاق كاذب"),
        ], VotesAuditor,
            "أوقع على «تجريب in-repo» فقط — لا «إطلاق سوق»");
    }

    private static bool CheckApiKeyEnforcement(string repoRoot)
    {
        var program = Path.Combine(repoRoot, "src", "Brqosh.Api", "Program.cs");
        if (!File.Exists(program)) return false;
        var text = File.ReadAllText(program);
        return text.Contains("EnsureProductionApiKey", StringComparison.Ordinal)
               && text.Contains("SecretComparer", StringComparison.Ordinal);
    }

    // Vote semantics: Approve = agree with the decision text as stated.
    // D2 «رفض ادعاء إطلاق سوق» → Approve = نرفض ادعاء الإطلاق (لا نُطلق SaaS الآن).

    private static IReadOnlyDictionary<string, VoteKind> VotesChampion(EvidenceBundle e)
    {
        var rate = UiPassRate(e);
        return new Dictionary<string, VoteKind>
        {
            ["D1"] = rate >= 0.9 && e.TestsFailed == 0 ? VoteKind.Approve : VoteKind.Conditional,
            ["D2"] = VoteKind.Conditional,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Conditional,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Approve,
        };
    }

    private static IReadOnlyDictionary<string, VoteKind> VotesSkeptic(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = e.UiAuditFailed <= 15 && e.TestsFailed == 0 ? VoteKind.Conditional : VoteKind.Reject,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = e.UiAuditFailed > 0 ? VoteKind.Conditional : VoteKind.Approve,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = e.UiAuditFailed > 5 ? VoteKind.Conditional : VoteKind.Approve,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesQuality(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = e.TestsFailed == 0 && e.UiAuditLogFound ? VoteKind.Approve : VoteKind.Reject,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Approve,
            ["D6"] = e.I18nArabicXamlLines > 500 ? VoteKind.Conditional : VoteKind.Approve,
            ["D7"] = e.NightlyUiAuditScript ? VoteKind.Approve : VoteKind.Conditional,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesStrategy(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = VoteKind.Conditional,
            ["D2"] = (e.G31Zero || e.G32Zero) ? VoteKind.Approve : VoteKind.Conditional,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Approve,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Conditional,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesSecurity(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = e.SecurityP0 == 0 ? VoteKind.Approve : VoteKind.Conditional,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Approve,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Approve,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesEngineer(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = e.ShuraToolExists && e.NightlyUiAuditScript ? VoteKind.Approve : VoteKind.Conditional,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Approve,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Approve,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesMarket(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = VoteKind.Conditional,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Conditional,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Conditional,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesAccounting(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = e.FullScenarioPass ? VoteKind.Approve : VoteKind.Reject,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Conditional,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Approve,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesEconomist(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = VoteKind.Conditional,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Conditional,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Approve,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesCeo(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = VoteKind.Approve,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Conditional,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Approve,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesTax(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = e.O01Open ? VoteKind.Conditional : VoteKind.Approve,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Conditional,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Approve,
        };

    private static IReadOnlyDictionary<string, VoteKind> VotesAuditor(EvidenceBundle e) =>
        new Dictionary<string, VoteKind>
        {
            ["D1"] = e.TestsFailed == 0 ? VoteKind.Approve : VoteKind.Reject,
            ["D2"] = VoteKind.Approve,
            ["D3"] = VoteKind.Approve,
            ["D4"] = VoteKind.Approve,
            ["D5"] = VoteKind.Approve,
            ["D6"] = VoteKind.Conditional,
            ["D7"] = VoteKind.Approve,
        };
}
