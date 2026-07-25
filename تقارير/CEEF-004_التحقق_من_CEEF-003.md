# CEEF-004 — التحقق المستقل من ادعاءات CEEF-003

**التاريخ:** 2026-07-08  
**الدور:** مراجع تحقق مستقل (Independent Verification Auditor)  
**المرجع:** ادعاءات `CEEF-003-B_تنفيذ_P0.md` — **لم يُفترض صحتها**  
**المنهج:** قراءة كود + `grep` شامل + تشغيل `dotnet test -c Release`  
**قيود:** لا تعديل · لا اقتراحات إصلاح

---

## تشغيل الاختبارات (2026-07-08)

```
dotnet test -c Release
→ Domain:  38/38 Passed
→ Application: 84/84 Passed
→ المجموع: 122/122 Passed
```

---

## تحقق P0-01 — Transaction Architecture

| السؤال | الحكم | الدليل |
|--------|-------|--------|
| هل `UnitOfWork` موجود؟ | ✅ نعم | `src/Brqosh.Infrastructure/Persistence/UnitOfWork.cs` |
| هل يستخدم `BeginTransaction`؟ | ✅ نعم | سطر 21: `await db.Database.BeginTransactionAsync(ct)` + Commit/Rollback |
| هل الـ25 orchestrator داخل معاملة؟ | ⚠️ جزئي | **25 ملف خدمة** تحتوي `ExecuteInTransactionAsync` (قائمة أدناه) — لكن **ليست كل العمليات المركّبة** |
| هل ZATCA بعد Commit؟ | ✅ نعم | `SalesInvoicePostingService.cs:41-59` · `CreditNotePostingService.cs:39-59` · `ProgressBillingPostingService.cs:15-48` · `PosSaleService.cs:27-71` — كلها بعد `ExecuteInTransactionAsync` |
| هل `SaveChanges` خارج المعاملة؟ | ⚠️ نعم (مقصود/جانبي) | `ZatcaEInvoiceGenerator.GenerateAsync` سطر 202 بعد commit الترحيل؛ `JournalPostingService.PostAsync` سطر 21 **بدون UoW** (قيد يدوي من UI) |
| هل بقيت عمليات غير ذرية؟ | ❌ نعم | انظر الجدول |

### ملفات الـ25 orchestrator (مُثبتة)

`SalesInvoicePostingService` · `SalesInvoiceCancellationService` · `CreditNotePostingService` · `CreditNoteCancellationService` · `PurchaseInvoicePostingService` · `PurchaseInvoiceCancellationService` · `ProgressBillingPostingService` · `ProgressBillingCancellationService` · `RetentionReleaseService` · `SubcontractRetentionWithholdService` · `ReceiptVoucherPostingService` · `PaymentVoucherPostingService` · `VoucherReversalService` · `ChequePostingService` · `PayrollPostingService` · `PayrollReversalService` · `FixedAssetAcquisitionPostingService` · `FixedAssetDisposalPostingService` · `DepreciationService` · `StockMovementPostingService` · `StockMovementCancellationService` · `StockCountAdjustmentService` · `JournalReversalService` · `WorkOrderService.CompleteAsync` · `PosSaleService`

**28 موقع استدعاء** (Cheque=6، VoucherReversal=2).

### عمليات مركّبة **خارج** UoW (مُثبتة)

| العملية | الملف | السطر |
|---------|-------|-------|
| إنشاء فاتورة من أمر شراء + `MarkFulfilled` | `PurchaseOrderService.cs` | 37-39 |
| إصدار أمر إنتاج | `WorkOrderService.ReleaseAsync` | 17-21 |
| إلغاء أمر إنتاج | `WorkOrderService.CancelAsync` | 93-95 |
| قيد يدوي | `JournalPostingService.PostAsync` | 13-21 |
| **تحويل مخزني من Desktop** | `StockMovementDialog.xaml.cs` | 84-89 — `TransferAsync` بلا `SaveChanges` |

### اختبارات P0-01

| الاختبار | يعمل؟ | ملاحظة تحقق |
|----------|-------|-------------|
| `TransactionAtomicityTests` (2) | ✅ | يختبر فاتورة مبيعات فقط؛ لا ZATCA/DB failure hooks |
| لا يغطي PO/Transfer/WorkOrder | — | فجوة تغطية |

