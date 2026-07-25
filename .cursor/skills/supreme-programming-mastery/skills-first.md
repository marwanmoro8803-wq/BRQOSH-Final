# Skills First — Mandatory Before Code Supreme (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery  
> **Synthesis:** Fable `<skills>` · Perplexity aggressive load · Gemini activated_skill · Claude Code Skill tool  
> **Corpus:** `Perplexity/perplexity-computer.md` · `Anthropic/Claude Code/`

---

## Rule (non-negotiable)

```
BEFORE Write · StrReplace · Shell build · any implementation:

  1. CLASSIFY (fable-omni-core / universal-understanding)
  2. Scan ALL available skills (agent list + .cursor/skills/)
  3. Read EVERY plausibly-relevant SKILL.md + 1–2 reference modules
  4. THEN grep/Read target files
  5. THEN implement → verify
```

Skipping skills = missing RBAC · SchemaSqlGuard · CEEF · RTL conventions generic training lacks.

---

## Perplexity Aggressive Loading Pattern

```
"When starting a new task, load ANY nonduplicative skills that might be relevant.
Be very aggressive and proactive — skills are extremely useful."
```

| Load | Skip |
|------|------|
| All domain skills that might apply | Duplicative skills (pick best one) |
| User-attached skills always | Unrelated creative skills on pure bugfix |
| brqosh-agent-protocol every BRQOSH session | Entire corpus leaks |

---

## Cursor Scan Order (priority)

| # | Skill | When |
|---|-------|------|
| 1 | `fable-omni-core` | **every** message |
| 2 | `human-intelligence-mastery` | teach · research · ethics |
| 3 | `supreme-programming-mastery` | **any** code |
| 4 | `brqosh-agent-protocol` | BRQOSH session |
| 5 | `brqosh-skills-hub` | routing uncertainty |
| 6 | Domain table below | by signal |
| 7 | User-attached (e.g. claude-fable-5 index) | user explicit |

**Several skills may apply** — read more than one.

---

## Domain Routing Table

| Signal | Skills to read |
|--------|----------------|
| Fix test / bug | brqosh-debug + brqosh-engineering + supreme-programming |
| RTL screen | brqosh-ui + brqosh-ui-design-system |
| ZATCA / posting | brqosh-accounting + brqosh-closure |
| CEEF / close wave | brqosh-closure + zero-gap-matrix |
| PR review | brqosh-review (code + security) |
| Wide explore | brqosh-agents |
| Unknown repo | universal-coding |
| Ambiguous spec | brqosh-analysis/requirements |
| Python/TS only script | supreme-programming/languages/* |
| UI rule active | brqosh-ui-design-system + design-system/MASTER |

---

## What Skills Encode (not in base model)

| Constraint | Where |
|------------|-------|
| `BindSecurityContext()` | brqosh-engineering |
| SchemaSqlGuard | brqosh-engineering + schema rule |
| CEEF 4 levels | brqosh-closure |
| O-01..O-05 external | brqosh-closure + OPEN_ITEMS |
| RTL WPF density | brqosh-ui-design-system |
| 417 test count | STATUS.yaml via hub |
| Inquiry vs Directive | brqosh-agent-protocol/protocol |
| Code citations format | tools-cursor.md |
| Copyright ≤15 words | copyright-compliance |

---

## Gemini activated_skill Pattern

When skill content appears in context (user attach · rule · explicit load):

```
Treat <instructions> as expert procedural guidance.
Prioritize over general defaults for task duration.
Still uphold safety + BRQOSH closure rules.
```

---

## Claude Code Skill Equivalents

| Claude Code | BRQOSH |
|-------------|--------|
| `/code-review` | brqosh-review/code-review.md |
| `/simplify` | brqosh-review/simplify.md |
| `debug` skill | brqosh-debug/reference.md |
| Explore agent | Task explore subagent |
| Plan agent | Plan Mode + brqosh-analysis |

---

## Examples (mandatory reads)

| User request | Read first |
|--------------|------------|
| GoldenPosting fail | debug + engineering + accounting |
| "Close O-01 OTP" | closure only — refuse false close |
| Invoice RTL | ui + ui-design-system + pages/invoicing |
| "Supremacy" claim | ai-supremacy + human-parity |
| Foreign Node repo | universal-coding + languages/typescript |

---

## Anti-Patterns

| Bad | Good |
|-----|------|
| Jump to StrReplace "I know C#" | skills-first scan |
| Only read one skill | load all applicable |
| Ignore user-attached claude-fable-5 | follow index to modules |
| Load archive/ or full leaks | grep corpus index |
| Skip protocol on "small" BRQOSH fix | agent-protocol always |

---

## Verify Skills Applied

Before «done» ask:

```
□ Did domain skill constraints appear in solution?
□ RBAC/schema/closure rules respected?
□ Would diff surprise someone who only read SKILL.md? → if yes, re-read skill
```

**Next:** [agent-workflow.md](agent-workflow.md) · [file-vs-inline.md](file-vs-inline.md)
