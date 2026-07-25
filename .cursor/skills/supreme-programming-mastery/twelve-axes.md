# Twelve Engineering Axes

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: supreme-programming-mastery | see per-file load order

Every non-trivial change — mentally check all 12 before claiming done.

| # | Axis | Key questions |
|---|------|----------------|
| 1 | **Correctness** | All inputs? Edge cases? Business rules? |
| 2 | **Types** | Null safety? Wrong type at boundary? |
| 3 | **Memory** | Leaks? Dispose? Scope? |
| 4 | **Concurrency** | Races? Deadlocks? UI thread? |
| 5 | **I/O** | N+1? Timeouts? Retries idempotent? |
| 6 | **Security** | Injection? Authz? Secrets in code? |
| 7 | **Observability** | Logs? Audit trail? Errors actionable? |
| 8 | **Testability** | Unit test? Repro test for bug? |
| 9 | **Deploy** | Migration reversible? Feature flag? |
| 10 | **Performance** | Hot path? Allocations? |
| 11 | **Maintainability** | Clear names? Boundaries? |
| 12 | **Domain** | Business invariants? (ERP: debit=credit) |

## Risk scaling

| Risk | Depth |
|------|-------|
| Copy typo fix | Axes 1,8 |
| Auth / money / schema | All 12 + security module |
| Public API change | 1,2,8,9,11 + migration plan |

## 90-minute codebase onboarding

```
0-15  tree, README, build command
15-35 grep services, routes, entry points
35-55 read tests — what does system guarantee?
55-75 data model
75-90 risk map (silent failures, security, domain)
```

## Four questions humans skip

1. What breaks **silently** (wrong result, no exception)?
2. Where is **source of truth**?
3. What hidden assumption in last change?
4. Which test **should** fail but doesn't exist yet?
