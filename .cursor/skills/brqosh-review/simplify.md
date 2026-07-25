# Simplify and Cleanup — Supreme Reference (v15)

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-review  
> **Synthesis:** Anthropic `/simplify` · 4 parallel cleanup agents · Codex judgment after bugs fixed  
> **Corpus:** `Anthropic/Claude Code/bundled-skills/` simplify patterns

> **Not bug hunting** — reuse · simplicity · efficiency · altitude after correctness

---

## 1. Simplify vs Code Review

| Dimension | code-review | simplify |
|-----------|-------------|----------|
| Goal | bugs · security · correctness | quality · reuse · clarity |
| Recall bias | high for P0 financial | moderate — skip noise |
| When | before/during merge | **after** P0 bugs fixed |
| Output | findings by severity | cleanup applied + skip reasons |
| Parallel | 8–10 angles | **4 angles** |

**Order:** `xhigh bugs → fix → simplify → standards/spec`

---

## 2. Phase 0 — Scope

```powershell
git diff main...HEAD
git diff HEAD   # uncommitted
```

Scope = diff + **behavior of touched functions** even if line unchanged.

---

## 3. Phase 1 — Four Angles (run parallel readonly)

### Angle A — Reuse (Anthropic reuse agent)

```
Does new code reimplement existing helper?
```

| Grep targets (BRQOSH) | Purpose |
|-----------------------|---------|
| `EmptyStateHelper` | empty states |
| `UiFeedback` · `UiDialogs` | user messages |
| `PostingOrchestrator` | posting path |
| `PagingState` · `SearchDebouncer` | lists |
| `PermissionService` | RBAC checks |
| `KpiMetricCardBuilder` | KPI cards |

**Rule:** grep `Brqosh.Application` + `Brqosh.Desktop/Ui` before adding abstraction.

### Angle B — Simplification

| Signal | Action |
|--------|--------|
| duplicated state | consolidate |
| copy-paste blocks | extract if 3+ |
| nesting >4 levels | early return · extract method |
| dead code | remove if proven unused |
| boolean soup | named predicate |

**Do not** simplify away explicit business rules.

### Angle C — Efficiency

| Signal | BRQOSH fix |
|--------|------------|
| N+1 EF | Include or projection |
| `ToList()` on huge table | paging · `AsNoTracking` |
| sequential awaits | parallel when safe |
| repeated DB roundtriips | batch query |
| UI reload entire grid | diff binding if exists |

### Angle D — Altitude (design height)

```
Is this a bandaid on shared infrastructure?
```

| Bandaid | Prefer |
|---------|--------|
| special case in Page | `PostingPolicy` extension |
| one-off validation in UI | Application service |
| duplicate nav logic | `NavigationCatalog` |
| magic string status | enum + central map |

**Codex:** abstraction only when removes real complexity or matches local pattern.

---

## 4. Phase 2 — Merge & Apply

```
1. Collect findings from all 4 angles
2. Dedup same line · same issue
3. Apply fixes that do NOT change behavior
4. Skip false positives — state reason explicitly
5. Report: fixed · skipped · already clean
```

| Skip reason examples | |
|----------------------|--|
| "Audit trail must stay verbose" | compliance |
| "Explicit for regulator" | domain |
| "Premature abstraction" | only 1 use site |

---

## 5. BRQOSH — Sacred (never simplify away)

| Do not touch | Why |
|--------------|-----|
| AuditLog writes | compliance |
| PostingPolicy checks | financial truth |
| CompanyId / BranchId filters | isolation |
| SchemaSqlGuard registrations | PostgreSQL |
| Golden test assertions | proof |
| RBAC guards | security |
| Reason fields on cancel | audit |

---

## 6. Subagent Delegation (4 parallel readonly)

```
Task reuse-agent      → grep helpers · report duplicates
Task simplify-agent   → nesting · dead code
Task efficiency-agent → EF · IO patterns
Task altitude-agent   → policy vs page hacks
→ parent merges · applies safe cleanups only
```

Subagent prompt must include: diff scope · readonly · return format · BRQOSH sacred list.

---

## 7. Verification After Simplify

```
□ dotnet build PASS
□ dotnet test PASS — especially Golden if financial touched
□ no behavior change in reviewed scenarios
□ diff still minimal vs pre-simplify scope creep
```

**Never** simplify and add features in same pass.

---

## 8. Anti-Patterns

| Avoid | Instead |
|-------|---------|
| Simplify before P0 fix | fix first |
| Mega-refactor "while here" | scoped cleanup |
| Delete "unused" without grep | prove zero references |
| Merge unrelated files | one concern per commit |
| Remove comments that explain ZATCA | keep domain comments |

---

## 9. Integration Pipeline

```
PR diff
 ├─ security-review (≥80% confidence)
 ├─ standards ║ spec (parallel)
 ├─ code-review xhigh (bugs)
 ├─ FIX P0/P1
 ├─ simplify 4 angles  ← this module
 └─ CEEF if wave
```

**With:** [code-review.md](code-review.md) · [standards.md](standards.md) · [security.md](security.md)
