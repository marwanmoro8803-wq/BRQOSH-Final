---
name: brqosh-closure
description: >-
  BRQOSH wave closure: CEEF reports, 4 levels (code/test/live/external),
  O-01..O-05 gates, dist/evidence, competition mode. Use when closing a
  phase, writing CEEF, or claiming a feature is complete.
---

# BRQOSH Closure

> **Language:** English only · binding MUST/MUST NOT — [language-enforcement.md](../brqosh-skills-hub/language-enforcement.md)
> **Cloud Code:** [CLOUD-CODE-AGENT-GUIDE.md](../../docs/CLOUD-CODE-AGENT-GUIDE.md) · [SKILLS-CLOUD-CODE-INDEX.yaml](../brqosh-skills-hub/SKILLS-CLOUD-CODE-INDEX.yaml)

## 4 levels

| Level | Proof |
|-------|-------|
| Code | src/ + build |
| Test | dotnet test PASS |
| Live | OTP, VM, ZAP in dist/evidence/ |
| External | docs/OPEN_ITEMS.md |

**No partial closure as complete.**

## Competition mode

explore -> narrow implement -> build+test -> gates -> CEEF -> update OPEN_ITEMS

## Reference

[reference.md](reference.md) · [zero-gap-matrix.md](zero-gap-matrix.md) · Status: `brqosh-skills-hub/STATUS.yaml`

---

## Engineering Roadmap

> **Spec:** [../brqosh-skills-hub/ROADMAP-SPEC.md](../brqosh-skills-hub/ROADMAP-SPEC.md)  
> **Path ID:** P-BRQOSH-CLOSE · **Layer:** L2 domain · **Status:** [STATUS.yaml](../brqosh-skills-hub/STATUS.yaml)

## Default path

```
SKILL → reference.md → 4 levels → gates script → CEEF → OPEN_ITEMS
```

---

### `SKILL.md` · L0 · 1/3

| | |
|--|--|
| **TRIGGER** | "close" · CEEF · wave · "done" · competition mode |
| **SKIP** | Never when claiming completion |
| **NEXT** | reference.md |
| **EXIT** | 4 levels named · no partial closure |
| **ANTI** | Checkmark without build/test/evidence |
| **VERIFY** | Each level has proof or explicit external |
| **TIME** | 25s |

---

### `zero-gap-matrix.md` · L2 · 2/3

| | |
|--|--|
| **TRIGGER** | Wave complete claim · "supremacy" · gap audit |
| **SOURCE** | archive/zero-gap-matrix-v8.md (merged v14) |
| **EXIT** | 20 gaps mapped or âڈ­ stated |
| **TIME** | 60s |

---

### `reference.md` · L3 · 3/3

| | |
|--|--|
| **TRIGGER** | CEEF write · O-01..O-05 · gates detail |
| **SKIP** | Quick status — SKILL suffices |
| **EXIT** | CEEF report + evidence paths |
| **ANTI** | OTP/ZAP/VM closed from code only |
| **VERIFY** | sync-agent-status · run-external-gates |
| **TIME** | 90s |

## 4 levels (mandatory order)

1. code + build  
2. dotnet test (STATUS.yaml count)  
3. live — dist/evidence/ only  
4. external — OPEN_ITEMS.md

## Scenarios

| Request | VERIFY |
|---------|--------|
| Close CEEF wave | CEEF file + test count |
| O-01 ZATCA OTP | external only |
