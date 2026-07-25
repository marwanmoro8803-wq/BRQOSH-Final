---
name: ai-dev-platform
description: >-
  AI-Integrated Comprehensive Software Development Platform (Program Developer).
  Routes architecture, multi-agent orchestration, Shura governance, SDLC, skill
  studio, and leak synthesis. Primary skill for this workspace.
---

# AI Dev Platform — Program Developer (Primary)

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)  
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)
> **Vision:** [platform-vision.md](platform-vision.md) · **Architecture:** [architecture.md](architecture.md)  
> **Shura:** [shura-governance.md](shura-governance.md) · **Leaks:** [leak-platform-synthesis.md](leak-platform-synthesis.md)

---

## What this project is

**Program Developer** = end-to-end AI-native software factory:

```
IDE shell + multi-agent orchestrator + skill studio + Shura governance + verified SDLC
```

NOT a chat wrapper. NOT prompt theatre. **Evidence-gated agents** building real software.

---

## Mandatory attach stack (this workspace)

```
1. fable-omni-core              — CLASSIFY · 12 principles
2. human-intelligence-mastery   — research · epistemology (non-pure-code)
3. supreme-programming-mastery — skills-first · SDLC · agent-workflow
4. ai-dev-platform (this)       — platform routing · Shura · orchestration
5. brqosh-agent-protocol        — impact · MEM · verify (reuse harness)
6. domain skill (1â€“3 max)       — per task type below
```

---

## Route by signal

| Signal | Modules | Councils / Shura |
|--------|---------|------------------|
| New feature / module | sdlc-pipeline · architecture | C01 + C02 |
| Multi-agent / parallel work | agent-orchestration · harness | brqosh-agents |
| Create / patch skill | skill-studio · language-enforcement | C12 |
| Architecture fork · launch | shura-governance · deep-cognition | SAVE → Shura tool |
| Bug / test fail | supreme-programming · brqosh-debug | C01 + C11 |
| Code review | brqosh-review | C07 + C11 |
| UI / product surface | brqosh-ui-design-system | C03 |
| Research unknown tech | research-intelligence | C09 |
| Leak deepening | leak-platform-synthesis | grep one file only |
| **Omni-QA / raw prompt** | [prompt-source-law.md](../omni-qa-army/prompt-source-law.md) — **ideas only · implementation frozen** | C12 |
| **Omni-QA Army** | [omni-qa-army/SKILL.md](../omni-qa-army/SKILL.md) · [analysis](omni-qa-prompt-analysis.md) | C11 + C07 + C03 |

---

## Platform pipeline (every substantive task)

```
CLASSIFY → IMPACT → DISCOVER → [PLAN if fork] → ACT → VERIFY → [SHURA if SAVE] → MEM → LEARN
```

| Phase | Module |
|-------|--------|
| CLASSIFY | fable-omni-core |
| IMPACT | impact-before-change.md |
| PLAN | sdlc-pipeline آ§2 · Codex Plan Mode rules |
| ORCHESTRATE | agent-orchestration.md |
| VERIFY | verify-gates.md |
| SHURA | shura-governance.md · `ShuraCouncil/` |
| MEM | PROJECT-MEMORY.yaml |

---

## Immutable platform laws

| ID | Law |
|----|-----|
| P01 | **Skills before code** — scan SKILL.md before Write/StrReplace |
| P02 | **Verify before done** — build + test; no fake closure |
| P03 | **Shura after SAVE** — major analysis → agent runs council tool |
| P04 | **Multi-agent with contract** — subagent gets goal · paths · success criteria |
| P05 | **Leak grep only** — principles into modules; never bulk-load 279 files |
| P06 | **Smallest complete diff** — no drive-by refactor |
| P07 | **English skills** — user UI bilingual at runtime only |

---

## Module map

| File | Purpose |
|------|---------|
| [cognitiveqa-module.md](cognitiveqa-module.md) | CognitiveQa product deep reference |
| [platform-vision.md](platform-vision.md) | Product north star · modules · non-goals |
| [architecture.md](architecture.md) | System layers · integration points |
| [agent-orchestration.md](agent-orchestration.md) | Multi-agent · spawn · parallel · persistence |
| [sdlc-pipeline.md](sdlc-pipeline.md) | Requirements → ship · Plan Mode |
| [skill-studio.md](skill-studio.md) | Author · patch · route skills (Hermes pattern) |
| [shura-governance.md](shura-governance.md) | Council + Shura for product decisions |
| [leak-platform-synthesis.md](leak-platform-synthesis.md) | 279-leak → platform principles |
| [verify-gates.md](verify-gates.md) | Closure levels for platform work |
| [omni-qa-prompt-analysis.md](omni-qa-prompt-analysis.md) | ط§ظ„ط¨ط±ظˆظ…ط¨ظٹطھ.txt · leak map · Shura wave plan |
| [../omni-qa-army/SKILL.md](../omni-qa-army/SKILL.md) | Cognitive desktop QA · P0 skills (P1 code deferred) |

---

## Verify

```powershell
rg "[\u0600-\u06FF]" .cursor/skills/ai-dev-platform/
dotnet build ShuraCouncil/ShuraCouncil.csproj -c Release
```

**Related:** [brqosh-skills-hub/SKILL.md](../brqosh-skills-hub/SKILL.md) · [claude-fable-5/SKILL.md](../claude-fable-5/SKILL.md)

---

## Engineering Roadmap

| | |
|--|--|
| **TRIGGER** | Any work in Program Developer workspace |
| **EXIT** | Correct module loaded · verify plan known |
| **TIME** | 30s |

### Default path

```
SKILL.md → platform-vision → [architecture | sdlc | orchestration] → supreme-programming → verify
```
