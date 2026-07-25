# CEEF-003-B — تقرير تنفيذ إصلاحات P0

**التاريخ:** 2026-07-07  
**المرجع:** CEEF-001، CEEF-002، CEEF-003-A  
**الحالة:** منفَّذ — اجتاز `dotnet build -c Release` و `dotnet test -c Release` (**122/122**)

---

## ملخص تنفيذي

| المرحلة | الحالة | دليل |
|---------|--------|------|
| P0-01 Transaction Architecture | ✅ | `UnitOfWork` + 25 orchestrator |
| P0-02 Construction VAT/ZATCA | ✅ | VAT على المستخلص + ZATCA + VatReturn |
| P0-03 Security Login | ✅ | معالج إعداد المدير، إزالة admin123 |
| P0-04 Licensing | ✅ | RSA + `tools/LicenseIssuer` |
| P0-05 Database Mode | ✅ | `BRQOSH_ENV=production` → PostgreSQL فقط |
| P0-06 Inventory Performance | ✅ | `inventory_balances` + اختبار 1,000 حركة |

---

## P0-01 — Transaction Architecture

### الملفات المعدَّلة (أبرزها)

- `src/Brqosh.Infrastructure/Persistence/UnitOfWork.cs` — معاملات ذرية + دعم معاملات متداخلة
- 25 خدمة ترحيل/إلغاء/عكس في `Sales/`, `Purchasing/`, `Construction/`, `Treasury/`, `Payroll/`, `Assets/`, `Inventory/`, `Manufacturing/`, `Retail/`, `Accounting/`

### السبب

كل عملية ترحيل مركّبة كانت تستدعي `SaveChanges` متعددة دون `BeginTransaction` — أي فشل يترك قيداً أو حركة مخزون جزئية.

### النمط المعتمد

1. كل عمليات DB داخل `UnitOfWork.ExecuteInTransactionAsync`
2. ZATCA (`ZatcaEInvoiceGenerator`, `ZatcaAutoSubmissionHelper`) **بعد** الـ commit
3. المعاملات المتداخلة (إلغاء → عكس قيد) تشارك نفس المعاملة

### الاختبارات

| الاختبار | النتيجة |
|----------|---------|
| `TransactionAtomicityTests.Sales_invoice_post_succeeds_atomically` | ✅ |
| `TransactionAtomicityTests.Insufficient_stock_rolls_back_invoice_and_journal` | ✅ |

### مشاكل متبقية

- `JournalPostingService.EnsurePeriodOpenAsync` ما زال يحفظ فترات مالية بشكل مستقل (آمن داخل UoW لكن غير مدمج)
- اختبارات فشل ZATCA/DB أثناء الحفظ تحتاج hooks إضافية (لم تُنفَّذ بعد)

---

## P0-02 — Construction VAT/ZATCA

### الملفات

- `src/Brqosh.Domain/Construction/ProgressBilling.cs` — `TaxableAmount`, `VatAmount`, `TotalAmount`, `GrandTotal`
- `src/Brqosh.Infrastructure/Construction/ProgressBillingPostingService.cs` — قيد ضريبة + ZATCA بعد commit
- `src/Brqosh.Infrastructure/Reporting/VatReturnQuery.cs` — إدراج المستخلصات
- `src/Brqosh.Infrastructure/Zatca/ZatcaEInvoiceGenerator.cs` — `TryGenerateForProgressBillingAsync`
- `src/Brqosh.Domain/Zatca/ZatcaEnums.cs` — `ZatcaDocumentType.ProgressBilling`

### القيد المحاسبي (مستخلص 1,000,000 — 15% VAT، محتجز 10%)

| حساب | مدين | دائن |
|------|------|------|
| ذمم عملاء | 1,050,000 | |
| محتجز | 100,000 | |
| إيراد عقد | | 1,000,000 |
| ضريبة مخرجات | | 150,000 |

### الاختبار الإلزامي

`ProgressBillingVatTests.Million_riyal_progress_billing_vat_journal_vat_return_and_zatca` — ✅

---

## P0-03 — Security Login

