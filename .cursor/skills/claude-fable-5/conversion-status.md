# Fable 5 Tri-Source — Conversion Status

**Date:** 2026-07-11  
**Status:** ✅ Tri-source skill active

## Raw sources (grep only — never bulk-load)

| File | Lines | Location |
|------|-------|----------|
| `claude-fable-5.md` | 2930 | `.cursor/skills/raw/` |
| `claude-code-2.1.172-fable-5.md` | 1497 | `.cursor/skills/raw/` |
| `merged_claude.md` | 3288 | `.cursor/skills/raw/` |

**Origin:** User-provided copies from `Projects/العقل/` and Desktop `merged_claude.md`.

## Architecture

| Layer | Role |
|-------|------|
| **Raw** | `.cursor/skills/raw/` — grep-by-tag or section heading |
| **Router** | `claude-fable-5/SKILL.md` — attach `/claude-fable-5` |
| **Tri-index** | `tri-source-index.yaml` — which raw for which signal |
| **Module routers** | `modules/01…05/SKILL.md` — domain entry points |
| **Synthesis** | `fable-omni-core` · `human-intelligence-mastery` · `supreme-programming-mastery` |
| **Maps** | `CONVERSION-MAP.yaml` · `source-index.md` · `claude-code-fable-index.md` |

## Domain coverage

| Domain | Module | Primary raw |
|--------|--------|-------------|
| Behavior & safety | `modules/01-behavior` | claude-fable-5.md |
| Memory & prefs | `modules/02-memory` | claude-fable-5.md |
| Search & research | `modules/03-search` | claude-fable-5.md |
| Programming & tools | `modules/04-programming` | claude-fable-5.md + CLI raw |
| Request evaluation | `modules/05-evaluation` | claude-fable-5.md |
| CLI harness | claude-code-cli.md | claude-code-2.1.172-fable-5.md |

## Skipped (claude.ai-only)

`product_information` · Cron tools · PushNotification · `voice_note` · `present_files` · Artifact React sandbox

## Verify

```powershell
.\scripts\ops\validate-tri-source.ps1
.\scripts\ops\validate-fable-conversion.ps1
.\scripts\ops\validate-merged-claude-index.ps1
```

**Wave closed:** CEEF-FABLE-5-WAVE-CLOSE-004 · MEM-164 · plan `cloud-code-knowledge-wave` closed

## Anti-pattern

❌ Paste ~7700 lines into one SKILL.md  
✅ Read synthesis module → grep one section if gap → extend module
