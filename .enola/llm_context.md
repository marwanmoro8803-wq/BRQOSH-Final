# Architecture Snapshot

## Repository Map

112 modules, 10851 symbols, grouped by area. Every module is in `facts.jsonl`, or query_facts(kind="module").

| Area | Modules | Symbols | Languages |
|------|---------|---------|-----------|
| `src` | 81 | 9815 | csharp, xaml |
| `tools` | 23 | 585 | csharp, python |
| `ShuraCouncil` | 1 | 354 | csharp |
| `PromptAuditEngine` | 1 | 75 | csharp |
| `scripts` | 2 | 22 | python |
| `tests` | 4 | 0 | csharp |

Largest modules:
- `src/Brqosh.Desktop/Views/Pages` — 2468 symbols (csharp)
- `src/Brqosh.Infrastructure/Reporting` — 718 symbols (csharp)
- `ShuraCouncil` — 354 symbols (csharp)
- `src/Brqosh.Infrastructure/Persistence` — 311 symbols (csharp)
- `src/Brqosh.Desktop/Ui` — 269 symbols (csharp)
- `src/Brqosh.Domain/Accounting` — 241 symbols (csharp)
- `src/Brqosh.Application/Reporting` — 240 symbols (csharp)
- `src/Brqosh.Infrastructure/Persistence/Migrations` — 234 symbols (csharp)
- `src/Brqosh.Infrastructure/Zatca` — 230 symbols (csharp)
- `src/Brqosh.Domain/Sales` — 227 symbols (csharp)
- `src/Brqosh.Domain/Treasury` — 227 symbols (csharp)
- `src/Brqosh.Domain/Purchasing` — 217 symbols (csharp)
- `src/Brqosh.Domain/Payroll` — 215 symbols (csharp)
- `src/Brqosh.Infrastructure/Treasury` — 193 symbols (csharp)
- `src/Brqosh.Infrastructure/Security` — 192 symbols (csharp)
- `src/Brqosh.Domain/Inventory` — 190 symbols (csharp)
- `src/Brqosh.Infrastructure/Operations` — 173 symbols (csharp)
- `src/Brqosh.Domain/Healthcare` — 171 symbols (csharp)
- `src/Brqosh.Domain/Hospitality` — 163 symbols (csharp)
- `src/Brqosh.Domain/Construction` — 145 symbols (csharp)

## Extraction Quality

- Files parsed: **1579** / 4382 seen (605 file(s) + 14 directory tree(s) skipped by ignore globs)
- Parse errors: 0

## Architecture Pattern

**Architecture pattern: dotnet-clean** (confidence: 67%)

Recognised dotnet-clean from directory names: 67 of 100 csharp/xaml modules classified — 93% of this repository's 108 modules. All 590 imports between ordered layers run inward; none run against the order.

Layer mapping:
- module "src/Brqosh.Api" maps to layer "api"
- module "src/Brqosh.Api/Idempotency" maps to layer "api"
- module "src/Brqosh.Api/Middleware" maps to layer "api"
- module "src/Brqosh.Application" maps to layer "application"
- module "src/Brqosh.Application/Accounting" maps to layer "application"
- module "src/Brqosh.Application/Intelligence" maps to layer "application"
- module "src/Brqosh.Application/MasterData" maps to layer "application"
- module "src/Brqosh.Application/Platform" maps to layer "application"
- module "src/Brqosh.Application/Reporting" maps to layer "application"
- module "src/Brqosh.Application/Setup" maps to layer "application"
- module "src/Brqosh.Application/Treasury" maps to layer "application"
- module "src/Brqosh.Domain" maps to layer "domain"
- … and 55 more (query_insights(explainer="layers") for all)

## How to Add a Feature

This code is laid out as **dotnet-clean**. A dependency runs from an outer layer to an inner one, so a feature is built inward:

1. **api** — e.g. `src/Brqosh.Api`
2. **infrastructure** — e.g. `src/Brqosh.Infrastructure`
3. **application** — e.g. `src/Brqosh.Application`
4. **domain** — e.g. `src/Brqosh.Domain`

