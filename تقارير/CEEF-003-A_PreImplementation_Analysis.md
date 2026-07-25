# تقرير CEEF — 003-A

| الحقل | القيمة |
|-------|--------|
| **رقم التقرير** | 003-A |
| **التاريخ** | 2026-07-07 |
| **المرحلة** | Pre-Implementation Impact Analysis |
| **المراجع** | CEEF-001 (`001_تقييم_الوضع_الحالي.md`)، CEEF-002 (`002_التحقق_من_المشاكل_الحرجة.md`) |
| **الهدف** | خطة تنفيذ آمنة لإصلاح P0 بدون كسر الوظائف الحالية |
| **القيود** | لا تعديل كود — تحليل هندسي فقط |

---

## ملخص تنفيذي

تم تحليل المسارات التنفيذية لجميع مشاكل P0 الست المؤكدة في CEEF-002. التوصية المعمارية المركزية:

1. **P0-01:** `IUnitOfWork` خفيف + `ExecuteInTransactionAsync` على مستوى **Orchestrators** (خدمات الترحيل/الإلغاء)، مع إخراج ZATCA وملفات XML وHTTP **خارج** حدود المعاملة.
2. **P0-02:** **Option A** — تطوير `ProgressBilling` بإضافة VAT/ZATCA (وليس تحويله لـ `SalesInvoice`) بسبب منطق المحتجزات.
3. **P0-03:** دمج إنشاء المدير في **Setup Wizard** + `MustChangePasswordOnNextLogin` — إزالة `EnsureDefaultAdminAsync` من مسار الدخول.
4. **P0-04:** **Hybrid Offline** — توقيع RSA (مفتاح عام في التطبيق، خاص عند المُصدِر) + Trial محدود زمنياً.
5. **P0-05:** `AppEnvironment` صريح — Production = **Fail Fast**؛ Development/Test = SQLite مسموح.
6. **P0-06:** **Solution A** — جدول `inventory_balances` كمصدر حقيقة للرصيد، مع إعادة بناء من الحركات كأداة صيانة.

**الحكم النهائي:** **جاهز للتنفيذ** — لا توجد معلومات حرجة ناقصة تمنع البدء. التفاصيل الوحيدة الاختيارية قبل P0-02: تأكيد محاسبي من مختص سعودي لمعالجة VAT على **صافي المستخلص** مقابل **إجمالي الأعمال** (انظر القسم 2).

---

## 1. تحليل P0-01 — Database Transactions

### 1.1 الخيارات المعمارية

| الخيار | الوصف | الملاءمة لـ BRQOSH |
|--------|-------|-------------------|
| **Transaction داخل كل Service** | كل `*PostingService` يفتح `BeginTransaction` | سريع التطبيق لكن تكرار + تعارض عند استدعاء خدمة من خدمة |
| **Unit Of Work مركزي** | `IUnitOfWork` يمرّر `DbContext` ويفتح معاملة واحدة للعملية | **الأفضل** — يتوافق مع نمط `new XService(db)` الحالي |
| **Transaction Pipeline** | Middleware/Decorator يلف كل العمليات | over-engineering لـ Desktop ERP حالياً |

**التوصية:** **Unit Of Work مركزي خفيف** — لا حاجة لإطار DI كامل فوراً:

```csharp
// مقترح تصميمي فقط — لا يُنفَّذ في هذا التقرير
public interface IUnitOfWork
{
    BrqoshDbContext Db { get; }
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
}
```

- الـ Orchestrators (`SalesInvoicePostingService`, `WorkOrderService.CompleteAsync`, …) تستدعي `ExecuteInTransactionAsync`.
- الخدمات الداخلية (`JournalPostingService`, `InventoryService`) **لا تفتح معاملة** — تفترض أنها داخل واحدة أو standalone للعمليات البسيطة.

### 1.2 أين يُوضَع Transaction Boundary؟

| نوع العملية | حدود المعاملة | خارج المعاملة |
|-------------|---------------|----------------|
| ترحيل فاتورة + قيد + مخزون | نعم — ذريّة | ZATCA generate/submit |
| إلغاء فاتورة + عكس قيد + مخزون | نعم | ZATCA cancellation submit |
| عكس قيد (`JournalReversalService`) | نعم — قيدان مرتبطان | AuditLog (يمكن داخله) |
| إكمال أمر إنتاج | نعم — مخزون + قيود + WO | AuditLog |
| ترحيل رواتب/أصول/سند | نعم | AuditLog |
| ZATCA Submit HTTP | لا | كامل — بعد commit المحاسبي |
| كتابة XML على القرص | لا | بعد commit + compensating delete عند الفشل |

### 1.3 تعارضات محتملة

