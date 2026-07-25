# Cloud Code Read Order — Machine Index

> **Purpose:** Ordered file list for Cloud Code / Cursor agents on first session  
> **Language:** English only in all listed agent-law files  
> **Guide:** [docs/CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md)

---

## L0 — Entry (5 min)

| # | Path | Exit criterion |
|---|------|----------------|
| 1 | [README.md](../../README.md) | Know primary product = CognitiveQa |
| 2 | [docs/CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) | Know pipeline + verify commands |
| 3 | [docs/PLATFORM-MAP.md](../../docs/PLATFORM-MAP.md) | Know every top-level folder |
| 4 | [STATUS.yaml](../../STATUS.yaml) | Know phase + test authority |
| 5 | [CognitiveQa/STATUS.yaml](../../CognitiveQa/STATUS.yaml) | Know M32 + 128 tests |

---

## L1 — Skill stack (10 min)

| # | Path | When |
|---|------|------|
| 6 | [fable-omni-core/SKILL.md](../fable-omni-core/SKILL.md) | Every task |
| 7 | [fable-omni-core/operating-manual.md](../fable-omni-core/operating-manual.md) | CLASSIFY |
| 8 | [supreme-programming-mastery/skills-first.md](../supreme-programming-mastery/skills-first.md) | Before any edit |
| 9 | [supreme-programming-mastery/tools-cursor.md](../supreme-programming-mastery/tools-cursor.md) | Tool usage |
| 10 | [ai-dev-platform/SKILL.md](../ai-dev-platform/SKILL.md) | Platform routing |
| 10b | [SKILLS-CLOUD-CODE-INDEX.yaml](SKILLS-CLOUD-CODE-INDEX.yaml) | All 26 routers — machine catalog |
| 11 | [supreme-programming-mastery/claude-code-cli.md](../supreme-programming-mastery/claude-code-cli.md) | Claude Code CLI → Cursor map |
| 12 | [brqosh-agent-protocol/impact-before-change.md](../brqosh-agent-protocol/impact-before-change.md) | Before Write/StrReplace |

---

## L2 — Domain (pick 1–3)

| Signal | Path |
|--------|------|
| Desktop UI / Cloud Code shell | [CognitiveQa/design-system/CLOUD-CODE-SHELL.md](../../CognitiveQa/design-system/CLOUD-CODE-SHELL.md) · [brqosh-ui/SKILL.md](../brqosh-ui/SKILL.md) |
| QA army / auditors | [omni-qa-army/SKILL.md](../omni-qa-army/SKILL.md) · [cloud-code-desktop.md](../omni-qa-army/cloud-code-desktop.md) |
| CognitiveQa product detail | [ai-dev-platform/cognitiveqa-module.md](../ai-dev-platform/cognitiveqa-module.md) |
| Bug / test fail | [brqosh-debug/SKILL.md](../brqosh-debug/SKILL.md) |
| Fable discipline | [claude-fable-5/SKILL.md](../claude-fable-5/SKILL.md) |
| Shura / launch | [ai-dev-platform/shura-governance.md](../ai-dev-platform/shura-governance.md) |
| Leak deepening | [leak-corpus-index.yaml](leak-corpus-index.yaml) — grep one file only |

---

## L3 — Rules (always applied — skim once)

| Path | Concern |
|------|---------|
| [brqosh-supreme-cortex.mdc](../../rules/brqosh-supreme-cortex.mdc) | Master pipeline |
| [brqosh-cloud-code-agent.mdc](../../rules/brqosh-cloud-code-agent.mdc) | This workspace |
| [brqosh-deep-memory.mdc](../../rules/brqosh-deep-memory.mdc) | MEM writeback |
| [brqosh-shura-auto.mdc](../../rules/brqosh-shura-auto.mdc) | Council after SAVE |

---

## L4 — Memory (before assuming "never done")

```powershell
rg "YourTopic|ClassName" docs/PROJECT-MEMORY.yaml
rg "YourTopic" .cursor/skills/brqosh-skills-hub/skill-lessons.yaml
```

---

## Verify read complete

Agent can answer:

1. What is the primary product folder?
2. What command proves tests pass?
3. Where does Shura output go?
4. What is FORBIDDEN for leak corpus?
5. What are the five Cloud Code shell regions?

If any answer is uncertain → re-read L0–L1.
