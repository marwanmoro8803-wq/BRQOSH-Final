# BRQOSH — Luxury Accounting & Financial Management Ecosystem

> **Generation:** Next-Gen Enterprise SaaS · Bilingual AR/EN · WPF Desktop + Design Spec  
> **Positioning:** Financial Operating System Platform — Big-4-grade · global finance teams  
> **Strategic source:** [`docs/مهم`](../docs/مهم) v4.0 · [`PLATFORM-ROADMAP.md`](PLATFORM-ROADMAP.md) · [`LIFECYCLE-ROADMAP-100.md`](LIFECYCLE-ROADMAP-100.md)  
> **Visual DNA:** Cinematic dark · Pearl light · Glass depth · Financial precision · AI-native shell  
> **Full vision:** See [`ECOSYSTEM-VISION.md`](ECOSYSTEM-VISION.md) for complete 13-module specification

---

## 0. Product Promise

Ultra-luxury, hyper-clean, high information density — every dashboard, form, report, and dialog is one unified platform. Futuristic enterprise SaaS realism in native WPF: premium minimalist, audit-grade tables, flawless bilingual AR/EN, embedded AI sidebar.

---

## 1. Design Philosophy

| Principle | Expression |
|-----------|--------------|
| **Financial Truth First** | Tabular numbers · audit trail · posting status · who/when/why — *strategic anchor: docs/مهم* |
| **Luxury Minimalism** | High density, zero clutter, every pixel earns its place |
| **Unified Platform** | One palette, one radius system, one motion language — every screen |
| **Bilingual Native** | AR RTL + EN LTR — not translation overlay |
| **Cinematic Depth** | Layered glass, soft volumetric light, subtle neon edge glow (dark only) |

---

## 2. Color System

### Dark Mode — Cosmic Cinematic

| Token | Hex | Usage |
|-------|-----|-------|
| `CosmicNavy` | `#050B18` | App canvas |
| `MidnightBlue` | `#0B1426` | Sidebar, status bar |
| `GlassSurface` | `#14FFFFFF` | Frosted panels (18% white overlay) |
| `ElectricCyan` | `#22D3EE` | Primary actions, active nav, chart lines |
| `MetallicGold` | `#D4AF37` | Accent, badges, premium highlights |
| `NeonGlow` | `#06B6D4` | Focus ring, AI panel edge (40% opacity) |
| `TextPrimary` | `#F1F5F9` | Headlines |
| `TextSecondary` | `#94A3B8` | Labels |
| `TextMuted` | `#64748B` | Captions |

### Light Mode — Pearl Executive

| Token | Hex | Usage |
|-------|-----|-------|
| `SoftIvory` | `#F7F5F0` | App canvas |
| `PearlWhite` | `#FFFFFF` | Cards, modals |
| `ElegantTeal` | `#0D9488` | Primary |
| `RoseGold` | `#B76E79` | Accent highlights |
| `WarmGray` | `#78716C` | Secondary text |
| `BorderSubtle` | `#E7E5E4` | Dividers |

### Semantic (both modes)

| Role | Dark | Light |
|------|------|-------|
| Success | `#34D399` | `#059669` |
| Danger | `#F87171` | `#DC2626` |
| Warning | `#FBBF24` | `#D97706` |
| Info | `#38BDF8` | `#2563EB` |

---

## 3. Typography

```
Primary:  "Segoe UI Variable", "Segoe UI", "IBM Plex Sans Arabic", Tahoma
Mono/Tabular: "Cascadia Mono", "Consolas", "Segoe UI" + TabularNumbers
```

| Role | Size | Weight | Notes |
|------|------|--------|-------|
| Display | 28–32px | Black | Dashboard hero |
| H1 Page | 22px | Bold | Page titles |
| H2 Section | 17px | SemiBold | Card headers |
| Body | 14px | Regular | Forms, tables |
| Caption | 12px | Regular | Metadata |
| KPI Value | 26–32px | Bold | **Tabular lining** |
| Table Numeric | 14px | Medium | Right-aligned, tabular |

---

## 4. Spacing & Shape

| Token | Value |
|-------|-------|
| radius-xs | 6px |
| radius-sm | 10px |
| radius-md | 14px |
| radius-lg | 18px |
| radius-xl | 24px |
| space-unit | 4px base (4, 8, 12, 16, 24, 32, 48) |
| stroke | 1px default · 1.5px focus · 2px AI glow |
| elevation-1 | soft shadow 8px blur |
| elevation-2 | card shadow 20px blur |
| elevation-3 | modal 32px blur |

---

## 5. Motion Language

| Token | Duration | Easing |
|-------|----------|--------|
| micro | 120ms | ease-out |
| fast | 200ms | ease-in-out |
| panel | 320ms | cubic-bezier(0.4, 0, 0.2, 1) |
| chart | 600ms | ease-out (data enter) |

Hover: translateY(-1px) + border brightening · Focus: 2px cyan ring · No bounce

---

## 5b. Visual Effects (WPF-safe)

