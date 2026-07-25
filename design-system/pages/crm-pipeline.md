# CRM — Luxury Spec

**Feel:** Pipeline clarity · relationship depth without clutter

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ KPI strip — leads · open opportunities · activities today      │
├──────────────────────────┬──────────────────────────────────┤
│ Filter toolbar           │ Primary grid (leads / opps / acts) │
├──────────────────────────┴──────────────────────────────────┤
│ Detail drawer or inline expand — stage · owner · next step    │
└─────────────────────────────────────────────────────────────┘
```

## States

| State | Treatment |
|-------|-----------|
| New lead | Cyan accent left border |
| Won opportunity | Success pill |
| Overdue activity | Warning timestamp |

## WPF Mapping

`CrmLeadsPage` · `CrmOpportunitiesPage` · `CrmActivitiesPage`

## Bilingual

Nav via `NavigationCatalog` · static labels via `XamlUiLocalizer` + `UiBilingualRegistry`
