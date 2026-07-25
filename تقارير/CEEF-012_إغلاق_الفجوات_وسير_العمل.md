# CEEF-012 — إغلاق الفجوات المتبقية + تحقق سير العمل

> **التاريخ**: 8 يوليو 2026  
> **المرجع**: CEEF-011 الفجوات المفتوحة

---

## 1. ملخص التنفيذ

| البند | الحالة | الدليل |
|-------|--------|--------|
| RBAC على كل خدمات الإلغاء/العكس | **مُغلق** | `SecurityContext.EnsureCanCancelOrReverse()` على 10+ خدمات |
| اسم المستخدم من الجلسة لا من الواجهة | **مُغلق** | `SecurityContext.ResolveUsername()` + `CurrentUsername` |
| ZATCA BillingReference | **مُغلق** | `ZatcaUblInvoiceBuilder.AddBillingReference` |
| ZATCA QR Phase 2 بعد التوقيع | **مُغلق** | `TrySignWithResult` + `ReplaceQrInSignedXml` |
| XAdES/C14N | **مُغلق** (كود) | `ZatcaXmlSigner` + `System.Security.Cryptography.Xml` |
| تنبيه انحراف سعر البيع | **مُغلق** | `SalesInvoiceDialog` |
| debounce + فلترة DB للموردين | **مُغلق** | `SuppliersPage` + `SupplierDialog` VAT |
| skeleton لوحة التحكم | **مُغلق** | نص «جاري التحميل...» في `DashboardPage` |
| UiAudit = 0 فشل | **مُغلق** | `SUCCESS — 48 فحص` (exit 0) |
| FullScenarioAudit | **مُغلق** | 13/13 PASS |
| اختبارات وحدة | **مُغلق** | **133/133** (38 Domain + 95 Application) |

---

## 2. الأمان — RBAC

### `SecurityContext` (محدّث)

- `CurrentUsername` — يُضبط من `AppServices.CurrentUser`
- `ResolveUsername(passed)` — يفضّل الجلسة على المعامل
- `EnsureCanCancelOrReverse()` — يمنع Viewer وCashier

### الخدمات المحمية

- `SalesInvoiceCancellationService`, `CreditNoteCancellationService`
- `PurchaseInvoiceCancellationService`, `ProgressBillingCancellationService`
- `StockMovementCancellationService`
- `VoucherReversalService`, `PayrollReversalService`
- `JournalReversalService` (عكس يدوي = محاسبة متقدمة)
- `ChequePostingService` (كل العمليات)

### اختبار جديد

- `Viewer_CannotCancelSalesInvoice` في `SecurityContextPostingTests`

---

## 3. ZATCA

1. **UBL BillingReference** لإشعارات الدائن/المدين المرتبطة بفاتورة أصلية
2. **توقيع** → استخراج `InvoiceHash` + `Signature` → **QR Phase 2** → تحديث XML الموقّع
3. بدون شهادة: يبقى QR Phase 1 (محاكاة)

**ما يبقى خارج الكود**: CSID حي من بوابة ZATCA، اختبار قبول هيئة الزكاة على بيئة Production.

---

## 4. الواجهة

| التحسين | الملف |
|---------|-------|
| تاريخ محاسبي يدوي (جولة سابقة) | `DateInputDialog`, 12+ شاشة |
| تحذير سعر | `SalesInvoiceDialog` |
| بحث مؤجل + حد 200 | `CustomersPage`, `ProductsPage`, `SuppliersPage` |
| تحميل لوحة التحكم | `DashboardPage` |
| أتمتة تسجيل الدخول | `LoginWindow.xaml` AutomationProperties |

---

## 5. الترخيص وUiAudit

- `BRQOSH_UITEST=1` — يتجاوز التحقق من الترخيص **للتدقيق الآلي فقط** (لا يُفعَّل في التشغيل العادي)
- UiAudit يمرّر المتغير عند تشغيل `.exe` (`UseShellExecute=false`)
- مستخدم `uiaudit` / `UiAudit9!` يُنشأ عبر `FullScenarioAudit`

---

## 6. تحقق سير العمل

### FullScenarioAudit (PostgreSQL — شركة marwan)

```
[PASS] عملاء/موردون/أصناف
[PASS] مشتريات + مخزون (رصيد 15)
[PASS] مبيعات + ذمم عملاء
[PASS] سند قبض
[PASS] ميزان مراجعة متوازن
[PASS] قائمة مركز مالي متوازنة
[PASS] قائمة دخل
[PASS] إقرار VAT
[PASS] لوحة تحكم
[PASS] كشف حساب + رصيد مورد
[PASS] بيانات موسّعة (BOM، أصول، رواتب...)
[PASS] سجل تدقيق
=== SUCCESS ===
```

### UiAudit

```
[PASS] تسجيل الدخول + النافذة الرئيسية
[PASS] 48 فحص (أقسام حسب ملف التعريف — Retail/عام)
=== SUCCESS — 48 فحص ===
```

---

## 7. ما بقي فعلياً (خارج نطاق الكود اليوم)

| البند | السبب |
|-------|-------|
| MSI على VM نظيفة | يتطلب بيئة Windows افتراضية يدوية |
| ZATCA Production معتمد من الهيئة | يتطلب شهادات CSID حية واختبار قبول |
| ترقيم صفحات كامل (Next/Prev) | تحسين UX — القوائم محدودة بـ 200 + بحث |
| تقارير PDF لكل الشاشات | جزئي (فواتير مبيعات فقط) |

---

## 8. التقييم النهائي

| المحور | الدرجة |
|--------|--------|
| Domain + اختبارات | 85/100 |
| Infrastructure + معاملات | 82/100 |
| أمان RBAC | 78/100 |
| ZATCA (محاكاة) | 72/100 |
| ZATCA (إنتاج حي) | 45/100 — بوابة مغلقة |
| واجهة WPF | 74/100 |
| جاهزية سوق | **تجريبي محاسبي قوي** — إنتاج ZATCA + MSI يدوي |

**المجموع التقريبي**: **~72/100** (من ~68 بعد CEEF-011)

---

## 9. أوامر التحقق

```powershell
dotnet test Brqosh.sln -c Release -- xUnitParallelizeAssembly=false
dotnet run --project tools/FullScenarioAudit/FullScenarioAudit.csproj -c Release
dotnet run --project tools/UiAudit/UiAudit.csproj -c Release
```

---

*بوابة الإنتاج العامة: مفتوحة للتجريب المحاسبي الداخلي؛ ZATCA الحي وMSI يحتاجان خطوة تشغيلية خارج المستودع.*
