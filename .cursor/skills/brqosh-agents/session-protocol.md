# Session Protocol — Mandatory Behavior Supreme (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-agents  
> **Synthesis:** always-on v10 · Claude Code session · Codex resume · Perplexity todos  
> **Corpus:** `Anthropic/Claude Code/` · `OpenAI/Codex/gpt-5.5.md`

> **TRIGGER:** Every session · every message  
> **Enforced by:** `.cursor/rules/brqosh-supreme-cortex.mdc` → brqosh-agent-protocol

---

## 0. Session Equation

```
Effective outcome = min(raw capability, protocol applied)
```

Raise intelligence by **following harness** — not by loading more leak text.

---

## 1. Message Start (≤10s — always-on)

```
① Latest user_query leads (steering refines, rarely cancels)
② CLASSIFY — universal-understanding Phase 0
③ Mode: execute | analyze | review | creative | teach | close
④ BRQOSH? → C06 + C03 auto
⑤ Skills scan — skills-first before code
⑥ Councils 1–3 — routing.md
⑦ grep PROJECT-MEMORY + skill-lessons if topic has history
```

---

## 2. Before Any Edit

```
□ parallel grep/glob — no memory assumptions
□ Read before StrReplace/Write
□ Roles: arch · dev · qa · sec · domain (implicit)
□ Inquiry vs Directive — protocol.md §4
□ Dirty worktree — don't revert user changes
```

---

## 3. During Execution

```
□ smallest complete diff — zero workflow break
□ BRQOSH order: Domain → App → Infra → UI (when applicable)
□ CompanyId · BindSecurityContext · SchemaSqlGuard
□ repro/test before fix (bugs)
□ parallel independent tool calls
□ no shell echo for user communication
□ todos: 3+ steps → list · one in_progress
```

---

## 4. Before "Done"

```
□ dotnet build PASS (BRQOSH)
□ dotnet test PASS — count from STATUS.yaml
□ zero new linter errors you introduced
□ financial path → Golden thought
□ UI substantive → audit script or Desktop.Tests
□ explicit ⏭ for external (OTP · SOCPA · ZAP · VM)
□ substantive analysis saved → agent ran run-shura-council.ps1 (exit 0)
□ LEARN loop if substantive — MEM + skill-lessons if lesson
□ answer matches NEWEST user_query (resume check)
```

Read [STATUS.yaml](../brqosh-skills-hub/STATUS.yaml) — not "many tests".

---

## 5. Large Task = Dream Team (mandatory)

| # | Role | Module |
|---|------|--------|
| 1 | Architect | brqosh-analysis / super-dev |
| 2 | Dev | brqosh-engineering |
| 3 | Polyglot | supreme-programming/polyglot-pitfalls |
| 4 | QA | brqosh-review code-review xhigh |
| 5 | Security | security.md ≥80% confidence |
| 6 | Domain | brqosh-accounting if money |
| 7 | Ops | brqosh-closure + gates |

**Parallel readonly before merge:** explore + security + standards.

**Detail:** [dream-team.md](dream-team.md) · [harness.md](harness.md)

---

## 6. After Compaction / Resume (Codex + Claude)

```
① What is latest user_query? (not ghost)
② Reseed todos if 3+ steps remain
③ Re-read STATUS if claiming test count
④ Continue — don't restart completed work
⑤ Sanity: tool actions align with newest steering
```

Claude Code: summarized context is normal — work continues from summary + unsummarized tail.

---

## 7. Aggressive Skill Loading

| Task size | Minimum skills |
|-----------|----------------|
| Any message | fable-omni-core |
| Small fix | + agent-protocol + domain |
| Medium feature | + engineering or ui or accounting |
| Large wave | + harness + dream-team |
| Review | + brqosh-review |
| Close CEEF | + closure + zero-gap-matrix |
| Analysis SAVE | + auto-shura (`brqosh-shura-auto.mdc`) |
| Research | + human-intelligence research |

---

## 8. Prohibitions (worse than slow team)

| Never | Why |
|-------|-----|
| «done» without test | false closure |
| suggestion when asked implement | Codex persistence fail |
| half-fix on posting | financial harm |
| O-01 closed from code | dishonest |
| foreign stack patterns in BRQOSH | convention break |
| Python to read when Read exists | Cursor anti-pattern |
| parallel mutate same files | corruption |
| load archive/ or full leaks | context poison |

---

## 9. Interruption Handling

| User mid-task | Action |
|---------------|--------|
| New conflicting request | newest leads |
| Non-conflicting add-on | honor all since last turn |
| "status only" | brief update · continue unless pause asked |
| "stop" / "only plan" | halt mutate · inquiry mode |

---

## 10. BRQOSH Session Overlay

```powershell
$env:BRQOSH_SKIP_STRESS='1'; $env:BRQOSH_TEST='1'
dotnet build Brqosh.sln -c Release --nologo
dotnet test Brqosh.sln -c Release --nologo
```

Wave major: `.\scripts\ops\run-external-gates.ps1`

---

## 11. LEARN Before End Turn (continuous learning)

If this turn had substantive work (fix · feature · decision · mistake · plan progress):

```
① knowledge-accumulation.md — REFLECT → ROUTE → EXTRACT → WRITE
② PROJECT-MEMORY.yaml row (or lesson type if failure)
③ skill-lessons.yaml if repeatable domain pattern
④ Update .cursor/plans/ status if plan work
⑤ Report MEM id to user
```

**Skip** for pure inquiry with no durable insight.

**Next:** [knowledge-accumulation.md](../brqosh-agent-protocol/knowledge-accumulation.md) · [protocol.md](../brqosh-agent-protocol/protocol.md) · [harness.md](harness.md)
