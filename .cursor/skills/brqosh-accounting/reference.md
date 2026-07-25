# Smart Mind — Accounting and ERP

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-accounting | see per-file load order

> Financial Truth · ZATCA · Saudi · Golden — **domain superiority over any general agent**

---

## Index

1. [Financial Truth Principle](#1-financial-truth-principle)
2. [DOMAIN_MAP Scopes](#2-domain_map-scopes)
3. [Posting Lifecycle](#3-posting-lifecycle)
4. [Workflow](#4-workflow)
5. [Inventory and Costing](#5-inventory-and-costing)
6. [Treasury and Banking](#6-treasury-and-banking)
7. [Tax and ZATCA](#7-tax-and-zatca)
8. [Branches and Currencies](#8-branches-and-currencies)
9. [Golden and FSA](#9-golden-and-fsa)
10. [Runtime Context](#10-runtime-context)
11. [Fatal Errors](#11-fatal-errors)
12. [Financial Acceptance Checklist](#12-financial-acceptance-checklist)

---

## 1. Financial Truth Principle

> No commercial/UI feature before journal correctness.

| # | Requirement |
|---|-------------|
| 1 | debit = credit |
| 2 | AuditLog on posting + Reason |
| 3 | no silent deletion of posted documents |
| 4 | Golden covers the path |
| 5 | PostingPolicy is enforced |

---

## 2. DOMAIN_MAP Scopes

### Financial Core
| Module | Domain | Note |
|--------|--------|------|
| GL | Accounting | source of truth |
| AP | Purchasing | PI · Payment · DN |
| AR | Sales | Invoice · Receipt · CN |
| Treasury | Treasury | Bank · Transfer · Forecast |
| Assets | Assets | depreciation |
| Tax | VAT + ZATCA | O-01 live ⏭ |

### Commercial
Sales · Purchasing · Inventory · Pricing(stub) · CRM(stub)

### Operational
Construction · Manufacturing(BOM/WO)

### Platform
Security · Workflow · Audit · Reporting · Events · AI ⏭

**Do not mix:** sales must not write journal entries directly in UI — via PostingService.

---

## 3. Posting Lifecycle

```
Draft → (Workflow approval) → PostingOrchestrator
  → IPostingPolicy.Validate
  → JournalEntry + lines
  → AuditLog (immutable)
  → Status = Posted
```

### Cancellation/Reversal
- documented official path
- full audit trail
- no silent UPDATE on posted `JournalEntry`

### Posting Services (Examples)
- `SalesInvoicePostingService`
- `PurchaseInvoicePostingService`
- `PaymentVoucherPostingService`
- … all via orchestrator + policy

---

## 4. Workflow

`WorkflowEngine` — CEEF-105:
- Purchase Request · PO · Payment · Manual JE
- approval before posting
- Desktop UI for approvals

**Rule:** no workflow bypass from admin code without authorization.

---

## 5. Inventory and Costing

- WAC / FIFO per policy
- GRN links purchasing and inventory
- Landed cost
- **no** sale with negative stock without explicit policy
- LIFECYCLE-33 reservation ⏭ — do not claim it closed

### Sales Orders (CEEF-118)
`SalesOrder`: Draft → Confirmed → Invoiced

---

## 6. Treasury and Banking

- `BankAccount` · reconciliation
- `CashFlowForecastService` — forecast
- `CashFlowStatementService` — report (indirect)
- `PaymentRunService`
- BranchId on transactions

---

## 7. Tax and ZATCA

### In-Repository ✅
- XML generation · QR
- CSID wizard UI
- integration tests
- `docs/ZATCA_LIVE_CSID.md`

### External ⏭ O-01
- authority OTP
- 10 accepted production invoices
- `dist/evidence/O-01-zatca/`

**Do not write "ZATCA production ready"** without evidence.

### VAT
- 15-digit TRN
- tax accounts from Chart
- `VatReturnQuery`

---

## 8. Branches and Currencies

- `CompanyId` — company isolation
- `Branch` + `BranchSessionContext`
- `FxConversion` · SAMA chain stub — O-08 live ⏭
- IC (intercompany) — CEEF-026+

---

## 9. Golden and FSA

| Gate | Purpose |
|------|---------|
| Golden 8/8 | accounting scenarios |
| FullScenarioAudit 13/13 | end-to-end path |
| Journal stress | O-04 prep 2k–5k |

```powershell
dotnet run --project tools\FullScenarioAudit\FullScenarioAudit.csproj
```

---

## 10. Runtime Context

```csharp
AppServices.CreateDb(); // BindSecurityContext internally
// CompanyId · User · Role · Branch
```

- Login = **user** · Provisioner = **company**
- Company switcher resets nav + RBAC

---

## 11. Fatal Errors

| # | Error |
|---|-------|
| 1 | unbalanced entry |
| 2 | editing posted document |
| 3 | posting without audit |
| 4 | bypassing closed fiscal period |
| 5 | VAT without account |
| 6 | query without CompanyId |
| 7 | admin without SecurityContext |
| 8 | claiming live ZATCA without OTP |

---

## 12. Financial Acceptance Checklist

- [ ] PostingPolicy tests
- [ ] new Golden scenario if new path
- [ ] AuditLog is written
- [ ] UI reflects Status
- [ ] RBAC on posting/cancellation
- [ ] CEEF if wave

**Review:** [code-review.md](code-review.md) angle H + financial path

---

## v4 — Extended Golden Scenarios

| # | Scenario | Proves |
|---|----------|--------|
| 1 | cash sale | AR + inventory |
| 2 | credit purchase | AP + GRN |
| 3 | posting + cancellation | audit + reversal |
| 4 | bank transfer | treasury |
| 5 | period close | period lock |
| 6 | separate branch | BranchId |
| 7 | workflow PO | approval |
| 8 | FX stub | conversion chain |

**Any new financial feature:** add a row here or extend Golden — do not close without it.

### ZATCA Truth Path
```
in-repo: XML/QR/tests ✅
live: OTP + 10 invoices ⏭ O-01
```
Do not mix the two levels in CEEF.

---

## v6 — Angle I + Cumulative Risk (from xhigh + Fable)

### Angle I — Financial Review (mandatory on every financial diff)
```
Every change in Posting* · Journal* · Invoice* · VAT*:
  → balanced? CompanyId? Audit? Period? Branch? Golden?
```

### cumulative risk (Fable pattern)
Judge the **aggregate conversation/diff** — not an isolated line.  
A chain of small changes may equal a posting vulnerability.

### legal/financial (Opus 4.8)
- facts and options — not "I advise you to buy/sell"
- BRQOSH = system — not a tax advisor

**Integration:** [integration](../brqosh-skills-hub/integration.md) · [brqosh-closure.md](brqosh-closure.md)

---

## v15c — Posting Decision Tree (agent quick ref)

```
Document event?
  ├─ Draft only → no JournalEntry
  ├─ Post requested
  │    ├─ Workflow required? → approved?
  │    ├─ PostingPolicy.Validate PASS?
  │    ├─ Period open? Branch allowed?
  │    ├─ Orchestrator → balanced lines
  │    └─ AuditLog written?
  └─ Cancel posted
       ├─ Official reversal path only
       └─ Audit + reason mandatory
```

---

## v15c — ZATCA Level Discipline (never mix)

| Level | Proof location | Agent may claim |
|-------|----------------|---------------|
| In-repo logic | src/ + tests | ✅ code + test |
| Integration stub | tests + docs | ✅ with limits stated |
| Live OTP production | dist/evidence/O-01-zatca/ | ⏭ only |
| 10 accepted invoices | same evidence pack | ⏭ only |

---

## v15c — Intercompany · FX · Multi-entity

| Topic | In-repo | External |
|-------|---------|----------|
| BranchId filter | ✅ | — |
| IC elimination stub | CEEF-026+ docs | partial |
| SAMA live rates | stub chain | O-08 ⏭ |
| Consolidation reporting | roadmap | — |

Always `CompanyId` on operational queries — cumulative IDOR risk across small diffs.

---

## v15c — Agent Competition vs Accountant Human

| Human accountant | Agent with this module |
|------------------|------------------------|
| Intuition on entry | PostingPolicy + Golden |
| Forgets branch filter | grep CompanyId habit |
| Tired skip audit | AuditLog mandatory in code path |
| Knows ZATCA live status | reads OPEN_ITEMS — honest ⏭ |

**Corpus deepen:** `system_prompts_leaks-main/OpenAI/Codex/` judgment · not copy.