## Entry Points

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
34 routes, 15 shown:
- **route** DELETE `/api/v1/company-groups/{groupId:long}/intercompany-account-maps/{accountCode}` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/billing-plans` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/branches` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/companies/{companyId:long}/exchange-rates` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/companies` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/company-groups/{groupId:long}/intercompany-account-maps` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/company-groups/{groupId:long}/reports/consolidated-balance-sheet` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/company-groups/{groupId:long}/reports/consolidated-income-statement` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/company-groups/{groupId:long}/reports/consolidated-trial-balance` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/company-groups/{groupId:long}/reports/intercompany-eliminations` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/company-groups` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/customers` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/data-quality` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/domain-events` (src/Brqosh.Api/Program.cs)
- **route** GET `/api/v1/event-tracking` (src/Brqosh.Api/Program.cs)
- … and 19 more (query_facts(kind="route") for all)

## Routes

| Method | Path | File | Type |
|--------|------|------|------|
| GET | `/api/v1/billing-plans` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/branches` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/companies` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/companies/{companyId:long}/exchange-rates` | `src/Brqosh.Api/Program.cs` |  |
| POST | `/api/v1/companies/{companyId:long}/exchange-rates/sync` | `src/Brqosh.Api/Program.cs` |  |
| POST | `/api/v1/companies/{companyId:long}/fx-closing-translation` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/company-groups` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/company-groups/{groupId:long}/intercompany-account-maps` | `src/Brqosh.Api/Program.cs` |  |
| POST | `/api/v1/company-groups/{groupId:long}/intercompany-account-maps` | `src/Brqosh.Api/Program.cs` |  |
| DELETE | `/api/v1/company-groups/{groupId:long}/intercompany-account-maps/{accountCode}` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/company-groups/{groupId:long}/reports/consolidated-balance-sheet` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/company-groups/{groupId:long}/reports/consolidated-income-statement` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/company-groups/{groupId:long}/reports/consolidated-trial-balance` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/company-groups/{groupId:long}/reports/intercompany-eliminations` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/customers` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/data-quality` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/domain-events` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/event-tracking` | `src/Brqosh.Api/Program.cs` |  |
| POST | `/api/v1/exchange-rates` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/exchange-rates/lookup` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/intelligence/financial` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/ops/metrics` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/products` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/report-snapshots` | `src/Brqosh.Api/Program.cs` |  |
| POST | `/api/v1/report-snapshots/trial-balance` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/reports/project-profitability` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/reports/trial-balance` | `src/Brqosh.Api/Program.cs` |  |
| POST | `/api/v1/tenants/provision` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/tenants/subscription` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/api/v1/webhooks/subscriptions` | `src/Brqosh.Api/Program.cs` |  |
| POST | `/api/v1/webhooks/subscriptions` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/health` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/health/live` | `src/Brqosh.Api/Program.cs` |  |
| GET | `/health/ready` | `src/Brqosh.Api/Program.cs` |  |

## Storage

163 data stores. query_facts(kind="storage") for all of them.

- **context** (1): `src/Brqosh.Infrastructure/Persistence.BrqoshDbContext`
- **entity** (84): `src/Brqosh.Domain/Accounting.Account`, `src/Brqosh.Domain/Accounting.Budget`, `src/Brqosh.Domain/Accounting.CostCenter`, `src/Brqosh.Domain/Accounting.FiscalPeriod`, `src/Brqosh.Domain/Accounting.JournalEntry`, `src/Brqosh.Domain/Accounting.OpeningBalanceItem`, `src/Brqosh.Domain/Assets.FixedAsset`, `src/Brqosh.Domain/Companies.Branch`, `src/Brqosh.Domain/Companies.Company`, `src/Brqosh.Domain/Companies.CompanyGroup`, `src/Brqosh.Domain/Companies.IntercompanyAccountMap`, `src/Brqosh.Domain/Construction.ConstructionContract`, `src/Brqosh.Domain/Construction.ConstructionProject`, `src/Brqosh.Domain/Construction.ProgressBilling`, `src/Brqosh.Domain/Construction.ProjectCostEntry`, `src/Brqosh.Domain/Construction.SubcontractAgreement`, `src/Brqosh.Domain/Crm.CrmActivity`, `src/Brqosh.Domain/Crm.CrmLead`, `src/Brqosh.Domain/Crm.CrmOpportunity`, `src/Brqosh.Domain/Healthcare.InsuranceClaim`, `src/Brqosh.Domain/Healthcare.InsurancePayer`, `src/Brqosh.Domain/Healthcare.InsurancePolicy`, `src/Brqosh.Domain/Healthcare.Patient`, `src/Brqosh.Domain/Healthcare.Visit`, `src/Brqosh.Domain/Hospitality.GuestFolio` … and 59 more
- **migration** (78): `src/Brqosh.Infrastructure/Persistence/Migrations.AddAssetPurchaseInvoice`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddAuditChain`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddBankStatementBalances`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddBatchTracking`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddBomConversionCosts`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddBudgets`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddBuyerRegulatoryIdentity`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddCancelledAtToTaxDocuments`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddCodCollectionPostedFlag`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddCompanyAddressEn`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddCompanyAuditorName`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddCostCenterToDocuments`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddCostConcurrencyTokens`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddCreditNoteCurrency`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddCreditNoteDocumentDiscount`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddCustomerCreditLimit`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddDebitNoteCurrency`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddDocumentCurrency`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddEmployeeAdvances`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddEmployeeProject`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddEmployeesAndGosi`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddFixedAssetCancelledOn`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddFixedAssetDepreciatedThrough`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddFixedAssetDepreciationMethod`, `src/Brqosh.Infrastructure/Persistence/Migrations.AddFixedAssetDisposalSale` … and 53 more

