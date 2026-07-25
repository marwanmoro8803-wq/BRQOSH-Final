# Omni-QA Army Prompt Analysis — Skills & Leak Synthesis Map

> **Source prompt:** `البرومبيت.txt` (repo root) — **ideas only** for Shura + skills · **NOT** implementation spec  
> **Binding law:** [prompt-source-law.md](../omni-qa-army/prompt-source-law.md) — frozen until user authorizes  
> **Language law:** [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)  
> **Shura:** run after SAVE of this analysis → `ShuraCouncil/run-shura-council.ps1`  
> **Corpus:** `system_prompts_leaks-main/system_prompts_leaks-main` (279 files) — grep only

---

## 1. Executive summary

`البرومبيت.txt` defines a **Cognitive Desktop QA Framework** — not a chat persona alone. It mandates:

| Layer | What it demands |
|-------|-----------------|
| **Cognitive** | 9 engines (superintelligence · vision · agility · sabotage · empathy · design · perf/sec · reporting · Arabic) |
| **Organizational** | 10 QA roles with 10+ dedicated deliverable documents |
| **Technical** | 20+ Python modules + PySide6/React launcher + CI/CD |
| **Output** | `AUDIT_REPORT.md` + `bug_tracker/` + per-role reports |

**Current skills stack covers ~40%** of the prompt via synthesis (review · debug · UI design-system · agents · Shura). **~60% is uncovered** — needs new skill package `omni-qa-army/` (recommended) under `ai-dev-platform/`.

**Leak corpus usage:** ~35 specific files across 7 families — NOT bulk load. Principles merge into ≤15 new/extended skill modules.

---

## 2. Prompt decomposition

### 2.1 Cognitive engines → skill modules

| Engine (prompt) | Core capability | Target skill module | Existing coverage |
|-----------------|-----------------|---------------------|-------------------|
| Superintelligence Core | state graph · oracles · chaos · reporting theory | `omni-qa-army/superintelligence-core.md` | partial: `shura-intelligence-core.md` |
| Human perception | UIA first · OCR fallback · state machine | `omni-qa-army/vision-perception.md` | none |
| Micro-documentation | chronicle diary template | `omni-qa-army/chronicle-protocol.md` | partial: `brqosh-debug` |
| Forensic self-healing | crash restart · log trace | `omni-qa-army/forensic-resilience.md` | partial: `brqosh-debug` |
| Mental agility | pace · context-switch · parallel | `omni-qa-army/exploratory-mindset.md` | partial: `agent-orchestration` |
| Creative sabotage | bored user · weird payloads | `omni-qa-army/chaos-exploration.md` | none |
| Empathetic UX | dignity · silent failure | `omni-qa-army/empathetic-ux.md` | partial: `human-cognition` |
| Design precision | typography · WCAG · anti-patterns | extend `brqosh-ui-design-system/` | strong base |
| Perf / sec / compliance | memory · mitm · CVE · oracles | `omni-qa-army/deep-audit.md` | partial: `security.md` |
| Report excellence | severity · repro · fix blocks | extend `brqosh-review/code-review.md` | partial |
| Arabic / RTL | payloads · bilingual reports | `omni-qa-army/arabic-qa.md` | partial: `brqosh-ui` RTL |
| Launcher UX | PySide6 + React + QWebChannel | `omni-qa-army/launcher-architecture.md` | none |

### 2.2 Ten QA roles → skill routers

| Role | Deliverable (prompt) | Skill home | Council |
|------|---------------------|------------|---------|
| QA Auto | framework + `TEST_FRAMEWORK_ARCHITECTURE.md` | `omni-qa-army/role-automation.md` | C01 + C11 |
| Manual QA | `EXPLORATORY_TEST_SESSIONS.md` | `omni-qa-army/role-exploratory.md` | C11 |
| UX Auditor | `UX_ACCESSIBILITY_COMPLIANCE_REPORT.md` | `brqosh-ui-design-system` + `omni-qa-army/role-accessibility.md` | C03 |
| Pen Tester | `PENTEST_FINDINGS_AND_RISK_ASSESSMENT.md` | `brqosh-review/security.md` + `omni-qa-army/role-pentest.md` | C07 |
| Beta Tester | `BETA_TESTER_USER_JOURNEY_FEEDBACK.md` | `omni-qa-army/role-beta.md` | C03 + C10 |
| Perf Tester | `PERFORMANCE_AND_MEMORY_ANALYSIS_REPORT.md` | `omni-qa-army/role-performance.md` | C11 |
| L10n/I18n | `I18N_AND_L10N_TEST_REPORT.md` | `omni-qa-army/role-i18n.md` | C03 |
| DevOps | `DEVOPS_INTEGRATION_GUIDE.md` | `brqosh-closure` + `omni-qa-army/role-devops.md` | C08 |
| Compatibility | `COMPATIBILITY_TEST_MATRIX.md` | `omni-qa-army/role-compatibility.md` | C03 |
| Tech Copywriter | `MICROCOPY_AND_DOCS_AUDIT.md` | `brqosh-communication` + `omni-qa-army/role-copy.md` | C10 |