---

## تحقق P0-02 — Construction VAT/ZATCA

| السؤال | الحكم | الدليل |
|--------|-------|--------|
| ProgressBilling يحتوي VAT؟ | ✅ نعم | `ProgressBilling.cs:33-38` — `TaxableAmount`, `VatAmount`, `TotalAmount` |
| القيود المحاسبية متوازنة؟ | ✅ نعم | `ProgressBilling.Post` سطر 94-100؛ مُختبر في `ProgressBillingVatTests` سطر 55-56 |
| `VatReturnQuery` يقرأ المستخلصات؟ | ✅ نعم | `VatReturnQuery.cs:41-42, 60-61` |
| `ZatcaDocumentType.ProgressBilling`؟ | ✅ نعم | `ZatcaEnums.cs:25` |
| مولد XML للمستخلصات؟ | ✅ نعم | `ZatcaEInvoiceGenerator.TryGenerateForProgressBillingAsync` سطر 67+ |
| اختبارات تغطي؟ | ✅ نعم | `ProgressBillingVatTests.Million_riyal_...` — VAT + قيد + VatReturn + ZATCA |

---

## تحقق P0-03 — Security Login

| السؤال | الحكم | الدليل |
|--------|-------|--------|
| اختفى admin/admin123 من **كامل المشروع**؟ | ❌ لا | `src/` نظيف من admin123 **ما عدا** `PasswordPolicy.cs:25` (رفض النمط) |
| admin123 في المستودع | ❌ موجود | `tools/UiAudit/Program.cs:95` · `tools/DbStartupProbe/Program.cs:25` |
| كلمات مرور افتراضية في Desktop؟ | ✅ لا | `LoginWindow.xaml` — لا نص افتراضي؛ `LoginWindow.xaml.cs` — لا تعبئة |
| `MustChangePasswordOnNextLogin` موجود؟ | ✅ نعم | `AppUser.cs:21` · عمود DB في `BrqoshSchemaUpdater.cs:33` |
| شاشة تغيير كلمة مرور؟ | ❌ لا | `App.xaml.cs:103-108` — `MessageBox` فقط ثم يفتح `MainWindow` |
| إنشاء المدير | ✅ معالج | `SetupWizardWindow` + `CreateInitialAdminAsync` |
| المدير يُنشأ بـ `mustChangePassword: false` | ⚠️ | `UserAuthService.cs:69-70` |
| `EnsureDefaultAdminAsync` | ⚠️ ميت | `[Obsolete]` · لا مُستدعٍ في `src/` — يرمي استثناء إن استُدعي |

---

## تحقق P0-04 — Licensing

| السؤال | الحكم | الدليل |
|--------|-------|--------|
| أُزيل Secret/HMAC؟ | ✅ من الكود الحالي | لا `HMAC`/`LicenseSecret` في `src/` (يوجد ذكر في `docs/ROADMAP.md` فقط) |
| `GenerateKey` القديم؟ | ✅ معطّل | `LicenseStore.cs:20-21` → `throw` |
| `Validate` القديم؟ | ✅ معطّل | `LicenseStore.cs:24` → `false` |
| التحقق RSA؟ | ✅ نعم | `RsaLicenseValidator.TryValidate` — RSA-SHA256 |
| Backdoor self-validate؟ | ✅ أُزيل | لا fallback يُقارن المفتاح بنفسه |
| تجاوز الترخيص ممكن؟ | ⚠️ نعم | `EmbeddedLicenseKeys.cs:18-22` — مفتاح عام من `%APPDATA%\BRQOSH\license-public.pem` قابل للاستبدال |
| تجاوز Dev | ⚠️ | `LicenseStore.cs:66-68` — 30 يوماً إذا `!IsProduction` |
| `LicenseIssuer` يوقّع صحيحاً؟ | ✅ | `tools/LicenseIssuer/Program.cs:66` يستخدم `RsaLicenseValidator.CreateSignedLicenseJson` |
| اختبارات | ✅ 3/3 | `LicensingTests` مع `RsaTestKeys.UseForValidation()` |

---

## تحقق P0-05 — Database Mode

