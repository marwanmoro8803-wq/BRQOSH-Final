# Agent Workflow — Fable-Class Execution Supreme (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery  
> **Synthesis:** OpenCode 5-step · Codex 5.5 persistence · Gemini Directive · Claude Code harness  
> **Corpus:** [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml)

---

## 1. Universal 5-Step (OpenCode + Fable)

```
① UNDERSTAND — CLASSIFY · grep/glob · PROJECT-MEMORY · skills-first · no assumptions
② PLAN       — impact-before-change §1–§6 · scale effort · Plan Mode if architectural fork
③ IMPLEMENT  — local conventions · smallest diff · Read before each edit
④ VERIFY TEST— project test command · repro-before-fix for bugs
⑤ VERIFY STD — build · lint · typecheck · schema guards · gates if BRQOSH wave
⑥ REMEMBER   — append PROJECT-MEMORY.yaml · CEEF if wave
⑦ LEARN      — skill-lessons · rules · plans per LEARNING-INDEX
```

**Do not skip ④ because change "looks small."**

---

## 2. Persistence (Codex 5.5 — mandatory)

```
Stay until goal handled end-to-end in the turn when feasible.
```

| Situation | Action |
|-----------|--------|
| User says fix | implement — don't stop at diagnosis |
| Test fails after fix | diagnose · retry · don't hand back immediately |
| Long build/test | run · wait or background + Await |
| Blocker | alternate approach before asking user |
| User asks plan only | Plan Mode — no mutate until approved |

**Unless:** explicit brainstorm · question-only · "don't implement yet."

---

## 3. Skills Before Code (Fable + Perplexity)

**Mandatory:** [skills-first.md](skills-first.md)

```
Before any code:
  1. fable-omni-core CLASSIFY
  2. Scan ALL plausible SKILL.md (hub routing table)
  3. Read reference modules for active domain
  4. Then grep/Read codebase
```

Skill instructions **override** generic coding habits for the task duration.

---

## 4. Inquiry vs Directive (Gemini)

| User | Agent |
|------|-------|
| "How would…?" / "Explain" | analyze · propose — **no edits** |
| "Fix" / "Implement" / "Add" | full 5-step |
| Bug report only | diagnose · confirm before fix if scope unclear |
| Ambiguous | one clarifying question max · then best-effort |

---

## 5. Plan Mode (Codex plan_mode.md synthesis)

**Enter Plan when:**
- multiple valid architectures with trade-offs
- conflicting requirements
- 5+ module impact · migration · schema wave
- posting · Golden · ZATCA · RBAC · behavior removal
- user explicitly wants plan

**Full checklist:** [impact-before-change.md](../brqosh-agent-protocol/impact-before-change.md)

**Skip Plan when:**
- obvious bug · typo · narrow test failure
- user said "implement" / "fix now"
- single-file clear fix

**Plan output:** phases · risks · verify strategy · smallest path — **no file mutations until approval.**

---

## 6. Edit Discipline (Cursor + Fable)

| Rule | Detail |
|------|--------|
| Read before edit | mandatory |
| Re-read after edit | before next StrReplace |
| Unique old_string | or widen context |
| Prefer StrReplace | over full Write rewrite |
| New file | only when new module warranted |
| Comments | non-obvious only — no narration |
| ASCII default | Unicode only when file already uses it |

See [file-vs-inline.md](file-vs-inline.md)

---

## 7. When to Create Files

| Situation | Action |
|-----------|--------|
| ≤15 lines answer | inline or tiny edit |
| new module/component | correct project folder |
| >20 lines new logic | dedicated file + tests |
| config/migration | follow repo conventions |
| skill doc | only when user/rules request |

**Cursor:** NEVER create files unless absolutely necessary.

---

## 8. Stack Detect (60 seconds)

```
glob: *.csproj · package.json · go.mod · Cargo.toml · pyproject.toml
→ languages/<stack>.md
→ identify test command from CI or README
→ BRQOSH? switch brqosh-engineering
```

Don't assume library exists — grep imports / lockfile first (Gemini).

---

## 9. Verify Scaling (Codex judgment)

| Risk | Verify breadth |
|------|----------------|
| comment · log | smoke |
| shared helper | unit tests touching paths |
| API contract | integration |
| auth · money · schema | broad + manual checklist |
| BRQOSH posting | Golden + dotnet test |

---

## 10. Git & Worktree

| Rule | |
|------|--|
| dirty worktree | never revert alien changes |
| commit | user request only |
| destructive git | explicit ask only |
| unrelated diff in file | read · work with · don't wipe |

See [git-delivery.md](git-delivery.md)

---

## 11. Review Workflow Branch

User says **"review"** → Codex stance:

```
Findings first — severity — file/line
Open questions
Change summary last
No issues → say so + residual risks
```

Load [review.md](review.md) + brqosh-review.

---

## 12. Goal-Driven Execution (Karpathy K4)

Every multi-step task gets a **verify plan** before implementation:

```
1. [Step] → verify: [command or observable]
2. [Step] → verify: [command or observable]
```

| Vague ask | Verifiable rewrite |
|-----------|-------------------|
| "Fix auth" | Failing test → green → full suite |
| "Add validation" | Invalid-input tests → implement → pass |
| "Make faster" | Define metric → benchmark → change → re-benchmark |

Details: [karpathy-synthesis.md](karpathy-synthesis.md) · `brqosh-debug` repro-first

---

## 12. Frontend Branch

| Product type | Style |
|--------------|-------|
| SaaS/CRM ops tool | quiet · dense · utilitarian (Codex) |
| Game/creative | expressive allowed |
| **BRQOSH ERP** | **invert** — dense RTL — brqosh-ui-design-system |

---

## 13. BRQOSH Handoff

After universal ①②:

```
→ brqosh-engineering DOMAIN_MAP
→ brqosh-accounting if money
→ brqosh-closure if wave
→ STATUS.yaml test count
→ SchemaSqlGuard if PG column
→ SecurityContext if admin DB
```

---

## 14. Failure Recovery

```
test fail → read message · grep · minimal fix · re-run
build fail → compiler line 1 · don't shotgun
tool fail → alternate approach · don't repeat 5× same command
context compacted → reseed todos · verify newest user_query
```

---

## 15. Completion Checklist

```
□ CLASSIFY done
□ Skills scanned
□ Read before edits
□ Tests run (or ⏭ why not)
□ Build green
□ Answer matches newest request
□ No secrets in output
□ Proportional response length
```

**Next:** [tools-cursor.md](tools-cursor.md) · [twelve-axes.md](twelve-axes.md) · [brqosh-engineering](../brqosh-engineering/SKILL.md)
