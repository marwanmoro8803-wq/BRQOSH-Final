using System.Text;
using System.Text.Json;

namespace ShuraCouncil;

internal static class MarkdownReportWriter
{
    public static string WriteMarkdown(
        CouncilResult result,
        string sourceCeefRelative,
        ProgrammingDisciplineEvaluators.DisciplineCouncilReport? disciplines = null)
    {
        var e = result.Evidence;
        var sb = new StringBuilder();
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        sb.AppendLine("# CEEF — مجلس الشورى v5 Supreme + Disciplines (مُولَّد آلياً)");
        sb.AppendLine();
        sb.AppendLine("| الحقل | القيمة |");
        sb.AppendLine("|-------|--------|");
        sb.AppendLine($"| **التاريخ** | {DateTime.Now:yyyy-MM-dd} |");
        sb.AppendLine($"| **المولّد** | `ShuraCouncil` · {stamp} |");
        sb.AppendLine($"| **الكيان** | [shura-intelligence-core.md](.cursor/skills/brqosh-analysis/shura-intelligence-core.md) |");
        sb.AppendLine($"| **التقرير المُناقَش** | [{Path.GetFileName(sourceCeefRelative)}]({sourceCeefRelative.Replace('\\', '/')}) |");
        sb.AppendLine($"| **الإجماع** | {(result.OverallConsensus ? "✅ معتمد" : "❌ غير معتمد")} |");
        sb.AppendLine($"| **Supreme v5** | {(result.SupremeGate.AllGatesPass ? "✅ certified" : "🟡 partial")} |");
        if (disciplines is not null)
            sb.AppendLine($"| **Discipline Council** | {disciplines.Ready}/{disciplines.Total} ({disciplines.Percent}%) |");
        sb.AppendLine($"| **Leak integrity** | {result.LeakSynthesis.IntegrityScore}/100 · {result.LeakSynthesis.FamiliesPresent.Count}/{LeakSynthesisEngine.FamilyCount} families |");
        sb.AppendLine($"| **تصعيد F00** | {(result.Facilitator.EscalationRequired ? "⚠️ نعم" : "لا")} |");
        sb.AppendLine();

        sb.AppendLine("## 1. الحقائق (جولة 1 — لا جدل)");
        sb.AppendLine();
        sb.AppendLine("| # | حقيقة | قيمة |");
        sb.AppendLine("|---|--------|------|");
        sb.AppendLine($"| F1 | اختبارات | {e.TestsTotal} إجمالي · {e.TestsFailed} فاشل |");
        sb.AppendLine($"| F2 | FlaUI | {e.UiAuditPassed}/{e.UiAuditTotal} ناجح · {e.UiAuditFailed} فشل |");
        sb.AppendLine($"| F3 | أمن P0/P1 | {e.SecurityP0} / {e.SecurityP1} |");
        sb.AppendLine($"| F4 | فجوات zero-code | {e.MarketZeroCode} |");
        sb.AppendLine($"| F5 | O-01 OTP | {(e.O01Open ? "⏭ مفتوح" : "مغلق")} |");
        sb.AppendLine($"| F6 | G-31 دعم / G-32 تسعير | {(e.G31Zero ? "ZERO" : "موجود")} / {(e.G32Zero ? "ZERO" : "موجود")} |");
        sb.AppendLine($"| F7 | FullScenario | {(e.FullScenarioPass ? "SUCCESS" : "غير مثبت")} |");
        sb.AppendLine($"| F8 | i18n XAML عربي | {e.I18nArabicXamlLines} سطر |");
        sb.AppendLine();

        sb.AppendLine("### مسارات الأدلة المقروءة");
        foreach (var p in e.EvidencePaths)
            sb.AppendLine($"- `{p}`");
        sb.AppendLine();

        sb.AppendLine("## 2. مقاعد المجلس (12 + F00)");
        sb.AppendLine();
        sb.AppendLine("| المقعد | الدور | مجالس | تسريبات | ذكاء v4 | skills | ملخص |");
        sb.AppendLine("|--------|------|-------|---------|---------|--------|------|");
        foreach (var seat in result.Seats)
        {
            var intel = SeatIntelligenceRegistry.GetForWorkspace(e.Workspace, seat.SeatId);
            var councils = intel is not null ? string.Join(",", intel.Councils) : "—";
            var leaks = intel is not null ? string.Join(";", intel.LeakFamilies) : "—";
            var pattern = intel?.IntelligencePattern ?? "—";
            var skills = intel is not null ? string.Join(";", intel.SkillModules) : "—";
            sb.AppendLine($"| {seat.SeatId} | {seat.RoleAr} | {councils} | {leaks} | {pattern} | {skills} | {seat.SummaryAr} |");
        }

        var f00 = SeatIntelligenceRegistry.GetForWorkspace(e.Workspace, "F00");
        if (f00 is not null)
        {
            sb.AppendLine($"| F00 | الوسيط | {string.Join(",", f00.Councils)} | {string.Join(";", f00.LeakFamilies)} | {f00.IntelligencePattern} | {string.Join(";", f00.SkillModules)} | {result.Facilitator.SummaryAr} |");
        }
        sb.AppendLine();

        sb.AppendLine("### عائلات التسريبات النشطة (v4 — grep فقط)");
        sb.AppendLine();
        sb.AppendLine("| العائلة | تطبيق الشورى |");
        sb.AppendLine("|---------|--------------|");
        foreach (var fam in result.LeakSynthesis.FamiliesPresent)
            sb.AppendLine($"| {fam.Label} | {fam.ShuraApplication} |");
        if (result.LeakSynthesis.FamiliesMissing.Count > 0)
            sb.AppendLine($"| **ناقص** | {string.Join(" · ", result.LeakSynthesis.FamiliesMissing)} |");
        sb.AppendLine();

        sb.AppendLine("### نتائج الفحص لكل مقعد");
        sb.AppendLine();
        sb.AppendLine("| المقعد | فحوصات |");
        sb.AppendLine("|--------|--------|");
        foreach (var seat in result.Seats)
        {
            var findings = string.Join("; ", seat.Findings.Select(f => $"{f.Code}:{(f.Pass ? "✓" : "✗")}"));
            sb.AppendLine($"| {seat.SeatId} | {findings} |");
        }
        sb.AppendLine();

        sb.AppendLine("## 3. التحقق المعادِ (جولة 3b — Anthropic deep-research)");
        sb.AppendLine();
        sb.AppendLine("| ID | الادعاء | ناجٍ | طبقة | ثقة | refute/confirm |");
        sb.AppendLine("|----|---------|------|------|-----|----------------|");
        foreach (var v in result.AdversarialVerdicts)
        {
            sb.AppendLine($"| {v.ClaimId} | {v.ClaimText} | {(v.Survived ? "✅" : "❌")} | {v.Tier} | {v.Confidence} | {v.VotesRefuted}/{v.VotesConfirmed} |");
        }
        sb.AppendLine();

        sb.AppendLine("## 4. التصويت على القرارات (جولة 4)");
        sb.AppendLine();
        sb.AppendLine("| القرار | ✅ | 🟡 | ❌ | نتيجة | إجماع |");
        sb.AppendLine("|--------|----|----|-----|-------|-------|");
        foreach (var d in result.Decisions)
        {
            var icon = d.Result switch
            {
                VoteKind.Approve => "✅",
                VoteKind.Conditional => "🟡",
                _ => "❌",
            };
            sb.AppendLine($"| {d.Id}: {d.TitleAr} | {d.Approve} | {d.Conditional} | {d.Reject} | {icon} | {(d.ConsensusMet ? "✅" : "❌")} |");
        }
        sb.AppendLine();

        if (result.ConflictResolutions.Count > 0)
        {
            sb.AppendLine("## 5. حسم التعارض (جولة 5 — Conflict Chain)");
            sb.AppendLine();
            sb.AppendLine("| قرار | حسم | معارضون | سبب |");
            sb.AppendLine("|------|-----|---------|-----|");
            foreach (var c in result.ConflictResolutions)
                sb.AppendLine($"| {c.DecisionId} | {c.ResolvedVote} | {c.DissentingSeats} | {c.ReasonAr} |");
            sb.AppendLine();
        }

        sb.AppendLine("## 6. توليف الوسيط F00 (جولة 6)");
        sb.AppendLine();
        sb.AppendLine($"> {result.Facilitator.SummaryAr}");
        sb.AppendLine();
        sb.AppendLine($"**Steelman للأقلية:** {result.Facilitator.SteelmanMinorityAr}");
        sb.AppendLine();

        if (result.Facilitator.Dissents.Count > 0)
        {
            sb.AppendLine("### معارضات محفوظة (للـ MEM)");
            sb.AppendLine();
            sb.AppendLine("| مقعد | قرار | اعتراض | دليل |");
            sb.AppendLine("|------|------|--------|------|");
            foreach (var d in result.Facilitator.Dissents.Take(12))
                sb.AppendLine($"| {d.SeatId} | {d.DecisionId} | {d.ObjectionAr} | {d.EvidenceId} |");
            sb.AppendLine();
        }

        sb.AppendLine("## 7. القرار الموحّد");
        sb.AppendLine();
        sb.AppendLine($"> {result.UnifiedVerdictAr}");
        sb.AppendLine();

        sb.AppendLine("## 8. خطة العمل (جولة 7 — P0→P2)");
        sb.AppendLine();
        var i = 1;
        foreach (var action in result.ActionP0)
            sb.AppendLine($"{i++}. {action}");
        sb.AppendLine();

        sb.AppendLine("## 9. تخليق leaks (جولة 8 — 279 corpus)");
        sb.AppendLine();
        sb.AppendLine($"**Integrity:** {result.LeakSynthesis.IntegrityScore}/100 · corpus={result.LeakSynthesis.CorpusFileCount} · index={(result.LeakSynthesis.IndexExists ? "yes" : "no")}");
        sb.AppendLine();
        sb.AppendLine("| عائلة | تطبيق Shura |");
        sb.AppendLine("|-------|-------------|");
        foreach (var f in result.LeakSynthesis.FamiliesPresent)
            sb.AppendLine($"| {f.Label} | {f.ShuraApplication} |");
        if (result.LeakSynthesis.FamiliesMissing.Count > 0)
            sb.AppendLine($"| *missing* | {string.Join(" · ", result.LeakSynthesis.FamiliesMissing.Take(5))} |");
        sb.AppendLine();

        sb.AppendLine("## 10. Supreme Gate v5 (7 بوابات)");
        sb.AppendLine();
        sb.AppendLine($"> {result.SupremeGate.CertificationSummary}");
        sb.AppendLine();
        sb.AppendLine("| بوابة | ناجية | تفاصيل |");
        sb.AppendLine("|-------|-------|--------|");
        foreach (var g in result.SupremeGate.Gates)
            sb.AppendLine($"| {g.Id} {g.Label} | {(g.Passed ? "✅" : "❌")} | {g.Detail} |");
        if (disciplines is not null)
        {
            var discPass = disciplines.Percent >= 60;
            sb.AppendLine($"| G-DISCIPLINE | {(discPass ? "✅" : "🟡")} | {disciplines.SummaryAr} |");
        }
        sb.AppendLine();

        if (disciplines is not null)
        {
            sb.AppendLine("## 11. مجلس التخصصات البرمجية (28 مقعد — D01–D28)");
            sb.AppendLine();
            sb.AppendLine($"> {disciplines.SummaryAr}");
            sb.AppendLine();
            sb.AppendLine("| المقعد | التخصص | الفئة | فحوصات | ملخص |");
            sb.AppendLine("|--------|--------|-------|--------|------|");
            foreach (var seat in disciplines.Seats)
            {
                var def = ProgrammingDisciplineRegistry.Get(seat.SeatId);
                var cat = def?.Category ?? "—";
                var role = def?.RoleAr ?? seat.RoleAr;
                var findings = string.Join("; ", seat.Findings.Select(f => $"{f.Code}:{(f.Pass ? "✓" : "✗")}"));
                sb.AppendLine($"| {seat.SeatId} | {role} | {cat} | {findings} | {seat.SummaryAr} |");
            }
            sb.AppendLine();
            sb.AppendLine("### تجميع حسب الفئة");
            sb.AppendLine();
            sb.AppendLine("| الفئة | جاهز | إجمالي |");
            sb.AppendLine("|-------|------|--------|");
            foreach (var grp in disciplines.Seats
                         .GroupBy(s => ProgrammingDisciplineRegistry.Get(s.SeatId)?.Category ?? "Other")
                         .OrderBy(g => g.Key))
            {
                var ready = grp.Count(s => s.Findings.All(f => f.Pass) || s.Findings.Count(f => f.Pass) >= s.Findings.Count - 1);
                sb.AppendLine($"| {grp.Key} | {ready} | {grp.Count()} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("*مُولَّد بواسطة `ShuraCouncil` v5 Supreme — 12 seats + 28 disciplines · leaks · 7-gate · adversarial · F00.*");

        return sb.ToString();
    }

    public static void WriteJson(
        CouncilResult result,
        string jsonPath,
        ProgrammingDisciplineEvaluators.DisciplineCouncilReport? disciplines = null)
    {
        var payload = new
        {
            version = "v5-supreme-disciplines",
            supremeCertified = result.SupremeGate.AllGatesPass,
            supremeSummary = result.SupremeGate.CertificationSummary,
            leakSynthesis = new
            {
                result.LeakSynthesis.IntegrityScore,
                result.LeakSynthesis.IntegrityMet,
                result.LeakSynthesis.CorpusFileCount,
                familiesPresent = result.LeakSynthesis.FamiliesPresent.Select(f => f.FamilyId),
                familiesMissing = result.LeakSynthesis.FamiliesMissing,
            },
            supremeGates = result.SupremeGate.Gates.Select(g => new
            {
                g.Id,
                g.Label,
                g.Passed,
                g.Detail,
            }),
            generatedUtc = DateTime.UtcNow,
            overallConsensus = result.OverallConsensus,
            unifiedVerdict = result.UnifiedVerdictAr,
            facilitator = new
            {
                result.Facilitator.SummaryAr,
                result.Facilitator.SteelmanMinorityAr,
                result.Facilitator.EscalationRequired,
                dissents = result.Facilitator.Dissents.Select(d => new
                {
                    d.SeatId,
                    d.DecisionId,
                    d.ObjectionAr,
                    d.EvidenceId,
                }),
            },
            adversarialVerdicts = result.AdversarialVerdicts.Select(v => new
            {
                v.ClaimId,
                v.ClaimText,
                v.Survived,
                tier = v.Tier.ToString(),
                confidence = v.Confidence.ToString(),
                v.VotesRefuted,
                v.VotesConfirmed,
                voters = v.VoterReasons,
            }),
            conflictResolutions = result.ConflictResolutions.Select(c => new
            {
                c.DecisionId,
                c.DecisionTitleAr,
                resolvedVote = c.ResolvedVote.ToString(),
                c.DissentingSeats,
                c.ReasonAr,
            }),
            evidence = new
            {
                result.Evidence.TestsTotal,
                result.Evidence.TestsFailed,
                result.Evidence.UiAuditPassed,
                result.Evidence.UiAuditTotal,
                result.Evidence.SecurityP0,
                result.Evidence.O01Open,
                paths = result.Evidence.EvidencePaths,
            },
            seats = result.Seats.Select(s => new
            {
                s.SeatId,
                s.RoleAr,
                s.SummaryAr,
                findings = s.Findings.Select(f => new { f.Code, f.Pass, f.Detail }),
                votes = s.DecisionVotes,
            }),
            decisions = result.Decisions.Select(d => new
            {
                d.Id,
                d.TitleAr,
                d.IsP0,
                d.Approve,
                d.Conditional,
                d.Reject,
                result = d.Result.ToString(),
                d.ConsensusMet,
            }),
            actions = result.ActionP0,
            disciplineCouncil = disciplines is null ? null : new
            {
                disciplines.Total,
                disciplines.Ready,
                disciplines.Percent,
                disciplines.SummaryAr,
                disciplines.SummaryEn,
                seats = disciplines.Seats.Select(s => new
                {
                    s.SeatId,
                    s.RoleAr,
                    category = ProgrammingDisciplineRegistry.Get(s.SeatId)?.Category,
                    s.SummaryAr,
                    findings = s.Findings.Select(f => new { f.Code, f.Pass, f.Detail }),
                }),
            },
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json, Encoding.UTF8);
    }
}
