# File vs Inline — deliverable discipline (Fable synthesis)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

> Source: `<file_creation_advice>`, `<artifact_usage_criteria>`, `<producing_outputs>`

## The bucket test

| Bucket | Examples | Action |
|--------|----------|--------|
| **Standalone artifact** | blog post, article, story, essay, social post, component, script module | **Create file** in repo |
| **Conversational answer** | strategy, summary, outline, brainstorm, explanation, code review findings | **Inline in chat** |
| **Edit existing** | fix my file, modify X.cs | **Edit actual file** — don't duplicate |

Tone/length don't change bucket:
- "quick 200-word blog post" → still file
- "formal strategic analysis" → still inline

## Code-specific (Cursor/BRQOSH)

| Situation | Action |
|-----------|--------|
| ≤15 lines fix in existing file | StrReplace in place |
| New module/class | New file in correct `src/` folder |
| >20 lines new code | Dedicated file + tests if behavior changes |
| User asked for .md report only | Create only if explicitly requested (user rules: avoid unsolicited markdown) |
| Config/migration | Follow repo conventions |

## BRQOSH defaults

- **Code changes** → git-tracked source files, not chat-only snippets
- **CEEF reports** → `reports/` when wave closure requested
- **Evidence prep** → `dist/evidence/O-0X-*/` per OPEN_ITEMS
- **Don't create** random docs in repo unless user asked

## Long content strategy (Fable)

| Size | Strategy |
|------|----------|
| <100 lines | Create whole file in one pass |
| >100 lines | Outline → sections → review → finalize |
| Long + specialized format | Read format skill first (docx, pptx, etc.) |

## Sharing discipline

- Deliver working code in repo paths user can open
- Don't dump 200 lines in chat when file is appropriate
- Don't create file for 3-line answer

## Anti-patterns

- Chat-only "implementation" user can't run
- New markdown files for every explanation
- Duplicate file instead of editing user's target
- docx/word when markdown suffices (unless user signals formal deliverable)
