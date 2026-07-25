# CEEF-109 — Desktop DI Golden Path

| الحقل | القيمة |
|-------|--------|
| **النطاق** | W4-A |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **✅ مغلق (كود)** |

## الدليل

- `DesktopAccountingFacade` — `AppServices.CreateInfrastructureScope()`
- صفحات Golden Path بدون `new XxxService(db)`:
  - `SalesInvoiceDialog` · `PurchaseInvoiceDialog` · `PaymentVoucherDialog` · `JournalEntryDialog`

⏭ باقي ~50 صفحة — تدريجياً
