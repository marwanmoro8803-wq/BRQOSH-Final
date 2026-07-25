# Fable 5 → Cursor Adaptations

> Translate claude.ai / Claude Code prompt concepts to Cursor Agent environment.  
> **Also read:** [merged-claude-index.yaml](../brqosh-skills-hub/merged-claude-index.yaml) · [claude-code-cli.md](../supreme-programming-mastery/claude-code-cli.md)

## Raw sources (grep only)

| File | Lines | Use |
|------|-------|-----|
| `raw/claude-fable-5.md` | ~2930 | Fable tags → CONVERSION-MAP.yaml |
| `raw/claude-code-2.1.172-fable-5.md` | ~1497 | CLI harness + tools → claude-code-fable-index.md |
| `raw/merged_claude.md` | ~3288 | Combined CLI + Fable tags + artifacts |
| `merged_claude_files.md` (repo root) | ~4428 | Legacy CLI index — prefer `raw/merged_claude.md` |
| `system_prompts_leaks-main/` | 279 files | Family principles → leak-corpus-index.yaml |

**FORBIDDEN:** Bulk-load any raw file in session.

| Fable concept | Cursor equivalent |
|---------------|-------------------|
| `/mnt/user-data/uploads` | User attachments + workspace files |
| `/home/claude` (scratch) | Temp edits in workspace; prefer tracked paths |
| `/mnt/user-data/outputs` | Workspace repo paths (user sees via IDE) |
| `/mnt/skills/public/<name>/SKILL.md` | `.cursor/skills/<name>/SKILL.md` or `~/.cursor/skills/` |
| `/mnt/skills/user/` | User personal skills `~/.cursor/skills/` |
| Read-only mounts | Respect git-tracked source; don't edit without intent |

## Tools

| Fable tool | Cursor tool |
|------------|-------------|
| `view` | Read (use offset/limit for large files) |
| `bash_tool` / bash | Shell |
| `create_file` | Write |
| `str_replace` | StrReplace |
| `present_files` | User opens files in IDE (no equivalent) |
| `web_search` | WebSearch |
| `web_fetch` | WebFetch |
| `conversation_search` | Grep + agent transcripts + git log |
| `memory_user_edits` | `.cursor/rules/`, user rules, STATUS.yaml |
| MCP in artifacts | GetMcpTools + CallMcpTool |
| `tool_search` (deferred) | GetMcpTools catalog / pattern search |
| `visualize:show_widget` | Mermaid in prose, or Canvas skill for rich UI |
| `AskUserQuestion` | AskQuestion tool (blocked decisions only) |
| `EnterPlanMode` | SwitchMode plan · brqosh-analysis |
| `Skill` (CLI) | Read SKILL.md when user attaches `/skill` |
| `TaskCreate` | TodoWrite |
| `Agent` | Task subagent (explore · shell · generalPurpose) |

## Skills system

Fable `<skills>` block: read every plausibly-relevant SKILL.md before code or file creation.

Cursor equivalent:

1. Check `agent_skills` list in system prompt
2. Grep `.cursor/skills/` for task-relevant skills
3. Read SKILL.md before implementing
4. Follow progressive disclosure (reference files on demand)

Key Fable rule preserved: **skills encode environment-specific constraints** — skipping the skill read lowers output quality.

## Memory & context

| Fable | Cursor |
|-------|--------|
| `userMemories` / memory system | `.cursor/rules/` · PROJECT-MEMORY.yaml · skill-lessons.yaml |
| Learning from session | LEARN loop — knowledge-accumulation.md · LEARNING-INDEX.yaml |
| Forbidden memory phrases | Don't narrate "I remember from our past chats" |
| Personalization | Apply rules naturally without citing storage |
| Long conversation | Prior messages in context; rules persist via `.cursor/rules/` |

## Search & citations

| Fable | Cursor |
|-------|--------|
| Search before answering current facts | WebSearch for post-cutoff / binary events |
| `<antml:cite>` tags | Markdown links to sources (user-facing) |
| CRITICAL_COPYRIGHT | human-intelligence/copyright-compliance.md |
| 2+ sources for unknown entities | universal-understanding protocol |

## Skip in Cursor (claude.ai only)

- `product_information` ads/settings (search docs.anthropic.com if asked)
- `voice_note` blocks
- Artifact React/Tailwind sandbox constraints
- `anthropic_api_in_artifacts` (Claude-in-Claude)
- `mcp_app_suggestions` / connector UI flows
- `network_configuration` / egress proxy headers
- Bundled skill paths under `/mnt/skills/public/` (use Cursor skill paths instead)
- `<userPreferences>` placeholder block

## BRQOSH-specific stack

When in BRQOSH repo, after Fable classify:

```
fable-omni-core → brqosh-agent-protocol → supreme-programming-mastery → domain skill
```

Verify: `dotnet build` + `dotnet test` per STATUS.yaml.  
Learn: LEARN loop per `brqosh-continuous-learning.mdc` after verify.
