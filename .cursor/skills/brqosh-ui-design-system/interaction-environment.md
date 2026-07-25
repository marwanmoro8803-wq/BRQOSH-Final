# Interaction & Environment (v18 Strict)

> **Binding:** [enforcement.md](enforcement.md) · **Scope:** how the program lives in its runtime — not appearance alone

---

## 1. Environment stack

```
┌─────────────────────────────────────────┐
│ User (accountant · manager · auditor)    │
├─────────────────────────────────────────┤
│ Session (company · branch · year · RBAC) │
├─────────────────────────────────────────┤
│ Shell (nav · header · status)            │
├─────────────────────────────────────────┤
│ Module screen (toolbar · content)        │
├─────────────────────────────────────────┤
│ Platform (WPF · Windows · DPI · keyboard)  │
└─────────────────────────────────────────┘
```

Each layer **constrains** layers above. **MUST NOT** design UI that ignores session context.

| Layer | Design questions |
|-------|------------------|
| User | What happens every 30 seconds? Where do errors occur? |
| Session | Which branch? Open period? Permissions? |
| Shell | Where am I? How do I return? |
| Module | What is the one job of this screen? |
| Platform | DPI · shortcuts · print · offline |

---

## 2. Interaction modes

| Mode | User behavior | Surface design |
|------|---------------|----------------|
| **Scan** | find exception | pills · stripes · sort · filter |
| **Compare** | compare rows | frozen columns · tabular · stable width |
| **Operate** | execute action | one clear primary · confirm · undo where possible |
| **Read** | understand report | hierarchy · sections · export |

ERP spends most time in **Scan + Compare + Operate**.

---

## 3. Vitality without noise

Vitality = **clear response**, not decoration.

| Required ✓ | Forbidden ✗ |
|------------|-------------|
| 150ms row hover | bounce on KPI card |
| state pill change | animated gradient |
| toast after post | background particles |
| skeleton in grid area | full-screen shimmer |
| visible focus ring | parallax on forms |

**Rule:** One orchestrated feedback beats ten scattered micro-animations.

---

## 4. Layout stability (mandatory)

- Toolbar height MUST stay fixed — loading text MUST NOT expand row
- Financial column widths MUST be stable or star-with-minimum
- KPI value area MUST keep height — placeholder "—" same size as number
- Locale switch MUST NOT break alignment

---

## 5. Dashboard filters

```
┌─ Filter row (above all scoped content) ────────┐
│ [Period ▾] [Branch ▾] [Search…]                │
└────────────────────────────────────────────────┘
│ KPI row │ chart │ DataGrid                      │
```

| Rule | Requirement |
|------|-------------|
| Single row | above all filtered content — NOT inside each card |
| Date first | presets: today · 7 · 30 · 90 · fiscal year |
| One slice | all KPIs/tables re-query same filter set |
| Refetch | hold previous render at reduced opacity — no layout jump |
| Color stability | filter MUST NOT recolor surviving series |

---

## 6. Hit targets and tooltips

| Element | Requirement |
|---------|-------------|
| Toolbar icon | tooltip AR/EN · minimum 24×24 hit area |
| DataGrid row | full row clickable when drill-down |
| Chart mark | hit area larger than painted mark |
| Disabled control | tooltip explains RBAC or closed period |

Tooltip: **value bold · label secondary**.

---

## 7. Keyboard and accessibility

| Pattern | ERP requirement |
|---------|-----------------|
| Tab order | logical · RTL mirrored |
| Enter | default button in dialog |
| Escape | cancel / close |
| Ctrl+S | save where convention exists |
| Focus | `FocusVisualStyle` always visible |
| Contrast | WCAG AA on text and tables |
| Reduced motion | respect Windows setting |

**MUST NOT** encode state by color alone — icon + label required.

---

## 8. Desktop native (WPF)

| Property | Design decision |
|----------|-----------------|
| Main window | persistent shell — no fake SPA routing |
| Dialogs | modal for critical · modeless rare |
| DPI | vector + theme — no bitmap UI chrome |
| Print/PDF | light layout · no glow effects |
| Clipboard | copy from grids where expected |
| Offline | honest connection state in status bar |

**FORBIDDEN:** hamburger on wide desktop · excessive toast stacks.

---

## 9. Multi-entity context (always visible)

Header or status MUST show:

```
Company: ACME  │  Branch: Riyadh  │  Year: 2026  │  Period: March (open)
```

Context change → **explicit reload** — MUST NOT silently mix data.

---

## 10. Session states

| State | Required UI |
|-------|-------------|
| Idle timeout | warning → logout |
| Session revoked | message → re-login |
| Period closed | Post disabled + tooltip |
| Read-only role | hide or disable + tooltip |
| Demo data | explicit "Sample data" badge |

---

## 11. Surgical edits

On small user request:

- MUST change only requested text/color/control
- MUST NOT redesign surrounding margins/fonts/layout
- MAY suggest broader improvement — MUST NOT apply without approval

---

## 12. Existing system preservation

When a design system exists → **MUST preserve it**. Bold invention is for greenfield sites only. BRQOSH luxury layer allowed only where `MASTER`/`ECOSYSTEM` explicitly permit.

---

## Interaction verify gate

```
□ Filter updates all figures consistently
□ Loading does not move toolbar
□ Tab reaches every interactive control
□ AR and EN layouts hold
□ RBAC denial shows message
```

**Related:** [human-interface.md](human-interface.md) · [wpf-patterns.md](wpf-patterns.md) · [modules.md](modules.md)
