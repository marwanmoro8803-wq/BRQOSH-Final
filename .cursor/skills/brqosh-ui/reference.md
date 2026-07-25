# Smart Mind — Design and User Interfaces

> **Roadmap:** [SKILL.md](SKILL.md#engineering-roadmap) | skill: brqosh-ui | see per-file load order

> Luxury ERP · RTL · WPF · design-system — **not** Codex marketing frontend

---

## Table of Contents

1. [ERP vs Marketing Philosophy](#1-erp-vs-marketing-philosophy)
2. [Design System](#2-design-system)
3. [Tokens and Theme](#3-tokens-and-theme)
4. [Page Patterns](#4-page-patterns)
5. [RTL and DataGrid](#5-rtl-and-datagrid)
6. [Dialogs and Forms](#6-dialogs-and-forms)
7. [Executive and KPI](#7-executive-and-kpi)
8. [Empty States](#8-empty-states)
9. [Navigation and Icons](#9-navigation-and-icons)
10. [Accessibility](#10-accessibility)
11. [LIFECYCLE-100](#11-lifecycle-100)
12. [Anti–AI Slop](#12-antiai-slop)
13. [UI Acceptance Checklist](#13-ui-acceptance-checklist)

---

## 1. ERP vs Marketing Philosophy

| Marketing (avoid) | BRQOSH ERP (do) |
|-------------------|-----------------|
| Oversized hero | Page header + breadcrumb |
| Nested decorative cards | Full-width bands · top KPI row |
| Purple gradients | Brand tokens · sidebar gradient |
| Feature explanations in UI | Concise empty state + CTA |
| Landing as first screen | Immediate work screen |

**Audience:** accountant · financial manager · cashier — scans the screen in seconds.

---

## 2. Design System

**Required skill:** `brqosh-ui-design-system`

| Source | Content |
|--------|---------|
| `design-system/MASTER.md` | Luxury identity |
| `design-system/pages/*.md` | Spec per screen |
| `LIFECYCLE-ROADMAP-100.md` | Steps 1–100 |
| `Themes/BrqoshTheme.xaml` | Implementation |

For a new screen: **copy a similar page** (list-crud · bank-cash · login).

---

## 3. Tokens and Theme

- `StaticResource` from `BrqoshTheme.xaml` — no arbitrary `#hex`
- `ThemeManager` for light/dark
- `AppFont` · `BackgroundBrush` · `SurfaceBrush` · `AccentBrush`
- Profile badge in sidebar: `ProfileBadge`

---

## 4. Page Patterns

### List CRUD
```
Header (title + action buttons)
→ search/filter bar
→ DataGrid (paging)
→ Empty state overlay
```

### Dashboard
- `ExecutiveDashboardBuilder` · `KpiMetricCardBuilder`
- `SparklineBuilder` for trends
- Cash flow forecast: `CashFlowForecastService` on dashboard

### Settings
- Clear sections · explicit save
- `CompanySettingsPage` — provisioning button for admin

---

## 5. RTL and DataGrid

- `FlowDirection="RightToLeft"` on Window/Page
- `DataGridRtlHelper` — Star columns, no gaps
- Number alignment: right for amounts
- Arabic column headers

**CEEF-040:** UI regression — no column gaps after theme change.

---

## 6. Dialogs and Forms

- Modal `Window` · `ShowDialog()`
- Validation before save — clear Arabic message
- Culture-aware `decimal` parsing
- Dates: `DatePicker` + Hijri where required

---

## 7. Executive and KPI

- KPI card: title · value · delta · sparkline
- No more than 4–6 KPIs above the fold
- Colors: positive/negative from tokens — not arbitrary red/green

---

## 8. Empty States

- `EmptyStateHelper` + `EmptyStateCatalog`
- Every CRUD list has a dedicated empty state
- One clear CTA ("Create first …")

---

## 9. Navigation and Icons

- `NavigationCatalog.ForProfile`
- `NavIconFactory` — monograms
- `PermissionService` filters items
- Company switcher in `MainWindow`

---

## 10. Accessibility

- WCAG contrast — O-13 future; apply tokens now
- `KeyboardNavigationHelper` for critical menus
- No information by color alone (state + text)

---

## 11. LIFECYCLE-100

When implementing a lifecycle step:
1. Update `design-system/LIFECYCLE-ROADMAP-100.md`
2. Page spec in `design-system/pages/` if new
3. Desktop.Tests for critical elements

Closed examples: login · quotations · company switcher

---

## 12. Anti–AI Slop

From Codex frontend (inverted for ERP):
- No marketing hero
- No card-in-card for sections
- No decorative orbs/blobs
- No in-app tutorial text
- No purple-dominated palette

---

## 13. UI Acceptance Checklist

- [ ] Correct RTL · DataGrid without gaps
- [ ] Empty state
- [ ] RBAC hides buttons
- [ ] UiFeedback for success/error
- [ ] Tokens, no hardcode
- [ ] Clear Arabic copy
- [ ] Desktop.Tests where possible

---

## v4 — Luxury vs Generic AI Models

| Codex frontend default | BRQOSH Luxury |
|------------------------|---------------|
| Bold marketing | Operational density |
| Hero images | KPI + grid |
| Playful games | Restrained gold accent |
| English-first | **Arabic RTL first** |

### LIFECYCLE critical steps (reference)
| Step | Screen |
|------|--------|
| 1 | login · setup · provision |
| 10 | org provisioning |
| 31 | quotations |
| 32 | sales orders |
| 97 | data migration stub |

Each step = spec in `design-system/` + Desktop + test where possible.

---

## v6 — Codex 5.5 Inverted + Desktop Verify

### From top Codex (inverted for ERP)
| Codex says | BRQOSH does |
|------------|-------------|
| SaaS quiet utilitarian | **Operational density** · fast scan |
| No landing hero | **Immediate work screen** |
| No card-in-card | Bands + DataGrid |
| Stable dimensions | RTL Star columns · no layout shift |
| Playwright verify | **Brqosh.Desktop.exe** for critical path |

### Verify skill (Claude Code)
UI change → **run the app** · observe behavior — not static test only.

### Gemini 3.5 scannable
- KPI above the fold · short titles · specific numbers in cards

**Integration:** [integration](../brqosh-skills-hub/integration.md) · [brqosh-ui-design-system](../brqosh-ui-design-system/SKILL.md)