| العنصر | التعارض | الحل المقترح |
|--------|---------|--------------|
| **SaveChanges المتعددة** | كل `SaveChanges` داخل نفس transaction = commit واحد عند `Commit` | دمج الحفظات اختياري؛ المعاملة أهم |
| **AuditLog** | `AuditService.LogAsync` يستدعي `SaveChanges` | يبقى داخل المعاملة — فشل audit = rollback كامل (مقبول) |
| **ZATCA** | HTTP + File I/O داخل transaction يطيل القفل | **Outbox pattern مبسّط:** commit أولاً، ثم ZATCA؛ حالة `PendingZatca` على المستند |
| **EnsurePeriodOpen** | قد ينشئ `FiscalPeriod` بـ SaveChanges منفصل | دمج في نفس UoW أو `Attach` بدون حفظ منفصل |
| **ApplyReceiptCostAsync** | SaveChanges لكل سطر وارد | إعادة تصميم: تحديث `Product.WeightedAverageCost` بدون SaveChanges منفصل داخل UoW |
| **خدمات متداخلة** | `SalesInvoicePostingService` → `InventoryService` → `JournalPostingService` | تمرير نفس `db` + عدم فتح معاملات متداخلة |

### 1.4 خدمات تحتاج إعادة تصميم (وليس wrapper فقط)

| الخدمة | السبب |
|--------|-------|
| `WorkOrderService.CompleteAsync` | 2 حركة مخزون + 2 قيد + 3 SaveChanges — orchestration معقد |
| `SalesInvoicePostingService` / `PurchaseInvoicePostingService` / `CreditNotePostingService` | GL + مستند + مخزون + ZATCA |
| `InventoryService` | `ApplyReceiptCostAsync` و`GetOnHandAsync` يجب فصلها عن نمط SaveChanges/scan الكامل (يرتبط P0-06) |
| `StockMovementPostingService` | قيد + حركة + WAC — خطوات متعددة |
| `PosSaleService` | يستدعي `SalesInvoicePostingService` + تحديث shift |
| `*CancellationService` (مبيعات/مشتريات) | عكس + مخزون متعدد الحركات |

### 1.5 جدول الخدمات

| Service | الوضع الحالي | الخطر | خطة الإصلاح | التعقيد |
|---------|--------------|-------|-------------|---------|
| `SalesInvoicePostingService` | 2 SaveChanges + مخزون + ZATCA | GL بدون مخزون | UoW للـ GL+فاتورة+مخزون؛ ZATCA بعد Commit | عالي |
| `PurchaseInvoicePostingService` | 2 SaveChanges + مخزون | نفس أعلاه | نفس النمط | عالي |
| `CreditNotePostingService` | 2 SaveChanges + مخزون + ZATCA | نفس أعلاه | نفس النمط | عالي |
| `SalesInvoiceCancellationService` | Reverse (2) + N مخزون + SaveChanges | عكس بدون إلغاء | UoW يشمل الكل | عالي |
| `PurchaseInvoiceCancellationService` | Reverse + N مخزون | نفس أعلاه | UoW | عالي |
| `CreditNoteCancellationService` | Reverse + مخزون + ZATCA | جزئي | UoW + ZATCA خارجاً | عالي |
| `ProgressBillingPostingService` | 2 SaveChanges | قيد بدون ربط | UoW + دمج حفظ | منخفض |
| `ProgressBillingCancellationService` | Reverse (2) + SaveChanges | جزئي عكس | UoW | منخفض |
| `ReceiptVoucherPostingService` | 2 SaveChanges | قيد بدون سند | UoW | منخفض |
| `PaymentVoucherPostingService` | 2 SaveChanges | نفس أعلاه | UoW | منخفض |
| `VoucherReversalService` | 2 SaveChanges | قيد عكسي جزئي | UoW | منخفض |
| `PayrollPostingService` | 2 SaveChanges | ربط JE | UoW | منخفض |
| `PayrollReversalService` | 1 SaveChanges + Reverse | متوسط | UoW | منخفض |
| `FixedAssetAcquisitionPostingService` | 2 SaveChanges | أصل بدون قيد | UoW | منخفض |
| `FixedAssetDisposalPostingService` | 2 SaveChanges | نفس أعلاه | UoW | منخفض |
| `JournalReversalService` | 2 SaveChanges | قيد معكوس بدون ربط | UoW | منخفض |
| `JournalPostingService` | 1-2 SaveChanges | فترة منفصلة | داخل UoW المتصل | منخفض |
| `StockMovementPostingService` | 1-2 + WAC saves | مخزون بدون قيد | UoW + إصلاح WAC | متوسط |
| `InventoryService.PostDocumentMovementAsync` | SaveChanges + WAC | عدم اتساق WAC | داخل UoW؛ لا SaveChanges مستقل | **إعادة تصميم** |
| `InventoryService.ApplyReceiptCostAsync` | SaveChanges لكل سطر | WAC جزئي | تحديث في الذاكرة؛ حفظ مرة | **إعادة تصميم** |
| `ChequePostingService` | 2 SaveChanges لكل عملية | شيك بدون قيد | UoW في `PostAndLinkAsync` | متوسط |
| `WorkOrderService.CompleteAsync` | مخزون×2 + 3 SaveChanges | WIP/مخزون/GL منفصل | UoW واحد للعملية كاملة | **عالي جداً** |
| `WorkOrderService.ReleaseAsync` | 1 SaveChanges | منخفض | اختياري | منخفض |
| `PosSaleService` | يستدعي Sales + SaveChanges shift | shift بدون فاتورة | UoW خارجي يشمل shift | متوسط |
| `RetentionReleaseService` | 1 SaveChanges | منخفض | UoW | منخفض |
| `SubcontractRetentionWithholdService` | 1 SaveChanges | منخفض | UoW | منخفض |
| `StockMovementCancellationService` | 2 SaveChanges | جزئي | UoW | متوسط |
| `StockCountAdjustmentService` | عدة `PostAsync` | جرد جزئي | UoW على كامل الجرد | متوسط |
| `DepreciationService` | SaveChanges لكل أصل | إهلاك جزئي | UoW على الدفعة الشهرية | متوسط |
| `ZatcaEInvoiceGenerator` | SaveChanges + File.Write | XML بدون DB | **خارج transaction** — بعد commit | متوسط |
| `ZatcaSubmissionService` | SaveChanges + HTTP | حالة ZATCA | **خارج transaction** | متوسط |
| `ZatcaAutoSubmissionHelper` | يبتلع أخطاء ZATCA | مقصود | يبقى — لا rollback محاسبي | — |
| `FiscalPeriodService` | SaveChanges | — | داخل UoW عند الحاجة | منخفض |
| `BankReconciliationQuery` | 2 SaveChanges | تسوية جزئية | UoW | منخفض |
| `ConstructionProjectService` | 3 SaveChanges | CRUD | أقل أولوية | منخفض |
| `PurchaseOrderService` | 1-3 SaveChanges | أوامر | UoW للترحيل | منخفض |

