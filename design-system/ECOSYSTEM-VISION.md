# BRQOSH — Accounting & Financial Management Ecosystem Vision

> **Target:** Next-gen enterprise SaaS · Big-4-grade · Global finance teams  
> **Platform:** WPF Desktop (native) · Bilingual AR/EN native · Unified design system  
> **Quality bar:** Award-winning product design · Hyper-clean · High information density · Zero visual inconsistency  
> **Strategic anchor:** [`docs/مهم`](../docs/مهم) · [`PLATFORM-ROADMAP.md`](PLATFORM-ROADMAP.md)

---

## Platform Identity

BRQOSH is a **Financial Operating System Platform** — not merely accounting software.

> نظام تشغيل اقتصادي للمؤسسات: محاسبة · تجارة · إدارة · عمليات · امتثال · AI · تحليلات — منصة واحدة.

The luxury UI is the **visual operating layer** of this platform. Design must serve **Financial Truth First** before aesthetics.

| Strategic priority | UI obligation |
|--------------------|---------------|
| 1. Accounting correctness | Balanced entries · posting gates · audit-visible |
| 2. Data integrity | Validation · fail-closed · no silent errors |
| 3. Security | RBAC · permission matrix · session idle |
| 4. Compliance | ZATCA/VAT status · filing calendar |
| 5. Scale | Only after 1–4 are trustworthy |

Council rules: **لا توسع قبل الثقة · لا ذكاء قبل البيانات · لا عالمية قبل المعمارية · لا ميزات قبل صحة المال**

---

## North Star (Visual)

One integrated platform from first screen to last. Every dashboard, form, report, and dialog must feel like the same product — same palette, radius, stroke, elevation, icons, charts, tables, forms, and interaction states in **both** dark and light modes.

| Pillar | Requirement |
|--------|-------------|
| **Financial Truth First** | Tabular numbers · audit trail visible · who/when/why on every financial action |
| **Premium minimalism** | Luxury without clutter; density without noise |
| **Flawless bilingual** | AR RTL + EN LTR native — not cosmetic translation |
| **Cinematic depth** | Glass layers, soft volumetric light, subtle neon edge (dark only) |
| **AI-native shell** | Assistant embedded in workflow — not a bolt-on widget |

---

## Visual Identity

### Dark Mode — Cosmic Cinematic

| Element | Specification |
|---------|---------------|
| Canvas | Deep cosmic navy `#050B18` |
| Surfaces | Midnight blue `#0B1426` |
| Primary accent | Electric cyan `#22D3EE` |
| Premium accent | Metallic gold `#D4AF37` |
| Glow | Holographic neon `#06B6D4` at ≤40% opacity — focus, AI panel edge only |
| Glass | Frosted panels `#14FFFFFF` · layered transparent depth |
| Depth | Soft shadows · subtle reflections · volumetric cinematic lighting |
| Ambience | Ultra-soft particle ambience (optional shell background — never distract from data) |
| Motion | Micro-animations 120–320ms · fluid · no bounce |

### Light Mode — Pearl Executive

| Element | Specification |
|---------|---------------|
| Canvas | Soft ivory `#F7F5F0` |
| Cards | Pearl white `#FFFFFF` |
| Primary | Elegant teal `#0D9488` |
| Accent | Rose gold `#B76E79` |
| Neutrals | Warm gray `#78716C` · borders `#E7E5E4` |
| Depth | Same elevation system as dark — identical luxury feel |

**Rule:** Light and dark are two skins of one product — never two different design languages.

---

## Unified Shell

```
┌─────────────────────────────────────────────────────────────────────────┐
│ AI Panel 300px      │ Main Content              │ Smart Sidebar 272px RTL│
│ Glass + cyan glow   │ Top bar · Page host       │ Monogram navigation    │
│ Chat · Predictions  │ Toast · Loading overlay   │ Company · User card    │
└─────────────────────────────────────────────────────────────────────────┘
│ Status bar — DB · Branch · Session idle · Sync · Language · Theme        │
└─────────────────────────────────────────────────────────────────────────┘
```

**Top bar:** Company/branch switcher · Roles indicator · EN/ع · Theme · Date/time · User badge  
**Navigation:** Grouped monogram nav · fast keyboard access · active state cyan edge  
**AI sidebar:** Context-aware chat · anomaly alerts · quick explain · next-best-action cards  
**Settings hub:** Multi-company · permissions matrix · branding · integrations · audit

