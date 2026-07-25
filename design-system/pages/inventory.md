# Inventory & Warehouse — Luxury Spec

**Feel:** Fast operational clarity · Alert-driven · Zone-aware

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Search · SKU filter · warehouse selector · valuation method │
├──────────────────────────┬──────────────────────────────────┤
│ Item cards / grid toggle │ Zone map · reorder alerts panel   │
│ Stock · cost · movement  │ Aging · slow mover flags          │
└──────────────────────────┴──────────────────────────────────┘
```

## Item Card (grid mode)

- SKU monogram badge
- Qty on hand · reorder point indicator (gauge mini)
- Valuation · last movement date
- Alert strip if below ROP

## List mode

- DataGrid 44px · movement history drill-down
- Warehouse zones as filter chips

## WPF Mapping

`ProductsPage` · `WarehousesPage` · `StockMovementsPage` · `StockCountPage` · `LandedCostPage`

## Bilingual

- AR: «المخزون» · «نقطة إعادة الطلب» · EN: «Inventory» · «Reorder Point»
