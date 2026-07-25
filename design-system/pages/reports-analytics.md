# Financial Reports & Analytics — Luxury Spec

**Feel:** Document-grade reports · Modern drill-down · Executive export

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Report picker — BS · IS · CF · TB · Tax · Custom KPI         │
│ Period · comparison · branch · export · schedule              │
├─────────────────────────────────────────────────────────────┤
│ Report canvas (document-style)                                │
│ Section headers · subtotals · indentation tree                │
├──────────────────────────┬──────────────────────────────────┤
│ Drill-down panel         │ Comparison sparkline / variance   │
└──────────────────────────┴──────────────────────────────────┘
```

## Report Types

| Report | Pattern |
|--------|---------|
| Balance Sheet | Assets / Liabilities / Equity sections |
| Income Statement | Revenue → COGS → OpEx → Net |
| Cash Flow | Operating · Investing · Financing |
| Trial Balance | Debit/credit columns · zero check |
| Tax reports | Jurisdiction-aware totals |
| KPI dashboards | Link to executive dashboard widgets |

## Visual Rules

- Section totals: semi-bold · top border
- Negative numbers: parentheses in EN · red semantic optional
- Drill-down: click row → ledger detail flyout
- Export: PDF · Excel chips in toolbar

## WPF Mapping

`BalanceSheetPage` · `IncomeStatementPage` · `TrialBalancePage` · `GeneralLedgerPage` · `CustomerStatementPage` · `PosReportsPage` · consolidated variants

## Bilingual

- AR report titles formal (SOCPA-style labels)
- EN: standard IFRS terminology where applicable
