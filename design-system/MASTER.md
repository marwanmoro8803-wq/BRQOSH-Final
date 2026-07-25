# BRQOSH — Design System MASTER (UI UX Pro Max v2.0)

> Generated: 2026-07-09 · Platform: WPF Desktop · RTL Arabic · Industry: Financial ERP / Invoice & Billing / Construction

```
+----------------------------------------------------------------------------------------+
|  TARGET: BRQOSH ERP — RECOMMENDED DESIGN SYSTEM                                        |
+----------------------------------------------------------------------------------------+
|  PATTERN: Data-Dense Dashboard + Sidebar Shell                                         |
|     Primary surface: KPI bento grid → drill-down lists → document dialogs              |
|     CTA hierarchy: Primary action in toolbar card · Secondary ghost · Danger isolated  |
|                                                                                        |
|  STYLE: Swiss Modernism 2.0 + Soft UI Evolution (ERP variant)                          |
|     Keywords: Grid clarity, teal trust, gold accent, subtle depth, no neon gradients   |
|     Best For: Accounting, treasury, ZATCA compliance, multi-module ERP                 |
|     Performance: Excellent (native WPF) · Accessibility: WCAG AA target                |
|                                                                                        |
|  COLORS (aligned with BrqoshTheme.xaml):                                               |
|     Primary:    #0D9488 (Teal 600) — actions, active nav, KPI positive                |
|     Secondary:  #0F172A (Slate 900) — sidebar shell                                   |
|     Accent:     #D4A853 (Gold) — highlights, login hero divider                        |
|     Background: #F1F5F9 (Slate 100) — page canvas                                     |
|     Surface:    #FFFFFF — cards, inputs                                               |
|     Text:       #0F172A primary · #64748B secondary · #94A3B8 muted                   |
|     Semantic:   Success #10B981 · Danger #EF4444 · Warning #F59E0B · Info #3B82F6     |
|                                                                                        |
|  TYPOGRAPHY: Segoe UI Variable / Segoe UI / Tahoma                                     |
|     Mood: Professional, readable at 14px body, dense tables at 44px row height         |
|                                                                                        |
|  KEY EFFECTS:                                                                          |
|     Card shadow elevation · 200ms hover on buttons · Focus ring 2px Primary            |
|     KPI cards: monogram badge + accent bar (no emoji icons)                            |
|                                                                                        |
|  AVOID (Anti-patterns for Finance ERP):                                                |
|     ✗ Emojis as navigation/KPI icons                                                   |
|     ✗ AI purple/pink gradients · neon crypto aesthetics                                |
|     ✗ Playful bounce animations · glassmorphism overload                                 |
|     ✗ Low-contrast gray-on-gray in data tables                                         |
|                                                                                        |
|  PRE-DELIVERY CHECKLIST (WPF mapping):                                                   |
|     [x] Monogram/SVG-style badges instead of emoji icons                               |
|     [x] Cursor=Hand on Button, Hyperlink, CheckBox, RadioButton                        |
|     [x] Hover states on NavItem, PrimaryButton, GhostButton                            |
|     [x] FocusVisualStyle on interactive controls                                       |
|     [x] RTL FlowDirection on all pages                                                 |
|     [x] AutomationProperties on KPI grid and page host                                   |
|     [ ] Full keyboard tab order audit (ongoing per page)                               |
+----------------------------------------------------------------------------------------+
```

## Component Tokens

| Token | Value | WPF Resource |
|-------|-------|--------------|
| radius-sm | 8px | CornerRadius in inputs |
| radius-md | 12px | SummaryCard |
| radius-lg | 14px | ContentCard, ToolbarCard |
| radius-xl | 20px | AuthCard |
| shadow-card | 0 2 8 / 0.08 | CardShadow |
| shadow-elevated | 0 8 24 / 0.12 | ElevatedShadow |
| motion-fast | 150ms | documented |
| motion-normal | 200ms | MotionDurationNormal |
| row-height | 44px | DataGrid RowHeight |
| touch-min | 36px | MinHeight buttons/combos |

## Layout Shell

```
┌──────────────────────────────────────────────────────────────┐
│ Sidebar 260px │ Header bar (branch · theme · user)           │
│ Nav groups    ├──────────────────────────────────────────────┤
│ Monogram nav  │ Page host (ToolbarCard + ContentCard grid)   │
│               │ Status bar                                   │
└──────────────────────────────────────────────────────────────┘
```

## Page Overrides

See `design-system/pages/` for screen-specific rules. Luxury ecosystem adds 13 module specs — see [`ECOSYSTEM-VISION.md`](ECOSYSTEM-VISION.md).

## Luxury Layer

For executive shell, cosmic dark / pearl light, AI panel, 13-module specs, and platform phases see:

- [`MASTER-LUXURY-ECOSYSTEM.md`](MASTER-LUXURY-ECOSYSTEM.md)
- [`ECOSYSTEM-VISION.md`](ECOSYSTEM-VISION.md)
- [`PLATFORM-ROADMAP.md`](PLATFORM-ROADMAP.md) — bridge from [`docs/مهم`](../docs/مهم) v4.0
- [`LIFECYCLE-ROADMAP-100.md`](LIFECYCLE-ROADMAP-100.md) — 100 steps login → close · ZATCA

Agent skill: `.cursor/skills/brqosh-ui-design-system/`

## Implementation Map

| Artifact | Path |
|----------|------|
| Theme dictionary | `src/Brqosh.Desktop/Themes/BrqoshTheme.xaml` |
| Nav monograms | `src/Brqosh.Desktop/Navigation/NavIconFactory.cs` |
| KPI builder | `src/Brqosh.Desktop/Ui/KpiMetricCardBuilder.cs` |
| Audit script | `scripts/ui-ux-pro-max-audit.ps1` |
| Human guide | `docs/UI_UX_DESIGN_SYSTEM.md` |