---

## 2. تحليل P0-02 — VAT/ZATCA للمستخلصات

### 2.1 دورة الحياة الحالية لـ ProgressBilling

```
[UI: ProgressBillingDialog.OnSave]
    │
    ▼
CreateDraft(company, project, customer, date, period, amount, retention%)
    │  ← لا مرحلة مسودة منفصلة في DB؛ الإنشاء والترحيل فوريان
    ▼
ProgressBillingPostingService.PostAsync
    │  EnsurePeriodOpen
    │  Post() → JournalEntry (ذمم + محتجز + إيراد عقد)
    │  SaveChanges ×2 (قيد + مستخلص + ربط JE)
    ▼
[انتهاء — لا VAT — لا ZATCA — لا VatReturn]
```

**ملاحظة:** لا توجد مرحلة «Approval» منفصلة — الزر «حفظ» = ترحيل مباشر.

### 2.2 دورة الحياة المستهدفة (بعد الإصلاح)

```
CreateDraft → (اختياري: حفظ مسودة) → PostAsync
    → JournalEntry (+ VatOutput 15%)
    → ProgressBilling (Subtotal, VatAmount, GrandTotal)
    → [Commit Transaction]
    → ZatcaEInvoiceGenerator.TryGenerateForProgressBillingAsync
    → VatReturnQuery يشمل ProgressBillings
    → CustomerBalanceQuery يستخدم GrandTotal (شامل VAT)
```

### 2.3 الكيانات التي تحتاج تعديل

| الكيان/الملف | التعديل المطلوب |
|--------------|-----------------|
| `ProgressBilling` (Domain) | `Subtotal`, `VatAmount`, `GrandTotal`؛ تعديل `Post()` لقيد VAT |
| `ProgressBillingPostingService` | حسابات `VatOutput`؛ ربط ZATCA بعد commit |
| `ProgressBillingCancellationService` | ZATCA cancellation (إن وُجد مستند) |
| `BrqoshDbContext` + Migration | أعمدة VAT؛ فهرس إن لزم |
| `VatReturnQuery` | إدراج `ProgressBillings` Posted |
| `ZatcaEInvoiceDocument` / `ZatcaDocumentType` | نوع `ProgressBilling` أو إعادة استخدام `SalesInvoice` في XML |
| `ZatcaEInvoiceGenerator` | `TryGenerateForProgressBillingAsync` |
| `ZatcaUblInvoiceBuilder` | دعم وصف مستخلص (نوع مستند UBL) |
| `CustomerBalanceQuery` | استخدام `GrandTotal` بدل `NetBillableAmount` للذمم |
| `DashboardQuery` | مبيعات الشهر — تضمين VAT |
| `ProgressBillingDialog` | عرض VAT محسوب؛ خيار «خاضع للضريبة» |
| اختبارات جديدة | ترحيل + VAT + إقرار + ZATCA simulation |

