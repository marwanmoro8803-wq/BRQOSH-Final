# Architecture Snapshot

## Repository Map

337 modules, 32676 symbols, grouped by area. Every module is in `facts.jsonl`, or query_facts(kind="module").

| Area | Modules | Symbols | Languages |
|------|---------|---------|-----------|
| `.claude` | 224 | 21694 | csharp, python, xaml |
| `src` | 81 | 9942 | csharp, xaml |
| `tools` | 23 | 585 | csharp, python |
| `ShuraCouncil` | 1 | 354 | csharp |
| `PromptAuditEngine` | 1 | 75 | csharp |
| `scripts` | 2 | 22 | python |
| `installer` | 1 | 4 | csharp |
| `tests` | 4 | 0 | csharp |

Largest modules:
- `src/Brqosh.Desktop/Views/Pages` — 2469 symbols (csharp)
- `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Desktop/Views/Pages` — 2468 symbols (csharp)
- `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Desktop/Views/Pages` — 2468 symbols (csharp)
- `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Reporting` — 718 symbols (csharp)
- `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Infrastructure/Reporting` — 718 symbols (csharp)
- `src/Brqosh.Infrastructure/Reporting` — 718 symbols (csharp)
- `.claude/worktrees/brqosh-dream-program-53744f/ShuraCouncil` — 354 symbols (csharp)
- `.claude/worktrees/sleepy-tesla-4ea32c/ShuraCouncil` — 354 symbols (csharp)
- `ShuraCouncil` — 354 symbols (csharp)
- `src/Brqosh.Infrastructure/Persistence` — 336 symbols (csharp)
- `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence` — 311 symbols (csharp)
- `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Infrastructure/Persistence` — 311 symbols (csharp)
- `src/Brqosh.Desktop/Ui` — 290 symbols (csharp)
- `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Desktop/Ui` — 269 symbols (csharp)
- `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Desktop/Ui` — 263 symbols (csharp)
- `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Accounting` — 241 symbols (csharp)
- `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Domain/Accounting` — 241 symbols (csharp)
- `src/Brqosh.Domain/Accounting` — 241 symbols (csharp)
- `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application/Reporting` — 240 symbols (csharp)
- `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Application/Reporting` — 240 symbols (csharp)

## Extraction Quality

- Files parsed: **4746** / 12329 seen (1833 file(s) + 41 directory tree(s) skipped by ignore globs)
- Parse errors: 0

## Architecture Pattern

**Architecture pattern: dotnet-clean** (confidence: 67%)

Recognised dotnet-clean from directory names: 201 of 301 csharp/xaml modules classified — 93% of this repository's 325 modules. All 39 imports between ordered layers run inward; none run against the order.

Layer mapping:
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api" maps to layer "api"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api/Idempotency" maps to layer "api"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api/Middleware" maps to layer "api"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application" maps to layer "application"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application/Accounting" maps to layer "application"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application/Intelligence" maps to layer "application"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application/MasterData" maps to layer "application"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application/Platform" maps to layer "application"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application/Reporting" maps to layer "application"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application/Setup" maps to layer "application"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application/Treasury" maps to layer "application"
- module ".claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain" maps to layer "domain"
- … and 189 more (query_insights(explainer="layers") for all)

## How to Add a Feature

This code is laid out as **dotnet-clean**. A dependency runs from an outer layer to an inner one, so a feature is built inward:

1. **api** — e.g. `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api`
2. **infrastructure** — e.g. `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure`
3. **application** — e.g. `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application`
4. **domain** — e.g. `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain`

## Entry Points

