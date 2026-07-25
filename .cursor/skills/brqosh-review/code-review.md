# Smart Mind — Code Review (8 Angles)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-review | see per-file load order

> Distilled from Anthropic code-review high/max · Codex review · Copilot — **recall-biased for BRQOSH**.

---

## 0. Scope Collection

```powershell
git diff main...HEAD
git diff HEAD   # if worktree is dirty
```

Scope = diff + touched functions even when the line itself did not change.

---

## 8 Independent Angles (up to 6 candidates per angle)

### A — Line-by-Line Scan
For each hunk: what input/timing/state breaks this line?
- off-by-one · null · missing await
- inverted condition · copy-paste variable
- swallowing catch · decimal/money rounding

### B — Removed-Behavior Auditor
Every **deleted** line: where was protection restored?
- deleted guard · deleted test · narrowed validation

### C — Cross-File Trace
- Who calls the changed function?
- Did return shape change?
- Did a callee in the same PR become unsafe?

### D — Reuse
Does it reimplement an existing helper?
- `EmptyStateHelper` · `UiFeedback` · `PostingOrchestrator`
- Grep before adding a new abstraction

### E — Simplification
Deep nesting · dead code · duplicated state

### F — Efficiency
Sequential I/O that could be parallelized · N+1 EF queries · full List load instead of paging

### G — Altitude
Is the fix a bandaid on shared infrastructure?
- Prefer generalizing the mechanism (e.g. PostingPolicy) over a special case

### H — Conventions
- `.cursor/rules/*` · `DOMAIN_MAP` · skills
- Arabic UI naming · `CompanyId` on queries

---

## Voting and Filtering

1. Collect candidates from all 8 angles
2. Verify: is there a **concrete** failure scenario?
3. Drop speculative findings below 80% confidence
4. **≤10 findings** in the final report (quality > quantity)

---

## Effort Levels

| Level | When |
|-------|------|
| **low** | Narrow change · typo |
| **medium** | Single feature |
| **high** | Posting · RBAC · schema |
| **max** | CEEF wave · Golden |

---

## Finding Template

```
[P0|P1|P2] Title — file:line
Summary: one sentence
Failure scenario: steps
Proposed fix: specific
```

**Response to user:** findings first · then summary · no filler.

---

## BRQOSH — Mandatory Financial Review

If the path touches:
- `*PostingService*` · `JournalEntry` · `SalesInvoice`
- Verify: balance · audit · policy · period · branch

Review `GoldenAccountingIntegrityTests` — is the scenario covered?

---

## v4 — Standards×Spec Integration

Before declaring "PR ready":
1. [standards-spec-review.md](standards-spec-review.md) — two axes
2. 8 angles — bugs
3. security — if security-relevant

**Beyond a single model:** three reviews · unified report by severity.

### v5 — After Bugs: Simplify
[simplify-cleanup.md](simplify-cleanup.md) — 4 parallel cleanup agents (not bug hunting).

---

## v6 — xhigh 10 Angles (Claude Code 2.1.172)

> **recall > false positive** for P0 financial or security issues

### Phase 0 — Scope
```bash
git diff @{u}...HEAD
git diff HEAD    # uncommitted
```

### Phase 1 — 10 Angles (parallel · up to 8 findings/angle)

| Angle | What to search |
|-------|----------------|
| **A** line-by-line | every hunk · surrounding function |
| **B** removed-behavior | what guards were deleted? |
| **C** cross-file | callers · callees · return shape |
| **D** language pitfalls | decimal·async·SQL·timezone |
| **E** wrapper/proxy | delegate vs global re-entry |
| **F** concurrency | race · double-post · idempotency |
| **G** error paths | empty catch · rollback |
| **H** test gaps | does Golden cover the scenario? |
| **I** **BRQOSH financial** | balance · CompanyId · Audit · PostingPolicy |
| **J** conventions | DOMAIN_MAP · naming · RBAC |

**Rule:** two angles on the same line for different reasons = **record both together**.

### Phase 2 — Merge
- dedup · sort by severity
- confidence <80% security → do not raise noise
- display order: Security → Financial → Bugs → Standards

**Source:** `code-review/xhigh.md` · **Adapted:** angle I is mandatory for BRQOSH

### v7 — polyglot
Before angles — identify diff languages → [polyglot-pitfalls-v7.md](polyglot-pitfalls-v7.md)

### adversarial self-review
After angles — review [deep-cognition.md](deep-cognition.md) · questions on null/concurrent/RBAC/PG≠SQLite
