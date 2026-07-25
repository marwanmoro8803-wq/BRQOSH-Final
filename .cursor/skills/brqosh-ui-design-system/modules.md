# Screen Modules (v18 Strict)

> **Binding:** [enforcement.md](enforcement.md) · **Specs:** `design-system/pages/` · **Lifecycle:** `LIFECYCLE-ROADMAP-100.md`

---

## Shell anatomy (fixed)

```
┌─ Sidebar 260px ─┬─ Header (branch · company · theme · user) ─┐
│ Monogram nav    ├─ ToolbarCard (filters · primary actions)    │
│ RBAC-filtered   ├─ ContentCard (grid · form · split)         │
│                 └─ StatusBar (period · connection)             │
└─────────────────┴──────────────────────────────────────────────┘
```

| Zone | Job | FORBIDDEN |
|------|-----|-----------|
| Sidebar | module discovery | emoji icons |
| Header | context switch | decorative hero |
| Toolbar | screen verbs | tutorial paragraphs |
| Content | truth surface | fake KPI |
| Status | ambient state | stack traces |

---

## Module types

| Type | When | BRQOSH pattern |
|------|------|----------------|
| **Table** | default for entities | DataGrid 44px · sort · filter |
| **Board** | workflow stages | status columns (optional) |
| **Document** | single record | dialog or detail pane |
| **Dashboard** | overview | KPI row + 1–2 charts + drill table |
| **Form** | create/edit | validated · posting gate |
| **Report** | read-only aggregate | export · period lock indicator |

**Default MUST be Table** unless lifecycle step requires dashboard.

---

## Page recipes (mandatory patterns)

### List page

```
ToolbarCard: [Search] [Filter ▾] [+ Create] [Export]
ContentCard:  DataGrid (status pill · amounts RTL)
EmptyState:   message + Create when permitted
```

### Document page

```
ToolbarCard: Save · Post · Print · ZATCA · Cancel
Split:       header fields (2-col grid) | lines DataGrid
Footer:      totals · tax · audit strip
```

### Dashboard

```
Row 1: KPI bento (4–6) — real metrics or honest empty
Row 2: one chart story + exception panel
Row 3: drill-down DataGrid
```

**MUST** pick chart form before color. Single number → stat tile, not one-bar chart.

### Settings

```
Grouped full-width bands — MUST NOT nest cards
Danger zone isolated at bottom
```

### Dialog / wizard

```
AuthCard radius-xl — login only
Modals: radius-lg · max width ~560px
Step indicator ONLY when order is real information
```

---

## State modules (required every screen)

| State | UI | Copy |
|-------|-----|------|
| Loading | skeleton in grid area | minimal or silent |
| Empty | `EmptyState` | what + next action |
| Error | inline banner | what · why · fix |
| Permission | disabled/hidden + message | never blank |
| Stale | subtle badge | refresh affordance |

**MUST NOT** ship a screen missing any applicable state.

---

## RBAC module

- MUST hide destructive actions when unauthorized
- MUST disable with tooltip when discovery matters
- Primary post actions MUST obey `CanPost` / permission keys
- Audit actions MUST use confirm with who/when preview

---

## Bilingual module

```
Visible text  → LocaleManager.T(ar, en) — MANDATORY
FlowDirection → from locale — MUST NOT hardcode LTR
Dates/numbers → culture formatters + tabular display
```

Test matrix **required:** AR dark · AR light · EN dark · EN light.

---

## Composition laws

| Law | Detail |
|-----|--------|
| No card-in-card | one ContentCard host |
| No section-as-card | full-width bands |
| Stable toolbar | load text must not resize |
| Summary before detail | balances before lines |
| Interactive affordance | hand cursor · hover · focus |

---

## Page spec index

| Domain | File |
|--------|------|
| Shell | `pages/shell.md` |
| Login | `pages/login.md` |
| Dashboard | `pages/dashboard.md` |
| GL | `pages/general-ledger.md` |
| Sales | `pages/invoicing-sales.md` |
| Purchases | `pages/purchases-suppliers.md` |
| Inventory | `pages/inventory.md` |
| Bank | `pages/bank-cash.md` |
| Payroll | `pages/payroll-hr.md` |
| Assets | `pages/fixed-assets.md` |
| Reports | `pages/reports-analytics.md` |
| Settings | `pages/settings-multi-company.md` |
| Compliance | `pages/compliance-tax.md` |
| AI | `pages/ai-assistant.md` |
| Provisioning | `pages/organization-provisioning.md` |

**MUST** have page spec before new XAML — create stub in `design-system/pages/` if missing.
