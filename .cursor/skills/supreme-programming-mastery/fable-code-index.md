# Fable → Supreme Programming Index (complete)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

> Source: `../claude-fable-5.md` — selective read only

## Programming-relevant sections

| Topic | Fable source | Our module |
|-------|--------------|------------|
| **Skills before code** | `<skills>` computer_use | **skills-first.md** |
| **File vs inline** | `<file_creation_advice>` | **file-vs-inline.md** |
| Read before edit / stale context | str_replace docs | agent-workflow.md, tools-cursor.md |
| Code execution environment | bash_tool, create_file | tools-cursor.md |
| When to create files | artifacts, producing_outputs | file-vs-inline.md |
| Malicious code refusal | refusal_handling | security-coding.md |
| Verify with tools not memory | knowledge_cutoff, search | universal-understanding + research-intelligence |
| Step-by-step reasoning | prompting guidance | twelve-axes.md |
| Infer from conversation | ask_user_input | context-continuity.md |
| Copyright in outputs | CRITICAL_COPYRIGHT | copyright-compliance.md |
| Unrecognized library/API | core_search_behaviors | agent-workflow — grep csproj first |
| Long file strategy | producing_outputs | file-vs-inline.md |
| Upload vs in-context | notes_on_user_uploaded_files | tools-cursor.md |

## Omni cross-link

Full synthesis: `../fable-omni-core/operating-manual.md`

## Cursor adaptation (ignore in raw Fable)

- `/mnt/user-data/outputs` → workspace repo paths
- Claude Code MCP XML → Cursor MCP + GetMcpTools
- Artifacts UI → git-tracked source files
- bash_tool → Shell + Read/Write/StrReplace
- present_files → user opens in IDE

## Mandatory load order for code tasks

```
1. fable-omni-core (CLASSIFY)
2. skills-first (scan all relevant SKILL.md)
3. agent-workflow (5-step)
4. languages/<stack>.md
5. brqosh-* if BRQOSH
```

## When to read raw

Grep tag → read section only → never full 3800 lines.
