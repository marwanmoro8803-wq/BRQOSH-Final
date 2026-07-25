# POS & Retail — Luxury Spec

**Feel:** Fast cashier flow · shift accountability

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Session bar — cashier · shift · opening cash · live total      │
├──────────────────────────┬──────────────────────────────────┤
│ Product search / grid    │ Cart + payment shortcuts           │
├──────────────────────────┴──────────────────────────────────┤
│ Shift close · Z-report · daily summary                         │
└─────────────────────────────────────────────────────────────┘
```

## WPF Mapping

`PosSessionPage` · `PosShiftsPage` · `PosReportsPage`

## Profile

Visible when `BusinessProfile.Retail`