### 2.4 الحسابات المحاسبية المطلوبة

القيد الحالي (بدون VAT) لأعمال 1,000,000 ومحتجز 10%:

| الطرف | الحالي | المستهدف (مع VAT 15% على الإجمالي)* |
|-------|--------|--------------------------------------|
| مدين ذمم (`1104`) | 900,000 | 900,000 + VAT على الصافي؟ |
| مدين محتجز (`1114`) | 100,000 | 100,000 + VAT على المحتجز؟ |
| دائن إيراد عقد (`4104`) | 1,000,000 | 1,000,000 (قبل VAT) |
| دائن VAT مخرجات (`2106`) | — | 150,000 |

\* **نقطة تحتاج تأكيد محاسبي:** هل VAT على `CurrentWorkAmount` كاملاً أم على `NetBillableAmount` فقط؟ التوصية المبدئية: VAT على **إجمالي قيمة الأعمال** (`CurrentWorkAmount`) مع توزيع الذمم/المحتجز بنفس النسب — يتطلب مراجعة SOCPA/ZATCA قبل الترميز.

### 2.5 تأثير على التقارير

| التقرير/الاستعلام | التأثير |
|-------------------|---------|
| **Customer Balance** | يجب استخدام `GrandTotal` (شامل VAT) في الذمم — حالياً `NetBillableAmount` فقط |
| **General Ledger** | قيد إضافي على `2106` — يظهر تلقائياً من JE |
| **Trial Balance** | متوازن إذا القيد صحيح |
| **VAT Return** | إضافة مخرجات من المستخلصات |
| **ZATCA** | مستند إلكتروني جديد في السلسلة |
| **Dashboard** | مبيعات الشهر ترتفع بمقدار VAT المستخلصات |
| **RetentionSummary** | لا تغيير جوهري — المحتجز يبقى قبل VAT أو يُعاد تعريفه |

### 2.6 Option A vs Option B

#### Option A: تطوير ProgressBilling

| الإيجابيات | السلبيات |
|-----------|----------|
| يحافظ على منطق المحتجزات (`RetentionAmount`, `NetBillableAmount`) | تكرار منطق VAT مع `SalesInvoice` |
| أقل تغيير في UI المقاولات | يحتاج مسار ZATCA جديد |
| `CustomerBalanceQuery` يعرف المستخلصات أصلاً | migration للبيانات القديمة (بدون VAT) |
| يتماشى مع مصطلحات المقاولات (مستخلص ≠ فاتورة بضائع) | |

#### Option B: تحويل المستخلص إلى SalesInvoice ضريبية

| الإيجابيات | السلبيات |
|-----------|----------|
| VAT + ZATCA + VatReturn **جاهزان** | **فقدان نموذج المحتجز** في القيد (3 طرفية) |
| إعادة استخدام `SalesInvoicePostingService` | يخلط مفهوم «فاتورة بضائع» مع «مستخلص أعمال» |
| أقل كود ZATCA جديد | يحتاج سطور وهمية/وصفية — لا منتجات |
| | إما فاتورتان (مستخلص + محتجز) أو قيد يدوي للمحتجز |
| | كسر تقارير المقاولات الحالية |

**التوصية:** **Option A** — تطوير `ProgressBilling` مع استخراج `IVatCalculator` مشترك من `SalesInvoice` لتجنب تكرار نسبة 15%.

### 2.7 بيانات قديمة (Migration)

- مستخلصات `Posted` بدون VAT: إما **ترحيل لمرة واحدة** (قيد تسوية VAT) أو اعتبارها «ما قبل الامتثال» مع flag `VatExemptLegacy`.
- يجب توثيق القرار في تقرير تنفيذ 003-B.

---

## 3. تحليل P0-03 — نظام الدخول

### 3.1 التصميم الحالي

```
DatabaseBootstrapService.InitializeAsync
  → EnsureDefaultAdminAsync("admin", "admin123")  [سطر 20]

LoginWindow
  → Username="admin", Password="admin123" مُعبَّأان
  → EnsureDefaultAdminAsync عند كل دخول
```