| السؤال | الحكم | الدليل |
|--------|-------|--------|
| Production يمنع SQLite؟ | ✅ في مسار Desktop | `AppServices.LoadDatabaseConfig` سطر 33-46 — يرمي إن لم يكن `postgresql` |
| فشل PG يوقف التشغيل في Production؟ | ✅ نعم | `EnsureDatabaseReachableAsync` سطر 69-72 |
| Fallback مخفي في Production؟ | ✅ لا | Fallback SQLite سطر 75-79 **فقط** عند `!IsProduction` |
| هل يمكن Production على SQLite؟ | ⚠️ نظرياً عبر Factory | `BrqoshDbContextFactory.Create(Sqlite)` متاح للاختبارات/أدوات — **ليس** عبر `AppServices` في Production |
| تعليق مضلل | ⚠️ | `DbContextFactory.cs:17` — «SQLite تلقائياً» لا ينطبق على Production Desktop |

---

## تحقق P0-06 — Inventory Performance

| السؤال | الحكم | الدليل |
|--------|-------|--------|
| `GetOnHandAsync` يتوقف عن مسح الحركات؟ | ✅ نعم | `InventoryService.cs:28-31` → `FindBalanceAsync` → `InventoryBalances` |
| يستخدم `inventory_balances`؟ | ✅ نعم | `FindBalanceAsync` سطر 258-260 |
| Update داخل نفس المعاملة؟ | ✅ عند الترحيل عبر UoW | `ApplyBalanceDeltaAsync` داخل خدمات الترحيل؛ **استثناء:** `TransferAsync` من Desktop بلا حفظ |
| استعلام O(n) متبقٍ؟ | ❌ نعم | `StockBalanceQuery.cs:35-39` — حلقة مستودعات×منتجات |
| `RebuildBalancesFromMovementsAsync` | ⚠️ | ما زال O(حركات) — للصيانة/bootstrap فقط |
| اختبار 1,000 حركة | ✅ | `InventoryPerformanceTests` — <500ms على SQLite in-memory |

---

## مراجعة إضافية (بنود 1–10)

### 1–3. الاختبارات الجديدة

| الاختبار | يعمل | تحقق سلوك حقيقي؟ |
|----------|------|-------------------|
| `TransactionAtomicityTests` (2) | ✅ | ✅ فشل مخزون يُرجع الفاتورة |
| `ProgressBillingVatTests` | ✅ | ✅ قيد + VAT + ZATCA |
| `LicensingTests` (3) | ✅ | ✅ RSA حقيقي بمفاتيح اختبار |
| `InventoryPerformanceTests` | ✅ | ✅ يقيس زمن `GetOnHandAsync` |
| `PostgreSqlStartupTests` | ⚠️ | **يتخطى صامتاً** إن لا `database.json` (سطر 16-17) — ليس false positive لكنه لا يُثبت شيئاً دائماً |
| `StartupSimulationTests` | ✅ | ✅ مسار bootstrap + auth بكلمة `TestUserFactory` |

**اختبارات لا تكشف ثغرات مُثبتة:** لا تغطية Transfer مخزني · لا تفرد أرقام قيود دفعية · لا إلغاء ZATCA.

### 4. TODO / FIXME / HACK

`grep` على `src/` → **لا نتائج**.

### 5. `catch(Exception)`

~70+ موقعاً — أغلبها Desktop UI (`Views/Pages/*.xaml.cs`) + `App.xaml.cs` + `BrqoshSchemaUpdater`. نمط عام: عرض رسالة للمستخدم دون تسجيل structured.

### 6. ثغرات أمنية ناتجة عن الإصلاحات

| المخاطرة | الدليل |
|----------|--------|
| تجاوز ترخيص باستبدال PEM | `EmbeddedLicenseKeys.cs` |
| تجربة 30 يوم Dev | `LicenseStore.ValidateForCompany` |
| تفويض UI فقط (لم يُصلح في CEEF-003) | `PermissionService` + `MainWindow.xaml.cs:94` |

### 7. Regressions

| الملاحظة | الدليل |
|----------|--------|
| مسار Transfer في Desktop **غير محفوظ** — لم يُغطَّ بـ P0-06 | `StockMovementDialog.xaml.cs:84-89` |
| `UiAudit`/`DbStartupProbe` **منفصلان** عن إصلاح Desktop login | `tools/` |

### 8–9. Dead Code / غير مستخدم

