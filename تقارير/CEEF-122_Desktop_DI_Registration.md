# CEEF-122 — تسجيل خدمات الترحيل في DI + إصلاح الواجهة

| الحقل | القيمة |
|-------|--------|
| **النطاق** | E2/E3 من CEEF-120 — Phase 2a |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **✅ مغلق (كود + اختبار)** |

## المشكلة (من CEEF-120)

- `DesktopAccountingFacade` كان يبني الخدمات يدوياً داخلياً (`new SalesInvoicePostingService(_db)`) — DI صوري.
- 15 خدمة `*PostingService` غير مسجّلة في الحاوية إطلاقاً.
- `CreateInfrastructureScope()` لا يعيد ربط `SecurityContext` (بعكس `CreateDb()`) — فجوة RBAC محتملة على مسار الواجهة.

## التغييرات

### 1. `InfrastructureServiceCollectionExtensions.cs`

سُجّلت **23 خدمة** scoped:

- **ترحيل (15):** Journal · SalesInvoice · CreditNote · PurchaseInvoice · DebitNote · GoodsReceipt · PaymentVoucher · ReceiptVoucher · Cheque · BankStatementDifference · StockMovement · Payroll · FixedAssetAcquisition · FixedAssetDisposal · ProgressBilling
- **إلغاء/عكس/فترات (8):** SalesInvoiceCancellation · PurchaseInvoiceCancellation · CreditNoteCancellation · DebitNoteCancellation · VoucherReversal · PaymentAllocation · YearEndClose · FiscalPeriod

### 2. `DesktopAccountingFacade.cs`

- كل الطرق تحلّ عبر `GetRequiredService<T>()` — صفر إنشاء يدوي
- أُضيف `Db` (قراءة ضمن نفس النطاق) و`GetService<T>()` عام للصفحات

### 3. `AppServices.CreateInfrastructureScope()`

- يستدعي `BindSecurityContext()` قبل إنشاء النطاق — نفس ضمانة `CreateDb()` (AsyncLocal لا يتدفق عبر كل نقاط دخول WPF)

## الدليل

| المستوى | الحالة | الدليل |
|---------|--------|--------|
| ✅ كود | تم | الملفات الثلاثة أعلاه |
| ✅ اختبار | `Facade_ItselfUsesDiResolution_NotNewService` PASS · Desktop 96/96 | `DesktopDiGuardTests.cs` |
| ✅ حي | — | لا ينطبق |
| ⏭ خارجي | — | — |
