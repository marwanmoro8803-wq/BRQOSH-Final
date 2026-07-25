---
name: brqosh-skills-hub
description: >-
  Catalog of BRQOSH agent skills (v12). Routes tasks to specialized skills.
  Use when unsure which skill to attach, for project status, or CEEF overview.
  Read STATUS.yaml for test counts and phase.
---

# BRQOSH Skills Hub

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](language-enforcement.md)  
> **Status:** [STATUS.yaml](STATUS.yaml) · **Master:** [SKILL.md](SKILL.md#engineering-roadmap) · **Spec:** [ROADMAP-SPEC.md](ROADMAP-SPEC.md)

## Hub modules (archive merged)

| Module | Purpose |
|--------|---------|
| [integration.md](integration.md) | L0–L6 routing |
| [archive-synthesis-index.yaml](archive-synthesis-index.yaml) | Full archive → skill map |
| [elite-models.md](elite-models.md) | Elite family patterns |
| [leak-corpus-index.yaml](leak-corpus-index.yaml) | 279 leak files → skill routing |
| [merged-claude-index.yaml](merged-claude-index.yaml) | merged_claude.md sections → synthesis |
| [CLOUD-CODE-READ-ORDER.md](CLOUD-CODE-READ-ORDER.md) | Cloud Code agent read order |
| [SKILLS-CLOUD-CODE-INDEX.yaml](SKILLS-CLOUD-CODE-INDEX.yaml) | All 26 routers — machine catalog |
| [leak-synthesis.md](leak-synthesis.md) | Principle merge map |
| [leak-audit-history.md](leak-audit-history.md) | v5–v8 audit waves |

## Attach by task

| Task | Skill |
|------|-------|
| **Omni supreme (any topic)** | `fable-omni-core` → operating-manual.md |
| Any session / routing | `fable-omni-core` + `brqosh-agent-protocol` + `supreme-programming-mastery` |
| Bug / test fail | `supreme-programming-mastery` + `brqosh-debug` |
| Code / .NET / WPF logic | `supreme-programming-mastery` + `brqosh-engineering` |
| UI / RTL / WPF screens | `brqosh-ui` (+ `brqosh-ui-design-system`) |
| Accounting / ZATCA / posting | `brqosh-accounting` |
| CEEF / gates / O-01..O-05 | `brqosh-closure` |
| Code or security review | `brqosh-review` |
| Architecture / requirements | `brqosh-analysis` |
| Explain / teach / bilingual | `brqosh-communication` |
| Subagents / parallel explore | `brqosh-agents` |
| Creative / games / media | `brqosh-creative` |
| Non-BRQOSH repo | `universal-coding` |
| **Human intelligence / Fable cognition** | `human-intelligence-mastery` |
| **Supreme programming** | `supreme-programming-mastery` |
| **AI dev platform (this project)** | `ai-dev-platform` |
| Supreme hub (alias) | `brqosh-skills-hub` — `supreme-intelligence-hub` redirect only (v14) |

## Workflow

```
TRIAGE -> IMPACT -> pick 1-3 skills -> ACT -> VERIFY -> MEM -> LEARN -> REPORT
```

**Memory:** [docs/PROJECT-MEMORY.yaml](../../docs/PROJECT-MEMORY.yaml) · [LEARNING-INDEX.yaml](../../LEARNING-INDEX.yaml) · [skill-lessons.yaml](skill-lessons.yaml)

## Scripts

```powershell
.\scripts\ops\sync-agent-status.ps1
.\scripts\ops\agent-skill-regression.ps1
```

## Archive (removed v14 final)

All `supreme-intelligence-hub/archive/` content merged into domain skills. Index: [archive-synthesis-index.yaml](archive-synthesis-index.yaml) · redirect: supreme-intelligence-hub redirect SKILL (see [integration.md](integration.md))

---

## Engineering Roadmap

> **Spec:** [ROADMAP-SPEC.md](ROADMAP-SPEC.md) · **Fable tags:** [fable-tag-registry.yaml](fable-tag-registry.yaml)  
> **Source skill:** [../claude-fable-5/SKILL.md](../claude-fable-5/SKILL.md) · **Raw:** `../claude-fable-5.md` (grep only)

## Default session path

```
① CLASSIFY     → fable-omni-core/FILE-ROADMAP.yaml (P-OMNI)
② MIND         → human-intelligence/universal-understanding (if not pure code)
③ IMPACT       → impact-before-change · grep PROJECT-MEMORY (before code)
④ CODE         → supreme-programming/skills-first (if code)
⑤ BRQOSH       → brqosh-agent-protocol + domain skill
⑥ VERIFY       → STATUS.yaml / dotnet test / 2+ sources
⑦ MEM          → PROJECT-MEMORY.yaml after verify
⑧ LEARN        → knowledge-accumulation · skill-lessons · rules/plans
```

## Skills — machine + human roadmaps

| Skill | SKILL (roadmap) | FILE-ROADMAP | Files |
|-------|---------|--------------|-------|
| fable-omni-core | [SKILL.md](../fable-omni-core/SKILL.md#engineering-roadmap) | [FILE-ROADMAP.yaml](../fable-omni-core/FILE-ROADMAP.yaml) | 7 |
| human-intelligence | [SKILL.md](../human-intelligence-mastery/SKILL.md#engineering-roadmap) | [FILE-ROADMAP.yaml](../human-intelligence-mastery/FILE-ROADMAP.yaml) | 21 |
| supreme-programming | [SKILL.md](../supreme-programming-mastery/SKILL.md#engineering-roadmap) | [FILE-ROADMAP.yaml](../supreme-programming-mastery/FILE-ROADMAP.yaml) | 31 |
| brqosh-agent-protocol | [SKILL.md](../brqosh-agent-protocol/SKILL.md#engineering-roadmap) | — | 3 |
| brqosh-engineering | [SKILL.md](../brqosh-engineering/SKILL.md#engineering-roadmap) | — | 5 |
| brqosh-debug | [SKILL.md](../brqosh-debug/SKILL.md#engineering-roadmap) | — | 2 |
| brqosh-accounting | [SKILL.md](../brqosh-accounting/SKILL.md#engineering-roadmap) | — | 2 |
| brqosh-ui | [SKILL.md](../brqosh-ui/SKILL.md#engineering-roadmap) | — | 2 |
| brqosh-ui-design-system | [SKILL.md](../brqosh-ui-design-system/SKILL.md#engineering-roadmap) | — | 1 |
| brqosh-closure | [SKILL.md](../brqosh-closure/SKILL.md#engineering-roadmap) | — | 2 |
| brqosh-review | [SKILL.md](../brqosh-review/SKILL.md#engineering-roadmap) | — | 5 |
| brqosh-analysis | [SKILL.md](../brqosh-analysis/SKILL.md#engineering-roadmap) | — | 6 |
| brqosh-communication | [SKILL.md](../brqosh-communication/SKILL.md#engineering-roadmap) | — | 3 |
| brqosh-agents | [SKILL.md](../brqosh-agents/SKILL.md#engineering-roadmap) | — | 4 |
| brqosh-creative | [SKILL.md](../brqosh-creative/SKILL.md#engineering-roadmap) | — | 2 |
| universal-coding | [SKILL.md](../universal-coding/SKILL.md#engineering-roadmap) | — | 2 |
| supreme-intelligence-hub | redirect SKILL — see [integration.md](integration.md) | — | alias |
| claude-fable-5 | [SKILL.md](../claude-fable-5/SKILL.md#engineering-roadmap) | [tri-source-index.yaml](../claude-fable-5/tri-source-index.yaml) | tri-source router |
| ai-dev-platform | [SKILL.md](../ai-dev-platform/SKILL.md#engineering-roadmap) | — | 8 |

## Cloud Code agent entry

| Document | Purpose |
|----------|---------|
| [README.md](../../README.md) | Workspace entry |
| [docs/CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) | Full agent onboarding |
| [CLOUD-CODE-READ-ORDER.md](CLOUD-CODE-READ-ORDER.md) | Ordered read list |
| [ai-dev-platform/cognitiveqa-module.md](../ai-dev-platform/cognitiveqa-module.md) | Product deep reference |

**Rule:** [brqosh-cloud-code-agent.mdc](../../rules/brqosh-cloud-code-agent.mdc)

## Standard paths (registry)

| ID | Use when |
|----|----------|
| `P-OMNI` | Any topic — classify first |
| `P-CODE` | Any code |
| `P-BRQOSH-BUG` | test/build failure |
| `P-RESEARCH` | current facts / unknown entity |

## Scenarios (with exit criteria)

| Request | Path | VERIFY |
|---------|------|--------|
| Fix failing test | P-BRQOSH-BUG | dotnet test PASS + STATUS count |
| What is X? (unknown) | P-RESEARCH | WebSearch before answer |
| Close CEEF wave | brqosh-closure | 4 levels · no partial closure |
| Live ZATCA OTP | — | external · dist/evidence |
| Financial PR review | brqosh-review | security → code-review + accounting |

## Fable rule (non-negotiable)

| DO | DON'T |
|----|-------|
| FILE-ROADMAP.yaml for order | Guess read order |
| Grep tag in claude-fable-5.md | Load 3826 lines |
| `validate-fable-conversion.ps1` | Assume map complete without check |
| Synthesis modules | Paste leak into SKILL.md |

