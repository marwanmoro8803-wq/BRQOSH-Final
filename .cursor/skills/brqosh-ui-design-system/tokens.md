# Design Tokens (v18 Strict)

> **Binding:** [enforcement.md](enforcement.md) · **Canonical:** `MASTER.md` · `BrqoshTheme.xaml`  
> **Rule:** neutrals chosen · accent deliberate · semantic separate

---

## Precedence (strict)

```
BrqoshTheme.xaml  >  MASTER.md  >  pages/*.md  >  agent
```

**MUST NOT** invent hex in XAML. **MUST** add missing token to theme dictionary.

---

## Color — light mode (operational default)

| Token | Hex | Role | WPF key |
|-------|-----|------|---------|
| `primary` | `#0D9488` | actions · active nav · positive KPI | `PrimaryBrush` |
| `shell` | `#0F172A` | sidebar | `SidebarBackgroundBrush` |
| `accent` | `#D4A853` | highlights · premium divider | `AccentBrush` |
| `canvas` | `#F1F5F9` | page background | `PageBackgroundBrush` |
| `surface` | `#FFFFFF` | cards · inputs | `SurfaceBrush` |
| `text-primary` | `#0F172A` | body · headings | `TextPrimaryBrush` |
| `text-secondary` | `#64748B` | labels | `TextSecondaryBrush` |
| `text-muted` | `#94A3B8` | disabled · meta | `TextMutedBrush` |

## Semantic (MUST NOT reuse as chart series)

| Token | Hex | Use |
|-------|-----|-----|
| `success` | `#10B981` | posted · balanced |
| `danger` | `#EF4444` | delete · critical |
| `warning` | `#F59E0B` | pending · near limit |
| `info` | `#3B82F6` | informational |

Status MUST ship with **icon + label** — never color alone.

## Dark mode (executive)

Per `ECOSYSTEM-VISION.md`: cyan `#22D3EE` · gold `#D4AF37` · canvas `#050B18`.  
Glow/neon **≤40% opacity** · focus/AI edge only — **FORBIDDEN** on table body.

---

## Typography (locked)

| Role | Family | Size | Weight |
|------|--------|------|--------|
| Display | Segoe UI Variable | 20–24px | SemiBold |
| Body | Segoe UI / Tahoma AR | 14px | Normal |
| Caption | Segoe UI | 12px | Normal |
| Data | Segoe UI | 13–14px | Normal + `tnum` |
| KPI value | Segoe UI | 28–36px | SemiBold |

**MUST NOT** use hero scale except one figure per dashboard.

---

## Spacing

| Token | Value | Use |
|-------|-------|-----|
| `space-xs` | 4px | inline |
| `space-sm` | 8px | control padding |
| `space-md` | 12px | card inner |
| `space-lg` | 16px | section |
| `space-xl` | 24px | page margin |
| `sidebar-width` | 260px | shell |
| `row-height` | 44px | DataGrid |
| `touch-min` | 36px | button min |

**MUST** use theme `Thickness` resources — **MUST NOT** scatter arbitrary margins.

---

## Radius and elevation

| Token | Value | Use |
|-------|-------|-----|
| `radius-sm` | 8px | inputs · chips |
| `radius-md` | 12px | summary cards |
| `radius-lg` | 14px | ContentCard · ToolbarCard |
| `radius-xl` | 20px | AuthCard only |
| `shadow-card` | 0 2 8 / 0.08 | default |
| `shadow-elevated` | 0 8 24 / 0.12 | modals |

BRQOSH allows up to 14px on operational cards (Codex default ≤8px overridden by MASTER).

---

## Motion

| Token | Duration | Use |
|-------|----------|-----|
| `motion-fast` | 150ms | hover |
| `motion-normal` | 200ms | button · nav |
| `motion-slow` | 320ms | panel expand |
| `motion-none` | 0 | high contrast · reduced motion |

**FORBIDDEN:** bounce · spring on financial actions.

---

## Chart tokens

| Parameter | BRQOSH value |
|-----------|--------------|
| Categorical | fixed order · max 8 then Other |
| Sequential | teal ramp light→dark |
| Diverging | teal ↔ amber · gray mid |
| Grid | hairline · one shade off surface |
| Marks | 2px lines · 2px stack gap |

Procedure: [visual-effects.md](visual-effects.md)
