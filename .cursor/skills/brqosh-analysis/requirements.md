# Smart Mind — Requirements · Deep Analysis · Human Mind

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-analysis | see per-file load order

> **From human intent to testable spec** — understand *why* before *how*.

---

## Index

1. [Requirements Pyramid](#1-requirements-pyramid)
2. [Deep Analysis — 12 Questions](#2-deep-analysis--12-questions)
3. [Requirement Types](#3-requirement-types)
4. [From Speech to Code](#4-from-speech-to-code)
5. [User Human Mind](#5-user-human-mind)
6. [Developer Human Mind](#6-developer-human-mind)
7. [Adversarial Analysis](#7-adversarial-analysis)
8. [BRQOSH — Templates](#8-brqosh--templates)
9. [Detail Precision](#9-detail-precision)

---

## 1. Requirements Pyramid

```
        ┌──────────────┐
        │   Vision     │  Why the product?
        └──────┬───────┘
               │
        ┌──────▼───────┐
        │  Goals       │  KPI · success · market
        └──────┬───────┘
               │
        ┌──────▼───────┐
        │  Capabilities│  ERP · ZATCA · multi-company
        └──────┬───────┘
               │
        ┌──────▼───────┐
        │  Features    │  invoice · posting · report
        └──────┬───────┘
               │
        ┌──────▼───────┐
        │  stories     │  As a user I want…
        └──────┬───────┘
               │
        ┌──────▼───────┐
        │  acceptance  │  Given/When/Then · test
        └──────────────┘
```

**Do not write code** below a clear **story + acceptance** level.

---

## 2. Deep Analysis — 12 Questions

Before any feature — answer (explore the repository first):

| # | Question |
|---|----------|
| 1 | **Who** is the user? What is their permission? |
| 2 | **What** do they want to achieve (outcome, not tool)? |
| 3 | **Why** now? P0/P1 priority? |
| 4 | **When** is it considered successful (measurable criteria)? |
| 5 | **Where** in DOMAIN_MAP? |
| 6 | **What is explicitly out of scope**? |
| 7 | **What if it fails**? rollback · message · audit? |
| 8 | **What if it partially succeeds**? (no partial closure) |
| 9 | **Who depends** on this? upstream/downstream |
| 10 | **What legal constraints** (ZATCA · SOCPA)? |
| 11 | **What evidence** for closure (4 levels)? |
| 12 | **What test** proves it? |

---

## 3. Requirement Types

| Type | Question | BRQOSH |
|------|----------|--------|
| **Functional** | What does it do? | Post invoice |
| **Non-functional** | How does it perform? | <2s list load |
| **Domain** | Accounting rules? | debit=credit |
| **Compliance** | Legal? | ZATCA XML |
| **UI** | Experience? | RTL · Arabic |
| **Operational** | Deployment? | MSI · O-03 |
| **Security** | Who sees what? | RBAC |
| **Data** | Isolation? | CompanyId |

Each type requires **different evidence** in CEEF.

---

## 4. From Speech to Code

### Conversion Path

```
Vague Arabic request
  → Extract verbs + entities + constraints
  → Match existing Domain entities
  → gap list (exists · missing · ⏭)
  → decision-complete spec
  → tasks → code → tests
```

### BRQOSH Example
**Request:** "I want sales quotations"

| Step | Output |
|------|--------|
| Entity | SalesQuotation · lines · customer |
| State | Draft → Sent → … |
| Link | Invoice · SalesOrder |
| UI | SalesQuotationsPage |
| test | SalesOrderTests |
| CEEF | 117/118 |

---

## 5. User Human Mind

### Mental Models (Saudi Accountant)

| Model | Expectation | Design |
|-------|-------------|--------|
| **Paper document** | Draft → approved → posted | Status badges |
| **Ledger** | Balanced entry | No posting without balance |
| **Tax** | Clear VAT | tax columns · ZATCA |
| **Fear of error** | Confirm before posting | dialogs · workflow |
| **Time pressure** | Shortcuts · search | keyboard · debounce search |

### Expected Human Errors
- Forgetting to select branch · customer · account
- Wrong decimal separator input
- Post then "undo" — needs official path
- Switch company without attention — clear switcher

**Design prevents error** — does not blame the user.

---

## 6. Developer Human Mind

| Bias | Remedy |
|------|--------|
| Assume old code is correct | grep + test |
| over-engineering | narrow scope |
| under-testing financial | Golden |
| "I'll test later" | test before closure |
| Admiration for clever solution | Simple code a peer can read |

---

## 7. Adversarial Analysis

Ask as an **adversary** to the requirement:

| Attack | Question |
|--------|----------|
| Security | Can RBAC be bypassed? |
| Financial | Can an unbalanced entry be posted? |
| Data | Is CompanyId missing? |
| Operations | Does it work on PG, not SQLite only? |
| Market | Does it claim ZATCA without OTP? |
| UX | Does RTL break the grid? |

What collapses here → **P0 before ship**.

---

## 8. BRQOSH — Templates

### Feature spec (decision-complete)

```markdown
## [ID] Title
**Scope:** DOMAIN_MAP → …
**User:** role · permission
**Before:** system state
**After:** system state
**Domain rules:** …
**Interfaces:** API/DTO if present
**edge cases:** …
**Tests:** file · scenario
**Closure:** code+test / ⏭
```

### Trace `docs/EXECUTION_ROADMAP_MASTER.md` / plan

```
Plan item → grep in src → status → CEEF
```

---

## 9. Detail Precision

### Detail Levels

| Level | When |
|-------|------|
| L1 | Understand request |
| L2 | spec + interfaces |
| L3 | every financial edge case |
| L4 | every UI field + validation |
| L5 | every PG type + migration |

**Financial = L3 minimum** · **schema = L5** · **UI button = L2**.

### "Understand Everything" in Practice

= Ability to **trace any question** through:
requirements pyramid → DOMAIN_MAP → file → test → closure evidence

Not memorizing every syntax in the world.

---

**Foundation:** [super-dev-foundation.md](super-dev-foundation.md) · **Languages:** [programming-universe.md](programming-universe.md)

---

## v4 — Consultant-Grade Analysis

### Stakeholder Matrix
| Party | Question |
|-------|----------|
| Accountant | Is the journal correct? |
| Manager | Is the KPI correct? |
| IT | Does PG work? |
| Authority | Is ZATCA ⏭ documented? |
| Investor | Is ⏭ explicit? |

### Risk Analysis (5×5)
Probability (1-5) × Impact (1-5) → rank · address >12 first

### Go/No-Go for Wave
| Go | No-Go |
|----|-------|
| tests PASS | any fail |
| spec clear | P0 ambiguity |
| Golden updated | financial without test |
| CEEF draft | fake live claim |

---

## v6 — Search-First for Requirements (Sonnet 5)

| Source | Before assuming |
|--------|-----------------|
| `docs/EXECUTION_ROADMAP_MASTER.md` | Read |
| `.cursor/plans/` | Read |
| `OPEN_ITEMS.md` | Read |
| ZATCA/SOCPA | WebSearch if needed |
| Code | grep — **ground truth** |

### Gemini Rule 1
Task with clear success criteria → **execute** · no follow-up "do you want?"

**Integration:** [integration](../brqosh-skills-hub/integration.md) · [deep-cognition.md](deep-cognition.md)
