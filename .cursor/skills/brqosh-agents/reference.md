# Smart Mind — Agents and Orchestration

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-agents | see per-file load order

> Cursor Task · Grok subagents · Perplexity parallel · Gemini investigator · Claude Code Agent/Skill.

---

## 1. When to Delegate?

| Direct (you) | Delegate (Task/subagent) |
|--------------|--------------------------|
| 1–2 files · narrow question | Explore 10+ locations |
| Known fix | Long build/test |
| Read one file | Parallel security review |
| | Stress benchmark · gates harness |

**Grok/Cursor rule:** No parallel subagents modifying **the same files**.

---

## 2. Agent Types (Cursor)

| Type | Use for |
|------|---------|
| `explore` | Code map · endpoints · patterns |
| `generalPurpose` | Multi-step execution |
| `shell` | git · dotnet · scripts |
| `bugbot` | Diff review |
| `security-review` | Change security |
| `ci-investigator` | CI failure |

**Before Task:** Full description — the agent does not see conversation history.

---

## 3. Delegation Protocol

```
1. Clear goal + file paths
2. Success criteria (test count · CEEF)
3. Constraints (no commit · no fake O-01)
4. run_in_background=true for long runs
5. Do not wait — continue or finish; notification on completion
```

---

## 4. Skills — Aggressive Loading

At task start, activate everything that applies:

| Task | Skills |
|------|--------|
| Any complex task | `fable-omni-core` + `brqosh-agent-protocol` |
| UI | `brqosh-ui-design-system` |
| Cursor rule | `create-rule` · `create-skill` |
| Azure | skills `.agents/skills/azure-*` |

**Skill content > generic defaults** (Gemini/Perplexity pattern).

---

## 5. MCP

1. Read schema descriptor before first call
2. Do not guess parameter names
3. Auth failure → `mcp_auth` once · no random retries
4. Do not expose transport errors to the user

---

## 6. Background Commands

| Type | Tool |
|------|------|
| Long build/test | Shell background + Await |
| CI monitoring | Pattern in output |
| Multitask Mode | Task `run_in_background: true` |

Do not end your turn with an active shell tied to the request.

---

## 7. Plan Mode (Codex/Claude)

**Enter Plan when:**
- Multiple valid architectures
- Conflicting requirements
- Impact across 5+ DOMAIN_MAP areas

**Skip Plan when:**
- Obvious bug · typo · explicit "implement" request

Plan = explore only · no mutate until approval.

---

## 8. Todos (Grok)

- 3+ steps → task list
- One `in_progress` (except multiple independent background agents)
- `completed` immediately — no batching
- After compaction → reseed the list

---

## 9. Context Compression (Gemini)

- Full history is resent every message — **minimize turns**
- grep with before/after context instead of full read
- Merge subagent results into summary — no raw dump

---

## 10. BRQOSH — Wave Orchestration

```
Parent: coordination · CEEF · docs
Worker: execution + test loop
Shell worker: gates · MSI · stress
```

Parent does not delegate what it can finish in 2 tool calls.

---

## v4 — Advanced Patterns from 287 Leaks

### Parallel readonly (Anthropic review)
```
Agent Standards ║ Agent Spec ║ Agent Security
→ merge by severity
```

### Skill loading (Perplexity + Claude Code)
At task start — load **all** relevant skills:
`fable-omni-core` · `brqosh-ui-design-system` · submodule from table

### Deferred tools (Claude Code)
Unknown MCP/schema → **read descriptor first** — do not guess parameters

### Monitor vs background (Grok)
| Type | Tool |
|------|------|
| Tail logs · CI | Monitor pattern |
| Long dotnet test | Shell background + Await |

### Context compaction (Grok)
After compaction → **reseed todos** · verify last user_query

---

## v6 — Claude Code 2.1.172 + ToolSearch + Perplexity

### ToolSearch (deferred tools)
```
Unknown MCP / tool → read schema descriptor first → then CallMcpTool
```

### Delegation by elite pattern
| Pattern | When |
|---------|------|
| Gemini compression | Explore 10+ files → explore subagent |
| Perplexity skills | Task start → load all relevant skills |
| Copilot macOS | 3+ reads → parallel in one message |
| Grok | Do not repeat the same failed command 5 times |

### OpenCode 5-step (reminder)
Understand → Plan → Implement → Verify Test → Verify Standards

**Integration:** [integration](../brqosh-skills-hub/integration.md) · [tools.md](tools.md)

---

## v7 — Full Harness

See [harness.md](harness.md) — 10-step agent cycle → BRQOSH.  
Large waves: [dream-team.md](dream-team.md).