---

## Module Catalog

| # | Module | Spec | Executive feel |
|---|--------|------|----------------|
| 1 | Main Intelligent Dashboard | `pages/dashboard-executive.md` | Command center — KPIs + charts + AI |
| 2 | Invoicing & Sales | `pages/invoicing-sales.md` | Split editor/preview · executive invoice doc |
| 3 | Purchases & Suppliers | `pages/purchases-suppliers.md` | 3-way match · aging · scorecards |
| 4 | General Ledger | `pages/general-ledger.md` | Double-entry · COA tree · audit trail |
| 5 | Bank & Cash | `pages/bank-cash.md` | Reconciliation matcher · liquidity |
| 6 | Fixed Assets | `pages/fixed-assets.md` | Timeline · depreciation schedule |
| 7 | Inventory | `pages/inventory.md` | SKU cards · zones · reorder alerts |
| 8 | Payroll & HR | `pages/payroll-hr.md` | Employee cards · cycle workflow |
| 9 | Expenses | `pages/expenses.md` | Receipt capture · approval chain |
| 10 | Reports & Analytics | `pages/reports-analytics.md` | Document-style · drill-down |
| 11 | Compliance & Tax | `pages/compliance-tax.md` | VAT/ZATCA · filing calendar |
| 12 | AI Assistant | `pages/ai-assistant.md` | Glass panel · glow · context chips |
| 13 | Settings & Multi-Company | `pages/settings-multi-company.md` | Permissions · integrations |

---

## Interaction System (all modules)

| State | Behavior |
|-------|----------|
| Hover | translateY(-1px) · border brighten · 200ms |
| Focus | 2px cyan ring · WCAG visible |
| Active nav | Cyan left/right edge · glass highlight |
| Loading | Skeleton or `LoadingStatePanel` — never blank flash |
| Empty | `EmptyStatePanel` monogram + guided subtitle |
| Error | `DangerBrush` inline · toast for global |
| Success post | Subtle green confirmation strip |

---

## Chart & Analytics Standards

Supported chart types (priority order for implementation):

1. Line · Area — trends, cash, revenue
2. Bar · Waterfall — comparisons, P&L bridges
3. Pie · Gauge — composition, KPI progress
4. Radar — multi-dimensional scorecards
5. Sparkline — inline KPI cards

Requirements: clean legends · precise tooltips · tabular axis labels · 600ms enter animation · theme-aware colors (cyan/gold semantic).

**WPF path:** `SparklineBuilder` (current) → LiveCharts2 / OxyPlot for full interactivity.

---

## Bilingual Excellence

- Every string: `LocaleManager.T(ar, en)`
- AR: RTL layout · optional Eastern Arabic numerals for display labels
- EN: LTR mirror · Western numerals · tabular alignment
- Accounting context: use domain terms (قيد، ترحيل، ذمم، ضريبة) — not generic UI translation
- Locale switch: instant refresh of page host + dynamic builders
- **Never** mix direction inside one control

---

## Absolute Consistency Checklist

Before shipping any screen:

- [ ] Uses `BrqoshTheme.xaml` style keys — no orphan colors
- [ ] Dark + light tested via `ThemeManager`
- [ ] AR + EN tested via `LocaleManager`
- [ ] Financial numbers use `TabularText`
- [ ] DataGrid row height 44px · monogram nav (no emoji)
- [ ] `UiResources` / `FindResource` in code-behind builders
- [ ] AutomationProperties on primary grid/KPI host
- [ ] Matches module spec in `design-system/pages/`

---

## Implementation Map

| Layer | Path |
|-------|------|
| Baseline ERP | `design-system/MASTER.md` |
| Luxury ecosystem | `design-system/MASTER-LUXURY-ECOSYSTEM.md` |
| Platform roadmap | `design-system/PLATFORM-ROADMAP.md` |
| Lifecycle 100 | `design-system/LIFECYCLE-ROADMAP-100.md` |
| Strategic source | `docs/مهم` |
| This vision | `design-system/ECOSYSTEM-VISION.md` |
| Page specs | `design-system/pages/*.md` |
| Agent skill | `.cursor/skills/brqosh-ui-design-system/` |
| WPF theme | `src/Brqosh.Desktop/Themes/` |
| UI builders | `src/Brqosh.Desktop/Ui/` |
