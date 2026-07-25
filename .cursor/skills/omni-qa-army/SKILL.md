---
name: omni-qa-army
description: >-
  Cognitive Desktop QA Army — skills only. Ten roles, vision, reports, Arabic QA.
  Raw prompt is ideas for Shura — no implementation code in repo.
---

# Omni-QA Army — Cognitive Desktop Audit (Skills Only)

> **Language:** English only · [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)  
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)
> **Raw prompt:** repo-root QA prompt file — **ideas only** · [prompt-source-law.md](prompt-source-law.md)  
> **Analysis:** [omni-qa-prompt-analysis.md](../ai-dev-platform/omni-qa-prompt-analysis.md)  
> **Status:** P0â€“P3 skills DONE · **CognitiveQa .NET 9 PRIMARY** (M32 Cloud Code shell) · Python `cognitive-qa/` **FROZEN**

---

## Implementation status

| Path | Status |
|------|--------|
| `CognitiveQa/` | **PRIMARY** — .NET 9 solution · Desktop + Cli + Auditors |
| `cognitive-qa/` | **FROZEN** — legacy Python reference only |
| CEEF | `dist/evidence/prompt-governance/CEEF-IMPLEMENT-001.md` (historical lift) |

Law: [prompt-source-law.md](prompt-source-law.md) · Desktop bridge: [cloud-code-desktop.md](cloud-code-desktop.md)

---

## Shura-bound scope

| Wave | Status | Content |
|------|--------|---------|
| **P0** | DONE | 7 modules + this router |
| **P1** | DONE | 10 `roles/role-*.md` |
| **Code** | **ACTIVE** | `CognitiveQa/` .NET — Cloud Code shell M32 |
| **P2** | DONE | 5 deep engine modules |
| **P3** | DONE | launcher-architecture · orchestrator extend |

---

## Attach stack

```
1. fable-omni-core
2. supreme-programming-mastery
3. ai-dev-platform
4. omni-qa-army (this)
5. brqosh-review (security · code-review on findings)
```

---

## Module map (P0 + P2)

| Module | Purpose |
|--------|---------|
| [superintelligence-core.md](superintelligence-core.md) | State graph · oracles · adversarial · reporting theory |
| [vision-perception.md](vision-perception.md) | UIA first · OCR fallback · modal detection |
| [exploratory-mindset.md](exploratory-mindset.md) | Pace · context-switch · critic pattern |
| [chronicle-protocol.md](chronicle-protocol.md) | Diary template · session structure · bilingual law |
| [chaos-exploration.md](chaos-exploration.md) | Creative sabotage · bored user · weird payloads |
| [empathetic-ux.md](empathetic-ux.md) | Human dignity · silent failure · graceful errors |
| [deep-audit.md](deep-audit.md) | Perf · network · CVE · business oracles |
| [forensic-resilience.md](forensic-resilience.md) | Crash recovery · self-healing · persistence |
| [report-excellence.md](report-excellence.md) | Severity-first findings · repro · fix blocks |
| [bug-tracker-spec.md](bug-tracker-spec.md) | BUG-XXXX tickets · index format |
| [arabic-qa.md](arabic-qa.md) | RTL · payloads · bilingual report law |
| [prompt-audit-engine.md](prompt-audit-engine.md) | Universal multi-language harness · always-on |
| [orchestrator-law.md](orchestrator-law.md) | Cognitive loop · Perfection Loop · CI exit codes |
| [launcher-architecture.md](launcher-architecture.md) | Desktop launcher · START scripts |
| [cloud-code-desktop.md](cloud-code-desktop.md) | Cloud Code WPF shell · FlaUI · XAML map |
| [prompt-source-law.md](prompt-source-law.md) | Raw prompt = ideas only |

---

## Ten roles (P1 — DONE)

| Role | Module | Report artifact |
|------|--------|-----------------|
| QA Auto | [roles/role-automation.md](roles/role-automation.md) | `TEST_FRAMEWORK_ARCHITECTURE.md` |
| Manual QA | [roles/role-exploratory.md](roles/role-exploratory.md) | `EXPLORATORY_TEST_SESSIONS.md` |
| UX Auditor | [roles/role-accessibility.md](roles/role-accessibility.md) | `UX_ACCESSIBILITY_COMPLIANCE_REPORT.md` |
| Pen Tester | [roles/role-pentest.md](roles/role-pentest.md) | `PENTEST_FINDINGS_AND_RISK_ASSESSMENT.md` |
| Beta Tester | [roles/role-beta.md](roles/role-beta.md) | `BETA_TESTER_USER_JOURNEY_FEEDBACK.md` |
| Perf Tester | [roles/role-performance.md](roles/role-performance.md) | `PERFORMANCE_AND_MEMORY_ANALYSIS_REPORT.md` |
| L10n/I18n | [roles/role-i18n.md](roles/role-i18n.md) | `I18N_AND_L10N_TEST_REPORT.md` |
| DevOps | [roles/role-devops.md](roles/role-devops.md) | `DEVOPS_INTEGRATION_GUIDE.md` |
| Compatibility | [roles/role-compatibility.md](roles/role-compatibility.md) | `COMPATIBILITY_TEST_MATRIX.md` |
| Tech Copywriter | [roles/role-copy.md](roles/role-copy.md) | `MICROCOPY_AND_DOCS_AUDIT.md` |

---

## Immutable laws

| ID | Law |
|----|-----|
| Q01 | Accessibility API before OCR |
| Q02 | Valid data path before malicious path on same form |
| Q03 | Findings verified · severity-first · max 32 per report |
| Q04 | Every bug → BUG-XXXX file + index row |
| Q05 | Arabic in reports/runtime · English in skills |
| Q06 | Evidence paths in `audit_reports/` (runtime — when code exists) |

---

## Verify

```powershell
rg "[\u0600-\u06FF]" .cursor/skills/omni-qa-army/
Get-ChildItem .cursor/skills/omni-qa-army/roles/role-*.md | Measure-Object
# Expected: 10 role files · no cognitive-qa/ directory
```

**Related:** [ai-dev-platform/SKILL.md](../ai-dev-platform/SKILL.md) · [shura-governance.md](../ai-dev-platform/shura-governance.md)
