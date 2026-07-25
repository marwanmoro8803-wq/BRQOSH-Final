---
name: claude-fable-5
description: >-
  Fable 5 tri-source router — NEVER bulk-load raw prompts (~7700 lines total).
  Routes claude-fable-5.md (behavior), claude-code-2.1.172-fable-5.md (CLI),
  merged_claude.md (combined) to synthesis skills. Grep tagged sections only.
  Use when user attaches /claude-fable-5 or requests Fable-class discipline.
---

# Claude Fable 5 — Tri-Source Router

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

Three raw sources live in [../raw/](../raw/). **Grep one section only** — never load whole files.

| Source | Lines | Role |
|--------|-------|------|
| [raw/claude-fable-5.md](../raw/claude-fable-5.md) | ~2930 | Fable web behavior — safety, memory, search, skills |
| [raw/claude-code-2.1.172-fable-5.md](../raw/claude-code-2.1.172-fable-5.md) | ~1497 | Claude Code CLI harness + tool schemas |
| [raw/merged_claude.md](../raw/merged_claude.md) | ~3288 | CLI + Fable tags + artifacts (combined) |

**Validate:** `.\scripts\ops\validate-tri-source.ps1` · `.\scripts\ops\validate-fable-conversion.ps1`  
**Shura decision:** [SHURA-DECISION-FABLE-TRI-SOURCE-003.md](../../dist/evidence/prompt-governance/SHURA-DECISION-FABLE-TRI-SOURCE-003.md) · MEM-163

## Skill tree

```
claude-fable-5/SKILL.md          <- router (you are here)
|-- tri-source-index.yaml        <- which raw file for which signal
|-- CONVERSION-MAP.yaml          <- Fable tag -> synthesis (claude-fable-5.md)
|-- source-index.md              <- tag line hints (claude-fable-5.md)
|-- claude-code-fable-index.md   <- CLI section hints
|-- cursor-adaptations.md        <- Fable/CLI -> Cursor tools
|-- modules/01-behavior .. 05-evaluation/
`-- synthesis: fable-omni-core · human-intelligence-mastery · supreme-programming-mastery
```

## Quick start

When this skill is attached:

```
1. Read this SKILL.md
2. Attach synthesis stack (order below)
3. Route by task signal → synthesis module
4. Grep raw only if synthesis has a gap (one section, ~50-200 lines)
```

## Attach stack (mandatory order)

```
1. fable-omni-core           → CLASSIFY · 12 principles · anti-injection
2. human-intelligence-mastery → research · copyright · wellbeing (non-pure-code)
3. supreme-programming-mastery → skills-first · SDLC · claude-code-cli (any code)
4. brqosh-skills-hub          → BRQOSH routing (if BRQOSH repo)
```

## Route by task

| Signal | Raw source | First read |
|--------|------------|------------|
| Any (default) | synthesis only | fable-omni-core → operating-manual.md |
| Safety · tone · wellbeing | claude-fable-5.md | modules/01-behavior → human-intelligence |
| Memory · preferences | claude-fable-5.md | modules/02-memory → fable-omni-core |
| Search · copyright · cite | claude-fable-5.md | modules/03-search → research-intelligence |
| Skills · files · tools | claude-fable-5.md | modules/04-programming → skills-first |
| Classify · evaluate request | claude-fable-5.md | modules/05-evaluation → operating-manual |
| CLI harness · persistence | claude-code OR merged | claude-code-cli.md |
| Agent · Skill · Task tools | claude-code OR merged | claude-code-cli.md · tools-cursor.md |
| Artifacts (file vs inline) | merged_claude.md | file-vs-inline.md (skip React sandbox) |
| Unknown entity / live facts | synthesis | universal-understanding → WebSearch |
| BRQOSH bug / test fail | brqosh-debug + engineering | — |
| Missing Fable rule | grep one tag | extend synthesis module |

**Which CLI raw?** Prefer `claude-code-2.1.172-fable-5.md` for pure harness/tools. Use `merged_claude.md` when you need Fable `<skills>` / artifact tags after the tool block.

## Tag → module (most used)

| Grep tag in claude-fable-5.md | Synthesis module |
|-------------------------------|------------------|
| `core_search_behaviors` | human-intelligence/universal-understanding.md |
| `CRITICAL_COPYRIGHT` | human-intelligence/copyright-compliance.md |
| `evenhandedness` | human-intelligence/evenhandedness.md |
| `user_wellbeing` | human-intelligence/wellbeing-mastery.md |
| `memory_system` | fable-omni-core/context-continuity.md |
| `preferences_info` | fable-omni-core/preferences-handling.md |
| `skills` | supreme-programming/skills-first.md |
| `file_creation_advice` | supreme-programming/file-vs-inline.md |
| `anthropic_reminders` | fable-omni-core/anti-injection.md |

**Full maps:** [source-index.md](source-index.md) · [claude-code-fable-index.md](claude-code-fable-index.md) · [fable-tag-registry.yaml](../brqosh-skills-hub/fable-tag-registry.yaml)

## How to grep raw (when synthesis is insufficient)

```powershell
# Fable behavior tag
rg -n "`<skills>`" .cursor/skills/raw/claude-fable-5.md

