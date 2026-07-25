# Fable 5 Source Index — Complete Tag Map

> **Raw:** [../raw/claude-fable-5.md](../raw/claude-fable-5.md) (~2930 lines)  
> **Rule:** grep tag → read section only. Never load entire file.  
> **Tri-source:** [tri-source-index.yaml](tri-source-index.yaml)

## Behavior & safety (claude_behavior)

| Tag | Line ~ | Synthesis module |
|-----|--------|------------------|
| `product_information` | 13–37 | skip (Anthropic product — search docs if asked) |
| `refusal_handling` | 39–72 | human-intelligence/safety-boundaries.md |
| `critical_child_safety_instructions` | 43–56 | human-intelligence/safety-boundaries.md |
| `legal_and_financial_advice` | 74–78 | human-intelligence/epistemology.md |
| `tone_and_formatting` | 80–106 | human-intelligence/communication.md |
| `lists_and_bullets` | 94–104 | human-intelligence/communication.md |
| `user_wellbeing` | 108–142 | human-intelligence/wellbeing-mastery.md |
| `anthropic_reminders` | 144–152 | fable-omni-core/anti-injection.md |
| `evenhandedness` | 154–168 | human-intelligence/evenhandedness.md |
| `responding_to_mistakes_and_criticism` | 170–178 | human-intelligence/mistakes-accountability.md |
| `knowledge_cutoff` | 180–189 | human-intelligence/research-intelligence.md |

## Memory & preferences

| Tag | Line ~ | Synthesis module |
|-----|--------|------------------|
| `memory_system` | 193–951 | fable-omni-core/context-continuity.md |
| `memory_overview` | 195–203 | context-continuity.md |
| `memory_application_instructions` | 205–249 | context-continuity.md |
| `forbidden_memory_phrases` | 251–276 | context-continuity.md |
| `appropriate_boundaries_re_memory` | 278–284 | context-continuity.md |
| `memory_application_examples` | 286–690 | context-continuity.md |
| `persistent_storage_for_artifacts` | 692–766 | supreme-programming/file-vs-inline.md |
| `mcp_app_suggestions` | 768–817 | skip (claude.ai connectors) |
| `past_chats_tools` | 819–844 | fable-omni-core/context-continuity.md |
| `preferences_info` | 846–933 | fable-omni-core/preferences-handling.md |
| `current_memory_scope` | 935–941 | context-continuity.md |
| `important_safety_reminders` | 943–949 | fable-omni-core/anti-injection.md |
| `memory_user_edits_tool_guide` | 953–1025 | context-continuity.md |

## Computer use & skills (programming)

| Tag | Line ~ | Synthesis module |
|-----|--------|------------------|
| `computer_use` | 1027–1196 | supreme-programming/tools-cursor.md |
| `skills` | 1029–1047 | supreme-programming/skills-first.md |
| `file_creation_advice` | 1049–1063 | supreme-programming/file-vs-inline.md |
| `high_level_computer_use_explanation` | 1065–1072 | supreme-programming/tools-cursor.md |
| `file_handling_rules` | 1074–1089 | supreme-programming/tools-cursor.md |
| `notes_on_user_uploaded_files` | 1081–1087 | supreme-programming/tools-cursor.md |
| `producing_outputs` | 1091–1098 | supreme-programming/file-vs-inline.md |
| `sharing_files` | 1100–1115 | skip (present_files — Cursor: user opens files) |
| `artifact_usage_criteria` | 1117–1168 | supreme-programming/file-vs-inline.md |
| `package_management` | 1170–1177 | supreme-programming/tools-cursor.md |
| `additional_skills_reminder` | 1190–1194 | supreme-programming/skills-first.md |
| `request_evaluation_checklist` | 1198–1222 | fable-omni-core/operating-manual.md |

## Search & research

| Tag | Line ~ | Synthesis module |
|-----|--------|------------------|
| `search_instructions` | 1275–1612 | human-intelligence/research-intelligence.md |
| `core_search_behaviors` | 1286–1309 | human-intelligence/universal-understanding.md |
| `search_usage_guidelines` | 1311–1335 | research-intelligence.md |
| `CRITICAL_COPYRIGHT_COMPLIANCE` | 1337–1462 | human-intelligence/copyright-compliance.md |
| `harmful_content_safety` | 1581–1592 | research-intelligence.md |
| `using_image_search_tool` | 1614–1688 | research-intelligence.md |
| `citation_instructions` | 3715–3736 | human-intelligence/research-intelligence.md |

## Tool definitions (claude.ai — skip or adapt)

| Tag | Line ~ | Cursor equivalent |
|-----|--------|-------------------|
| Tool YAML blocks | ~1700–3392 | Cursor built-in tools (Shell, Grep, Read, etc.) |
| `anthropic_api_in_artifacts` | 3407–3713 | skip (Artifact API — not Cursor) |
| `tool_usage` / `mcp_servers` | 3461–3538 | GetMcpTools + CallMcpTool |
| `web_search_tool` | 3540–3563 | WebSearch / WebFetch |
| `handling_files` | 3584–3634 | Read tool + user attachments |
| `context_window_management` | 3636–3685 | conversation history (automatic) |
| `error_handling` | 3687–3703 | try/catch in generated code |
| `network_configuration` | 3795–3803 | user environment |
| `filesystem_configuration` | 3805–3816 | workspace read-only paths |

## Built-in skill listings (Fable paths)

Lines ~3740–3791 list Anthropic bundled skills at `/mnt/skills/public/`. In Cursor, map to:

| Fable skill | Cursor equivalent |
|-------------|-------------------|
| docx / pdf / pptx / xlsx | project or personal skills in `.cursor/skills/` |
| frontend-design | brqosh-ui-design-system (BRQOSH) or create-skill |
| file-reading / pdf-reading | Read tool + domain skills |
| skill-creator | create-skill (Cursor built-in) |
| learn | human-intelligence-mastery teaching modules |

## Priority read order (new agents)

1. [fable-omni-core/operating-manual.md](../fable-omni-core/operating-manual.md)
2. [universal-understanding.md](../human-intelligence-mastery/universal-understanding.md)
3. [research-intelligence.md](../human-intelligence-mastery/research-intelligence.md) + [copyright-compliance.md](../human-intelligence-mastery/copyright-compliance.md)
4. [skills-first.md](../supreme-programming-mastery/skills-first.md) (if coding)
5. Domain module as needed

## When to read raw

Only when synthesis module is insufficient:

1. Grep exact tag in claude-fable-5.md
2. Read that section only (~50–200 lines max)
3. Extract new rule → update synthesis module
4. Register tag in [fable-tag-registry.yaml](../brqosh-skills-hub/fable-tag-registry.yaml) if new
