# Claude Code 2.1.172 Fable 5 — Section Index

> **Raw:** [../raw/claude-code-2.1.172-fable-5.md](../raw/claude-code-2.1.172-fable-5.md) (1497 lines)  
> **Rule:** grep section heading → read ~50–150 lines only. Never bulk-load.  
> **Synthesis:** [claude-code-cli.md](../supreme-programming-mastery/claude-code-cli.md) · [tools-cursor.md](../supreme-programming-mastery/tools-cursor.md)

## Harness & communication

| Section | Lines ~ | Synthesis module |
|---------|---------|------------------|
| `# Harness` | 7–12 | claude-code-cli.md §2 |
| `# Communicating with the user` | 14–31 | claude-code-cli.md · operating-manual.md |
| `# Session-specific guidance` | 34–36 | skip (Claude Code CLI only) |
| `# Environment` | 38–49 | skip (runtime injected) |
| `# Context management` | 51–62 | claude-code-cli.md §3 · agent-workflow.md |

## Tools (grep `# \`ToolName\``)

| Tool | Lines ~ | Cursor equivalent |
|------|---------|-------------------|
| `Agent` | 80–149 | Task subagent |
| `AskUserQuestion` | 151–287 | AskQuestion |
| `Bash` | 288–334 | Shell |
| `CronCreate/Delete/List` | 335–441 | skip (CLI only) |
| `Edit` | 442–481 | StrReplace |
| `EnterPlanMode` | 482–579 | SwitchMode plan |
| `EnterWorktree` / `ExitWorktree` | 580–756 | git worktree / Task isolation |
| `Monitor` | 757–857 | Await / background shell |
| `NotebookEdit` | 858–910 | EditNotebook |
| `PushNotification` / `RemoteTrigger` / `ScheduleWakeup` | 911–1092 | skip (CLI only) |
| `Skill` | 1093–1135 | Read SKILL.md when attached |
| `TaskCreate` … `TaskUpdate` | 1136–1510 | TodoWrite |
| `WebFetch` | 1511–1541 | WebFetch |
| `WebSearch` | 1542–1581 | WebSearch |
| `Workflow` | 1582–1795 | skip or project scripts |
| `Write` | 1796+ | Write |

## Grep workflow

```powershell
# From repo root
rg -n "^# \`Skill\`" .cursor/skills/raw/claude-code-2.1.172-fable-5.md
# Read with offset/limit around the match
```

1. Check synthesis module first (claude-code-cli.md covers harness + persistence + Skill law).
2. Grep section heading in raw only if module lacks detail.
3. Translate via [cursor-adaptations.md](cursor-adaptations.md) — never copy CLI paths verbatim.

## Relationship to merged_claude.md

| File | Lines | When to grep |
|------|-------|--------------|
| `claude-code-2.1.172-fable-5.md` | 1497 | CLI harness + tool schemas only |
| `merged_claude.md` | 3288 | Same CLI sections + Fable `<skills>` / artifact tags (lines 2280+) |

Prefer `claude-code-2.1.172-fable-5.md` for pure CLI. Use `merged_claude.md` when you need Fable behavior tags embedded after the tool block.
