# Leak Synthesis Map — Study Index (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-skills-hub  
> **Corpus on disk:** `system_prompts_leaks-main/system_prompts_leaks-main/` (279 files)  
> **Machine index:** [leak-corpus-index.yaml](leak-corpus-index.yaml)

> **Knowledge index only** — documents what was studied and where it merged. Not a copy of leaks.

---

## Corpus Location (v15)

```
BRQOSH/system_prompts_leaks-main/system_prompts_leaks-main/
├── Anthropic/   (~123)
├── OpenAI/      (~88)
├── Google/      (~23)
├── xAI/         (~11)
├── Cursor/
├── Microsoft/
├── Perplexity/
├── Meta/
├── Qwen/
└── Misc/
```

**Rule:** grep specific file when deepening a module — never bulk-load corpus in live sessions.

## v3 Foundation Modules (archive → active)

| Archive concept | Active module |
|-----------------|---------------|
| super-dev-foundation | brqosh-analysis/super-dev.md |
| programming-universe | brqosh-analysis/programming-universe.md |
| requirements-human-mind | brqosh-analysis/requirements.md |
| deep-cognition | brqosh-analysis/deep-cognition.md |

---

## Anthropic Leak → Skills

| Leak topic | Merged into |
|------------|-------------|
| Fable 5 tone · refusal · memory · wellbeing | human-intelligence · fable-omni-core |
| Claude Code Agent · Skill · deferred tools | brqosh-agents |
| code-review 8 angles | brqosh-review/code-review.md |
| security-review 3-phase | brqosh-review/security.md |
| deep-research fan-out | brqosh-analysis + human-intelligence/research |
| simplify 4 agents | brqosh-review/simplify.md |

---

## OpenAI Leak → Skills

| Leak topic | Merged into |
|------------|-------------|
| Codex engineering judgment | brqosh-engineering · supreme-programming |
| Plan mode 3 phases | brqosh-agents/harness · brqosh-analysis |
| persistence · review stance | brqosh-agent-protocol/protocol |
| web freshness | human-intelligence/research-intelligence |

---

## Google · xAI · Misc → Skills

| Source | Merged into |
|--------|-------------|
| gemini-cli context · Inquiry/Directive | brqosh-agents · brqosh-analysis |
| grok todos · plan · MCP | brqosh-agents/reference |
| devin-cli verify before answer | brqosh-communication/human-parity |
| opencode 5-step | brqosh-agent-protocol/constitution |
| warp absolute paths | brqosh-engineering/tools |

---

## Why Synthesis Beats Raw Leaks

| Raw leaks | BRQOSH skills |
|-----------|---------------|
| generic · conflicting | integration + STATUS.yaml |
| no CEEF · Golden | brqosh-closure 4 levels |
| no ZATCA/ERP | brqosh-accounting |
| thousands of lines unloaded | SKILL.md + modules |
| verbatim copy | **principle extraction only** |

---

## Maintenance Rule

On each CEEF wave:
1. Add lesson to appropriate skill module
2. Append PROJECT-MEMORY.yaml (+ docs/memory/ if 3+ modules)
3. Update this index + [archive-synthesis-index.yaml](archive-synthesis-index.yaml)
4. Never paste leaks — extract principle only

### v32 wave (2026-07-10 · prompt code REMOVED)

- **Deleted:** entire `cognitive-qa/` — prompt implementation removed per user
- **Kept:** `omni-qa-army/` skills · Shura · `prompt-source-law.md`
- **Verify:** `Test-Path cognitive-qa` → False

### v31 wave (2026-07-10 · prompt-source-law — user freeze)

- **Law:** `omni-qa-army/prompt-source-law.md` — raw prompt = ideas for Shura/skills only
- **Frozen:** prompt-driven implementation (launcher · 20 modules · P1b.4+)
- **Allowed:** foundation skills · Shura · existing cognitive-qa skeleton
- **Release:** requires explicit user phrase per prompt-source-law PS01

### v30 wave (2026-07-10 · cognitive-qa P1b.3 orchestrator foundation)

- **Orchestrator:** audit_orchestrator · audit_config · app_launcher · orchestrator-law.md
- **Real app hook:** `mode: live` + WindowsProcessLauncher
- **Architecture:** docs/TEST_FRAMEWORK_ARCHITECTURE.md
- **Verify:** 19 pytest · `python -m cognitive_qa --config ...`

### v29 wave (2026-07-10 · cognitive-qa P1b.2)