### 3.2 التصميم المستهدف

| المكوّن | التوصية |
|---------|---------|
| **إزالة admin/admin123** | حذف `EnsureDefaultAdminAsync` من `DatabaseBootstrapService` و`LoginWindow` |
| **First Run Setup** | **نعم** — خطوة 4 في `SetupWizardWindow`: اسم مستخدم + كلمة مرور + تأكيد |
| **Temporary Password** | لا حاجة إذا الإنشاء في Setup |
| **MustChangePassword** | **نعم** — `AppUser.MustChangePasswordOnNextLogin` للحالات الاستثنائية (reset من المدير) |
| **Password Policy** | الحد الأدنى: 8 أحرف، حرف + رقم (قابل للتوسع) |

### 3.3 مسار التدفق المقترح

```
SetupWizard (جديد)
  → company + profile + VAT + admin user/password
  → DatabaseBootstrapService.CreateInitialAdmin(user, hash)
  → لا EnsureDefaultAdmin

LoginWindow
  → حقول فارغة
  → إن MustChangePassword → ChangePasswordWindow إلزامي
```

### 3.4 التأثير

| الجانب | التأثير | المعالجة |
|--------|---------|----------|
| **مستخدمون حاليون** | DB موجودة فيها admin/admin123 | Migration script: فرض `MustChangePassword=true` لـ admin أو حذف admin وإنشاء من الإعدادات |
| **الاختبارات** | `StartupSimulationTests` يعتمد admin123 | Factory method `TestData.CreateAdminUser(db, password)` |
| **UiAudit** | `Program.cs` سطر 95: admin/admin123 | متغير بيئة `BRQOSH_TEST_PASSWORD` أو إنشاء مستخدم في fixture |
| **DbStartupProbe** | نفس الاعتماد | تحديث |
| **Setup Wizard** | لا يجمع بيانات مستخدم حالياً | إضافة خطوة UI |

### 3.5 المخاطر

- **منخفض:** كسر اختبارات — محلول بتحديث fixtures.
- **متوسط:** عملاء لديهم تثبيت قديم — يحتاجون تغيير كلمة مرور عند أول تشغيل بعد التحديث.

---

## 4. تحليل P0-04 — License System

### 4.1 الوضع الحالي (ملخص CEEF-002)

- `Secret` في `LicenseService.cs`
- `ValidateForCompany` fallback دائم النجاح
- `EnsureTrialKey` يولّد ترخيصاً تلقائياً
- `license.json` بدون تشفير في `%APPDATA%\BRQOSH\`

### 4.2 مقارنة الخيارات

| الخيار | Offline | أمان | تعقيد | الملاءمة |
|--------|---------|------|-------|----------|
| **RSA Digital Signature** | ✅ | جيد — لا secret في التطبيق | متوسط | **الأفضل لـ Desktop ERP** |
| **Online License Server** | ❌ يحتاج إنترنت | ممتاز | عالي | لاحقاً كطبقة إضافية |
| **Hybrid** | ✅ أساسي + تحقق اختياري | جيد جداً | عالي | **الهدف طويل المدى** |

### 4.3 Architecture مقترحة (Hybrid Offline-first)

```
┌─────────────────────────────────────────────────────────┐
│  License Issuer (خارج التطبيق — أداة داخلية للبائع)    │
│  - يحتفظ بـ RSA Private Key (لا يُوزَّع)                │
│  - يُصدِر license.brq (JSON موقّع)                      │
└────────────────────────┬────────────────────────────────┘
                         │ ملف ترخيص
                         ▼
┌─────────────────────────────────────────────────────────┐
│  BRQOSH Desktop                                         │
│  - RSA Public Key مُضمَّن (ليس secret — آمن)           │
│  - يتحقق من: company + machineId + expiry + features    │
│  - Trial: 30 يوم، موقّع بنفس المفتاح، tier=trial        │
└─────────────────────────────────────────────────────────┘
```

**محتوى `license.brq` (مقترح):**
```json
{
  "companyName": "...",
  "machineId": "...",
  "tier": "standard|trial",
  "expiresUtc": "2027-01-01T00:00:00Z",
  "features": ["construction", "zatca"],
  "signature": "base64-RSA-SHA256..."
}
```

### 4.4 Trial Mode

| البند | التصميم |
|-------|---------|
| **المدة** | 30 يوماً من `firstRunUtc` |
| **التوقيع** | Trial payload موقّع بمفتاح البائع — لا يمكن تمديده محلياً |
| **بعد الانتهاء** | وضع قراءة فقط أو إيقاف — قرار منتج |
| **إزالة** | حذف `EnsureTrialKey` و`ValidateForCompany` fallback |

### 4.5 منع Secret داخل التطبيق

- **Private key:** أداة CLI منفصلة (`tools/LicenseIssuer`) — ليست في `Brqosh.Desktop`.
- **Public key:** `Resources/license-public.pem` — للتحقق فقط.
- **تدوير المفاتيح:** إصدارات متعددة من public key في التطبيق.

### 4.6 Online (اختياري لاحقاً)

- نقطة `/api/license/heartbeat` للتحقق من الإبطال — لا تمنع التشغيل إذا offline (grace period 7 أيام).

---

## 5. تحليل P0-05 — Database Provider

### 5.1 التدفق الحالي

```
App.OnStartup
  → LoadDatabaseConfig()        [افتراضي SQLite إن لا database.json]
  → EnsureDatabaseReachableAsync()  [fallback صامت إلى SQLite]
  → DatabaseBootstrapService
  → (تحذير MessageBox إن fallback)