### 2.3 Python modules (implementation — not skills)

Prompt may mandate code in future waves. Skills **govern** code when authorized; they do not replace it. **No code in repo** until user release.

| Module | Skill that governs it |
|--------|---------------------|
| `audit_orchestrator.py` | `omni-qa-army/orchestrator-law.md` |
| `vision_brain.py` | `vision-perception.md` § modal patch |
| `human_navigator.py` | `chaos-exploration.md` + `exploratory-mindset.md` |
| `bug_tracker.py` | `omni-qa-army/bug-tracker-spec.md` |
| `report_builder.py` | `report-excellence.md` |
| Launcher (PySide6+React) | `launcher-architecture.md` |

---

## 3. Gap matrix — existing skills vs prompt

| Area | Status | Action |
|------|--------|--------|
| Fable classify / verify | ✅ `fable-omni-core` | reuse |
| Agent orchestration | ✅ `ai-dev-platform/agent-orchestration` | extend for QA parallel |
| Shura governance | ✅ `shura-*` + `ShuraCouncil/` | extend evidence for QA gates |
| Code/security review | ✅ `brqosh-review` | extend desktop/OWASP patterns |
| UI design audit | ✅ `brqosh-ui-design-system` | map WCAG § to enforcement |
| Debug / repro | ✅ `brqosh-debug` | link forensic-resilience |
| Desktop UI automation QA | ❌ missing | **new** `omni-qa-army/` |
| Chronicle diary protocol | ❌ missing | **new** |
| Chaos / exploratory | ❌ missing | **new** |
| Perf/memory profiling law | ❌ missing | **new** |
| Network mitm / CVE scan law | ❌ partial | extend security + new deep-audit |
| Arabic QA payloads | ❌ missing | **new** arabic-qa |
| Launcher stack spec | ❌ missing | **new** |
| Bug tracker file format | ❌ missing | **new** bug-tracker-spec |
| Prompt itself in skills | ⚠️ wrong place | keep `البرومبيت.txt` raw; synthesize to modules |

---

## 4. Leak corpus — what to grep (by skill module)

**Rule:** one file (or section) per deepening session · ≤5 line principle extract · no paste.

### 4.1 Anthropic (highest yield for QA army)

| Leak file | Principle for | Target module |
|-----------|---------------|---------------|
| `bundled-skills/verify.md` | runtime observation not unit-test theatre | `vision-perception.md` · `verify-gates` |
| `bundled-skills/security-review.md` | 3-phase · diff-only · confidence filter | `role-pentest.md` · extend `security.md` |
| `bundled-skills/code-review/report-findings-tool.md` | findings-first · severity · CONFIRMED | `report-excellence.md` |
| `bundled-skills/code-review.md` | 8-angle review | `brqosh-review/code-review.md` |
| `bundled-skills/deep-research/SKILL.md` | 3-vote adversarial verify | `superintelligence-core.md` |
| `bundled-skills/batch.md` | parallel readonly agents | `agent-orchestration.md` |
| `bundled-skills/doctor/SKILL.md` | health-check · disuse evidence | `role-devops.md` |
| `bundled-skills/debug.md` | repro-first | `forensic-resilience.md` |
| `claude-code-2.1.172-fable-5.md` | 12 principles · classify | `fable-omni-core` (exists) |

### 4.2 OpenAI Codex

| Leak file | Principle for | Target module |
|-----------|---------------|---------------|
| `Codex/plan_mode.md` | decision-complete plan before framework wave | `sdlc-pipeline.md` |
| `Codex/gpt-5.5-full.md` | multi_agent_v1 · spawn contract | `agent-orchestration.md` |
| `Codex/control-chrome.md` | localhost verify · screenshot discipline | `vision-perception.md` |
| `Codex/gpt-5.5-full.md` (Lighthouse) | accessibility snapshot > screenshot | `role-accessibility.md` |
| `Codex/old/gpt-5.md` | update_plan · verify steps | `role-automation.md` |