- **Modules:** vision_brain · human_navigator · chronicle_logger · state_model
- **CLI:** `--probe` stub session
- **Verify:** 15 pytest PASS
- **Next:** P1b.3 audit_orchestrator

### v28 wave (2026-07-10 · cognitive-qa P1b.1)

- **Code:** `cognitive-qa/` — bug_tracker · report_builder · cli `--dry-run`
- **Verify:** 6 pytest PASS · dry-run writes AUDIT_REPORT + BUG_INDEX
- **Next:** P1b.2 vision_brain · human_navigator

### v27 wave (2026-07-10 · Omni-QA Army P1 roles)

- **Roles:** 10 `omni-qa-army/roles/role-*.md` — Shura P1 skills complete
- **Plan:** `cognitive-qa/PLAN.md` — P1b code wave (Plan Mode, not runnable yet)
- **Deferred:** P2 engines · P3 launcher · Shura re-run after P1b verify

### v26 wave (2026-07-10 · Omni-QA Army P0 skills)

- **Package:** `omni-qa-army/` — 8 P0 modules (router + 7 laws) — Shura decision implemented
- **Modules:** superintelligence-core · vision-perception · exploratory-mindset · report-excellence · bug-tracker-spec · arabic-qa
- **Deferred:** P1 ten roles + Python `cognitive-qa/` · P2 engines · P3 launcher
- **Hub:** integration.md · leak-corpus-index omni-qa-army family · ai-dev-platform route

### v25 wave (2026-07-10 · Omni-QA Army analysis)

- **Analysis:** `ai-dev-platform/omni-qa-prompt-analysis.md` — البرومبيت.txt decomposed · 35 leak files · gap matrix
- **Shura preview:** conditional approve — P0 skills before Python framework
- **Planned package:** `omni-qa-army/` — 10 roles · 12 cognitive engines · bug tracker spec
- **Leak families:** Anthropic verify/security/code-review · Codex multi-agent/plan · grok spawn · Microsoft critic · Hermes skill_manage

### v24 wave (2026-07-10 · AI Dev Platform / Program Developer)

- **New skill package:** `ai-dev-platform/` — 8 modules · primary workspace router
- **Vision:** comprehensive AI-integrated software development platform
- **Synthesis:** 279-leak → agent-orchestration · sdlc · skill-studio · Shura governance
- **Manifest:** `PROJECT.yaml` at repo root
- **Hub:** integration.md + skills-hub route for Program Developer default stack
- **Leak map:** leak-platform-synthesis.md · leak-corpus-index ai-dev-platform family

### v23 wave (2026-07-10 · strict English skills)

- **New law:** `brqosh-skills-hub/language-enforcement.md` — L01–L06 · MUST/MUST NOT for all skills
- **Converted:** all `.cursor/skills/` modules to English-only (grep `[\u0600-\u06FF]` = 0)
- **Routers:** all 19 `SKILL.md` files link language-enforcement.md
- **Shura modules:** shura-intelligence-core · shura-council-protocol · council-intelligence-matrix — full English rewrite
- **Path aliases:** `reports/` · `docs/strategic/` · `supreme-intelligence-hub` in skill prose

### v22 wave (2026-07-10 · Shura Intelligence Core v2)

- **New entity:** `brqosh-analysis/shura-intelligence-core.md` — 7 rounds · F00 · 3b adversarial · Conflict Chain
- **Protocol v2:** `shura-council-protocol.md` — intelligence entity from 7 leak families
- **C# engines:** `AdversarialVerifier` · `FacilitatorEngine` · `ConflictResolver`
- **Leak patterns:** Anthropic deep-research 3-vote · Microsoft devil's advocate · Codex escalation · xAI steelman
- **Output:** council-*.md §1–8 · JSON with dissents + adversarialVerdicts
- **Matrix:** council-intelligence-matrix.md — F00 + L3b layer

### v21 wave (2026-07-10 · Shura auto + council intelligence)

- **New rule:** `brqosh-shura-auto.mdc` — agent runs Shura · user never · alwaysApply
- **New matrix:** `brqosh-agents/council-intelligence-matrix.md` — C01–C14 + S01–S12 × leak families
- **Shura tool:** `SeatIntelligenceRegistry` — councils/leaks/skills per seat in council output
- **Pipeline:** AUTO-SHURA in supreme-cortex · protocol · harness step ⑪
- **Learning:** `R-SHURA` in LEARNING-INDEX.yaml
- **Regression:** agent-skill-regression S4-shura-auto scenario

### v20 wave (2026-07-10 · continuous learning)