- **main**: `.claude/worktrees/brqosh-dream-program-53744f/scripts/apply-empty-state-grid-count.main` (.claude/worktrees/brqosh-dream-program-53744f/scripts/apply-empty-state-grid-count.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/scripts/apply-ui-cards.main` (.claude/worktrees/brqosh-dream-program-53744f/scripts/apply-ui-cards.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/scripts/ops/build-leak-corpus-index.main` (.claude/worktrees/brqosh-dream-program-53744f/scripts/ops/build-leak-corpus-index.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/scripts/ops/finalize-skills-v14.main` (.claude/worktrees/brqosh-dream-program-53744f/scripts/ops/finalize-skills-v14.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/scripts/ops/fix-skill-encoding.main` (.claude/worktrees/brqosh-dream-program-53744f/scripts/ops/fix-skill-encoding.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/scripts/red_proof.main` (.claude/worktrees/brqosh-dream-program-53744f/scripts/red_proof.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/scripts/third_party_notices.main` (.claude/worktrees/brqosh-dream-program-53744f/scripts/third_party_notices.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/tools/brand/build_brand.main` (.claude/worktrees/brqosh-dream-program-53744f/tools/brand/build_brand.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/tools/brand/currency_flags.main` (.claude/worktrees/brqosh-dream-program-53744f/tools/brand/currency_flags.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/tools/desktop-cognitive-audit/audit_orchestrator.main` (.claude/worktrees/brqosh-dream-program-53744f/tools/desktop-cognitive-audit/audit_orchestrator.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/tools/desktop-omni-audit/audit_orchestrator.main` (.claude/worktrees/brqosh-dream-program-53744f/tools/desktop-omni-audit/audit_orchestrator.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/tools/desktop-qa-army/audit_orchestrator.main` (.claude/worktrees/brqosh-dream-program-53744f/tools/desktop-qa-army/audit_orchestrator.py)
- **main**: `.claude/worktrees/brqosh-dream-program-53744f/tools/pg-latency-proxy.main` (.claude/worktrees/brqosh-dream-program-53744f/tools/pg-latency-proxy.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/scripts/apply-empty-state-grid-count.main` (.claude/worktrees/sleepy-tesla-4ea32c/scripts/apply-empty-state-grid-count.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/scripts/apply-ui-cards.main` (.claude/worktrees/sleepy-tesla-4ea32c/scripts/apply-ui-cards.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/scripts/ops/build-leak-corpus-index.main` (.claude/worktrees/sleepy-tesla-4ea32c/scripts/ops/build-leak-corpus-index.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/scripts/ops/finalize-skills-v14.main` (.claude/worktrees/sleepy-tesla-4ea32c/scripts/ops/finalize-skills-v14.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/scripts/ops/fix-skill-encoding.main` (.claude/worktrees/sleepy-tesla-4ea32c/scripts/ops/fix-skill-encoding.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/scripts/red_proof.main` (.claude/worktrees/sleepy-tesla-4ea32c/scripts/red_proof.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/scripts/third_party_notices.main` (.claude/worktrees/sleepy-tesla-4ea32c/scripts/third_party_notices.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/tools/brand/build_brand.main` (.claude/worktrees/sleepy-tesla-4ea32c/tools/brand/build_brand.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/tools/desktop-cognitive-audit/audit_orchestrator.main` (.claude/worktrees/sleepy-tesla-4ea32c/tools/desktop-cognitive-audit/audit_orchestrator.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/tools/desktop-omni-audit/audit_orchestrator.main` (.claude/worktrees/sleepy-tesla-4ea32c/tools/desktop-omni-audit/audit_orchestrator.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/tools/desktop-qa-army/audit_orchestrator.main` (.claude/worktrees/sleepy-tesla-4ea32c/tools/desktop-qa-army/audit_orchestrator.py)
- **main**: `.claude/worktrees/sleepy-tesla-4ea32c/tools/pg-latency-proxy.main` (.claude/worktrees/sleepy-tesla-4ea32c/tools/pg-latency-proxy.py)
- **main**: `scripts/apply-empty-state-grid-count.main` (scripts/apply-empty-state-grid-count.py)
- **main**: `scripts/apply-ui-cards.main` (scripts/apply-ui-cards.py)
- **main**: `scripts/ops/build-leak-corpus-index.main` (scripts/ops/build-leak-corpus-index.py)
- **main**: `scripts/ops/finalize-skills-v14.main` (scripts/ops/finalize-skills-v14.py)
- **main**: `scripts/ops/fix-skill-encoding.main` (scripts/ops/fix-skill-encoding.py)
- **main**: `scripts/red_proof.main` (scripts/red_proof.py)
- **main**: `scripts/third_party_notices.main` (scripts/third_party_notices.py)
- **main**: `tools/brand/build_brand.main` (tools/brand/build_brand.py)
- **main**: `tools/brand/currency_flags.main` (tools/brand/currency_flags.py)
- **main**: `tools/desktop-cognitive-audit/audit_orchestrator.main` (tools/desktop-cognitive-audit/audit_orchestrator.py)
- **main**: `tools/desktop-omni-audit/audit_orchestrator.main` (tools/desktop-omni-audit/audit_orchestrator.py)
- **main**: `tools/desktop-qa-army/audit_orchestrator.main` (tools/desktop-qa-army/audit_orchestrator.py)
- **main**: `tools/pg-latency-proxy.main` (tools/pg-latency-proxy.py)
102 routes, 15 shown:
- **route** DELETE `/api/v1/company-groups/{groupId:long}/intercompany-account-maps/{accountCode}` (.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api/Program.cs)
- **route** DELETE `/api/v1/company-groups/{groupId:long}/intercompany-account-maps/{accountCode}` (.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Api/Program.cs)
- **route** DELETE `/api/v1/company-groups/{groupId:long}/intercompany-account-maps/{accountCode}` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/billing-plans` (.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/billing-plans` (.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/billing-plans` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/branches` (.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/branches` (.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/branches` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/companies/{companyId:long}/exchange-rates` (.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/companies/{companyId:long}/exchange-rates` (.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/companies/{companyId:long}/exchange-rates` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/companies` (.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/companies` (.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/companies` (src/Brqosh.Api/Program.cs)
- … and 87 more (query_facts(kind="route") for all)

## Routes

102 routes, grouped by path prefix. query_facts(kind="route") for all of them.

| Prefix | Routes | Methods | Example |
|--------|--------|---------|----------|
| `/api` | 93 | DELETE, GET, POST | `/api/v1/billing-plans` |
| `/health` | 9 | GET | `/health` |

## Storage

321 data stores. query_facts(kind="storage") for all of them.

- **context** (3): `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence.BrqoshDbContext`, `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Infrastructure/Persistence.BrqoshDbContext`, `src/Brqosh.Infrastructure/Persistence.BrqoshDbContext`
- **entity** (84): `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Accounting.Account`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Accounting.Budget`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Accounting.CostCenter`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Accounting.FiscalPeriod`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Accounting.JournalEntry`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Accounting.OpeningBalanceItem`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Assets.FixedAsset`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Companies.Branch`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Companies.Company`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Companies.CompanyGroup`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Companies.IntercompanyAccountMap`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Construction.ConstructionContract`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Construction.ConstructionProject`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Construction.ProgressBilling`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Construction.ProjectCostEntry`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Construction.SubcontractAgreement`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Crm.CrmActivity`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Crm.CrmLead`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Crm.CrmOpportunity`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Healthcare.InsuranceClaim`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Healthcare.InsurancePayer`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Healthcare.InsurancePolicy`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Healthcare.Patient`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Healthcare.Visit`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain/Hospitality.GuestFolio` … and 59 more
- **migration** (234): `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddAssetPurchaseInvoice`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddAuditChain`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddBankStatementBalances`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddBatchTracking`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddBomConversionCosts`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddBudgets`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddBuyerRegulatoryIdentity`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddCancelledAtToTaxDocuments`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddCodCollectionPostedFlag`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddCompanyAddressEn`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddCompanyAuditorName`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddCostCenterToDocuments`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddCostConcurrencyTokens`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddCreditNoteCurrency`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddCreditNoteDocumentDiscount`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddCustomerCreditLimit`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddDebitNoteCurrency`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddDocumentCurrency`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddEmployeeAdvances`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddEmployeeProject`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddEmployeesAndGosi`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddFixedAssetCancelledOn`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddFixedAssetDepreciatedThrough`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddFixedAssetDepreciationMethod`, `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure/Persistence/Migrations.AddFixedAssetDisposalSale` … and 209 more

## Dependency Rules

173 internal module dependencies. The modules that reach furthest; traverse() or query_facts(kind="dependency") for the edges themselves.

| Module | Depends on |
|--------|------------|
| `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure` | 29 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Infrastructure` | 29 modules |
| `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain` | 23 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Domain` | 23 modules |
| `src/Brqosh.Infrastructure` | 6 modules |
| `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Application` | 5 modules |
| `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Desktop` | 5 modules |
| `.claude/worktrees/brqosh-dream-program-53744f/tools` | 5 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Application` | 5 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Desktop` | 5 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c/tools` | 5 modules |
| `src/Brqosh.Desktop/Views` | 5 modules |
| `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Desktop/Views` | 4 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Desktop/Views` | 4 modules |
| `.claude/worktrees/brqosh-dream-program-53744f/src` | 3 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c/src` | 3 modules |
| `src` | 3 modules |
| `.claude/worktrees/brqosh-dream-program-53744f` | 2 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c` | 2 modules |
| `tools` | 2 modules |
| `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Api` | 1 modules |
| `.claude/worktrees/brqosh-dream-program-53744f/tools/desktop-qa-army` | 1 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Api` | 1 modules |
| `.claude/worktrees/sleepy-tesla-4ea32c/tools/desktop-qa-army` | 1 modules |
| `tools/desktop-qa-army` | 1 modules |

## Critical Modules

| Module | Fan-In | Fan-Out | Criticality |
|--------|--------|---------|-------------|
| `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Infrastructure` | 0 | 40 | high |
| `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Infrastructure` | 0 | 40 | high |
| `src/Brqosh.Application/Intelligence` | 30 | 0 | high |
| `.claude/worktrees/brqosh-dream-program-53744f/src/Brqosh.Domain` | 0 | 23 | high |
| `.claude/worktrees/sleepy-tesla-4ea32c/src/Brqosh.Domain` | 0 | 23 | high |
| `src/Brqosh.Infrastructure` | 2 | 15 | high |
| `src/Brqosh.Domain/Accounting` | 14 | 0 | high |
| `tools/desktop-cognitive-audit` | 11 | 0 | high |
| `.claude/worktrees/brqosh-dream-program-53744f/tools` | 0 | 7 | medium |
| `.claude/worktrees/sleepy-tesla-4ea32c/tools` | 0 | 7 | medium |

---

*Generated at 2026-10-27T00:28:34Z in 2m23.0213954s. 67276 facts, 184 insights.*
