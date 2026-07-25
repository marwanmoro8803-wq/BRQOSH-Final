# Prompt Source Law — Raw QA Prompt

> **Authority:** User directive 2026-07-10 · **PS01-RELEASED** 2026-07-10 (`نفّذ البرومبيت`)  
> **Raw file:** repo-root QA prompt (`البرومبيت.txt`) — governs skills + authorized code

---

## Immutable law

| ID | Law |
|----|-----|
| PS01 | **RELEASED** — user authorized implementation 2026-07-10 |
| PS02 | Raw prompt principles live in skills · not pasted wholesale |
| PS03 | Prior removal superseded by authorized wave |
| PS04 | **MUST NOT** paste raw prompt into skills · grep principles only |
| PS05 | Framework claims require `cognitive-qa` + verify + Shura DP1-LIFT |
| PS06 | **PromptAuditEngine** (C# harness) **allowed** alongside cognitive-qa |
| PS07 | **CognitiveQa/** (.NET 9 / FlaUI) is **primary** · `cognitive-qa/` Python **legacy frozen** |
| PS08 | M8 closure: `AUDIT_MANIFEST.json` + `MicrocopyAuditor` + bilingual `AUDIT_REPORT_ar.md` |
| PS09 | M9: `XamlQualityAnalyzer` + real role auditors + `COVERAGE_GAPS.md` (no placeholder deliverables) |
| PS10 | M10: `ArabicPayloads` + `BetaTesterAuditor` + `PentestSourceAuditor` + `EVIDENCE_INDEX.md` |
| PS11 | M11: `QaAutoAuditor` + `PARITY_MATRIX.md` + `AUDIT_EVIDENCE.zip` + BRQOSH sibling resolver + O02 SHUTDOWN |
| PS12 | M12: `AuditSessionResultWriter` + `PhaseVerifyLogger` O07 + `PackageVulnerabilityAuditor` + `--verify-tests` |
| PS13 | M13: `ReportQualityWriter` S12 + `AUDIT_DASHBOARD.html` + `EvidenceArchiveWriter` + GitHub CI |
| PS14 | M14: 17 minds + `COGNITIVE_COUNCIL.md` + `INTELLIGENCE_BRIEF.md` + compare/intelligence CLI + full gate ops |
| PS15 | M15: Supreme Perception Mind + `PerceptionFusionEngine` + `COGNITIVE_PERCEPTION.md` + `run-cognitive-perception.ps1` |
| PS16 | M16: `OcrTextReader` + `SUPREME_CLOSURE.md` + `run-cognitive-supreme-gate.ps1` + holistic closure |

---

## implementation: active

| Component | Path | Status |
|-----------|------|--------|
| **Primary** .NET framework | `CognitiveQa/` | **ACTIVE** CEEF-REPLATFORM-001 M1–M16 |
| Legacy Python framework | `cognitive-qa/` | FROZEN — `--LegacyPython` only |
| Launcher (legacy) | `cognitive-qa/launcher/` | FROZEN |
| CEEF Python | `dist/evidence/prompt-governance/CEEF-IMPLEMENT-001.md` | DONE |
| CEEF .NET | `dist/evidence/prompt-governance/CEEF-REPLATFORM-001.md` | M1–M16 DONE |
| STATUS | `CognitiveQa/STATUS.yaml` | M16 supreme closure |

---

## Release phrase (recorded)

User: **نفّذ البرومبيت** — Shura CEEF-IMPLEMENT-001 · DP1-LIFT

---

## Verify

```powershell
Test-Path CognitiveQa/src/CognitiveQa.Orchestrator/AuditOrchestrator.cs   # MUST be True
dotnet test CognitiveQa/CognitiveQa.sln -c Release --nologo
.\scripts\ops\run-cognitive-audit.ps1 -MaxSteps 3 -SkipBuild
.\scripts\ops\run-shura-council.ps1 -Source dist/evidence/prompt-governance/CEEF-REPLATFORM-001.md
```

**Related:** [omni-qa-prompt-analysis.md](../ai-dev-platform/omni-qa-prompt-analysis.md)