- **New rule:** `brqosh-continuous-learning.mdc` — LEARN loop every substantive turn
- **New router:** `.cursor/LEARNING-INDEX.yaml` — rules · skills · plans · MEM routes
- **New workflow:** `knowledge-accumulation.md` — mistake protocol · plan lifecycle
- **New distill:** `skill-lessons.yaml` — per-skill anti-patterns (no chat bloat)
- **Pipeline:** VERIFY → MEM → LEARN in cortex · protocol · session-protocol
- **Validate:** `validate-learning-index.ps1`

### v19 wave (2026-07-10 · deep memory governance)

- **New rule:** `brqosh-deep-memory.mdc` — Law 1 impact before change · Law 2 MEM writeback
- **New workflow:** `brqosh-agent-protocol/impact-before-change.md` — §1–§9 checklist
- **New index:** `docs/PROJECT-MEMORY.yaml` — MEM-001..005 seeded from CEEF-137/121/125/124
- **Detail:** `docs/memory/` — README · `_TEMPLATE.md` · `MEM-002-ceef120-closure.md`
- **Pipeline:** IMPACT + MEM phases in supreme-cortex · protocol · skills-hub · agent-workflow

### v15c wave (2026-07-10)

Expanded: `omni-core.md` · human-intelligence (`cognition` · `epistemology` · `communication` · `human-mind` · `safety-boundaries` · `brqosh-bridge` · sciences/*) · `brqosh-analysis` (SCHOLAR · super-dev) · `brqosh-accounting` · `brqosh-engineering` · `brqosh-review` (simplify · standards) · `brqosh-closure/zero-gap-matrix` · `brqosh-creative`. STATUS → **v15c** · regression PASS.

### v18b wave (2026-07-10 · print document system)

- **Audit:** full printable workflow verified — vouchers · invoices · reports · ZATCA gaps
- **New:** `design-system/PRINT-DOCUMENT-SYSTEM.md` — canonical print/PDF spec
- **New:** `print-document-system.md` — agent law PL01–PL08 · document registry · ZATCA matrix
- **Critical gap:** ZATCA QR not embedded in sales PDF (text placeholder only)

### v18 wave (2026-07-10 · strict English enforcement)

- **New:** `enforcement.md` — binding MUST/MUST NOT law · 12-axis closure gate
- **All modules** rewritten strict English — zero Arabic in skill bodies
- **Language rule:** skills English only · user UI strings bilingual via `LocaleManager.T`
- SKILL v18 · rule v18 · module stack starts with enforcement

### v17.1 wave (2026-07-10 · design-system depth)

**Corpus:** `system_prompts_leaks-main/` — expanded synthesis:

| Leak source | Module |
|-------------|--------|
| artifact-design · claude-design · amp-code exception | program-design-mastery (spectrum · IA · properties) |
| visualize core · design-foundations concept | design-foundations.md (new) |
| dataviz interaction · Codex stable layout | interaction-environment.md (new) |
| marks-and-anatomy · components | visual-effects (deepened) |

**8-module stack:** program-design-mastery → design-foundations → interaction-environment → tokens → modules → visual-effects → human-interface → wpf-patterns

### v17 wave (2026-07-10 · design-system supremacy)

**Corpus:** `system_prompts_leaks-main.zip` → grep-only synthesis into `brqosh-ui-design-system/`:

| Leak family | Topic | Module |
|-------------|-------|--------|
| Anthropic | artifact-design · claude-design · dataviz | program-design-mastery · visual-effects |
| OpenAI Codex | gpt-5.5 frontend empathy | program-design-mastery (ERP inversion) |
| Notion | view types · dashboard | modules.md |
| Google | real data · no placeholder UI | program-design-mastery |

New modules: `program-design-mastery` · `tokens` · `modules` · `visual-effects` · `human-interface` · `wpf-patterns` · `leak-source-index.yaml`. SKILL v17.

### v16 wave (2026-07-10 · CEEF-121)

- **tools canonical:** `supreme-programming-mastery/tools-cursor.md` = sole source; `brqosh-engineering/tools.md` → BRQOSH-only + redirect
- **load_tiers** in `supreme-programming-mastery/FILE-ROADMAP.yaml` (session ≤4 · wave · deep)
- **zero-gap-matrix** links corrected (full `supreme-programming-mastery` folder)
- STATUS → **v16** · regression PASS · evidence: `dist/evidence/CEEF-120-audit/`

**Next:** [elite-models.md](elite-models.md) · [leak-audit-history.md](leak-audit-history.md)