| العنصر | الملف |
|--------|-------|
| `EnsureDefaultAdminAsync` | لا مُستدعٍ — `[Obsolete]` |
| `LicenseService.GenerateKey` / `Validate` | `[Obsolete]` — ترجع throw/false |
| `docs/ROADMAP.md` | يذكر «ترخيص HMAC» — **توثيق قديم** |

### 10. مخالفات CEEF

| المخالفة | التفصيل |
|----------|---------|
| ادعاء إزالة admin123 «نهائياً» | أدوات `tools/` ما زالت تحتويه |
| ادعاء اكتمال الذرية | PO · Transfer · قيد يدوي خارج UoW |
| ادعاء جاهزية MustChangePassword | Flag + MessageBox فقط |
| CEEF-003-B ذكر مشاكل متبقية | **صحيح** — UiAudit · MustChangePassword UI · اختبارات ZATCA atomicity |

---

# النتائج النهائية

## A — الادعاءات الصحيحة (مُثبتة من الكود)

1. **P0-01:** `UnitOfWork` موجود ويستخدم `BeginTransactionAsync` + Commit/Rollback + دعم معاملات متداخلة.
2. **P0-01:** **25 ملف orchestrator** تستخدم `ExecuteInTransactionAsync`.
3. **P0-01:** ZATCA يُولَّد/يُرسَل **بعد** commit معاملة الترحيل (فواتير · إشعارات · مستخلصات · POS).
4. **P0-02:** `ProgressBilling` يحسب VAT (15%) و`GrandTotal` بشكل صريح.
5. **P0-02:** قيد المستخلص متوازن ومُختبر (`ProgressBillingVatTests`).
6. **P0-02:** `VatReturnQuery` يضم المستخلصات المرحّلة.
7. **P0-02:** `ZatcaDocumentType.ProgressBilling` + `TryGenerateForProgressBillingAsync`.
8. **P0-03:** Desktop login **خالٍ** من admin123 والتعبئة التلقائية.
9. **P0-03:** معالج إعداد ينشئ مديراً عبر `CreateInitialAdminAsync` + `PasswordPolicy`.
10. **P0-03:** خاصية `MustChangePasswordOnNextLogin` موجودة في Domain وDB.
11. **P0-04:** لا HMAC secret في الكود؛ تحقق RSA؛ `LicenseIssuer` يوقّع عبر `RsaLicenseValidator`.
12. **P0-04:** `GenerateKey`/`Validate` القديمان معطّلان.
13. **P0-05:** `BRQOSH_ENV=production` يفرض PostgreSQL في `AppServices` ويوقف التشغيل عند فشل الاتصال.
14. **P0-05:** لا fallback SQLite في Production عبر `AppServices`.
15. **P0-06:** `GetOnHandAsync` يقرأ `inventory_balances` لا يمسح `StockMovements`.
16. **P0-06:** `InventoryPerformanceTests(1000)` ناجح.
17. **الاختبارات:** `dotnet test -c Release` → **122/122** ناجحة.

---

## B — الادعاءات غير الصحيحة أو غير مكتملة

1. **«إزالة admin/admin123 نهائياً»** — **غير صحيح** للمستودع بالكامل (`tools/UiAudit`, `tools/DbStartupProbe`).
2. **«MustChangePassword جاهز للاستخدام»** — **غير مكتمل**: لا شاشة إجبار؛ المدير يُنشأ بـ `mustChangePassword: false`؛ الدخول يستمر بعد MessageBox.
3. **«كل عمليات الترحيل المركّبة ذرية»** — **غير صحيح**: `PurchaseOrderService.CreateInvoiceFromOrderAsync` · `WorkOrderService.ReleaseAsync/CancelAsync` · `JournalPostingService.PostAsync` · **تحويل مخزني Desktop**.
4. **«لا استعلام O(n) متبقٍ»** (ضمني في P0-06) — **غير صحيح**: `StockBalanceQuery` O(مستودعات×منتجات).
5. **«اختبارات atomicity كافية»** — **غير مكتمل**: سيناريohanان فقط؛ لا ZATCA-failure-during-post.
6. **توثيق `DbContextFactory`** — يوحي بـ SQLite تلقائي **بينما Production Desktop لا يفعل ذلك**.

---

## C — مشاكل جديدة ظهرت (لم تكن ادعاءات CEEF-003)

