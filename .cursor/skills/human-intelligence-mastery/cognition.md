# Cognition — thinking architecture

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: human-intelligence-mastery | see per-file load order

## Five-layer stack

```
L0 Intent    — why does the human ask?
L1 Requirements — what counts as success?
L2 Model     — structure, data flow, constraints
L3 Mechanism — code, equations, procedures
L4 Evidence  — test, measurement, citation
L5 Human     — who benefits, who bears risk?
```

Do not implement at L3 before L1 is clear (except hotfix with known repro).

## Universal comprehension (any topic)

Before deep analysis, run [universal-understanding.md](universal-understanding.md) CLASSIFY phase.

## Thinking modes

| Mode | Use when | Output |
|------|----------|--------|
| **Deductive** | Rules defined (logic, law, types) | Step proof |
| **Inductive** | Data → pattern | Hypothesis + confidence |
| **Abductive** | Best explanation for symptoms | Top 2 hypotheses |
| **Analogical** | New domain | Map + limits of analogy |
| **First principles** | Broken assumptions | Decompose to axioms |
| **Inversion** | Risk, security | How would this fail? |
| **Second-order** | Policy, systems | Then what happens? |

## Problem decomposition

```
GOAL -> constraints -> subproblems -> order by dependency -> smallest verifiable step
```

## Learning loop (90 min system study)

| Phase | Action |
|-------|--------|
| 0–15 | Map territory (README, structure) |
| 15–30 | Entry points |
| 30–45 | One vertical slice |
| 45–60 | Tests / invariants |
| 60–75 | Edge cases |
| 75–90 | Gaps + plan |

## Meta-cognition

After producing answer:
- Did I answer the **last** question?
- Did format match need (one sentence vs report)?
- Did I over-format (bullet spam)?
- Did I skip verify step?

## Integration

BRQOSH deep study: `brqosh-analysis/deep-cognition.md`

---

## v15c — Cognitive load budgeting (agent)

| User signal | Depth | Format |
|-------------|-------|--------|
| "why broken?" | L0–L2 only | 1 hypothesis + grep plan |
| "fix it" | L3 + L4 | implement + test |
| "study the system" | Full L0–L5 | SCHOLAR 90-min |
| "explain to accountant" | L5 heavy | prose, no jargon wall |

### Inversion checklist (security · ERP)

Before shipping diff ask:
1. What if `BranchId` is null?
2. What if period is closed?
3. What if RBAC denies but UI allowed?
4. What if PG migration missing column?

### Second-order (policy)

"If we skip AuditLog here, what breaks in CEEF closure?" — answer before merge.

**Corpus:** Codex persistence · Gemini inquiry-vs-directive — [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml)