| Effect | Dark | Light | Implementation |
|--------|------|-------|----------------|
| Glassmorphism | `#14FFFFFF` frost | `#CCFFFFFF` frost | `GlassPanel` · `GlassSurfaceBrush` |
| Layered depth | Card shadow stack | Same elevation scale | `CardShadow` · `ElevatedShadow` |
| Volumetric light | Sidebar gradient | Subtle warm gradient | `SidebarGradientBrush` |
| Neon glow | ≤40% cyan edge | Teal focus ring | AI panel border · focus only |
| Particle ambience | Login/splash only | Same | Optional · never on ledger grids |
| Micro-motion | 120–600ms | Same | See section 5 |

**Avoid:** purple AI gradients · crypto neon · glass on every DataGrid row

---

## 6. Shell Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│ [AI Panel 300px] │ Main Content Area          │ Smart Sidebar 272px RTL │
│  Glass + glow    │  Header · Page Host        │  Monogram nav           │
│  Chat · Insights │  Toast overlay             │  Company · User card    │
└─────────────────────────────────────────────────────────────────────────┘
│ Status bar — DB · Session · Sync · Language · Theme                      │
└─────────────────────────────────────────────────────────────────────────┘
```

**Header tools:** Company/Branch switcher · Language EN/ع · Theme · Date · User badge  
**AI Sidebar:** Context-aware suggestions · Anomaly alerts · Quick explain · Next-best-action

---

## 7. Module Screen Map

| Module | Spec File | Key Patterns |
|--------|-----------|--------------|
| Executive Dashboard | `pages/dashboard-executive.md` | KPI bento · Charts · AI cards · Activity feed |
| Invoicing & Sales | `pages/invoicing-sales.md` | Split editor/preview · Line items · PDF panel |
| Purchases & Suppliers | `pages/purchases-suppliers.md` | 3-way match pipeline · Aging · Scorecards |
| General Ledger | `pages/general-ledger.md` | COA tree · Double-entry · Audit trail |
| Bank & Cash | `pages/bank-cash.md` | Reconciliation matcher · Liquidity gauges |
| Fixed Assets | `pages/fixed-assets.md` | Timeline · Depreciation schedule |
| Inventory | `pages/inventory.md` | SKU cards · Reorder alerts · Zones |
| Payroll & HR | `pages/payroll-hr.md` | Employee cards · Cycle workflow |
| Expenses | `pages/expenses.md` | Receipt OCR feel · Approval chain |
| Reports & Analytics | `pages/reports-analytics.md` | Document-style reports · Drill-down |
| Compliance & Tax | `pages/compliance-tax.md` | VAT/ZATCA · Filing calendar |
| AI Assistant | `pages/ai-assistant.md` | Glass panel · Glow · Context chips |
| Settings | `pages/settings-multi-company.md` | Permissions matrix · Integrations |

All page specs live in `design-system/pages/`. Agent skill: `.cursor/skills/brqosh-ui-design-system/`.

---

## 8. Component Library (WPF)

| Style Key | Purpose |
|-----------|---------|
| `GlassPanel` | Frosted card with subtle border glow |
| `LuxuryKpiCard` | Executive KPI with sparkline slot |
| `ChartPanel` | Chart container with legend header |
| `AiInsightCard` | AI recommendation with cyan edge |
| `AlertStrip` | Smart notification bar |
| `TabularText` | Financial numbers |
| `QuickActionChip` | Pill CTA |
| `ActivityFeedItem` | Timeline row |

**Chart types (priority):** line · area · bar · waterfall · pie · gauge · radar · sparkline  
**Dashboard KPIs (live + roadmap):** Revenue · Expenses · Net Profit · Cash Flow · AR/AP · VAT · Inventory · Periods · *(Phase 2: MRR · Burn Rate · Forecast Accuracy)*

---

## 9. Bilingual Rules

- `LocaleManager.T(ar, en)` for all user-visible strings in new luxury components
- `FlowDirection` toggles on Window + PageHost refresh
- AR: Eastern Arabic numerals optional · EN: Western numerals + tabular
- Labels: AR full width RTL · EN mirror layout LTR
- **Never** mix direction within a single control

---

## 10. Anti-Patterns

- Generic purple AI gradients · Crypto neon overload
- Emoji icons · Inconsistent card radius
- Non-tabular financial numbers · Cluttered chart legends
- Light/dark components that look like different products

---

## 11. Implementation Map

| Layer | Path |
|-------|------|
| Theme tokens | `BrqoshTheme.xaml` |
| Theme runtime | `ThemeManager.cs` |
| i18n | `LocaleManager.cs` |
| Executive UI | `DashboardPage` · `ExecutiveDashboardBuilder.cs` |
| AI Shell | `MainWindow.xaml` AI column |
| Audit | `scripts/ui-ux-pro-max-audit.ps1` |
| Ecosystem vision | `design-system/ECOSYSTEM-VISION.md` |
| Platform roadmap | `design-system/PLATFORM-ROADMAP.md` ← `docs/مهم` v4.0 |
| Agent skill | `.cursor/skills/brqosh-ui-design-system/SKILL.md` |
