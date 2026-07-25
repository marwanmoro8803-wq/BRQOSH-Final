namespace ShuraCouncil;

internal static class ConsensusEngine
{
    private static readonly DecisionSpec[] Decisions =
    [
        new("D1", "الحكم — نواة ERP in-repo جاهزة للتشغيل التجريبي (تصنيع)", true, _ => VoteKind.Approve),
        new("D2", "رفض ادعاء «جاهز للسوق العام / SaaS»", true, _ => VoteKind.Approve),
        new("D3", "تنفيذ موجة أمن API P0 قبل أي إطلاق خارجي", true, _ => VoteKind.Approve),
        new("D4", "O-01 OTP كبوابة حية تالية (مع المستخدم)", true, _ => VoteKind.Approve),
        new("D5", "إعادة FlaUI على ملف مقاولات + تجزئة", false, _ => VoteKind.Approve),
        new("D6", "موجة G-27 i18n (شهر+)", false, _ => VoteKind.Conditional),
        new("D7", "قبول إغلاق LIVE-001..008 في CEEF-145", false, _ => VoteKind.Approve),
    ];

    public static CouncilResult Run(EvidenceBundle evidence, IReadOnlyList<SeatVerdict> seats)
    {
        if (evidence.Workspace == WorkspaceKind.ProgramDeveloper)
            return PromptConsensusEngine.Run(evidence, seats);

        var outcomes = new List<DecisionSpecOutcome>();
        foreach (var spec in Decisions)
        {
            var approve = 0;
            var conditional = 0;
            var reject = 0;
            foreach (var seat in seats)
            {
                if (!seat.DecisionVotes.TryGetValue(spec.Id, out var vote))
                    continue;
                switch (vote)
                {
                    case VoteKind.Approve: approve++; break;
                    case VoteKind.Conditional: conditional++; break;
                    case VoteKind.Reject: reject++; break;
                }
            }

            var supportive = approve + conditional;
            var consensusMet = supportive >= 9 && reject <= 1;
            if (spec.IsP0 && reject > 1)
                consensusMet = false;

            var resultVote = reject > approve + conditional
                ? VoteKind.Reject
                : reject > 0 && approve < 9
                    ? VoteKind.Conditional
                    : approve >= conditional
                        ? VoteKind.Approve
                        : VoteKind.Conditional;

            outcomes.Add(new DecisionSpecOutcome(spec, approve, conditional, reject, resultVote, consensusMet));
        }

        var p0Pass = outcomes.Where(o => o.Spec.IsP0).All(o => o.ConsensusMet);
        var d1 = outcomes.First(o => o.Spec.Id == "D1");
        var d2 = outcomes.First(o => o.Spec.Id == "D2");
        var overall = p0Pass && d1.ConsensusMet && d2.ConsensusMet;

        var decisionOutcomes = outcomes.Select(o => o.ToOutcome()).ToList();
        var adversarial = AdversarialVerifier.VerifyCriticalClaims(evidence);
        var actions = BuildActionPlan(evidence, adversarial);

        return CouncilResultAssembler.Assemble(
            evidence, seats, decisionOutcomes, adversarial, overall, actions,
            (consensus, facilitator) => BuildUnifiedVerdict(evidence, consensus, adversarial, facilitator),
            (_, adv) => !adv.Any(a => !a.Survived && a.ClaimId is "C-SEC" or "C-TEST"));
    }

    private static string BuildUnifiedVerdict(
        EvidenceBundle e,
        bool consensus,
        IReadOnlyList<AdversarialVerdict> adversarial,
        FacilitatorSynthesis facilitator)
    {
        if (!consensus)
            return facilitator.SummaryAr + " — راجع مقاعد الرفض والادعاءات المقتولة في 3b.";

        var rate = e.UiAuditTotal > 0
            ? $"{e.UiAuditPassed}/{e.UiAuditTotal} ({100.0 * e.UiAuditPassed / e.UiAuditTotal:F1}%)"
            : "غير متاح";

        return $"BRQOSH ERP — بعد تدقيق حي ({rate} FlaUI): النواة المحاسبية والواجهة التشغيلية «جاهزة تجريب in-repo» لقطاع التصنيع؛ " +
               $"ليست «جاهزة إطلاق سوق» حتى O-01 OTP حي" +
               (e.G31Zero || e.G32Zero ? " + إكمال G-31/G-32" : " (G-31/G-32 MVP in-repo)") +
               "؛ وتُستكمل الشفافية بتشغيل FlaUI على ملفي مقاولات وتجزئة وموجة i18n مشروطة. " +
               $"[{facilitator.SummaryAr}]";
    }

    private static IReadOnlyList<string> BuildActionPlan(
        EvidenceBundle e,
        IReadOnlyList<AdversarialVerdict> adversarial)
    {
        var items = new List<string>();
        foreach (var failed in adversarial.Where(a => !a.Survived))
            items.Add($"P0: إعادة verify ادعاء {failed.ClaimId} — {failed.ClaimText}");
        if (e.SecurityP0 > 0)
            items.Add($"P0: إغلاق {e.SecurityP0} ثغرة API P0 — verify: security-findings.csv → 0 P0");
        if (e.O01Open)
            items.Add("P0: O-01 ZATCA OTP حي — verify: dist/evidence/O-01-*/");
        if (e.UiAuditFailed > 0)
            items.Add($"P1: إعادة UiAudit — verify: scripts/ops/run-ui-audit-nightly.ps1 ({e.UiAuditFailed} فشل)");
        if (!e.UiAuditLogFound || e.UiAuditTotal == 0)
            items.Add("P1: FlaUI على BusinessProfile.Construction + Retail — verify: BRQOSH_AUDIT_ALL_PROFILES=1");
        else if (e.UiAuditFailed > 0)
            items.Add("P1: FlaUI على BusinessProfile.Construction + Retail");
        if (e.G31Zero || e.G32Zero)
            items.Add("P1: تأسيس G-31 دعم + G-32 تسعير (MVP)");
        if (e.I18nArabicXamlLines > 500)
            items.Add($"P2: موجة G-27 i18n ({e.I18nArabicXamlLines} سطر XAML عربي)");
        return items;
    }

    private sealed record DecisionSpecOutcome(
        DecisionSpec Spec,
        int Approve,
        int Conditional,
        int Reject,
        VoteKind Result,
        bool ConsensusMet)
    {
        public DecisionOutcome ToOutcome() =>
            new(Spec.Id, Spec.TitleAr, Spec.IsP0, Approve, Conditional, Reject, Result, ConsensusMet);
    }
}