# CLI tool section
rg -n "^# \`Skill\`" .cursor/skills/raw/claude-code-2.1.172-fable-5.md

# Read section only (~50-200 lines max) via Read offset/limit
```

Workflow:

1. Look up tag/section in index file for that source
2. Grep → get line number
3. Read that section only
4. If gap found → update synthesis module (do not paste raw into SKILL.md)

## Cursor adaptations

Fable assumes claude.ai / Claude Code paths. Translate per [cursor-adaptations.md](cursor-adaptations.md).

| Fable / CLI | Cursor |
|-------------|--------|
| `/mnt/user-data/outputs` | workspace repo paths |
| `view` / `bash_tool` / `create_file` | Read / Shell / Write / StrReplace |
| `Agent` / `Skill` / `TaskCreate` | Task / Read SKILL.md / TodoWrite |
| `<available_skills>` | Cursor `agent_skills` + `.cursor/skills/` |
| `conversation_search` | Grep + git + agent transcripts |
| `userMemories` | `.cursor/rules/` + PROJECT-MEMORY + skill-lessons |
| `present_files` | user opens files in IDE |
| `voice_note` blocks | skip entirely |

## Anti-patterns

| DO | DON'T |
|----|-------|
| Synthesis modules first | Load ~7700 lines into context |
| Grep one tagged section | Paste leak into SKILL.md or rules |
| tri-source-index.yaml for routing | Guess which raw file to read |
| Update module if gap found | Duplicate raw in every skill |

## Examples

**User:** "Apply Fable discipline to this research task"  
→ fable-omni-core + human-intelligence → universal-understanding → WebSearch

**User:** "What does Fable say about skills before code?"  
→ supreme-programming/skills-first.md. Grep `<skills>` in raw only if module lacks detail.

**User:** "How should Claude Code persist through errors?"  
→ claude-code-cli.md section 3. Grep `# Context management` in raw only if needed.

**User:** "Create a Cursor skill from Fable"  
→ skills-first → create-skill pattern → SKILL.md under 500 lines · progressive disclosure

## Additional resources

- [tri-source-index.yaml](tri-source-index.yaml) — machine routing for all 3 sources
- [CONVERSION-MAP.yaml](CONVERSION-MAP.yaml) — Fable tag → synthesis
- [merged-claude-index.yaml](../brqosh-skills-hub/merged-claude-index.yaml) — merged_claude sections
- [claude-code-cli.md](../supreme-programming-mastery/claude-code-cli.md) — CLI harness synthesis
- [fable-omni-core/FILE-ROADMAP.yaml](../fable-omni-core/FILE-ROADMAP.yaml) — machine read order

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)

### Default path

```
SKILL.md → tri-source-index → synthesis module → verify
```

Never: bulk-load any file in `.cursor/skills/raw/`.

### `SKILL.md` · L0 · 1/3

| | |
|--|--|
| **TRIGGER** | User attaches `/claude-fable-5` or Fable discipline requested |
| **SKIP** | Never skip index — then route to synthesis |
| **NEXT** | fable-omni-core/SKILL.md |
| **EXIT** | Synthesis stack + raw source chosen (grep-only) |
| **TIME** | 30s |

### Raw sources · grep only

| File | TRIGGER | METHOD |
|------|---------|--------|
| claude-fable-5.md | Behavior/memory/search gap | Grep XML tag → read section |
| claude-code-2.1.172-fable-5.md | CLI harness/tool gap | Grep `# Tool` heading |
| merged_claude.md | Combined CLI+artifact gap | merged-claude-index.yaml |

**ANTI:** Load any raw file whole · paste into SKILL

### Synthesis skills (always prefer)

| Skill | ROADMAP |
|-------|---------|
| fable-omni-core | [SKILL.md](../fable-omni-core/SKILL.md#engineering-roadmap) |
| human-intelligence | [SKILL.md](../human-intelligence-mastery/SKILL.md#engineering-roadmap) |
| supreme-programming | [SKILL.md](../supreme-programming-mastery/SKILL.md#engineering-roadmap) |