```

### 5.2 تصميم Environment Mode المقترح

| الوضع | المصدر | SQLite | PostgreSQL Fallback | عند فشل الاتصال |
|-------|--------|--------|---------------------|-----------------|
| **Development** | `appsettings` / env `BRQOSH_ENV=Development` | ✅ مسموح | ✅ مسموح مع تحذير | تحذير + متابعة |
| **Test** | في الاختبارات | ✅ فقط | ❌ | N/A |
| **Production** | `BRQOSH_ENV=Production` أو MSI flag | ❌ ممنوع | ❌ **Fail Fast** | إيقاف + رسالة واضحة |

**تخزين الوضع:** `%APPDATA%\BRQOSH\app.json` أو متغير بيئة — **ليس** في `database.json` (فصل الاهتمامات).

### 5.3 سيناريوهات التشغيل

| الحالة | Development | Production |
|--------|-------------|------------|
| **PostgreSQL unavailable** | Fallback SQLite + تحذير | **Shutdown** — «تعذر الاتصال بقاعدة البيانات الإنتاجية» |
| **database.json missing** | SQLite `%APPDATA%\BRQOSH\brqosh.db` | **Shutdown** — «ملف إعداد قاعدة البيانات مطلوب» |
| **Connection string invalid** | Fallback أو خطأ حسب المحتوى | **Shutdown** مع تفاصيل (بدون كشف كلمة المرور) |
| **database.json تالف** | SQLite | Shutdown |

### 5.4 Fail Fast — متطلبات Production

1. `database.json` **إلزامي** مع `Provider: postgresql`.
2. `EnsureDatabaseReachableAsync` — عند `Production`: rethrow بدلاً من catch.
3. إزالة تعليق «SQLite للتشغيل الفوري» من سلوك Production في `DbContextFactory`.
4. تسجيل log: `provider=PostgreSQL`, `host=...`, `database=...` (بدون password).
5. MSI installer: يُنشئ `database.json` template أو يطلبه في Setup.

### 5.5 التأثير على الاختبارات

- الاختبارات تستخدم `BrqoshDbContextFactory.Create(Sqlite, ...)` مباشرة — **لا تتأثر**.
- `StartupSimulationTests` تبقى على SQLite صريح.

---

## 6. تحليل P0-06 — Inventory Performance

### 6.1 الوضع الحالي (ملخص)

`GetOnHandAsync`: `ToListAsync()` على **كل** `StockMovements.Posted` للشركة → O(حركات × سطور) لكل استعلام.

### 6.2 مقارنة الحلول

| المعيار | A: Balance Table | B: SQL Aggregation | C: Caching |
|---------|------------------|-------------------|------------|
| **دقة متعدد المستخدمين** | ممتاز (مع transaction) | جيد | ضعيف — invalidation |
| **أداء القراءة** | O(1) | O(حركات المنتج) | O(1) بعد warm |
| **أداء الكتابة** | تحديث + إدراج | إدراج فقط | إدراج + cache bust |
| **تعقيد التنفيذ** | متوسط — migration + backfill | منخفض | متوسط |
| **WAC (متوسط مرجح)** | لا يحل وحده | لا يحل | لا يحل |
| **التقارير التاريخية** | يحتاج snapshot أو إعادة حساب | من الحركات | — |
| **المخاطر** | عدم اتساق إن فشل تحديث | بطء مع الملايين | stale data |

### 6.3 التوصية: Solution A (Balance Table) + B كتحقق

**جدول مقترح:**
```
inventory_balances (
  company_id, warehouse_id, product_id,
  quantity_on_hand DECIMAL(18,4),
  row_version / updated_at_utc,
  PRIMARY KEY (company_id, warehouse_id, product_id)
)
```

**القواعد:**
- كل `PostDocumentMovementAsync` / `TransferAsync` يحدّث `inventory_balances` **داخل نفس UoW**.
- `GetOnHandAsync` → `SELECT quantity_on_hand` مباشرة.
- **أداة صيانة:** `InventoryReconciliationService` يقارن balance vs aggregation من الحركات (ليلاً أو عند الطلب).
- **Backfill:** migration script يحسب الأرصدة من الحركات الحالية.

### 6.4 التأثير على الوحدات

| الوحدة | التأثير |
|--------|---------|
| **Stock Posting** | تحديث balance في نفس transaction |
| **Weighted Average Cost** | يبقى على `Product` — `ApplyReceiptCostAsync` يُعاد تصميمه (لا يستدعي GetOnHand scan) |
| **Manufacturing** | `WorkOrderService` يستفيد من قراءة O(1) |
| **Transfers** | تحديث balance في مستودعين |
| **Reports** | تقرير «أرصدة المخزون» من الجدول مباشرة — جديد أو تحسين |
| **Stock Count** | `SystemQty` من balance table بدل scan |

### 6.5 ترتيب التنفيذ مع P0-01

يُفضَّل تنفيذ P0-06 **مع** P0-01 (نفس UoW) أو **بعده مباشرة** — تحديث balance بدون transaction = خطر اتساق.

---

## 7. ترتيب التنفيذ

### Phase 1 — الأساس (أسبوع 1)

| البند | المحتوى | Dependencies |
|-------|---------|--------------|
| **1.1** | `IUnitOfWork` + `ExecuteInTransactionAsync` | — |
| **1.2** | تطبيق UoW على: `JournalReversalService`, `ProgressBillingPostingService`, Vouchers, Payroll, Assets | 1.1 |
| **1.3** | اختبارات atomicity (فشل متعمد منتصف العملية) | 1.1 |
| **1.4** | `AppEnvironment` + Production Fail Fast | — |

**المخاطر:** كسر اختبارات موجودة — تشغيل `dotnet test` بعد كل خدمة.

**Rollback:** feature branch؛ كل commit يمرّ بالاختبارات.

### Phase 2 — العمليات المركّبة (أسبوع 2)

| البند | المحتوى | Dependencies |
|-------|---------|--------------|
| **2.1** | UoW على `SalesInvoicePostingService`, `PurchaseInvoicePostingService`, `CreditNotePostingService` | Phase 1 |
| **2.2** | ZATCA خارج transaction + حالة `PendingZatca` | 2.1 |
| **2.3** | UoW على Cancellation services | 2.1 |
| **2.4** | `WorkOrderService.CompleteAsync` إعادة تصميم | 1.1 + 2.1 |

### Phase 3 — الأمن (أسبوع 2-3، متوازي جزئياً)

| البند | المحتوى | Dependencies |
|-------|---------|--------------|
| **3.1** | P0-03: Setup Wizard admin + إزالة admin123 | — |
| **3.2** | P0-04: RSA license + إزالة fallback | أداة LicenseIssuer |
| **3.3** | تحديث UiAudit + اختبارات | 3.1 |

### Phase 4 — الامتثال (أسبوع 3-4)

| البند | المحتوى | Dependencies |
|-------|---------|--------------|
| **4.1** | P0-02: VAT على ProgressBilling | تأكيد محاسبي (اختياري لكن مُستحسن) |
| **4.2** | ZATCA للمستخلصات | 4.1 + 2.2 |
| **4.3** | VatReturn + CustomerBalance تحديث | 4.1 |

### Phase 5 — الأداء (أسبوع 4-5)

| البند | المحتوى | Dependencies |
|-------|---------|--------------|
| **5.1** | `inventory_balances` table + migration backfill | Phase 1 UoW |
| **5.2** | استبدال `GetOnHandAsync` | 5.1 |
| **5.3** | Reconciliation tool + اختبار 100K حركة | 5.2 |

### Rollback Strategy (عام)

1. **Git:** فرع `feature/p0-remediation` — دمج تدريجي per phase.
2. **DB:** كل migration لها `Down` أو script عكسي موثّق.
3. **Feature flags:** `UseInventoryBalances`, `StrictProductionDb` — إن لزم تخفيف مخاطر الإطلاق.
4. **البيانات:** نسخة احتياطية إلزامية قبل Phase 4 و 5.

---

## 8. معايير النجاح (Acceptance Criteria)

### P0-01 — Transactions

- [ ] `grep BeginTransaction` أو `ExecuteInTransactionAsync` في كل orchestrator مركّب.
- [ ] اختبار: محاكاة فشل بعد حفظ القيد وقبل المخزون → **صفر** قيود/فواتير/حركات جديدة في DB.
- [ ] اختبار: فشل ZATCA HTTP **لا يلغي** ترحيل الفاتورة المحاسبي.
- [ ] `dotnet test`: 115+ tests pass (مع اختبارات atomicity جديدة).
- [ ] `WorkOrderService.CompleteAsync`: إما كامل النجاح أو rollback كامل.

### P0-02 — VAT/ZATCA مستخلصات

- [ ] مستخلص مرحّل يحتوي `VatAmount` = 15% من الوعاء المعتمد.
- [ ] القيد متوازن ويشمل حساب `2106`.
- [ ] `VatReturnQuery` يعكس مخرجات المستخلصات.
- [ ] عند تفعيل ZATCA: مستند في `zatca_einvoice_documents` مع XML صالح.
- [ ] `CustomerBalanceQuery` يتسق مع `GrandTotal`.
- [ ] اختبار إلغاء مستخلص يعكس VAT.

### P0-03 — الدخول

- [ ] لا يوجد `admin123` في الكود المصدري (بما في ذلك UI وtests إلا عبر fixture عشوائي).
- [ ] تثبيت جديد: لا دخول بدون إنشاء مستخدم في Setup Wizard.
- [ ] `MustChangePassword` يمنع الوصول للـ MainWindow حتى التغيير.
- [ ] `EnsureDefaultAdminAsync` محذوف أو محصور في test helpers فقط.

### P0-04 — الترخيص

- [ ] لا `Secret` في `Brqosh.Desktop` أو `Brqosh.Infrastructure` المُوزَّع.
- [ ] `ValidateForCompany` بدون fallback ذاتي — false عند ترخيص غير صالح.
- [ ] تعديل `license.brq` يدوياً **يفشل** التحقق (توقيع RSA).
- [ ] Trial منتهٍ يمنع التشغيل (أو وضع محدود — حسب قرار المنتج).
- [ ] أداة `LicenseIssuer` موثّقة خارج التطبيق.

### P0-05 — Database Provider

- [ ] `BRQOSH_ENV=Production` + PostgreSQL down → **لا يبدأ** التطبيق.
- [ ] Production بدون `database.json` → **لا يبدأ**.
- [ ] Development: fallback مسموح مع log/تحذير.
- [ ] الاختبارات لا تعتمد على `AppServices` للـ provider.

### P0-06 — Inventory Performance

- [ ] `GetOnHandAsync` لا يستدعي `ToListAsync` على كل الحركات.
- [ ] اختبار أداء: 100K حركة — استعلام رصيد &lt; 100ms (PostgreSQL، hardware مرجعي).
- [ ] `InventoryReconciliationService`: فرق = 0 بعد كل ترحيل في الاختبارات.
- [ ] Manufacturing + Transfer + Stock Count يعملون بدون regression.

---

## 9. مخاطر التنفيذ الشاملة

| المخاطرة | الاحتمال | الأثر | التخفيف |
|----------|----------|-------|---------|
| كسر 115 اختبار أثناء UoW | عالي | تأخير | تطبيق تدريجي + CI |
| بيانات مستخلصات قديمة بلا VAT | متوسط | تقارير مختلطة | migration flag + تسوية |
| قفل SQLite أطول مع transactions | منخفض | بطء | Production على PostgreSQL فقط |
| تعقيد WorkOrder refactor | متوسط | regression تصنيع | اختبارات Manufacturing موجودة |
| رفض ZATCA للمستخلص (UBL type) | متوسط | امتثال | اختبار simulation قبل production |

---

## 10. معلومات إضافية (اختيارية — لا تمنع البدء)

| # | السؤال | التأثير إن أُجيب خطأ | التوصية |
|---|--------|---------------------|---------|
| 1 | VAT على `CurrentWorkAmount` كاملاً أم `NetBillableAmount`؟ | إقرار خاطئ | مراجعة محاسب قبل Phase 4 |
| 2 | Trial بعد 30 يوم: إيقاف كامل أم قراءة فقط؟ | UX | قرار منتج |
| 3 | هل MSI يُنشئ `database.json` تلقائياً؟ | تثبيت Production | قرار في Phase 1.4 |

---

## الحكم النهائي

# جاهز للتنفيذ

يمكن البدء بـ **Phase 1** (UoW + Fail Fast) فور اعتماد هذا التقرير.  
يُستحسن الحصول على تأكيد محاسبي لنقطة VAT على المستخلص **قبل Phase 4** وليس قبل Phase 1.

**التقرير التالي المقترح بعد التنفيذ:** `CEEF-003-B_P0-01_Transactions_Implementation.md`

---

*نهاية تقرير CEEF-003-A — لم يُجرَ أي تعديل على الكود.*
