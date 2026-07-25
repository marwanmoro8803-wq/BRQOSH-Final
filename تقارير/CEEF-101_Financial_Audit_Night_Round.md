# CEEF-101 — تدقيق المسارات المالية + إصلاحات الجولة الليلية

| الحقل | القيمة |
|-------|--------|
| **النطاق** | مسارات مالية · استعلامات LINQ · ترقيم قوائم · أخطاء WPF |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **مغلق داخل المستودع** — دليل تشغيل أدناه |

## ملخص الإصلاحات

| # | المشكلة | الإصلاح | ملفات |
|---|---------|---------|-------|
| 1 | 4 صفحات مخفية عن المحاسب | مفاتيح `PermissionService` | `PermissionService.cs` |
| 2 | إقرار VAT بدون إشعارات مدين | `VatReturnQuery` + UI + PDF | `VatReturnQuery.cs`, `VatReturnPage` |
| 3 | عكس COGS بـ WAC الحالي | تكلفة من حركة `SalesInvoice:{رقم}` | `CreditNotePostingService.cs` |
| 4 | POS بدون Enter للباركود | `OnSearchKeyDown` | `PosSessionPage` |
| 5 | `EmptyStateHelper` يعطل الشاشات | DockPanel/Grid + try/catch | `EmptyStateHelper.cs` |
| 6 | ثيم مجمّد `#0F172A` | `ThemeManager` gradient جديد | `ThemeManager.cs` |
| 7 | `SumAsync(GrandTotal)` لا يُترجم | أعمدة مخزّنة + `DocumentTotalsQueryable` | Domain entities, `BrqoshSchemaUpdater` |
| 8 | Landed Cost `SingleAsync(IsDefault)` | `GetDefaultWarehouseAsync()` | `LandedCostPage` |
| 9 | SQLite schema guard | `REAL NOT NULL DEFAULT 0` | `SchemaSqlGuard.cs` |
| 10 | PO→فاتورة `SingleAsync` مستودع | `InventoryService.GetDefaultWarehouseAsync` | `PurchaseOrderService.cs` |
| 11 | قوائم سندات/أوامر/إشعارات `Take(100)` | `PagingState` + `RunSafeAsync` | Payment/Receipt Vouchers, PO, CreditNotes |

## اختبارات جديدة

- `GoldenPathPurchaseCycleTests` — PO → GR تلقائي → فاتورة → سداد → AP=0
- `GoldenPathPurchaseCycleTests` — مستودعان افتراضيان لا يسببان `InvalidOperationException`
- (جولة سابقة) صلاحيات محاسب · VAT إشعار مدين · COGS إشعار دائن · `DocumentTotalsQueryable`

## الدليل التشغيلي

| البوابة | النتيجة |
|---------|---------|
| اختبارات | **344/344 PASS** (58 Domain · 185 Application · 26 Api · 75 Desktop) |
| FullScenarioAudit | **13/13 PASS** |
| فحص أمن ثابت | PASS |
| coverlet (Application.Tests) | **80.64%** ≥ 75% |

```powershell
$env:BRQOSH_SKIP_STRESS='1'; $env:BRQOSH_TEST='1'
dotnet test Brqosh.sln -c Release
dotnet run --project tools\FullScenarioAudit\FullScenarioAudit.csproj -c Release
.\scripts\security-static-scan.ps1
.\scripts\check-coverage.ps1
```

## مسارات مالية — حالة التحقق

| المسار | التغطية | ملاحظة |
|--------|---------|--------|
| بيع → ZATCA → تحصيل | ✅ `GoldenPathSellZatcaCollectTests` + FullScenario | |
| شراء → GR → فاتورة → دفع | ✅ `GoldenPathPurchaseCycleTests` + Phase4 | |
| إشعار دائن + COGS | ✅ اختبار تكلفة البيع الأصلية | |
| إشعار مدين + VAT | ✅ `PurchasingCycleTests.VatReturn_DebitNote_*` | |
| مطابقة ثلاثية PO/GR/PI | ✅ `Phase4AdvancedPurchasingTests` | |
| Landed cost | ✅ `LandedCost_IncreasesFifoLayerCost` | UI أُصلح |
| إقفال فترة | ⏭ جزئي | `PeriodClose` tests موجودة؛ لا golden path كامل في هذه الجولة |

## ⏭ ما بقي مفتوحاً (ليس «نقص كود» أو مؤجّل)

| البند | السبب |
|-------|--------|
| ZATCA OTP / Production حي | OTP هيئة الزكاة |
| SOCPA / MSI VM / Load 1M / OWASP ZAP | خارجي — runbooks في `docs/ops/` |
| `Take(100)` متبقي | PosShifts · PurchaseRequests · StockMovements · ZatcaInvoices · Payroll |
| `RunSafeAsync` على كل الصفحات | ~30 صفحة CRUD — تدريجي |
| `PurchaseRequestsPage` DbContext مباشر | يحتاج use cases Application |
| ZATCA outbox بعد commit | قرار معماري — فشل إرسال يترك فاتورة مرحّلة بدون e-invoice |
| `ProgressBilling` إجماليات محسوبة | لا خطأ LINQ حالياً (تجميع في الذاكرة) |
| BranchId على كل الكيانات | O-07 — CEEF-024 أغلق الجلسة فقط |

## الخطوة التالية المقترحة

1. إكمال ترقيم القوائم المتبقية + `RunSafeAsync` للصفحات الحرجة (مخزون · ZATCA · رواتب).
2. Golden path إقفال فترة + تسوية بنك.
3. O-01…O-05 عند توفر OTP / مراجع / VM — [`docs/ops/WAKE_UP_CHECKLIST.md`](../docs/ops/WAKE_UP_CHECKLIST.md).
