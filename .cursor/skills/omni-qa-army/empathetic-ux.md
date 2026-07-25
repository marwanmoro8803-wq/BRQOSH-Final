# Empathetic UX — Human Dignity Layer

> **Source:** raw prompt L100–111 (ideas only)  
> **Leaks:** human-cognition · Nielsen heuristics (via brqosh-ui)  
> **Roles:** UX Auditor (S03) · Beta Tester (S05)  
> **Related:** [roles/role-accessibility.md](roles/role-accessibility.md) · [roles/role-beta.md](roles/role-beta.md) · [brqosh-ui-design-system](../brqosh-ui-design-system/SKILL.md)

---

## Purpose

Test for **human dignity** — not just technical correctness. Behind every user is a tired, impatient, distracted person who wants to finish a task.

---

## Affective flow analysis (MUST per core journey)

| Checkpoint | Question |
|------------|----------|
| Start | Does user know what to do? |
| Mid-flow | Confused · empowered · frustrated? |
| Error | Abandoned · stupid · enraged? |
| End | Accomplished or betrayed? |

**Flag:** cryptic errors · sudden data loss · invisible progress · dead-ends without back · silent buttons.

---

## Graceful failure audit

| Condition | MUST judge |
|-----------|------------|
| No network | calm recovery path |
| Invalid file | human-readable message |
| Missing permissions | clear next step |
| Full disk | no stack trace to user |
| Simultaneous actions | no corruption |

**Critical:** raw technical jargon shown to user = UX failure.

---

## Expectation vs reality mapping

| Check | Failure type |
|-------|--------------|
| Looks like button, acts like button? | affordance betrayal |
| Greyed-out looks clickable? | signifier mismatch |
| Dropdown looks editable? | micro-betrayal |
| Save appears to work? | silent failure (Critical) |

---

## Consistency and forgiveness

| MUST test | Flag if |
|-----------|---------|
| Same task via different entry points | behavior differs |
| Double submit | duplicate records |
| Accidental back-navigation | data lost |
| Cancel on modal | partial save remains |
| Close with unsaved data | no warning |

---

## Silent failure detection (highest priority)

| Signal | Method |
|--------|--------|
| Save with no file write | filesystem check |
| Send with no confirmation | UI + log |
| Setting reverts after restart | persistence probe |
| Progress bar lies | compare actual state |

**Every silent failure = Critical** in report.

---

## Contextual benchmarking

Deduce app type and audience from UI patterns, then judge accordingly:

| App type | Catastrophic if |
|----------|-----------------|
| Accounting | decimal mishandling |
| Children's app | SQL error visible |
| Developer tool | cryptic only OK if audience expects |

Document inferred audience in report executive summary.

---

## Beta Tester deliverable link

First-person narrative in `BETA_TESTER_USER_JOURNEY_FEEDBACK.md` — frustration · delight · confusion.

---

## Forbidden

| FORBIDDEN | DO INSTEAD |
|-----------|------------|
| WCAG-only pass as "UX done" | layer empathy findings |
| Severity without user impact | tie to journey step |
| Arabic skill body text | English skills · AR in reports |

---

## Future code owners (when authorized)

`design_auditor.py` empathy section · `copywrite_docs_auditor.py` tone checks

---

## Verify

```powershell
Test-Path .cursor/skills/omni-qa-army/empathetic-ux.md
rg "empathetic-ux" .cursor/skills/omni-qa-army/roles/role-accessibility.md
```
