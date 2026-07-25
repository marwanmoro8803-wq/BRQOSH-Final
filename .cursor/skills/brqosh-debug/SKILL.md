---
name: brqosh-debug
description: >-
  Evidence-first debugging for BRQOSH: repro, hypothesis tree, minimal fix,
  rollback safety. Use when build fails, tests fail, null refs, flaky tests,
  ERP imbalance, or user says code is broken.
---

# BRQOSH Debug

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

## Before any fix

1. Repro, stack trace, or git diff (**Karpathy K4** — goal-driven, not blind patches)
2. Likely file/line
3. One main hypothesis + one alternative

## Quick tree

```
SYMPTOM -> reproducible? -> recent change? -> diff first
         -> else: config | dep | logic | data | env
```

## Report format

Root (1 sentence) -> evidence -> fix -> verify command -> external items if any

## BRQOSH

Schema changes need SchemaSqlGuard review. Golden tests for posting imbalance.

**Karpathy:** repro-first verify plan — [karpathy-synthesis.md](../supreme-programming-mastery/karpathy-synthesis.md) آ§3

## Reference

Full matrix: [reference.md](reference.md)

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-BRQOSH-BUG · **Layer:** L2 domain  
> **With:** supreme-programming + brqosh-engineering

## Default path

```
SKILL → reference.md → repro → hypothesis → minimal fix → dotnet test
```

---

### `SKILL.md` · L0 · 1/2

| | |
|--|--|
| **TRIGGER** | build fail · test fail · "code is broken" |
| **SKIP** | Never — triage before fix |
| **NEXT** | reference.md |
| **EXIT** | Rapid tree applied · one hypothesis stated |
| **ANTI** | Fix without repro · wide refactor |
| **VERIFY** | Root + evidence + verify command named |
| **TIME** | 30s |

---

### `reference.md` · L3 · 2/2

| | |
|--|--|
| **TRIGGER** | Golden fail · schema · flaky · WPF NullRef |
| **SOURCE** | archive/rapid-diagnosis-v10.md (merged v13) |
| **SKIP** | Simple fix obvious from stack trace |
| **EXIT** | Root cause + verify command |
| **ANTI** | Schema change without SchemaSqlGuard |
| **VERIFY** | dotnet test PASS on targeted suite |
| **TIME** | 120s |

## Scenarios

| Symptom | Path | VERIFY |
|---------|------|--------|
| GoldenPosting fail | reference Golden section → accounting | Golden pass |
| NullRef WPF | reference UI → brqosh-ui | repro + fix |
| Schema PG | reference SchemaSqlGuard | guard + migration test |
