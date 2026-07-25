# Visual Effects (v18 Strict)

> **Binding:** [enforcement.md](enforcement.md) · **Rule:** data before decoration

---

## Chart procedure (MUST follow this order)

```
1. Pick form      — job determines chart type
2. Assign color   — by job only (identity/magnitude/polarity/status)
3. Validate       — contrast + CVD separation (target ≥12 adjacent ΔE)
4. Apply marks    — thin · hairline grid · 2px gaps
5. Add interaction — tooltip · stable colors on filter
6. A11y pass      — legend · table fallback · explicit dark mode
7. Eyeball        — label collision · RTL overflow
```

**Color LAST.** Picking color first is a **closure blocker**.

---

## Form heuristic

| Data job | MUST use | MUST NOT use |
|----------|----------|--------------|
| Single KPI + trend | stat tile + sparkline | one-bar chart |
| 4–6 headlines | KPI bento row | grouped bar |
| Compare categories | horizontal bar | pie with 8 slices |
| Trend | line (1–3 series) | dual y-axis |
| Part-to-whole | stacked bar ≤6 | donut for close values |
| Many classes | table | 9th generated hue |
| One series matters | emphasis (accent + gray) | rainbow |

### Series-count ladder

| Series | Treatment |
|--------|-----------|
| 1–3 | color OK · direct label |
| 4 | direct labels mandatory |
| 5–6 | legend or small multiples |
| 7–8 | fold to Other |
| 9+ | table or facet — **never generate hue** |

---

## Color jobs

| Job | Rule |
|-----|------|
| Categorical | fixed order · entity keeps hue on filter |
| Sequential | one hue light→dark |
| Diverging | warm/cool poles + gray midpoint |
| Status | semantic only — never series 4 |

**MUST NOT** color label text with series hue — use text tokens.

---

## Ship blockers (automatic fail)

| FORBIDDEN | REQUIRED |
|-----------|----------|
| Dual-axis chart | two charts or indexed series |
| Recolor on filter | color follows entity |
| Rainbow sequential | one hue magnitude |
| Thick saturated blocks | thin marks · recessive grid |
| Value on every point | selective labels |
| Status color as series | separate palettes |
| Fake chart data | empty or real query |
| Purple gradient KPI | monogram + teal accent bar |

---

## KPI card (mandatory anatomy)

```
┌─────────────────────────────┐
│ [Monogram]  LABEL      ▲ 3%│  ← delta semantic
│  1,234,567.89 SAR           │  ← tabular · locale
│  ─── sparkline (if data)    │
└─────────────────────────────┘
```

- Monogram badge — **MUST NOT** use emoji
- 4px left accent bar — brand only, not status
- Loading: skeleton in value area
- Empty: "—" + honest subtitle
- Builder: `KpiMetricCardBuilder.cs`

---

## Depth and glass

| Element | Light | Dark |
|---------|-------|------|
| Card | white + shadow-card | midnight + subtle border |
| Glass | forbidden on tables | shell only ≤14% white |
| Glow | none on data | cyan edge ≤40% — AI/focus only |

**FORBIDDEN:** glass on DataGrid cells · neon under figures.

---

## Motion catalog

| Interaction | Duration | Rule |
|-------------|----------|------|
| Button hover | 150ms | tint only |
| Nav active | 200ms | indicator |
| Panel expand | 280ms | height only — width stable |
| Toast | 200ms | non-blocking |
| Chart draw | 300ms max | skip if reduced motion |
| Page transition | 0 | instant |

**FORBIDDEN:** bounce · parallax · delay before Post.

---

## Sparklines

- MUST have ≥3 real data points from service
- MUST NOT render decorative fake trend
- 32–48px tall · no axes · label in card

---

## Verify

```powershell
.\scripts\ui-ux-pro-max-audit.ps1
```

Manual: filter series — colors stable · dark selected · amounts RTL-aligned.