## Dependency Rules

634 internal module dependencies. The modules that reach furthest; traverse() or query_facts(kind="dependency") for the edges themselves.

| Module | Depends on |
|--------|------------|
| `src/Brqosh.Desktop/Views/Pages` | 60 modules |
| `tools/SectorProbe` | 34 modules |
| `src/Brqosh.Infrastructure/Persistence` | 31 modules |
| `src/Brqosh.Desktop` | 30 modules |
| `src/Brqosh.Infrastructure` | 29 modules |
| `src/Brqosh.Infrastructure/Reporting` | 22 modules |
| `src/Brqosh.Infrastructure/Sales` | 21 modules |
| `tools/FullScenarioAudit` | 21 modules |
| `src/Brqosh.Api` | 17 modules |
| `src/Brqosh.Infrastructure/Purchasing` | 17 modules |
| `src/Brqosh.Infrastructure/Accounting` | 16 modules |
| `src/Brqosh.Infrastructure/Treasury` | 16 modules |
| `src/Brqosh.Desktop/Views` | 14 modules |
| `src/Brqosh.Infrastructure/Assets` | 14 modules |
| `src/Brqosh.Infrastructure/Hospitality` | 14 modules |
| `src/Brqosh.Infrastructure/Healthcare` | 13 modules |
| `src/Brqosh.Infrastructure/Construction` | 12 modules |
| `src/Brqosh.Infrastructure/Payroll` | 12 modules |
| `src/Brqosh.Infrastructure/RealEstate` | 12 modules |
| `src/Brqosh.Infrastructure/Setup` | 12 modules |
| `src/Brqosh.Infrastructure/Logistics` | 11 modules |
| `src/Brqosh.Infrastructure/Platform` | 11 modules |
| `src/Brqosh.Infrastructure/Retail` | 11 modules |
| `src/Brqosh.Infrastructure/Inventory` | 10 modules |
| `src/Brqosh.Infrastructure/Zatca` | 10 modules |
| `src/Brqosh.Desktop/Ui` | 8 modules |
| `src/Brqosh.Infrastructure/Intelligence` | 8 modules |
| `src/Brqosh.Infrastructure/Manufacturing` | 8 modules |
| `tools/ScalePerfProbe` | 8 modules |
| `src/Brqosh.Infrastructure/Workflow` | 7 modules |
| `tools/PrintEvidence` | 7 modules |
| `src/Brqosh.Domain/Companies` | 5 modules |
| `src/Brqosh.Infrastructure/Companies` | 5 modules |
| `src/Brqosh.Domain/Accounting` | 4 modules |
| `src/Brqosh.Domain/Purchasing` | 4 modules |
| `src/Brqosh.Domain/Sales` | 4 modules |
| `src/Brqosh.Infrastructure/Licensing` | 4 modules |
| `src/Brqosh.Api/Middleware` | 3 modules |
| `src/Brqosh.Desktop/Reporting` | 3 modules |
| `src/Brqosh.Domain/Assets` | 3 modules |
| `src/Brqosh.Domain/Healthcare` | 3 modules |
| `src/Brqosh.Domain/Hospitality` | 3 modules |
| `src/Brqosh.Domain/RealEstate` | 3 modules |
| `src/Brqosh.Infrastructure/Crm` | 3 modules |
| `src/Brqosh.Infrastructure/Operations` | 3 modules |
| `src/Brqosh.Infrastructure/Security` | 3 modules |
| `tools/DbStartupProbe` | 3 modules |
| `tools/LicenseProbe` | 3 modules |
| `tools/UpdatePublisher` | 3 modules |
| `tools/ZatcaLiveCsidProbe` | 3 modules |
| `src` | 2 modules |
| `src/Brqosh.Application/Platform` | 2 modules |
| `src/Brqosh.Desktop/Navigation` | 2 modules |
| `src/Brqosh.Desktop/Security` | 2 modules |
| `src/Brqosh.Desktop/Views/Controls` | 2 modules |
| `src/Brqosh.Domain/Construction` | 2 modules |
| `src/Brqosh.Domain/Inventory` | 2 modules |
| `src/Brqosh.Domain/Logistics` | 2 modules |
| `src/Brqosh.Domain/Manufacturing` | 2 modules |
| `src/Brqosh.Domain/Payroll` | 2 modules |

