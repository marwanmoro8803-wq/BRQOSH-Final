# CEEF-007-D — مجلس الوكلاء: CEEF-003-A/B

| الحقل | القيمة |
|-------|--------|
| **الملفات** | `CEEF-003-A_PreImplementation_Analysis.md` · `CEEF-003-B_تنفيذ_P0.md` |
| **التاريخ** | 2026-07-08 |
| **الاختبارات** | **128/128** (بعد جلسات 007-A→G) |

---

## 1. ملخص المجلس

CEEF-003-A خطّط لـ P0-01..06 بدقة. CEEF-003-B نفّذها بنجاح **جوهري**. إعادة التحقق (004/005/007) كشفت فجوات تكميلية — **أُغلقت في جلسة المجلس الحالية**.

| P0 | CEEF-003-B | حالة 007-D |
|----|------------|------------|
| P0-01 UnitOfWork | ✅ | ✅ + PO/WorkOrder/Journal داخل UoW |
| P0-02 VAT مستخلصات | ✅ | ✅ |
| P0-03 أمان الدخول | ✅ جزئي | ✅ PasswordChangeWindow + mustChange=true |
| P0-04 ترخيص RSA | ✅ | 🟡 PEM مضمّن |
| P0-05 Production PG | ✅ | ✅ |
| P0-06 inventory_balances | ✅ | ✅ + WAC interleave |

---

## 2. تنفيذ إضافي (007-D)

| ID | الإصلاح |
|----|---------|
| D-01 | `PurchaseOrderService.CreateInvoiceFromOrderAsync` → UnitOfWork |
| D-02 | `WorkOrderService.ReleaseAsync/CancelAsync` → UnitOfWork |
| D-03 | `JournalPostingService.PostAsync` → UnitOfWork + SecurityContext |
| D-04 | `PasswordChangeWindow` إجبار تغيير كلمة المرور |
| D-05 | `CreateInitialAdminAsync` → `mustChangePassword: true` |
| D-06 | `SecurityContext` + ربط `AppServices.CurrentUser` |
| D-07 | WAC: interleave cost+balance لكل سطر وارد (INV-002) |
| D-08 | `ReverseReceiptCostAsync` عند إلغاء مشتريات (INV-003) |

---

## 3. قرار المجلس

**CEEF-003 مُغلق** كمرحلة تنفيذ P0 — مع ملاحظة أن بوابة الإنتاج تتطلب CEEF-006/ZATCA P1.

*التالي: CEEF-004/004-B → `CEEF-007-E`*
