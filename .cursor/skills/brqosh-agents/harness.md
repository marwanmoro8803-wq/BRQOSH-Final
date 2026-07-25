# Agent Harness Elite — Full Cycle Supreme (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-agents  
> **Synthesis:** Claude Code 2.1 harness · Perplexity todos · Codex persistence · OpenCode 5-step  
> **Corpus:** `Anthropic/Claude Code/claude-code-2.1.172-opus-4.8.md`

> **TRIGGER:** multi-phase delivery · CEEF wave · background build · quality competition  
> **SKIP:** trivial one-line fix with obvious test

---

## 1. Skill Equivalents (Claude Code → BRQOSH)

| Claude pattern | BRQOSH module |
|----------------|---------------|
| `/code-review` · xhigh | brqosh-review/code-review.md |
| `/simplify` | brqosh-review/simplify.md |
| `/verify` | agent-workflow + engineering |
| `/deep-research` | research-intelligence + deep-cognition |
| `debug` | brqosh-debug/reference.md |
| Explore agent | Task explore readonly |
| Plan agent | Plan Mode + brqosh-analysis |
| `loop` / background | Shell background + Await |
| `batch` | parallel readonly subagents |
| `compact` | session-protocol §5 |
| ToolSearch / deferred MCP | reference.md § MCP |
| `ultrareview` | dream-team adversarial (user-triggered concept) |

---

## 2. Full Harness Cycle (10 steps)

```
① LOAD SKILLS     — aggressive · all applicable (skills-first)
② UNDERSTAND      — CLASSIFY · parallel grep/glob
③ PLAN            — if ambiguous · 5+ modules · readonly only
④ IMPLEMENT       — surgical diff · Read before each edit
⑤ VERIFY TEST     — dotnet test / project native · repro-before-fix
⑥ VERIFY BUILD    — compiler · linter introduced errors
⑦ VERIFY RUNTIME  — Desktop.exe observe if UI critical path
⑧ REVIEW          — brqosh-review angles · security if touch auth
⑨ SIMPLIFY        — simplify.md if scope allows · not during hotfix
⑩ GATES + CEEF    — if wave · run-external-gates · OPEN_ITEMS
⑪ AUTO-SHURA      — agent runs run-shura-council.ps1 after SAVE (user never)
```

**Codex:** do not end turn while required exec still running.

---

## 3. Plan Mode (Codex + Claude Plan agent)

**Enter when:**
- 5+ namespaces / modules
- conflicting architecture choices
- major CEEF wave · schema migration
- user explicitly wants plan
- Perplexity: PDF/multi-step deliverable pattern

**In Plan:**
- explore readonly only — **no mutate**
- output phases · risks · verify strategy
- smallest path recommendation

**Exit:** explicit approval · or clear "implement now" directive.

---

## 4. Subagent Prompt Contract (Claude Agent tool)

Subagent **does not see parent chat** — prompt must include:

| Field | Content |
|-------|---------|
| Goal | one sentence outcome |
| Paths | files · dirs · patterns |
| Constraints | no commit · no O-01 false close · readonly |
| Success criteria | test names · PASS · structured return format |
| BRQOSH context | RBAC · BranchId if relevant |

| Type | Return format |
|------|---------------|
| explore | file paths · symbols · 5-line summary per area |
| security-review | P0/P1 findings · confidence |
| bugbot | severity-ordered findings |
| shell | command output summary |

**Relay to user:** subagent final message is for parent — synthesize, don't raw dump.

---

## 5. Parallel Delegation Matrix

| Safe parallel | **Unsafe** |
|---------------|------------|
| explore + security-review readonly | two agents StrReplace same files |
| grep batch + Read batch | parallel schema migrations |
| background test + grep | parallel git writes |

**Claude Code:** send multiple Agent calls in **one message** for concurrent readonly.

---

## 6. Background / Monitor

| Situation | Pattern |
|-----------|---------|
| dotnet test >60s | Shell background + Await pattern |
| full solution build | adequate block_until_ms |
| CI tail | output pattern notify |
| Multitask Mode | Task `run_in_background: true` |

Do not end turn with active shell tied to user request.

---

## 7. Todos (Perplexity + Grok)

```
3+ steps → todo list
Mark in_progress when starting step
Mark completed immediately — don't batch at end
Revise list when requirements change
After compaction → reseed todos
```

---

## 8. Compaction Recovery

```
1. Latest user_query — not ghost thread
2. Reseed todos if multi-step
3. Re-read STATUS.yaml if claiming counts
4. Don't restart work already done — continue
```

Claude Code: context may be summarized — **continue naturally**, don't restart from scratch.

---

## 9. Outcome Reporting (Claude Code harness)

```
If tests fail → say so with output
If step skipped → say so
If done and verified → state plainly without hedging
Don't claim save/copy file — user has same filesystem
```

---

## 10. BRQOSH Wave Harness

```
Parent: coordination · CEEF · docs · gates orchestration
Worker: implement + test loop
Shell worker: run-external-gates · stress (when requested)

Parent should NOT delegate what it can finish in 2 tool calls.
```

**Detail:** [dream-team.md](dream-team.md) · [session-protocol.md](session-protocol.md)

---

## 11. Anti-Patterns

| Avoid | Instead |
|-------|---------|
| Skip review on financial diff | angles I + security |
| Simplify during P0 hotfix | minimal fix first |
| Subagent without success criteria | full prompt contract |
| Wait idle on background | continue other work or Await |
| Claim harness complete without test | step ⑤ mandatory |

**Next:** [dream-team.md](dream-team.md) · [reference.md](reference.md) · [session-protocol.md](session-protocol.md)