## Critical Modules

| Module | Fan-In | Fan-Out | Criticality |
|--------|--------|---------|-------------|
| `src/Brqosh.Desktop/Views/Pages` | 1 | 741 | high |
| `src/Brqosh.Domain/Accounting` | 307 | 5 | high |
| `src/Brqosh.Infrastructure/Persistence` | 214 | 51 | high |
| `src/Brqosh.Desktop/Ui` | 161 | 14 | high |
| `src/Brqosh.Infrastructure/Security` | 152 | 23 | high |
| `src/Brqosh.Infrastructure/Reporting` | 37 | 120 | high |
| `src/Brqosh.Infrastructure/Treasury` | 34 | 108 | high |
| `src/Brqosh.Infrastructure/Accounting` | 80 | 55 | high |
| `src/Brqosh.Infrastructure/Sales` | 27 | 82 | high |
| `src/Brqosh.Infrastructure/Purchasing` | 19 | 85 | high |

## Risk Zones

- **Cyclic dependency detected (6 modules)** (confidence: 100%): The following modules form a dependency cycle: src/Brqosh.Domain/Accounting -> src/Brqosh.Domain/Companies -> src/Brqosh.Domain/Inventory -> src/Brqosh.Domain/Retail -> src/Brqosh.Domain/Sales -> src/Brqosh.Domain/Tax -> src/Brqosh.Domain/Accounting. All of them compile into "Brqosh.Domain", so this is NOT a build-order problem — mutual references between namespaces or submodules inside one assembly/crate are legal and ordinary, and the build system forbids cycles between units anyway. Read it as a coupling signal: the cycle means these directories cannot be understood, moved or split apart independently.

---

*Generated at 2026-10-25T14:20:10Z in 1m3.8250453s. 22273 facts, 210 insights.*
