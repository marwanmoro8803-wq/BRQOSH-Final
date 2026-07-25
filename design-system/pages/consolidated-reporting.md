# Consolidated Reporting — Luxury Spec

**Feel:** Group CFO command center · elimination transparency

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Company group selector · report currency · as-of date          │
├─────────────────────────────────────────────────────────────┤
│ Consolidated TB / P&L / BS — dual summary + drill grid         │
├──────────────────────────┬──────────────────────────────────┤
│ Intercompany map         │ Elimination journal preview        │
└──────────────────────────┴──────────────────────────────────┘
```

## WPF Mapping

`ConsolidatedTrialBalancePage` · `ConsolidatedIncomeStatementPage` · `ConsolidatedBalanceSheetPage` · `IntercompanyEliminationPage` · `CompanyGroupsPage`

## Bilingual

Report headers bilingual via registry · group names remain user data
