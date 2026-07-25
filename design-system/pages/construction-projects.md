# Construction & Projects — Luxury Spec

**Feel:** Job-site control · retention and BOQ discipline

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Project selector · % complete · retention held · margin KPI    │
├──────────────────────────┬──────────────────────────────────┤
│ BOQ / contracts grid     │ Progress billings + subcontractors │
├──────────────────────────┴──────────────────────────────────┤
│ Profitability chart · retention release actions                 │
└─────────────────────────────────────────────────────────────┘
```

## WPF Mapping

`ProjectsPage` · `ContractsPage` · `BoqPage` · `ProgressBillingsPage` · `SubcontractorsPage` · `RetentionsPage` · `ProjectProfitabilityPage`

## Profile

Visible when `BusinessProfile.Construction` — audited via `BRQOSH_AUDIT_ALL_PROFILES`
