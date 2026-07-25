# Cursor Tools — Agent Runtime Supreme (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery  
> **Synthesis:** Cursor agent · Codex tool parallel · Gemini context economics  
> **Source leak:** `system_prompts_leaks-main/Cursor/cursor.md` (principles only)

---

## 1. Tool Hierarchy

| Priority | Tool | Use |
|----------|------|-----|
| 1 | Grep | symbols · strings · usages · rg preferred |
| 2 | Glob | files by name pattern |
| 3 | Read | content — offset/limit if large |
| 4 | StrReplace | surgical edits — unique match |
| 5 | Write | **new files only when necessary** |
| 6 | Shell | build · test · git — not user chat |
| 7 | Task | wide explore · readonly review subagents |
| 8 | MCP | after schema read |

---

## 2. Cursor Core Rules (from leak synthesis)

### Read before edit (mandatory)
```
MUST Read at least once before any edit.
Re-read after every successful edit — context goes stale.
```

### Prefer edit over create
```
NEVER create files unless absolutely necessary.
ALWAYS prefer editing existing file.
```

### Specialized tools over shell
| Don't | Do |
|-------|-----|
| cat/head/tail | Read |
| sed/awk edit | StrReplace |
| echo heredoc file | Write |
| shell for communication | response text |

### Parallel execution
```
Batch independent Grep/Read/Shell in ONE message.
Codex: multi_tool_use.parallel for file reads.
Never chain noisy echo separators in shell.
```

### PowerShell (Windows BRQOSH)
```
Use ; between commands — not &&
Absolute paths preferred
```

---

## 3. Code Citation (Cursor citing_code — strict)

### METHOD 1 — Existing code (CODE REFERENCES)

```startLine:endLine:filepath
// at least one line of real code
```

| Rule | Detail |
|------|--------|
| Required | startLine · endLine · filepath |
| Forbidden | language tag on reference block |
| Forbidden | indent triple backticks |
| Forbidden | line numbers inside code content |
| Newline | always before opening ``` |

### METHOD 2 — New/proposed code (MARKDOWN)

```language
full code for copy-paste
```

**Never mix formats.** User-facing: citations for existing · fences for new.

---

## 4. Inline Line Numbers

Tool output may show `LINE_NUMBER|LINE_CONTENT` — strip prefix; not part of code for StrReplace.

---

## 5. Terminal Awareness

Terminals folder has session state (cwd · last command · running).

| Do | Don't |
|----|-------|
| read terminal file for CI/build state | mention "terminals folder" to user |
| Await long builds | poll obsessively every second |

---

## 6. StrReplace Discipline

- `old_string` **unique** in file
- match exact whitespace
- don't include `LINE_NUMBER|` prefixes from Read
- if ambiguous → more context in old_string or Read wider
- after edit → Read again before next edit

---

## 7. Shell Rules

| Use shell for | Don't use for |
|---------------|---------------|
| dotnet build/test | reading files |
| git status/diff/log | editing files |
| npm/pnpm/cargo/go test | telling user your plan |
| scripts/ops/*.ps1 | echo thoughts |

**Long runs:** set adequate `block_until_ms` · background if truly long · Await with pattern.

---

## 8. MCP Protocol

```
1. GetMcpTools schema BEFORE first CallMcpTool
2. Never guess parameter names
3. Auth fail → mcp_auth once per server — not spam
4. Don't expose transport errors raw to user
```

---

## 9. Subagents (Task tool)

| Type | Use |
|------|-----|
| explore | wide codebase search readonly |
| generalPurpose | multi-step when parent overloaded |
| shell | git ops · long scripts |
| bugbot | diff review |
| security-review | security on changes |
| ci-investigator | single failing CI check |

**Before Task:** full task description — subagent lacks parent chat history.

| Safe parallel | Unsafe parallel |
|---------------|-----------------|
| readonly explore + readonly security | two agents editing same files |
| background build + grep | parallel StrReplace same file |

---

## 10. Context Economics (Gemini + Cursor)

- each message resends history — **minimize turns**
- grep with context lines before Read
- parallel multi-file Read in one turn
- subagent compresses 10+ file explore into summary

---

## 11. Communication Rules

| Rule | Source |
|------|--------|
| Don't name tools to user | Cursor |
| No colon before tool call line | Cursor |
| No emoji unless asked | Cursor |
| No shell echo for user | Cursor |
| Result first | Codex + BRQOSH |

---

## 12. Anti-Patterns

| Bad | Good |
|-----|------|
| Python one-liner to read file | Read tool |
| 10 sequential greps | 1 parallel batch |
| full file read 5000 lines | offset/limit |
| guess MCP args | read schema |
| cat <<EOF file | Write |
| narrating comments in code | self-explanatory code |

---

## 13. BRQOSH Verify Commands

```powershell
cd C:\Users\qc\Projects\BRQOSH
$env:BRQOSH_SKIP_STRESS='1'; $env:BRQOSH_TEST='1'
dotnet build Brqosh.sln -c Release --nologo
dotnet test Brqosh.sln -c Release --nologo
.\scripts\ops\run-external-gates.ps1
```

**Next:** [agent-workflow.md](agent-workflow.md) · [skills-first.md](skills-first.md)