### التغييرات

- إزالة `admin/admin123` من `LoginWindow` و`DatabaseBootstrapService`
- `SetupWizardWindow` — إنشاء حساب مدير + `PasswordPolicy`
- `UserAuthService.CreateInitialAdminAsync` — PBKDF2 + سياسة كلمة مرور
- `AppUser.MustChangePasswordOnNextLogin` — جاهز للاستخدام

### الاختبارات

- `StartupSimulationTests` — يستخدم `TestUserFactory` بدلاً من admin123

### مشاكل متبقية

- `tools/UiAudit/Program.cs` و`tools/DbStartupProbe/Program.cs` ما زالا يشيران إلى admin123 (أدوات تطوير — يحتاجان تحديثاً يدوياً)
- لا توجد شاشة تغيير كلمة مرور عند `MustChangePasswordOnNextLogin` (تنبيه فقط حالياً)

---

## P0-04 — Licensing

### التغييرات

- حذف `LicenseService.Secret` والـ fallback الذاتي
- `RsaLicenseValidator` — تحقق RSA-SHA256
- `LicenseStore` — ترخيص موقَّع JSON؛ تجريبي 30 يوماً في Development فقط
- `tools/LicenseIssuer/` — `gen-keys` و `sign`

### الاختبارات

- `LicensingTests` (3 اختبارات) — ✅

### تشغيل أداة الترخيص

```powershell
dotnet run --project tools/LicenseIssuer -- gen-keys
dotnet run --project tools/LicenseIssuer -- sign "اسم المنشأة" <MachineId> %APPDATA%\BRQOSH\license-private.pem
```

---

## P0-05 — Database Mode

### التغييرات

- `src/Brqosh.Application/Setup/AppEnvironment.cs` — `BRQOSH_ENV`
- `src/Brqosh.Desktop/AppServices.cs`:
  - **Production:** PostgreSQL إلزامي — فشل الاتصال يوقف التشغيل
  - **Development/Test:** SQLite مسموح؛ fallback إلى SQLite عند فشل PG

### الاختبارات

- `PostgreSqlStartupTests` — يتخطى إن لم يوجد `database.json`

---

## P0-06 — Inventory Performance

### التغييرات

- `src/Brqosh.Domain/Inventory/InventoryBalance.cs`
- جدول `inventory_balances` + `BrqoshSchemaUpdater`
- `InventoryService` — قراءة/تحديث الرصيد داخل المعاملة
- `RebuildBalancesFromMovementsAsync` عند أول bootstrap
- `DocumentNumberGenerator` — يحسب الأرقام المعلّقة في الذاكرة

### الاختبار

`InventoryPerformanceTests.On_hand_query_scales_with_balance_table(1000)` — ✅ (< 500ms)

### مشاكل متبقية

- اختبار 1,000,000 حركة لم يُشغَّل في CI (بطيء) — يُنصح بتشغيله يدوياً على PostgreSQL
- يُفضَّل إضافة migration EF رسمي لـ `inventory_balances` (حالياً EnsureCreated + SchemaUpdater)

---

## نتائج التحقق

```
dotnet build -c Release   → 0 errors
dotnet test -c Release    → 122 passed (38 Domain + 84 Application)
```

اختبارات جديدة: +7 (من 115 إلى 122)

---

## الممنوعات — التزام

| البند | الحالة |
|-------|--------|
| ميزات جديدة خارج P0 | ✅ لم تُضف |
| حذف اختبارات | ✅ لم يُحذف |
| تعطيل QA | ✅ لا |
| اعتبار النجاح بدون تشغيل | ✅ مُثبت أعلاه |

---

## الخطوات التالية المقترحة (خارج نطاق P0)

1. migration EF لـ `inventory_balances` و`MustChangePasswordOnNextLogin`
2. شاشة تغيير كلمة المرور الإلزامية
3. اختبارات atomicity لفشل ZATCA وDB
4. تحديث UiAudit لاستخدام `BRQOSH_TEST_PASSWORD`
5. نشر `license-public.pem` عبر متغير بيئة في Production
