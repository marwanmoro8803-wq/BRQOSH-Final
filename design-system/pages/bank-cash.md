# Bank & Cash Management — Luxury Spec

**Feel:** Professional reconciliation power tool · Liquidity command

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Cash position KPIs — per account · consolidated · forecast    │
├──────────────────────────┬──────────────────────────────────┤
│ Bank statement lines     │ Ledger entries (unmatched)        │
│ Checkbox match           │ Drag-link or auto-match suggest   │
├──────────────────────────┴──────────────────────────────────┤
│ Match summary bar · difference · sync status · categorization │
└─────────────────────────────────────────────────────────────┘
```

## Reconciliation Matcher

| State | Color |
|-------|-------|
| Matched | Success green strip |
| Suggested | Cyan dashed link |
| Unmatched bank | Warning left border |
| Unmatched ledger | Danger right border |

## Additional Views

- Multi-account dashboard cards
- Liquidity gauge (7/30/90 day)
- Forecast line chart
- Bank sync status pill
- Transaction category tags

## WPF Mapping

`BankAccountsPage` · `BankReconciliationPage` · `BankTransfersPage` · `ChequesPage` · `ReceiptVouchersPage` · `PaymentVouchersPage`

## Bilingual

- AR: «مطابقة بنكية» · EN: «Bank Reconciliation»
