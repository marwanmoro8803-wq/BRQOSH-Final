namespace ShuraCouncil;

/// <summary>
/// 3-vote adversarial verification (Anthropic deep-research pattern).
/// Claim survives unless 2+ of 3 voters refute it.
/// </summary>
internal static class AdversarialVerifier
{
    private const int RefuteThreshold = 2;

    public static IReadOnlyList<AdversarialVerdict> VerifyCriticalClaims(EvidenceBundle e)
    {
        if (e.Workspace == WorkspaceKind.ProgramDeveloper)
            return VerifyPromptClaims(e);
        var claims = BuildClaims(e);
        var verdicts = new List<AdversarialVerdict>(claims.Count);
        foreach (var claim in claims)
            verdicts.Add(VerifyClaim(claim, e));
        return verdicts;
    }

    private static IReadOnlyList<AdversarialVerdict> VerifyPromptClaims(EvidenceBundle e)
    {
        var skillsOk = PromptGovernanceEvidence.SkillsFoundationComplete(e.OmniQaSkillModules, e.OmniQaRoleModules);
        var implement = PromptGovernanceEvidence.IsPromptCodeWaveCeef(e);
        var frameworkReady = PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot);
        var dotnetReady = PromptGovernanceEvidence.CognitiveQaDotNetReady(e.RepoRoot);
        var released = PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot);

        var claims = new List<CriticalClaim>();
        var authorized = implement || released;
        if (authorized)
        {
            var codeClaim = PromptGovernanceEvidence.IsReplatformCeef(e) || dotnetReady
                ? ("CognitiveQa .NET مُصرَّح وموجود", dotnetReady && released, dotnetReady ? "CognitiveQa.sln ready" : "dotnet framework incomplete")
                : ("cognitive-qa مُصرَّح وموجود", frameworkReady && released, frameworkReady ? "cognitive-qa ready" : "framework incomplete");
            claims.Add(new("C-PROMPT-CODE", codeClaim.Item1, codeClaim.Item2, codeClaim.Item3));
            claims.Add(new("C-PROMPT-SKILLS", "أساس مهارات omni-qa-army مكتمل", skillsOk,
                $"modules={e.OmniQaSkillModules} roles={e.OmniQaRoleModules}"));
            claims.Add(new("C-PROMPT-LAW", "prompt-source-law.md موجود", e.PromptSourceLawExists,
                e.PromptSourceLawExists ? "law present" : "missing"));
            claims.Add(new("C-PROMPT-LEAK", "فهرس leaks متاح", e.LeakCorpusExists, $"files={e.LeakCorpusFiles}"));
            claims.Add(new("C-PROMPT-ENGINE", "PAE + CognitiveQa مُصرَّحان معاً",
                PromptGovernanceEvidence.PaeGovernanceReady(e),
                $"strength={e.AuditStrengthScore} pass={e.AuditOverallPass}"));
            claims.Add(new("C-PROMPT-FALSE", "إطار قابل للتشغيل", frameworkReady,
                frameworkReady ? "CognitiveQa ready" : "framework incomplete"));
            claims.Add(new("C-PROMPT-P2", "مهارات P2 deep engines مكتملة",
                PromptGovernanceEvidence.P2SkillsComplete(e.RepoRoot), "5 P2 modules"));
            claims.Add(new("C-PROMPT-P3", "قانون P3 launcher مكتمل",
                PromptGovernanceEvidence.P3SkillsComplete(e.RepoRoot), "launcher-architecture+orchestrator"));
        }
        else
        {
            claims.Add(new("C-PROMPT-CODE", "لا كود برومبيت في المستودع", e.PromptCodeAbsent,
                e.PromptCodeAbsent ? "cognitive-qa absent" : "cognitive-qa EXISTS"));
            claims.Add(new("C-PROMPT-SKILLS", "أساس مهارات omni-qa-army مكتمل", skillsOk,
                $"modules={e.OmniQaSkillModules} roles={e.OmniQaRoleModules}"));
            claims.Add(new("C-PROMPT-LAW", "prompt-source-law.md موجود", e.PromptSourceLawExists,
                e.PromptSourceLawExists ? "law present" : "missing"));
            claims.Add(new("C-PROMPT-LEAK", "فهرس leaks متاح", e.LeakCorpusExists, $"files={e.LeakCorpusFiles}"));
            claims.Add(new("C-PROMPT-ENGINE", "محرك PAE موجود وليس cognitive-qa",
                e.AuditEngineExists && e.PromptCodeAbsent, $"strength={e.AuditStrengthScore}"));
            claims.Add(new("C-PROMPT-FALSE", "لا ادعاء إطار كامل", e.PromptCodeAbsent, "no runnable cognitive-qa"));
            claims.Add(new("C-PROMPT-P2", "مهارات P2 deep engines مكتملة",
                PromptGovernanceEvidence.P2SkillsComplete(e.RepoRoot), "5 P2 modules"));
            claims.Add(new("C-PROMPT-P3", "قانون P3 launcher مكتمل",
                PromptGovernanceEvidence.P3SkillsComplete(e.RepoRoot), "launcher-architecture+orchestrator"));
        }

        if (PromptGovernanceEvidence.IsKarpathyCeef(e))
        {
            var karpathyOk = PromptGovernanceEvidence.KarpathySynthesisComplete(e.RepoRoot);
            claims.Add(new("C-KARP-SYNTH",
                "Karpathy مُخلَّص في المهارات — لا قاعدة alwaysApply مكررة",
                karpathyOk,
                karpathyOk ? "karpathy-synthesis+index" : "synthesis incomplete"));
        }

        if (e.PlatformBridgeReady || e.AiCouncilIntegrated)
        {
            var bridgeOk = e.PlatformBridgeReady && e.AiCouncilIntegrated && e.CouncilChronicleExists;
            claims.Add(new("C-PLATFORM-BRIDGE",
                "جسر المنصة v4 — AiCouncil + Chronicle + CognitiveQa",
                bridgeOk,
                bridgeOk ? "platform bridge complete" : $"bridge={e.PlatformBridgeReady} aicouncil={e.AiCouncilIntegrated} chronicle={e.CouncilChronicleExists}"));
        }

        return claims.Select(c => VerifyClaim(c, e)).ToList();
    }

    private static IReadOnlyList<CriticalClaim> BuildClaims(EvidenceBundle e)
    {
        var rate = e.UiAuditTotal > 0 ? (double)e.UiAuditPassed / e.UiAuditTotal : 0;
        return
        [
            new("C-SEC", "ثغرات API P0 = 0", e.SecurityP0 == 0, e.SecurityP0 == 0
                ? $"security-findings.csv: P0={e.SecurityP0}"
                : $"P0={e.SecurityP0} — مفتوحة"),
            new("C-UI", "FlaUI pass rate ≥ 90%", rate >= 0.90, $"{e.UiAuditPassed}/{e.UiAuditTotal} ({rate:P1})"),
            new("C-GL", "FullScenario SUCCESS", e.FullScenarioPass, e.FullScenarioPass ? "SUCCESS" : "غير مثبت"),
            new("C-ZATCA", "O-01 OTP مغلق حياً", !e.O01Open, e.O01Open ? "⏭ مفتوح" : "مغلق in-repo"),
            new("C-TEST", "اختبارات: 0 فاشل", e.TestsFailed == 0, $"{e.TestsFailed}/{e.TestsTotal}"),
        ];
    }

    private static AdversarialVerdict VerifyClaim(CriticalClaim claim, EvidenceBundle e)
    {
        var voters = new[]
        {
            VoteSkeptic(claim, e),
            VoteQuality(claim, e),
            VoteAuditor(claim, e),
        };

        var refutes = voters.Count(v => v.Refuted);
        var confirms = voters.Length - refutes;
        var survived = refutes < RefuteThreshold;

        var confidence = survived switch
        {
            true when confirms == 3 => ConfidenceLevel.High,
            true when confirms == 2 => ConfidenceLevel.Medium,
            true => ConfidenceLevel.Low,
            false when refutes == 3 => ConfidenceLevel.High,
            _ => ConfidenceLevel.Medium,
        };

        return new AdversarialVerdict(
            claim.Id,
            claim.Text,
            refutes,
            confirms,
            survived,
            confidence,
            ResolveTier(survived, confirms, refutes),
            voters.Select(v => v.Reason).ToList());
    }

    private static ClaimTier ResolveTier(bool survived, int confirms, int refutes)
    {
        if (!survived) return ClaimTier.Unverified;
        if (confirms == 3) return ClaimTier.Confirmed;
        if (confirms >= 2 && refutes <= 1) return ClaimTier.Plausible;
        return ClaimTier.Unverified;
    }

    private static VoterResult VoteSkeptic(CriticalClaim claim, EvidenceBundle e)
    {
        var codeWave = PromptGovernanceEvidence.IsPromptCodeWaveCeef(e);
        var framework = PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot);
        var released = PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot);
        var authorized = codeWave || released;
        return claim.Id switch
        {
            "C-PROMPT-CODE" => authorized
                ? RefuteIf(!framework || !released, "S02: CognitiveQa غير مُصرَّح")
                : RefuteIf(!e.PromptCodeAbsent, "S02: cognitive-qa موجود"),
            "C-PROMPT-SKILLS" => RefuteIf(!PromptGovernanceEvidence.SkillsFoundationComplete(e.OmniQaSkillModules, e.OmniQaRoleModules), "S02: مهارات ناقصة"),
            "C-PROMPT-LAW" => RefuteIf(!e.PromptSourceLawExists, "S02: لا قانون"),
            "C-PROMPT-LEAK" => RefuteIf(!e.LeakCorpusExists, "S02: لا leaks"),
            "C-PROMPT-ENGINE" => authorized
                ? RefuteIf(!PromptGovernanceEvidence.PaeGovernanceReady(e), "S02: PAE+CognitiveQa ناقص")
                : RefuteIf(!e.AuditEngineExists || !e.PromptCodeAbsent, "S02: لا PAE أو cognitive-qa موجود"),
            "C-PROMPT-FALSE" => authorized
                ? RefuteIf(!framework, "S02: orchestrator مفقود")
                : Confirm("S02: N/A"),
            "C-KARP-SYNTH" => RefuteIf(!PromptGovernanceEvidence.KarpathySynthesisComplete(e.RepoRoot), "S02: لا تخليق Karpathy"),
            "C-PLATFORM-BRIDGE" => RefuteIf(!e.PlatformBridgeReady || !e.AiCouncilIntegrated || !e.CouncilChronicleExists, "S02: جسر المنصة ناقص"),
            "C-SEC" => RefuteIf(e.SecurityP0 > 0, "S02: P0 مفتوحة — لا ثقة"),
            "C-UI" => RefuteIf(e.UiAuditFailed > 15, "S02: فشل FlaUI مرتفع"),
            "C-GL" => RefuteIf(!e.FullScenarioPass, "S02: لا SUCCESS في السجل"),
            "C-ZATCA" => RefuteIf(e.O01Open, "S02: OTP حي مطلوب"),
            "C-TEST" => RefuteIf(e.TestsFailed > 0, "S02: اختبارات حمراء"),
            _ => Confirm("S02: لا اعتراض"),
        };
    }

    private static VoterResult VoteQuality(CriticalClaim claim, EvidenceBundle e)
    {
        var codeWave = PromptGovernanceEvidence.IsPromptCodeWaveCeef(e);
        var framework = PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot);
        var released = PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot);
        var authorized = codeWave || released;
        return claim.Id switch
        {
            "C-PROMPT-CODE" => authorized
                ? RefuteIf(!framework, "S03: framework incomplete")
                : RefuteIf(!e.PromptCodeAbsent, "S03: كود برومبيت"),
            "C-PROMPT-SKILLS" => RefuteIf(e.OmniQaRoleModules < 10, "S03: roles < 10"),
            "C-PROMPT-LAW" => RefuteIf(!e.PromptSourceLawExists, "S03: law missing"),
            "C-PROMPT-LEAK" => RefuteIf(e.LeakCorpusFiles < 50, "S03: corpus thin"),
            "C-PROMPT-ENGINE" => authorized
                ? RefuteIf(!PromptGovernanceEvidence.PaeGovernanceReady(e), "S03: PAE+CognitiveQa")
                : RefuteIf(!e.AuditEngineExists, "S03: لا PromptAuditEngine"),
            "C-PROMPT-FALSE" => authorized
                ? RefuteIf(!framework, "S03: لا orchestrator")
                : RefuteIf(!e.PromptAnalysisExists, "S03: لا تحليل"),
            "C-KARP-SYNTH" => RefuteIf(!PromptGovernanceEvidence.KarpathySynthesisComplete(e.RepoRoot), "S03: لا karpathy-synthesis"),
            "C-PLATFORM-BRIDGE" => RefuteIf(!e.PlatformBridgeReady, "S03: PlatformPaths ناقص"),
            "C-SEC" => RefuteIf(e.SecurityP0 > 0, "S03: CSV يثبت P0"),
            "C-UI" => RefuteIf(!e.UiAuditLogFound, "S03: لا سجل FlaUI"),
            "C-GL" => RefuteIf(!e.FullScenarioPass, "S03: سيناريو غير مُشغَّل"),
            "C-ZATCA" => RefuteIf(e.O01Open, "S03: O-01 في OPEN_ITEMS"),
            "C-TEST" => RefuteIf(e.TestsFailed != 0 || e.TestsTotal < 400, "S03: تغطية اختبار ناقصة"),
            _ => Confirm("S03: أدلة كافية"),
        };
    }

    private static VoterResult VoteAuditor(CriticalClaim claim, EvidenceBundle e)
    {
        var codeWave = PromptGovernanceEvidence.IsPromptCodeWaveCeef(e);
        var framework = PromptGovernanceEvidence.CognitiveQaFrameworkReady(e.RepoRoot);
        var released = PromptGovernanceEvidence.PromptImplementationReleased(e.RepoRoot);
        var authorized = codeWave || released;
        return claim.Id switch
        {
            "C-PROMPT-CODE" => authorized
                ? RefuteIf(!framework, "S12: framework incomplete")
                : RefuteIf(!e.PromptCodeAbsent, "S12: كود ممنوع"),
            "C-PROMPT-SKILLS" => RefuteIf(!PromptGovernanceEvidence.SkillsFoundationComplete(e.OmniQaSkillModules, e.OmniQaRoleModules), "S12: لا توقيع"),
            "C-PROMPT-LAW" => RefuteIf(!e.PromptSourceLawExists, "S12: لا قانون"),
            "C-PROMPT-LEAK" => RefuteIf(!e.LeakCorpusExists, "S12: لا أدلة leaks"),
            "C-PROMPT-ENGINE" => authorized
                ? RefuteIf(!PromptGovernanceEvidence.PaeGovernanceReady(e), "S12: PAE+CognitiveQa")
                : RefuteIf(!e.AuditEngineExists || !e.PromptCodeAbsent, "S12: لا توقيع PAE"),
            "C-PROMPT-FALSE" => authorized
                ? RefuteIf(!framework, "S12: لا orchestrator")
                : RefuteIf(!e.PromptCodeAbsent, "S12: cognitive-qa ممنوع"),
            "C-KARP-SYNTH" => RefuteIf(!PromptGovernanceEvidence.KarpathySynthesisComplete(e.RepoRoot), "S12: لا توقيع Karpathy"),
            "C-PLATFORM-BRIDGE" => RefuteIf(!e.CouncilChronicleExists, "S12: لا chronicle موحّد"),
            "C-SEC" => RefuteIf(e.SecurityP0 > 0, "S12: لا إغلاق أمني"),
            "C-UI" => RefuteIf(e.UiAuditTotal == 0, "S12: لا دليل حي UI"),
            "C-GL" => RefuteIf(!e.FullScenarioPass, "S12: لا إثبات ميزان"),
            "C-ZATCA" => RefuteIf(e.O01Open, "S12: ⏭ EXTERNAL مفتوح"),
            "C-TEST" => RefuteIf(e.TestsFailed > 0, "S12: فشل اختبار = لا توقيع"),
            _ => Confirm("S12: يقبل in-repo فقط"),
        };
    }

    private static VoterResult RefuteIf(bool condition, string reason) =>
        condition ? new VoterResult(true, reason) : new VoterResult(false, reason.Replace(" — لا ثقة", " — مقبول").Replace(" مفتوحة", " = 0"));

    private static VoterResult Confirm(string reason) => new(false, reason);

    private sealed record CriticalClaim(string Id, string Text, bool Supported, string Evidence);

    private sealed record VoterResult(bool Refuted, string Reason);
}
