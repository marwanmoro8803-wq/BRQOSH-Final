using System.Text;
using System.Text.Json;

namespace ShuraCouncil;

internal static class MarkdownReportWriter
{
    public static string WriteMarkdown(CouncilResult result, string sourceCeefRelative)
    {
        var e = result.Evidence;
        var sb = new StringBuilder();
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        sb.AppendLine("# CEEF — مجلس الشورى (مُولَّد آلياً)");
        sb.AppendLine();
        sb.AppendLine("| الحقل | القيمة |");
        sb.AppendLine("|-------|--------|");
        sb.AppendLine($"| **التاريخ** | {DateTime.Now:yyyy-MM-dd} |");
        sb.AppendLine($"| **المولّد** | `tools/ShuraCouncil` · {stamp} |");
        sb.AppendLine($"| **التقرير المُناقَش** | [{Path.GetFileName(sourceCeefRelative)}]({sourceCeefRelative.Replace('\\', '/')}) |");
        sb.AppendLine($"| **الإجماع** | {(result.OverallConsensus ? "✅ معتمد" : "❌ غير معتمد")} |");
        sb.AppendLine();

        sb.AppendLine("## 1. الحقائق (من ملفات الأدلة — لا جدل)");
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

        sb.AppendLine("## 2. مقاعد المجلس (12 — مُقيَّمة من الكود)");
        sb.AppendLine();
        sb.AppendLine("| المقعد | الدور | مجالس | تسريبات | skills | ملخص |");
        sb.AppendLine("|--------|------|-------|---------|--------|------|");
        foreach (var seat in result.Seats)
        {
            var intel = SeatIntelligenceRegistry.Get(seat.SeatId);
            var councils = intel is not null ? string.Join(",", intel.Councils) : "—";
            var leaks = intel is not null ? string.Join(";", intel.LeakFamilies) : "—";
            var skills = intel is not null ? string.Join(";", intel.SkillModules) : "—";
            sb.AppendLine($"| {seat.SeatId} | {seat.RoleAr} | {councils} | {leaks} | {skills} | {seat.SummaryAr} |");
        }
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

        sb.AppendLine("## 3. التصويت على القرارات");
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

        sb.AppendLine("## 4. القرار الموحّد");
        sb.AppendLine();
        sb.AppendLine($"> {result.UnifiedVerdictAr}");
        sb.AppendLine();

        sb.AppendLine("## 5. خطة العمل (P0→P2)");
        sb.AppendLine();
        var i = 1;
        foreach (var action in result.ActionP0)
            sb.AppendLine($"{i++}. {action}");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine("*هذا الملف مُولَّد بواسطة `dotnet run --project tools/ShuraCouncil` — ليس نصاً مسرحياً.*");

        return sb.ToString();
    }

    public static void WriteJson(CouncilResult result, string jsonPath)
    {
        var payload = new
        {
            generatedUtc = DateTime.UtcNow,
            overallConsensus = result.OverallConsensus,
            unifiedVerdict = result.UnifiedVerdictAr,
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
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json, Encoding.UTF8);
    }
}
