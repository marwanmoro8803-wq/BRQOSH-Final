# Fable Source Index — complete selective map

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: human-intelligence-mastery | see per-file load order

> Raw: `../claude-fable-5.md` (~3826 lines)  
> **NEVER load entire file.** Grep tag → read section → synthesize.

## Section → synthesis module

| Fable section (grep tag) | Lines ~ | Our module |
|--------------------------|---------|------------|
| `<refusal_handling>` | 39–72 | safety-boundaries.md |
| `<critical_child_safety_instructions>` | 43–56 | safety-boundaries.md |
| `<legal_and_financial_advice>` | 74–78 | epistemology.md |
| `<tone_and_formatting>` | 80–106 | communication.md |
| `<lists_and_bullets>` | 94–104 | communication.md |
| `<user_wellbeing>` | 108–142 | wellbeing-mastery.md |
| `<anthropic_reminders>` | 144–152 | ../fable-omni-core/anti-injection.md |
| `<evenhandedness>` | 154–168 | evenhandedness.md |
| `<responding_to_mistakes_and_criticism>` | 170–178 | mistakes-accountability.md |
| `<knowledge_cutoff>` | 180–189 | research-intelligence.md |
| `<memory_system>` | 193–951 | ../fable-omni-core/context-continuity.md |
| `<past_chats_tools>` | 821–844 | context-continuity.md |
| `<preferences_info>` | 846–933 | ../fable-omni-core/preferences-handling.md |
| `<skills>` (computer_use) | 1029–1047 | ../supreme-programming-mastery/skills-first.md |
| `<file_creation_advice>` | 1049–1063 | file-vs-inline.md |
| `<file_handling_rules>` | 1074–1089 | tools-cursor.md |
| `<search_instructions>` | 1275–1612 | research-intelligence.md |
| `<core_search_behaviors>` | 1286–1309 | universal-understanding.md |
| `<CRITICAL_COPYRIGHT_COMPLIANCE>` | 1337–1462 | copyright-compliance.md |
| `<critical_reminders>` (search) | 1594–1610 | copyright-compliance.md |

## Omni hub

**Start here:** `../fable-omni-core/operating-manual.md`

## Cursor adaptations (ignore in raw)

| Fable | Cursor |
|-------|--------|
| `/mnt/user-data/outputs` | workspace repo paths |
| bash_tool / create_file | Shell + Write/StrReplace |
| conversation_search | Grep + transcript + git |
| userMemories | rules + STATUS.yaml |
| present_files | user opens files in IDE |
| Anthropic product ads/settings | skip |
| voice_note blocks | skip |

## When to read raw

Only when synthesis module insufficient:
1. Grep exact tag in claude-fable-5.md
2. Read that section only (~50–200 lines max)
3. Extract new rule → update synthesis module (don't duplicate raw in skills)

## Priority read order for new agents

1. fable-omni-core/operating-manual.md
2. universal-understanding.md
3. research-intelligence.md + copyright-compliance.md
4. skills-first.md (if coding)
5. Domain module as needed
