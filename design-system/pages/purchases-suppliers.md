# Purchases & Suppliers — Luxury Spec

**Feel:** Institutional supplier relationship platform · Trust · Pipeline clarity

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ KPI strip — Open PO · Bills due · 3-way exceptions · Aging   │
├─────────────────────────────────────────────────────────────┤
│ Status pipeline — Draft → Ordered → Received → Matched → Paid│
├──────────────────────────┬──────────────────────────────────┤
│ PO / Bills list          │ Supplier scorecard + detail panel   │
│ Filters · aging badges   │ Contact · terms · history sparkline │
└──────────────────────────┴──────────────────────────────────┘
```

## Key Patterns

| Feature | UI |
|---------|-----|
| Purchase orders | List + `PurchaseOrderDialog` |
| Goods received | GRN matching indicators |
| Three-way match | PO ↔ GRN ↔ Bill pipeline with exception flags |
| Aging summary | Horizontal bar mini-chart per supplier |
| Supplier scorecards | On-time % · quality · spend · radar (future) |
| Delay indicators | `WarningBrush` badge · days overdue tabular |

## Visual Trust Signals

- Green match · amber partial · red exception
- Linked document chips (PO-1234 → GRN-567)
- Audit timestamp on every status change

## WPF Mapping

`PurchaseOrdersPage` · `PurchaseInvoicesPage` · `GoodsReceiptsPage` · `SuppliersPage` · `SupplierStatementPage`

## Bilingual

- AR: «أمر شراء» · «مطابقة ثلاثية» · EN: «Purchase Order» · «Three-Way Match»