### 4.3 xAI grok-build

| Principle | Target module |
|-----------|---------------|
| verify before complete | `verify-gates.md` |
| spawn_subagent parallel | `agent-orchestration.md` |
| enter_plan_mode / exit | `sdlc-pipeline.md` |
| persistence after single failure | `forensic-resilience.md` |

### 4.4 Microsoft

| Leak file | Principle for | Target module |
|-----------|---------------|---------------|
| `copilot-cli.md` critic agent | devil's advocate · actionable feedback | `chaos-exploration.md` · Shura S02 |
| `vscode-copilot-agent.md` | sub-agent delegate · plan review | `agent-orchestration.md` |
| `github-copilot.md` pr-reviewer | PR-scoped review | `role-pentest.md` |

### 4.5 Perplexity

| Leak file | Principle for | Target module |
|-----------|---------------|---------------|
| `perplexity-computer.md` | wide_research · document-review QA | `role-exploratory.md` |
| `comet-browser-assistant.md` | verify before assume | `superintelligence-core.md` |

### 4.6 Google Gemini

| Principle | Target module |
|-----------|---------------|
| Inquiry vs Directive | `exploratory-mindset.md` — analyze vs execute |
| explore before ask | `sdlc-pipeline.md` |

### 4.7 Misc

| Leak file | Principle for | Target module |
|-----------|---------------|---------------|
| `Misc/hermes.md` | skill_manage · patch skills · memory vs procedures | `skill-studio.md` |
| `Misc/docker-gordon-ai.md` | sub-agent absolute paths · plan before tools | `agent-orchestration.md` |
| `Misc/zed.md` | multi-agent delegation | `agent-orchestration.md` |

### 4.8 Files NOT needed (for this wave)

| Skip | Why |
|------|-----|
| Bulk Anthropic raw persona files | already in fable-omni-core |
| OpenAI chatgpt consumer prompts | not engineering |
| Creative/media leaks | out of scope |
| Accounting/domain leaks | BRQOSH ERP only |

**Estimated grep budget:** 35 files · ~8–12 hours synthesis · 15 skill modules

---

## 5. Recommended new skill package structure

```
.cursor/skills/omni-qa-army/
  SKILL.md                      — router (attach with ai-dev-platform)
  superintelligence-core.md     — oracles · state graph · adversarial
  vision-perception.md          — UIA · OCR · detect_new_surface
  exploratory-mindset.md        — agility · boredom · diary
  chaos-exploration.md          — sabotage · payloads
  empathetic-ux.md              — dignity · silent failure
  deep-audit.md                 — perf · network · CVE · business oracles
  arabic-qa.md                  — RTL · payloads · bilingual reports
  report-excellence.md          — severity · repro · fix template
  bug-tracker-spec.md           — BUG-XXXX format · index
  launcher-architecture.md      — PySide6 · React · QWebChannel
  orchestrator-law.md           — audit loop · CI exit codes
  roles/
    role-automation.md
    role-exploratory.md
    role-accessibility.md
    role-pentest.md
    role-beta.md
    role-performance.md
    role-i18n.md
    role-devops.md
    role-compatibility.md
    role-copy.md
```

**Router link:** `ai-dev-platform/SKILL.md` → add row: Omni-QA / desktop audit → `omni-qa-army`

**Raw prompt link:** `prompts/البرومبيت.txt` or keep repo root — never inline in SKILL.md

---

## 6. Shura council preview (12 seats on this plan)

Simulated consensus on **"proceed with omni-qa-army skill wave"**:

| Seat | Vote | Rationale |
|------|------|-----------|
| S01 Champion | 🟡 | Vision strong — scope enormous |
| S02 Skeptic | 🟡 | Prompt oversells "god-like" — must ground in evidence |
| S03 Quality | 🟡 | Need phased waves · not 20 modules at once |
| S04 Strategy | ✅ | Fits Program Developer platform |
| S05 Security | ✅ | Pentest module aligns |
| S06 Engineer | 🟡 | 20 Python files = CEEF wave · Plan Mode first |
| S07 Market | ✅ | Arabic QA = differentiator |
| S08 Accounting | — | N/A |
| S09 Economist | 🟡 | ROI: skills before code |
| S10 CEO | 🟡 | P0 skills only · defer launcher UI |
| S11 Tax | — | N/A |
| S12 Auditor | 🟡 | No false "framework complete" without runnable code |
| **F00** | **Conditional** | Approve **skill synthesis wave** · defer implementation to P1 |

