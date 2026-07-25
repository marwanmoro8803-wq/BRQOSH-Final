---
name: fable-memory
description: >-
  Fable 5 memory & preferences domain — context continuity, PROJECT-MEMORY,
  preferences handling. Synthesized from claude-fable-5.md memory_system tags.
---

# Fable 5 — Memory & Preferences

> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../../../docs/CLOUD-CODE-AGENT-GUIDE.md)

> **Map:** [../../CONVERSION-MAP.yaml](../../CONVERSION-MAP.yaml) · domain `memory`

## Read order

| Topic | Module |
|-------|--------|
| Memory · continuity · forbidden phrases | [fable-omni-core/context-continuity.md](../../../fable-omni-core/context-continuity.md) |
| User preferences | [fable-omni-core/preferences-handling.md](../../../fable-omni-core/preferences-handling.md) |
| Artifacts on disk | [supreme-programming-mastery/file-vs-inline.md](../../../supreme-programming-mastery/file-vs-inline.md) |
| BRQOSH MEM law | [brqosh-deep-memory.mdc](../../../../rules/brqosh-deep-memory.mdc) |

## Covered raw tags

`memory_system` · `memory_overview` · `memory_application_instructions` · `forbidden_memory_phrases` · `appropriate_boundaries_re_memory` · `memory_application_examples` · `persistent_storage_for_artifacts` · `past_chats_tools` · `preferences_info` · `preferences_examples` · `current_memory_scope` · `important_safety_reminders` · `memory_user_edits_tool_guide`

## Cursor mapping

| Fable | Cursor |
|-------|--------|
| userMemories | `.cursor/rules/` · `PROJECT-MEMORY.yaml` · `skill-lessons.yaml` |
| conversation_search | Grep · git · agent transcripts |
| memory_user_edits | StrReplace on rules/MEM — user-approved |
