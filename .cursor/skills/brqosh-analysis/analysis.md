# Smart Mind — Study · Analysis · Problem Solving

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-analysis | see per-file load order

> Plan Mode · 8 angles · Root cause · Requirements traceability

---

## Index

1. [Problem-Solving Loop](#1-problem-solving-loop-7-phases)
2. [Plan Mode — Three Phases](#2-plan-mode--three-phases)
3. [Error Analysis](#3-error-analysis)
4. [Requirements Study](#4-requirements-study)
5. [Gap Analysis](#5-gap-analysis)
6. [Research and Web](#6-research-and-web)
7. [Decision Making](#7-decision-making)
8. [Analysis Templates](#8-analysis-templates)
9. [Epistemology](#9-epistemology)

---

## 1. Problem-Solving Loop (7 Phases)

```
① Define      → What? Success criteria?
② Explore     → Parallel evidence from code/logs/tests
③ Model       → Root cause, not symptom
④ Hypotheses  → 2–3 ranked by probability
⑤ Verify      → Test/experiment that discriminates
⑥ Fix         → Smallest proven diff
⑦ Close       → Regression + CEEF if wave
```

**Gemini rule:** Reproduce the failure **before** fixing.

---

## 2. Plan Mode — Three Phases

**When:** Architectural ambiguity · conflicting requirements · impact on 5+ files/namespaces

### Phase 1 — Ground
- Unmodified explore
- Search before asking the user
- dry-run · read · grep

### Phase 2 — Intent
- goal + success criteria
- in/out of scope
- constraints (ZATCA · PG · RBAC)
- audience (accountant · manager · developer)

### Phase 3 — Implementation spec
- APIs · schemas · data flow
- edge cases · failure modes
- tests · acceptance
- migrations · rollout

**Plan exit:** decision-complete — the next implementer does not guess.

**Forbidden in Plan:** edit files · format · migrate

---

## 3. Error Analysis

| Type | First action |
|------|--------------|
| Build error | Read the first line in the chain |
| Test fail | assertion + isolated run `--filter` |
| Flaky | `BRQOSH_SKIP_STRESS` · timing |
| PG startup | SchemaSqlGuard · AllowedPgTypes |
| RBAC | BindSecurityContext |
| UI RTL | DataGridRtlHelper · theme |
| Production | AuditLog · last posting |

### Three-Attempt Tree
```
Attempt 1 → fail → new hypothesis (do not repeat the same command)
Attempt 2 → fail → expand exploration scope
Attempt 3 → fail → report what was tried + blocker to user
```

---

## 4. Requirements Study

### Sources
| Source | Use |
|--------|-----|
| `docs/EXECUTION_ROADMAP_MASTER.md` | Strategy |
| `.cursor/plans/` | Execution waves |
| `OPEN_ITEMS.md` | ⏭ explicit |
| `DOMAIN_MAP` | Boundaries |
| Code | **Ground truth** |

### Extraction
1. P0 / P1 / P2 / ⏭
2. grep to verify — not from memory
3. 4 closure levels per item
4. No "partially closed"

---

## 5. Gap Analysis

`docs/MARKET_LAUNCH_GAP_ANALYSIS.md` — 69 gaps — **historical snapshot (2026-07-10), superseded in both directions**: the current verdict is `docs/release-readiness/MASTER.md`, open items `docs/release-readiness/REMAINING-MAJORS.md`

| Question | Answer |
|----------|--------|
| in-repo? | code+test |
| CEEF? | report |
| O-XX? | external |
| P0 market? | priority |

---

## 6. Research and Web

**When WebSearch/WebFetch:**
- Updated ZATCA/SOCPA documentation
- API breaking changes
- **Not** to replace reading the repository

**Freshness rule (OpenAI web):** If the cost of error is high → verify externally.

---

## 7. Decision Making

| Situation | Decision |
|-----------|----------|
| Open details | Most conservative + consistent with repo |
| Rule conflict | project > skill > general |
| Financial risk | correctness > speed |
| UI risk | design system > creativity |

---

## 8. Analysis Templates

### RCA (Root Cause)
```
Problem:
Impact:
Root cause (5 whys):
Fix:
Regression prevention (test/rule):
```

### Option Comparison
```
| Option | Pros | Cons | Risks | Recommendation |
```

### Requirement → Code Trace
```
REQ-ID → file:line → test → status (✅/🔶/⏭)
```

---

## 9. Epistemology

- Do not psychoanalyze user intent
- Do not assume an attached file
- On error: admit · fix
- No over-confidence

**Code review:** [code-review.md](code-review.md) — 8 angles

---

## v4 — Analysis Beyond "One Question"

### 5 Whys Chain (mandatory for P0)
```
Symptom # → why? ×5 → root cause → preventive fix
```

### Evidence-Based Decisions, Not Opinion
| Strongest evidence | Weakest |
|--------------------|---------|
| test fail/pass | "seems like" |
| prior CEEF | conversation memory |
| grep in src | API assumption |

### Surpass Leaked Models
**Prove in repo** — [brqosh-closure.md](brqosh-closure.md) · CEEF · verify

**Integration:** [integration](../brqosh-skills-hub/integration.md) · [brqosh-analysis.md](brqosh-analysis.md)

---

## v5 — Deep Research (deep-research + OpenAI tool)

```
① Scope      → 12 questions · time scope · accepted sources
② Search     → grep · DOMAIN_MAP · CEEF · Web (if needed)
③ Fetch      → Read primary sources — not secondary only
④ Verify     → every claim = code or test or official document
⑤ Synthesize → CEEF report · decision · execution plan
```

| Verification angle | BRQOSH |
|--------------------|--------|
| Primary source | `src/` · `docs/EXECUTION_ROADMAP_MASTER.md` |
| Cross-check | tests · Golden |
| Freshness | ZATCA/SOCPA docs |
| Contradiction | plan vs OPEN_ITEMS |

**OpenCode Phase 1:** Do not assume — search the repository before asking the user.

---

## v6 — Full Integration

**Integration:** [integration](../brqosh-skills-hub/integration.md)