| ID | المشكلة | الملف | السطر |
|----|---------|-------|-------|
| **V-001** | التحويل المخزني لا يُحفظ | `StockMovementDialog.xaml.cs` | 84-89 |
| **V-002** | أرقام قيود مكررة — إهلاك متعدد | `DepreciationService.cs` | 38-41 |
| **V-003** | أرقام قيود مكررة — إكمال أمر إنتاج | `WorkOrderService.cs` | 72-75 |
| **V-004** | إلغاء فاتورة لا يولّد ZATCA (يشترط Posted بعد Cancel) | `SalesInvoiceCancellationService.cs` + `ZatcaEInvoiceGenerator.cs` | 66 · 137 |
| **V-005** | `JournalEntryNumberGenerator` لا يرى القيود المعلّقة | `JournalEntryNumberGenerator.cs` | 14-24 |

---

## D — مشاكل قديمة ما زالت موجودة

1. تفويض على مستوى UI فقط (`PermissionService`).
2. ZATCA خارج معاملة الترحيل — فاتورة مرحّلة بلا مستند عند فشل ZATCA.
3. تجاوز ترخيص عبر استبدال `license-public.pem`.
4. فترة تجريب 30 يوم في Development.
5. `PurchaseOrderService` — `MarkFulfilled` غير ذري مع ترحيل الفاتورة.
6. غياب FK صريحة في migrations للكيانات الرئيسية.
7. مسار مخطط ثلاثي: EnsureCreated + Migrate + SchemaUpdater.
8. `admin123` في أدوات التطوير.
9. `PostgreSqlStartupTests` يتخطى بصمت عند غياب PG.
10. لا تزامن تفاؤلي على `InventoryBalance`.

---

## E — قائمة P0 / P1 / P2 الجديدة (من هذا التحقق)

### P0

| ID | الوصف |
|----|--------|
| V-001 | تحويل مخزني Desktop بلا `SaveChanges` — فقدان بيانات |
| V-002 | تكرار أرقام قيود الإهلاك الشهري |
| V-003 | تكرار أرقام قيود إكمال أمر الإنتاج |
| V-004 | كسر ZATCA عند إلغاء فاتورة مبيعات |

### P1

| ID | الوصف |
|----|--------|
| V-005 | `JournalEntryNumberGenerator` بلا حساب Local (سبب جذري V-002/V-003) |
| V-006 | `PurchaseOrderService` غير ذري بالكامل |
| V-007 | تجاوز ترخيص RSA بملف PEM مستخدم |
| V-008 | `StockBalanceQuery` N+1 |
| V-009 | `MustChangePassword` غير مُطبَّق (MessageBox فقط) |
| V-010 | اختبارات atomicity/ZATCA/Transfer ناقصة رغم 122/122 |

### P2

| ID | الوصف |
|----|--------|
| V-011 | `admin123` في `tools/UiAudit` و`DbStartupProbe` |
| V-012 | `PostgreSqlStartupTests` skip صامت |
| V-013 | Dead code: `EnsureDefaultAdminAsync` · `LicenseService.Validate` |
| V-014 | `docs/ROADMAP.md` يذكر HMAC بينما الكود RSA |
| V-015 | `WorkOrderService.ReleaseAsync/CancelAsync` خارج UoW |

---

## F — بوابة ما قبل الإنتاج (Pre-Production Gate)

# ❌ لا — المشروع **غير جاهز** للانتقال إلى Pre-Production Gate

**أسباب مُثبتة:**

1. **أربع ثغرات P0 جديدة** (V-001 → V-004) في مسارات تشغيل حقيقية (مخزون · قيود · ZATCA).
2. ادعاءات CEEF-003 **غير مكتملة** في الأمان (admin123 في tools · MustChangePassword) والذرية (PO · Transfer · قيد يدوي).
3. **122/122** اختباراً **لا تغطي** الثغرات أعلاه — نجاح CI لا يُغلق البوابة.
4. إصلاحات CEEF-003 **موجودة فعلاً** في جوهرها (UoW · VAT مستخلص · RSA · PG في Production · balances) لكن **لا تكفي** وحدهما.

---

*نهاية CEEF-004 — تحقق مستقل من CEEF-003 — بدون تعديل على المشروع.*