**Unified verdict:** Proceed with **P0 skill modules (8 files)** + leak grep list · **Plan Mode** for Python framework · Shura re-run after first runnable audit.

---

## 7. Implementation waves (skills only)

### P0 — Foundation (week 1) — **COMPLETE**

| # | Module | Leak grep | Status |
|---|--------|-----------|--------|
| 1 | `omni-qa-army/SKILL.md` | — | DONE |
| 2 | `superintelligence-core.md` | deep-research · shura-intelligence | DONE |
| 3 | `vision-perception.md` | verify.md · control-chrome | DONE |
| 4 | `exploratory-mindset.md` | grok-build · copilot critic | DONE |
| 5 | `report-excellence.md` | report-findings-tool | DONE |
| 6 | `bug-tracker-spec.md` | code-review severity | DONE |
| 7 | `arabic-qa.md` | — (domain) | DONE |
| 8 | Update `leak-corpus-index.yaml` omni-qa family | — | DONE |

### P1b — Framework code — **REMOVED**

`cognitive-qa/` deleted per user directive — skills only until explicit release.

### P1 — Ten roles — **COMPLETE (skills)**

### P2 — Deep engines — **COMPLETE**

| Module | Status |
|--------|--------|
| `chaos-exploration.md` | DONE |
| `empathetic-ux.md` | DONE |
| `deep-audit.md` | DONE |
| `forensic-resilience.md` | DONE |
| `chronicle-protocol.md` | DONE |

### P3 — Launcher law — **COMPLETE**

`launcher-architecture.md` · `orchestrator-law.md` (extended) · `superintelligence-core` Knowledge Constitution · `report-excellence` master template

---

## 8. Anti-patterns (Shura + Fable)

| FORBIDDEN | DO INSTEAD |
|-----------|------------|
| Paste `البرومبيت.txt` into SKILL.md | synthesize to modules |
| Load 279 leaks in one session | grep 35-file list §4 |
| Claim QA army works without runnable code | 4-level closure |
| Arabic in skill bodies | English skills · Arabic in app/reports only |
| Skip Shura after SAVE | agent runs council tool |

---

## 8b. Shura Council seat remap (from البرومبيت.txt)

The prompt defines **10 QA roles** + **Superintelligence Core** + report as "council of experts guided by omniscient AI overseer". Shura seats in **Program Developer** mode now mirror this (skills-only — no Python):

| Seat | Prompt role | Skill home | 3b voter |
|------|-------------|------------|----------|
| S01 | QA Automation Engineer | `role-automation.md` · `orchestrator-law.md` | — |
| S02 | Manual / Exploratory Tester | `role-exploratory.md` · `exploratory-mindset.md` | ✅ |
| S03 | UX / Accessibility Auditor | `role-accessibility.md` · `vision-perception.md` | — |
| S04 | Penetration Tester | `role-pentest.md` · `security.md` | — |
| S05 | Beta Tester | `role-beta.md` | — |
| S06 | Performance Tester | `role-performance.md` | — |
| S07 | L10n/I18n (+ Arabic mastery) | `role-i18n.md` · `arabic-qa.md` | — |
| S08 | DevOps | `role-devops.md` | — |
| S09 | Compatibility QA | `role-compatibility.md` | — |
| S10 | Tech Copywriter | `role-copy.md` | — |
| S11 | Superintelligence Overseer | `superintelligence-core.md` | — |
| S12 | Report / Bug-tracker Auditor | `report-excellence.md` · `bug-tracker-spec.md` | ✅ |
| F00 | Omniscient Facilitator | `orchestrator-law.md` · `shura-intelligence-core.md` | synthesis |

**Conflict chain (prompt mode):** Pen (S04) > Overseer (S11) > Report (S12) > Perf (S06) > i18n (S07) > …

**Code:** `PromptSeatIntelligenceRegistry.cs` · `PromptGovernanceSeatEvaluators.cs`

---

## 9. Verify plan

```powershell
rg "[\u0600-\u06FF]" .cursor/skills/omni-qa-army/
Test-Path cognitive-qa   # MUST be False — prompt code removed
```

---

## 10. Related artifacts

| Artifact | Path |
|----------|------|
| Raw prompt | `البرومبيت.txt` |
| Platform router | `ai-dev-platform/SKILL.md` |
| Leak index | `brqosh-skills-hub/leak-corpus-index.yaml` |
| Shura tool | `ShuraCouncil/` |
| Project manifest | `PROJECT.yaml` |

**Next step:** Foundation phase — skills · Shura · platform only. No prompt code until user release.
