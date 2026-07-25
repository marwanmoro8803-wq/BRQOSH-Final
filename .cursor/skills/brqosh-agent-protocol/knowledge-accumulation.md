# Knowledge Accumulation — LEARN Loop & Cursor Artifact Routing (v1)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-agent-protocol  
> **Rule:** [brqosh-continuous-learning.mdc](../../rules/brqosh-continuous-learning.mdc)  
> **Index:** [LEARNING-INDEX.yaml](../../LEARNING-INDEX.yaml) · [skill-lessons.yaml](../brqosh-skills-hub/skill-lessons.yaml)

---

## Purpose

Make **all Cursor configuration** accumulate knowledge across sessions:

- rules learn new laws
- skills learn domain patterns and anti-patterns
- plans learn progress and close into memory
- MEM indexes everything grep-able

Chat disappears. **Files persist.**

---

## When LEARN runs

| Activity | LEARN? | Minimum write |
|----------|--------|---------------|
| Code fix/feature after verify PASS | **Yes** | MEM row |
| Analysis/decision with user approval | **Yes** | MEM `decision` or `note` |
| Discussion — durable insight agreed | **Yes** | MEM `note` |
| Discussion — casual / no new fact | No | — |
| Verify FAIL or user correction | **Yes** | MEM `lesson` + skill-lessons |
| CEEF wave close | **Yes** | CEEF + MEM + leak-synthesis |
| Inquiry-only explain | No* | *unless user says "remember this" |

**Same turn** as VERIFY when feasible — do not defer LEARN to "next session".

---

## LEARN loop (step-by-step)

### 1. REFLECT (30s)

Answer internally:

- What was the **problem** or question?
- What **changed** (files · behavior · law)?
- What **failed** or was corrected?
- What would a **future agent** need to not repeat this?

### 2. ROUTE

Open [LEARNING-INDEX.yaml](../../LEARNING-INDEX.yaml) → pick **one primary** target.

Decision tree:

```
New always-on law?        → .cursor/rules/
Repeatable domain pattern?→ skill-lessons.yaml (+ reference if deep)
Workflow gap in harness?  → protocol / agent-workflow / relevant SKILL.md
3+ modules / arch fork?   → docs/memory/ or ADR
Wave complete?            → CEEF + MEM
Plan done?                → close plan header + MEM
Everything else verified  → PROJECT-MEMORY.yaml
```

### 3. EXTRACT

Write **principle form** — not narrative:

```yaml
# Good lesson (skill-lessons.yaml)
anti_pattern: "Edit posting without Golden filter"
do_instead: "Run Golden InvoicePosting before claim done"
verify: "dotnet test --filter Golden"
```

```markdown
# Bad — do not store
User said they were frustrated and we talked for 20 minutes about...
```

### 4. WRITE

| Target | Format |
|--------|--------|
| MEM | YAML row per brqosh-deep-memory.mdc |
| skill-lessons | append under `skills.<name>.lessons[]` |
| rule | new or StrReplace section in `.mdc` |
| plan | update YAML front header in `.cursor/plans/` |
| CEEF | `reports/CEEF-NNN_*.md` |
| leak-synthesis | `### vNN wave` bullet under Maintenance |

### 5. INDEX

- MEM `links.skill_lesson` → id in skill-lessons.yaml if both written
- Update `LEARNING-INDEX.yaml` `last_updated` if new route added
- Report to user: `MEM-NNN` · lesson id · rule path

---

## Mistake protocol (learn from errors)

```
FAIL detected
  → fix if in scope
  → MEM type: lesson
      mistake: one sentence
      root_cause: one sentence
      correction: one sentence
      verify: command
  → skill-lessons entry with anti_pattern + do_instead
  → if 2nd occurrence of same mistake class → promote to .mdc rule
```

**Never** delete lesson entries — supersede with `supersedes: LESSON-xxx` if wrong.

---

## Plans lifecycle

Active plan file header (top of `.cursor/plans/*.plan.md`):

```yaml
---
status: active
updated: 2026-07-10
owner: agent
mem: null
---
```

On completion:

```yaml
status: closed
updated: 2026-07-10
mem: MEM-042
```

Extract: goal · outcome · gaps ⏭ → MEM. Plan remains as archive — **STATUS.yaml + MEM = live truth**.

---

## Skills learning (without bloat)

| Layer | Holds |
|-------|-------|
| `SKILL.md` | trigger · roadmap · pointers |
| `reference.md` / `engineering.md` | stable deep patterns |
| `skill-lessons.yaml` | session-distilled lessons · anti-patterns |
| `leak-synthesis.md` | corpus merge waves only |

**Before editing a skill module for a lesson:** check skill-lessons.yaml — append there first unless the lesson changes the workflow contract (then edit SKILL.md roadmap).

---

## Rules learning

Promote to `.cursor/rules/` when:

- Violation caused verify fail or user correction **twice**
- Security · schema · closure · memory law affected
- User explicitly requests "always apply this"

Otherwise keep in skill-lessons + MEM.

---

## Start-of-session read order

```
① STATUS.yaml
② grep PROJECT-MEMORY.yaml + skill-lessons.yaml for task keywords
③ LEARNING-INDEX.yaml if unsure where to write
④ domain SKILL.md + impact-before-change before code
```

---

## End-of-session checklist

```
□ Substantive work this turn?
□ LEARN loop completed?
□ MEM id reported?
□ Plan status updated if applicable?
□ No chat-only "memory" left unwritten?
```

---

## Anti-patterns

| Avoid | Instead |
|-------|---------|
| Duplicate lesson in 5 skills | skill-lessons.yaml once |
| 200-line plan as truth | MEM + close plan |
| Skip LEARN on "small" fix | MEM row is cheap |
| Load full claude-fable-5.md | synthesis modules |
| Edit supreme-intelligence-hub redirect | brqosh-skills-hub |

**Next:** [impact-before-change.md](impact-before-change.md) · [protocol.md](protocol.md)
