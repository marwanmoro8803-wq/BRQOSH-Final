# Design Foundations (v18 Strict)

> **Binding:** [enforcement.md](enforcement.md) · **Fallback:** when no page spec — then apply ERP inversion  
> **Sources:** visualize core · artifact-design · dataviz system tier

---

## 1. Visual hierarchy (fixed read order)

Design for this eye path — do not invert without reason:

```
1. Critical state (error · limit breach · unposted)
2. Primary figure (KPI · total · variance)
3. Primary action (Post · Save · Approve)
4. Context (period · branch · filters)
5. Detail rows (DataGrid body)
6. Metadata (timestamps · internal ids)
```

**ERP rule:** The DataGrid is primary content after toolbar — not background decoration.

---

## 2. Four color jobs only

| Job | When | BRQOSH |
|-----|------|--------|
| **Identity** | distinguish series/entity | fixed categorical order |
| **Magnitude** | more/less | sequential teal ramp |
| **Polarity** | above/below baseline | diverging teal↔amber |
| **Status** | good/warn/bad/critical | semantic palette |

**MUST NOT** use status red as series color 4. **MUST NOT** mix jobs on one mark.

---

## 3. Type scale (locked)

| Level | Size | Weight | Use |
|-------|------|--------|-----|
| L0 Hero | 36–48px | SemiBold | **one per dashboard only** |
| L1 Title | 20–24px | SemiBold | page title |
| L2 Section | 16–18px | Medium | group header |
| L3 Body | 14px | Normal | default UI |
| L4 Caption | 12px | Normal | hints |
| Data | 13–14px | Normal + tabular | amounts |

**MUST** use two weights only (Normal/SemiBold). **MUST NOT** use 700 Bold in ERP chrome.

---

## 4. Spacing rhythm (4px grid)

```
4 · 8 · 12 · 16 · 24 · 32
```

| Context | Token |
|---------|-------|
| Inline gap | 4–8px |
| Control padding | 8–12px |
| Field groups | 16px |
| Page sections | 24px |
| Page margin | 16–24px |

**MUST** use Grid gap / theme thickness. **MUST NOT** use arbitrary `Margin="17,3,..."`.

---

## 5. Depth (max 3 elevation levels + focus)

| Level | Treatment | ERP |
|-------|-----------|-----|
| 0 Canvas | page background | `#F1F5F9` light |
| 1 Surface | card | white + shadow-card |
| 2 Elevated | modal · flyout | shadow-elevated |
| 3 Focus | 2px primary ring | keyboard |

**FORBIDDEN:** orbs · bokeh · mesh gradients · glass on table cells.  
Dark luxury: glass on shell only — **never** on DataGrid body.

---

## 6. Borders and dividers

| Type | Spec |
|------|------|
| Hairline divider | 1px muted |
| Card border | optional 1px tertiary |
| KPI accent stripe | 4px left edge |
| Section | full-width band — **not** floating card |

**MUST NOT** round only one side of a border accent — full radius or zero.

---

## 7. Icons

| Rule | BRQOSH |
|------|--------|
| Toolbar | icon + tooltip |
| Critical commands | icon + text |
| Ambiguous icons | tooltip AR/EN required |
| Emoji | **FORBIDDEN** — monogram only |
| Size | 16–20px inline |

---

## 8. Density modes

| Mode | When | Row height |
|------|------|------------|
| Compact | GL power users | 40px |
| **Default** | **most ERP** | **44px** |
| Comfort | long forms only | 48px |

**Default is 44px.** Do not use Comfort on list screens without page spec approval.

---

## 9. Contrast and dark mode

**MUST** test every color decision in:

- Light operational
- Dark executive
- Windows high contrast

| Element | Light | Dark |
|---------|-------|------|
| Body text | slate 900 on white | near-white on midnight |
| Muted | slate 500 | slate 400 |
| Grid line | 15% alpha | 10% alpha |

Hardcoded `#333` = **automatic fail** in dark mode.

---

## 10. Composition

### Shell grid

```
┌──────────┬────────────────────┐
│ Nav 260  │  Header            │
│ fixed    │  Toolbar (auto)    │
│          │  Content (*)       │
│          │  Status (auto)     │
└──────────┴────────────────────┘
```

### Dashboard grid

```
Row 1: [KPI][KPI][KPI][KPI]
Row 2: [ Chart 60% ][ Exceptions table ]
Row 3: [ Full-width drill DataGrid ]
```

**MUST NOT** place more than one hero figure (≥48px) per view.

---

## 11. No art direction fallback

If user gives no visual direction:

1. Read `MASTER.md` + `BrqoshTheme.xaml`
2. Apply `tokens.md`
3. Operational density — **MUST NOT** apply greenfield "bold surprising" patterns

---

## 12. Foundations closure checklist

```
□ Hierarchy clear in 3 seconds
□ Four color jobs separated
□ Type scale locked
□ Spacing on 4px grid
□ Depth ≤ 3 levels
□ State uses icon + label
□ Light + dark readable
```

**Next:** [tokens.md](tokens.md) · [program-design-mastery.md](program-design-mastery.md)
