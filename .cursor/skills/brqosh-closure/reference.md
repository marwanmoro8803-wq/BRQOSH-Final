# Smart Mind — Strength · Precision · Execution · Closure

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-closure | see per-file load order

> CEEF · Gates · Evidence · Competition mode

---

## Index

1. [Definition of Strength](#1-definition-of-strength)
2. [4 Closure Levels](#2-4-closure-levels)
3. [Competition Mode](#3-competition-mode)
4. [CEEF Protocol](#4-ceef-protocol)
5. [Gates O-01…O-05](#5-gates-o-01o-05)
6. [Continuity](#6-continuity)
7. [Strength Metrics](#7-strength-metrics)
8. [GTM stubs](#8-gtm-stubs)
9. [Weakness Countermeasures](#9-weakness-countermeasures)
10. [Wake-Up Checklist](#10-wake-up-checklist)

---

## 1. Definition of Strength

```
BRQOSH strength = proven execution + honesty + accumulated rules/tests
                − false claims − scope expansion without request
```

**Not:** verbose talk · excessive files · prompt copies.

---

## 2. 4 Closure Levels

| Symbol | Meaning | Evidence |
|--------|---------|----------|
| ✅ code | `src/` | build |
| ✅ test | behavior | `dotnet test` 0 fail |
| ✅ live | OTP · VM · ZAP · SOCPA | `dist/evidence/` |
| ⏭ | documented external | OPEN_ITEMS + runbook |

**No exceptions.** No "ready to launch" without a level.

---

## 3. Competition Mode

```
① parallel exploration (grep/read · PROJECT-MEMORY history)
② impact-before-change — scope · links · conflicts
③ load domain skill (accounting/design/…)
④ narrow execution
⑤ dotnet build + test
⑥ run-external-gates.ps1 (major waves)
⑦ CEEF-XXX report
⑧ append PROJECT-MEMORY.yaml (+ docs/memory/ if 3+ modules)
⑨ update OPEN_ITEMS · plan · rules
⑩ ⏭ explicit for what remains
```

---

## 4. CEEF Protocol

### Report Structure
```markdown
# CEEF-XXX — Wave Title
## Scope
## Changes (files)
## Verification (commands + results)
## What was closed (4 levels)
## What remains ⏭
## References
```

### CEEF Sequence
101–116 V12 · 117–118 V13 · 119 O-gates prep · …

---

## 5. Gates O-01…O-05

| Item | in-repo | live ⏭ |
|------|---------|---------|
| O-01 ZATCA | runbook · UI · tests | OTP · 10 invoices |
| O-02 SOCPA | package | signed PDF |
| O-03 MSI | verify-msi-build | clean VM |
| O-04 Load | stress 2k–5k | 1M VM |
| O-05 ZAP | static scan | Docker baseline |

```powershell
.\scripts\ops\run-external-gates.ps1
```

**MASTER:** `docs/ops/MASTER_EXTERNAL_GATES_RUNBOOK.md`

---

## 6. Continuity

| Principle | Source |
|-----------|--------|
| do not stop at analysis | Codex |
| alternate path when blocked | Grok |
| last request after compaction | Cursor |
| background shell until exit | Grok |
| no commit unless requested | Gemini |

---

## 7. Strength Metrics

| Metric | Reference value |
|--------|-----------------|
| Tests | **417+** PASS |
| Golden | 8/8 |
| FullScenario | 13/13 |
| Coverlet | ≥75% (80.64%) |
| Desktop.Tests | 80 |
| Harness | PASS |

**Raise the count** with each wave — do not regress.

---

## 8. GTM stubs

`docs/gtm/` — PRICING · SLA · TRAINING  
partial in-repo · Stripe/helpdesk ⏭ O-09

---

## 9. Weakness Countermeasures

| Weakness | Remedy |
|----------|--------|
| "done" without test | run test |
| fake O-01 | evidence folder |
| forgotten PG | schema gate |
| forgotten RBAC | BindSecurityContext |
| premature marketing | MARKET_LAUNCH_GAP |

---

## 10. Wake-Up Checklist

`docs/ops/WAKE_UP_CHECKLIST.md`

```powershell
cd C:\Users\qc\Projects\BRQOSH
$env:BRQOSH_SKIP_STRESS='1'; $env:BRQOSH_TEST='1'
.\scripts\ops\run-external-gates.ps1
# then O-01 if OTP available
```

**Current phase:** CEEF-118 closed · CEEF-119 prep ✅ · next O-01…O-05 live ⏭

---

## v4 — Evidence Guide + CHAMPION

| Item | Folder | in-repo | live |
|------|--------|---------|------|
| prep | O-00-prep | ✅ | — |
| ZATCA | O-01-zatca | readiness | OTP |
| SOCPA | O-02-socpa | manifest | PDF |
| MSI | O-03-msi | publish | VM |
| load | O-04-load | stress CI | 1M |
| ZAP | O-05-zap | static | baseline |

---

## persistence + plan

### Do not stop before
```
build PASS → test PASS → gates PASS → CEEF draft → evidence folders
```
active shell = do not end · compaction = last request leads.

### plan on first turn (major CEEF waves)
| warrants plan | do not start with |
|---------------|-------------------|
| O-01…O-05 live | typo · single test |
| 5+ namespaces | clarifying question |
| conflicting requirements | obvious build fix |

### search-first for state
`OPEN_ITEMS.md` · `WAKE_UP_CHECKLIST.md` · `dist/evidence/` — read before claiming closure.

**Integration:** [integration](../brqosh-skills-hub/integration.md) · [STATUS.yaml](../brqosh-skills-hub/STATUS.yaml)
