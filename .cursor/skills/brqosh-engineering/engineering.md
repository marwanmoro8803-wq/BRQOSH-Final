# Smart Mind — Software Engineering and Code

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-engineering | see per-file load order

> **Full reference** · Codex · Copilot · Cursor · Gemini · Claude Code · BRQOSH DOMAIN_MAP  
> **v15c:** [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml) — grep OpenAI/Codex + Cursor for deepen

---

## Table of Contents

1. [Execution Philosophy](#1-execution-philosophy)
2. [Code Exploration](#2-code-exploration)
3. [BRQOSH Architecture](#3-brqosh-architecture)
4. [.NET 9 and C#](#4-net-9-and-c)
5. [Entity Framework and Schema](#5-entity-framework-and-schema)
6. [Dependency Injection](#6-dependency-injection)
7. [WPF Desktop](#7-wpf-desktop)
8. [API](#8-api)
9. [Test Pyramid](#9-test-pyramid)
10. [Git and Collaboration](#10-git-and-collaboration)
11. [Performance](#11-performance)
12. [Anti-Patterns](#12-anti-patterns)
13. [v5 — Runtime Surface Verification](#13-v5--runtime-surface-verification)

---

## 1. Execution Philosophy

| # | Rule | Source |
|---|------|--------|
| 1 | Read before write | Cursor |
| 2 | Local pattern wins | Codex |
| 3 | Surgical but **complete** | Copilot |
| 4 | Persist until verify | Codex |
| 5 | No narrative comments | Cursor/Codex |
| 6 | ASCII in code; Arabic in UI strings | Codex + BRQOSH |
| 7 | **Verify with tools before answering** | Devin |

**A half-fix in ERP is a greater financial risk than no change.**

---

## 2. Code Exploration

### Exploration Loop
```
grep/glob (parallel)
  → entry points (Program.cs · App.xaml.cs · *Service.cs)
  → interfaces in Application
  → implementations in Infrastructure
  → UI bindings in Desktop
  → tests mirror
```

### Gemini + Warp Rules
- **Inquiry**: analysis only — do not mutate
- **Directive**: execute + verify
- grep with `before`/`after` context to reduce reads
- File <200 lines: read fully · >500: parallel sections
- **Absolute paths** when possible — no repeated `cd` (Warp/OpenCode)

### Tools
| Task | Tool |
|------|------|
| Symbol/text | Grep |
| Files by name | Glob |
| Read | Read (offset/limit) |
| Edit | StrReplace / Write |
| build/test | Shell |

**PowerShell:** use `;` between commands — not always `&&`.

---

## 3. BRQOSH Architecture

### Domains (DOMAIN_MAP)

```
Financial Core    → Accounting · Treasury · Tax · Assets
Commercial Core   → Sales · Purchasing · Inventory
Operational Core  → Construction · Manufacturing
Platform Core     → Security · Workflow · Audit · Reporting
```

### Dependency Rules

| From | To | Allowed? |
|------|-----|----------|
| Domain | — | pure |
| Application | Domain | ✅ |
| Infrastructure | Domain + Application | ✅ |
| Desktop/Api | Application (+ Infra via DI) | ✅ |
| Domain | Infrastructure | ❌ |

### Adding a Feature — Correct Order

1. `Brqosh.Domain` — entity + enums + domain rules
2. `Brqosh.Application` — interface + contracts/DTOs
3. `Brqosh.Infrastructure` — EF config + service
4. `InfrastructureServiceCollectionExtensions` — DI registration
5. Desktop page/dialog + `NavigationCatalog`
6. `PermissionService` entry
7. Tests

---

## 4. .NET 9 and C#

### BRQOSH Patterns
- `record` for results and lightweight contracts
- `decimal` for amounts — **not** `double`
- `DateOnly` for accounting dates
- private setters + factory methods on entities
- `CancellationToken` in Application services

### Error Handling
- Domain: `ArgumentException` for violations
- Application: clear results or documented exceptions
- UI: `UiFeedback` / `UiDialogs` — no stack trace for the user
- No empty catch blocks

### Async
- `async`/`await` in Infrastructure and UI event handlers
- No `.Result` / `.Wait()` on the UI thread

---

## 5. Entity Framework and Schema

### Governance (mandatory)
See `docs/SCHEMA_GOVERNANCE.md` + `brqosh-schema-governance.mdc`

| Step | Action |
|------|--------|
| New column | Fluent + SchemaSqlGuard type |
| Legacy upgrade | BrqoshSchemaUpdater |
| Test | SQLite in-memory **+** PG when possible |

### Common PG Types in BRQOSH
- `TEXT` / `character varying(N)` — **register N in AllowedPgTypes**
- `BIGINT` · `NUMERIC` · `BOOLEAN` · `TIMESTAMP`

### Queries
- Always filter `CompanyId` on operational data
- `AsNoTracking` for read-only paths
- Avoid N+1: deliberate `Include` or projection

---

## 6. Dependency Injection

### Desktop (`AppServices`)
- `CreateDb()` → **BindSecurityContext()** before any DB access
- scope per operation where required

### Api (`Program.cs`)
- Same services from Infrastructure
- health endpoints for O-10 stubs

### Registering a New Service
```csharp
// InfrastructureServiceCollectionExtensions.cs
services.AddScoped<IMyService, MyService>();
```

---

## 7. WPF Desktop

### Page Patterns
- `*Page.xaml` + code-behind: load in `Loaded` · `AppServices.CreateDb()`
- Dialogs: modal · return `bool` or result record
- Paging: `PagingState` · `SearchDebouncer`

### RBAC
```csharp
if (!PermissionService.CanX) { UiFeedback.Warn(...); return; }
```

### Navigation
- `NavigationCatalog.ForProfile(BusinessProfile)`
- `MainWindow` rebuilds on company switch

---

## 8. API

- Minimal APIs or controllers — follow what exists
- Idempotency store for sensitive mutations
- No secrets in responses
- Test: `Brqosh.Api.Tests`

---

## 9. Test Pyramid

| Layer | Project | When |
|-------|---------|------|
| Domain | Brqosh.Domain.Tests | pure rules |
| Application | Brqosh.Application.Tests | services · Golden |
| Api | Brqosh.Api.Tests | endpoints |
| Desktop | Brqosh.Desktop.Tests | static UI · helpers |
| E2E | FullScenarioAudit | 13/13 |

### Test Environment
```powershell
$env:BRQOSH_SKIP_STRESS='1'
$env:BRQOSH_TEST='1'
```

### Before Fixing
1. Write/modify a failing test
2. Fix the code
3. Test passes + regression suite

### Golden (Accounting)
`GoldenAccountingIntegrityTests` — 8 scenarios minimum

---

## 10. Git and Collaboration

| Allowed | Forbidden |
|---------|-----------|
| commit when user requests | `reset --hard` |
| work with dirty worktree | revert user changes |
| HEREDOC for messages | `git commit -i` |

---

## 11. Performance

- `BRQOSH_SKIP_STRESS=1` in normal CI
- stress separate: `run-stress-benchmark.ps1`
- Desktop: virtualize large lists · paging
- Do not load entire tables into memory

---

## 12. Anti-Patterns

- Python to read a file
- abstraction for two lines
- side refactor without request
- raw SQL without guard
- forgetting `CompanyId`
- forgetting `BindSecurityContext`
- claiming closure without test

**Advanced review:** [code-review.md](code-review.md) · **Security:** [security-review.md](security-review.md)

---

## 13. v5 — Runtime Surface Verification (from Anthropic verify)

**Verification = runtime observation** — not only `dotnet test`.

| Change touches | BRQOSH surface | Verify |
|----------------|----------------|--------|
| Desktop UI | WPF window | run · capture behavior |
| CLI/script | PowerShell | run-external-gates |
| Posting | test + FSA | Golden |
| API | Api.Tests | integration |

**Do not use import-and-call** as a substitute for running the application when asked "does it work?".

```powershell
dotnet build Brqosh.sln -c Release
dotnet test Brqosh.sln -c Release
# Desktop: Brqosh.Desktop.exe for critical path
```

### v6 — Copilot macOS + Cursor
- **parallel reads** — multiple files in one turn
- **bash async** — long build/test in background
- **Read before Edit** — mandatory

**Integration:** [integration](../brqosh-skills-hub/integration.md) · [tools.md](tools.md)

### v7 — Tools
[tools.md](tools.md) — tool hierarchy · MCP · parallel

---

## v15c — Engineering wave synthesis

| Elite pattern | BRQOSH enforcement |
|---------------|-------------------|
| Codex smallest complete diff | no partial "fix" without test |
| Cursor Read-before-Edit | grep parallel → read section → StrReplace |
| Gemini context economy | head_limit on grep; offset/limit on Read |
| Copilot parallel review | standards ║ security on large PR |

### Verify ladder (never skip rung)

```
grep symbol → read callsite → minimal diff → dotnet build → dotnet test → gates if wave
```

### Sacred surfaces (do not simplify away)

`PostingPolicy` · `GoldenPosting` · `SchemaSqlGuard` · `SecurityContext` · `AuditLog` · `BranchId`

**Corpus:** grep `OpenAI/Codex` + `Cursor` in [leak-corpus-index.yaml](../brqosh-skills-hub/leak-corpus-index.yaml)
