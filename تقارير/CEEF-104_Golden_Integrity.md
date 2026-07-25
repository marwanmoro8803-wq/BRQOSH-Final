# CEEF-104 — Golden Accounting Integrity

| الحقل | القيمة |
|-------|--------|
| **النطاق** | W1-C — R-05 |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **✅ مغلق (كود + اختبار)** |

## السيناريوهات (8/8)

| # | سيناريو | ملف الاختبار |
|---|---------|--------------|
| 1 | شراء أصل | `Golden_AssetAcquisition_*` |
| 2 | بيع أصل | `Golden_AssetDisposal_*` |
| 3 | إهلاك | `Golden_Depreciation_*` |
| 4 | جرد مخزون | `Golden_StockAdjustment_*` |
| 5 | مرتجع/إشعار دائن | `Golden_CreditNote_*` |
| 6 | ضريبة | `Golden_VatReturn_*` |
| 7 | فروقات عملة | `Golden_FxRevaluation_*` |
| 8 | وضع قراءة فقط | `OperationalMode_ReadOnly_*` |
