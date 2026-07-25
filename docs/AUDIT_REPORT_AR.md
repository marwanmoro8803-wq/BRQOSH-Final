# تقرير التدقيق الشامل — نظام BRQOSH ERP

**التاريخ:** 8 يوليو 2026  
**النطاق:** `C:\Users\qc\Projects\BRQOSH` — Desktop ERP محاسبي سعودي  
**المنهجية:** مراجعة evidence-based على الكود المصدري، الاختبارات، التوثيق، وCI — دون افتراض صحة التنفيذ  
**المراجع:** IFRS / SOCPA (مبادئ عامة)، ZATCA Phase 2، OWASP ASVS (مستوى تطبيق سطح مكتب)، CEEF v1.0

---

## جدول المحتويات

1. [الملخص التنفيذي](#1-الملخص-التنفيذي)
2. [تقييم الهندسة المعمارية العامة](#2-تقييم-الهندسة-المعمارية-العامة)
3. [مراجعة المحاسبة](#3-مراجعة-المحاسبة)
4. [مراجعة الامتثال](#4-مراجعة-الامتثال)
5. [مراجعة الأمان](#5-مراجعة-الأمان)
6. [مراجعة الأداء](#6-مراجعة-الأداء)
7. [مراجعة واجهة المستخدم](#7-مراجعة-واجهة-المستخدم)
8. [مراجعة قاعدة البيانات](#8-مراجعة-قاعدة-البيانات)
9. [مراجعة واجهات البرمجة](#9-مراجعة-واجهات-البرمجة)
10. [تقييم قابلية التوسع](#10-تقييم-قابلية-التوسع)
11. [تقييم المخاطر](#11-تقييم-المخاطر)
12. [تقرير الديون التقنية](#12-تقرير-الديون-التقنية)
13. [الأخطاء الحرجة](#13-الأخطاء-الحرجة)
14. [الأخطاء ذات الأولوية العالية](#14-الأخطاء-ذات-الأولوية-العالية)
15. [الأخطاء ذات الأولوية المتوسطة](#15-الأخطاء-ذات-الأولوية-المتوسطة)
16. [الأخطاء ذات الأولوية المنخفضة](#16-الأخطاء-ذات-الأولوية-المنخفضة)
17. [الميزات الناقصة](#17-الميزات-الناقصة)
18. [منطق الأعمال الخاطئ](#18-منطق-الأعمال-الخاطئ)
19. [المنطق المحاسبي الخاطئ](#19-المنطق-المحاسبي-الخاطئ)
20. [المتطلبات السعودية الناقصة](#20-المتطلبات-السعودية-الناقصة)
21. [نقاط ضعف واجهة المستخدم](#21-نقاط-ضعف-واجهة-المستخدم)
22. [اختناقات الأداء](#22-اختناقات-الأداء)
23. [الثغرات الأمنية](#23-الثغرات-الأمنية)
24. [رائحة الكود](#24-رائحة-الكود)
25. [فرص إعادة الهيكلة](#25-فرص-إعادة-الهيكلة)
26. [الهندسة المعمارية الموصى بها](#26-الهندسة-المعمارية-الموصى-بها)
27. [خطة إعادة البناء خطوة بخطوة](#27-خطة-إعادة-البناء-خطوة-بخطوة)
28. [خارطة طريق التطوير](#28-خارطة-طريق-التطوير)
29. [استراتيجية الاختبار](#29-استراتيجية-الاختبار)
30. [خطة اختبار الانحدار](#30-خطة-اختبار-الانحدار)
31. [خطة النشر](#31-خطة-النشر)
32. [استراتيجية الصيانة](#32-استراتيجية-الصيانة)
33. [خطة التوسع المستقبلية](#33-خطة-التوسع-المستقبلية)

---

## 1. الملخص التنفيذي

### الحكم العام

BRQOSH **ليس جاهزاً للإنتاج المؤسسي** رغم اتساع الوظائف الظاهري (~50 شاشة WPF، 133 اختباراً ناجحاً، تغطية وحدات متعددة). النظام يمثل **نموذجاً أولياً متقدماً (Advanced Prototype)** بقيمة تجريبية عالية، لكنه يحمل ثغرات حوكمة وأمان وامتثال تمنع اعتماده لبيئة ERP مؤسسية دون إعادة هيكلة جوهرية.

### نقاط القوة المثبتة (بأدلة)

| المجال | الدليل | التقييم |
|--------|--------|---------|
| القيد المزدوج | `JournalEntry.cs` — توازن إلزامي، حالات Draft/Posted/Reversed | ✅ جيد |
| عكس القيود | `JournalReversalService.cs` — يمنع عكس قيود المستندات يدوياً | ✅ جيد |
| اختبارات وحدة | 133 اختبار (38 Domain + 95 Application) — **0 فشل** (تشغيل 8 يوليو 2026) | ✅ جيد نسبياً |
| ZATCA أساسي | `ZatcaEInvoiceGenerator.cs`, `ZatcaXmlSigner.cs`, golden tests | ⚠️ جزئي |
| دليل حسابات سعودي | `SaudiChartOfAccounts.cs` — 100+ حساب مُهيكل | ✅ جيد |
| RBAC واجهة | `PermissionService.cs` — 4 أدوار، 40+ شاشة | ⚠️ سطحي |

### نقاط الضعف الحرجة (بأدلة)

| # | الخلل | الخطورة | الملف |
|---|-------|---------|-------|
| 1 | تجاوز الصلاحيات عند `SecurityContext.CurrentRole == null` | 🔴 حرج | `SecurityContext.cs:37-47` |
| 2 | إغلاق/إعادة فتح الفترات بدون `EnsureAdministrator` | 🔴 حرج | `FiscalPeriodService.cs`, `FiscalPeriodsPage.xaml.cs` |
| 3 | إنشاء فترات مالية تلقائياً دون حوكمة | 🟠 عالي | `JournalPostingService.cs:34-39` |
| 4 | ZATCA خارج معاملة الترحيل المحاسبي | 🟠 عالي | `SalesInvoicePostingService.cs:57-66` |
| 5 | لا يوجد API — تكامل خارجي مستحيل | 🟠 عالي | بحث `Controller\|WebApplication` — 0 نتائج |
| 6 | إقرار VAT ناقص (لا تسويات، لا استيراد بنكي) | 🟠 عالي | `VatReturnQuery.cs` |
| 7 | شركة واحدة لكل تثبيت (`AppServices.CompanyId`) | 🟡 متوسط | `AppServices.cs:20` |
| 8 | CI بدون فحص أمني/تغطية/UI | 🟡 متوسط | `.github/workflows/ci.yml` |

### التوصية التنفيذية

**لا تُطلق نسخة Production** قبل إغلاق الثغرات الحرجة (القسم 13)، فرض RBAC على طبقة الخدمات بشكل صارم، وإكمال اختبار ZATCA الحي (`docs/ZATCA_LIVE_CSID.md`). المدة التقديرية للجاهزية المؤسسية: **6–9 أشهر** بفريق 3–5 مطورين.

---

## 2. تقييم الهندسة المعمارية العامة

### البنية الحالية

```
Brqosh.sln
├── Brqosh.Domain          — كيانات وقواعد أعمال نقية
├── Brqosh.Application     — إعداد وتهيئة (طبقة رفيعة)
├── Brqosh.Infrastructure  — EF Core، خدمات ترحيل، ZATCA، تقارير
├── Brqosh.Desktop         — WPF — كل الواجهات (~50 صفحة)
├── Brqosh.Modules.*       — Construction, Manufacturing, Retail (مشاريع منفصلة، استخدام محدود)
└── tests/                 — Domain.Tests + Application.Tests
```

### التقييم

| المعيار | الحالة | الملاحظة |
|---------|--------|----------|
| فصل الطبقات | ⚠️ جزئي | Domain نظيف؛ Infrastructure يحتوي كل شيء تقريباً |
| Dependency Injection | ❌ ضعيف | `new Service(db)` مباشرة من الصفحات (`FiscalPeriodsPage.xaml.cs:64`) |
| CQRS / Mediator | ❌ غير موجود | خدمات إجرائية مباشرة |
| واجهة برمجة | ❌ غير موجودة | تطبيق سطح مكتب monolith |
| تعدد الشركات | ❌ | `CompanyId` ثابت بعد الإعداد |
| الوحدات (Modules) | ⚠️ | مشاريع `Brqosh.Modules.*` موجودة لكن المنطق في Infrastructure |

### نقاط إيجابية

- `JournalEntry` في Domain يفرض invariant القيد المزدوج (`JournalEntry.cs:93-95`).
- `UnitOfWork.ExecuteInTransactionAsync` يُستخدم في مسارات حرجة.
- دعم SQLite (تطوير) وPostgreSQL (إنتاج) عبر `BrqoshDbContextFactory`.

### نقاط سلبية

- **God Infrastructure**: ملف `BrqoshDbContext.cs` يتجاوز 570 سطراً مع كل الكيانات.
- **تكرار مسار الترحيل**: `SalesInvoicePostingService` يستدعي `entry.Post()` مباشرة بدلاً من `JournalPostingService.PostAsync()` — ازدواجية ومسار أمني مختلف.
- **Static AppServices**: حالة عامة (`CurrentUser`, `CompanyId`) — صعب الاختبار والتوسع.

**التقييم الإجمالي: 5.5/10** — هيكل مقبول للـ MVP، غير كافٍ للمؤسسة.

---

## 3. مراجعة المحاسبة

### 3.1 القيود اليومية

**الملف المرجعي:** `src/Brqosh.Domain/Accounting/JournalEntry.cs`

| القاعدة | التنفيذ | الحكم |
|---------|---------|-------|
| توازن مدين/دائن | `IsBalanced` قبل `Post()` | ✅ |
| حد أدنى سطرين | `_lines.Count < 2` | ✅ |
| منع التعديل بعد الترحيل | `EnsureDraft()` | ✅ |
| العكس فقط للتصحيح | `CreateReversal()` | ✅ |
| منع الحسابات التجميعية | `account.IsPostable` | ✅ |

**بوابة الترحيل:** `JournalPostingService.cs` — يتحقق من الفترة المفتوحة ويستدعي `SecurityContext.EnsureCanPostAccounting()`.

**خلل:** مسارات أخرى (فواتير، رواتب، مخزون) تتجاوز `JournalPostingService.PostAsync()` وتنفذ `entry.Post()` + `db.SaveChangesAsync()` مباشرة — نفس النتيجة لكن **بدون بوابة موحدة**.

### 3.2 ميزان المراجعة

**الملف:** `TrialBalanceQuery.cs`

- يعتمد على `Status == Posted` فقط — ✅ صحيح.
- تجميع على مستوى DB عبر `GroupBy` — ✅ أداء مقبول.
- **لا يُظهر أرصدة افتتاحية منفصلة** عن حركة الفترة — مقبول إذا كان `fromDate` = بداية السنة؛ لكن واجهة المستخدم قد تسمح بفترات جزئية دون توضيح.

### 3.3 قائمة المركز المالي

**الملف:** `BalanceSheetQuery.cs`

- يستمد من `TrialBalanceQuery` من `1900-01-01` — ✅ أرصدة تراكمية.
- يضيف حساباً اصطناعياً `3999` «أرباح العام الجاري (غير مقفلة)» من `IncomeStatementQuery` — **مقبول عملياً** لكنه يخرج عن دليل الحسابات الرسمي وقد يُربك المراجعين الخارجيين.

### 3.4 قائمة الدخل

**الملف:** `IncomeStatementQuery.cs`

- حركة فترة من حسابات الإيراد/المصروف — ✅ أساسي صحيح.
- **لا يوجد إقفال سنوي** لنقل صافي الربح إلى `3103` الأرباح المبقاة.

### 3.5 الفترات المالية

**الملف:** `FiscalPeriod.cs`, `JournalPostingService.cs`

| السلوك | المشكلة |
|--------|---------|
| إنشاء فترة تلقائياً عند أول ترحيل | يتجاوز قرار الإدارة المالية — مخالف لـ SOCPA/IFRS practice |
| إغلاق الفترة | لا يتحقق من ترحيل كل المستندات المعلقة |
| إعادة الفتح | بدون صلاحية مدير فقط على مستوى الخدمة |

### 3.6 الذمم والأرصدة الفرعية

اختبارات `SubLedgerAccuracyTests.cs` (7 حقائق) تغطي أرصدة العملاء/الموردين — دليل على اهتمام سابق بـ QA. `docs/ROADMAP.md` يوثق إصلاحات POS والشيكات والمحتجزات.

### 3.7 التقييم المحاسبي

**7/10 للوظائف الأساسية** — القيد المزدوج والعكس قويان. **ناقص:** إقفال سنوي، تسويات، عملات متعددة، قيود افتتاحية رسمية، فصل صلاحيات الإقفال.

---

## 4. مراجعة الامتثال

### 4.1 ZATCA (الفوترة الإلكترونية)

| المتطلب | الحالة | الدليل |
|---------|--------|--------|
| UBL/XML | ✅ | `ZatcaUblInvoiceBuilder.cs` |
| توقيع XAdES | ✅ | `ZatcaXmlSigner.cs` |
| QR | ✅ | `ZatcaQrGenerator.cs` |
| Reporting/Clearance | ✅ | `ZatcaEInvoiceGenerator.cs:32-34` |
| CSID Onboarding | ⚠️ | `ZatcaCsidOnboardingService.cs` — اختبار حي اختياري |
| إرسال Production | ⚠️ | `ZatcaSubmissionService.cs` — شهادات على filesystem |
| فواتير المستخلصات | ✅ | `TryGenerateForProgressBillingAsync` |
| إشعار دائن / إلغاء | ✅ | `TryGenerateForCreditNoteAsync`, إلغاء CN |

**ثغرات امتثال ZATCA:**

1. **بيئة المحاكاة** تقبل المستند دون تحقق (`ZatcaSubmissionService.cs:65-74`) — خطر الخلط بين بيئات.
2. **مفاتيح خاصة على قرص محلي** — مخالف لممارسات HSM/Key Vault.
3. **لا اختبار CI إلزامي** لـ golden XML (`ZatcaGoldenTests` موجود لكن بدون مقارنة مع sandbox رسمي في CI).
4. **فاتورة مبسطة vs ضريبية** تعتمد على `customer.SupportsStandardTaxInvoice` — يحتاج تحقق من قواعد ZATCA للـ B2C/B2B.

### 4.2 ضريبة القيمة المضافة

**الملف:** `VatReturnQuery.cs`

يشمل: مبيعات، مستخلصات، إشعارات دائن، مشتريات (مع `VatDeductible`).

**غير مشمول:**

- تسويات يدوية لحساب `1111`/`2106`
- استيرادات / reverse charge
- فواتير POS منفصلة (مُدمجة في `SalesInvoices` إذا رُحّلت — يحتاج تحقق ميداني)
- ملف XML لرفع هيئة الزكاة

### 4.3 GOSI / الزكاة

- حسابات موجودة في الدليل (`2109`, `2111`, `5203`, `5216`) — `SaudiChartOfAccounts.cs`
- `PayrollPostingService.cs` يُرحّل GOSI — ✅ جزئي
- **لا وحدة زكاة** ولا إقرار زكاة/ضريبة دخل

### 4.4 SOCPA / IFRS

- لا دعم لمعايير الإفصاح الموسّع
- لا إعادة تقييم / عملات أجنبية
- مجمع الإهلاك مُصنَّف `Asset` (`1207`) بدلاً من contra-asset — **عرض تقريري غير قياسي**

**تقييم الامتثال: 4.5/10** — أساس ZATCA موجود؛ الفجوات التنظيمية كبيرة.

---

## 5. مراجعة الأمان

### 5.1 المصادقة

**الملف:** `UserAuthService.cs`

| الميزة | التقييم |
|--------|---------|
| PBKDF2-SHA256, 100k iterations | ✅ OWASP مقبول |
| ترقية من SHA-256 legacy | ✅ |
| `FixedTimeEquals` | ✅ |
| سياسة كلمة مرور | ⚠️ 8 أحرف + حرف ورقم فقط (`PasswordPolicy.cs`) |
| قفل حساب / MFA | ❌ غير موجود |
| جلسات / timeout | ❌ — تسجيل دخول واحد حتى إغلاق التطبيق |

### 5.2 التفويض (RBAC)

**طبقتان منفصلتان:**

1. **UI:** `PermissionService.cs` — يمنع فتح الشاشات
2. **خدمات:** `SecurityContext.cs` — يمنع الترحيل/الإلغاء

**الثغرة الجوهرية:**

```csharp
// SecurityContext.cs — كل دوال Ensure* تبدأ بـ:
if (CurrentRole is null) return;  // ← يمرّر أي عملية بدون دور!
```

هذا يعني: أي استدعاء خدمة خارج سياق المستخدم (أدوات، اختبارات ناقصة، استدعاء مستقبلي من API) **بدون قيود**.

**ثغرات إضافية:**

| الثغرة | الملف | الخطورة |
|--------|-------|---------|
| إغلاق فترة بدون `EnsureAdministrator` | `FiscalPeriodService.cs` | 🔴 |
| `username` من الواجهة قابل للتزوير نظرياً | `FiscalPeriodsPage.xaml.cs:63` | 🟠 |
| لا تدقيق على تسجيل الدخول الفاشل | `UserAuthService.cs` | 🟡 |
| ترخيص: MachineId قابل للتوقع | `LicenseStore.cs:14` | 🟡 |

### 5.3 التدقيق (Audit)

`AuditService` يُستخدم في: عكس قيود، إغلاق فترات، إلغاء فواتير، رواتب، أصول — **ليس شاملاً** لكل CRUD.

### 5.4 التشفير والأسرار

- اتصال DB في `%APPDATA%\BRQOSH\database.json` — **نص صريح**
- شهادات ZATCA: مسارات ملفات في DB
- لا encryption at rest لـ SQLite الافتراضي

**تقييم الأمان: 3.5/10** — أساسيات hashing جيدة؛ RBAC وسرية البيانات ضعيفان.

---

## 6. مراجعة الأداء

### نقاط إيجابية

- `TrialBalanceQuery` — تجميع DB لا تحميل كامل (`TrialBalanceQuery.cs:30-42`)
- `InventoryBalance` table + اختبار 1000 حركة < 500ms (`InventoryPerformanceTests.cs`)
- فهارس على `(CompanyId, EntryDate)` و`(CompanyId, EntryNumber)` (`BrqoshDbContext.cs:89-90`)
- دليل partitioning يدوي: `docs/postgres-partitioning.sql`

### اختناقات

| الاختناق | الدليل |
|----------|--------|
| `VatReturnQuery` يحمّل كل الفواتير `ToListAsync()` | `VatReturnQuery.cs:36-54` |
| `FiscalPeriodsPage` — N+1 queries لعدد القيود | `FiscalPeriodsPage.xaml.cs:32-35` |
| `HttpClient` static واحد لـ ZATCA | `ZatcaSubmissionService.cs:50` |
| لا caching للتقارير | كل استعلام يضرب DB |
| 95 اختبار Application في ~94 ثانية | بطء نسبي في CI |

**تقييم الأداء: 6/10** — مقبول لشركة صغيرة؛ غير مُختبر تحت حمل 5M+ سجل رغم وثائق التقسيم.

---

## 7. مراجعة واجهة المستخدم

### النطاق

~50 صفحة WPF في `src/Brqosh.Desktop/Views/Pages/` — تغطية واسعة: محاسبة، مبيعات، مشتريات، مخزون، مقاولات، POS، ZATCA، تقارير.

### إيجابيات

- ثيم موحّد: `Themes/BrqoshTheme.xaml`
- `UiFeedback` / `UiDialogs` لمعالجة أخطاء موحّدة (`docs/ROADMAP.md`)
- `NavigationCatalog` حسب `BusinessProfile`
- دعم أتمتة FlaUI: `UiTestAutomation.cs`, `tools/UiAudit`

### سلبيات

| المشكلة | الدليل |
|---------|--------|
| لا i18n كامل (عربي أساسي، إنجليزي جزئي في الحسابات) | `Account.NameEn` |
| كل صفحة تنشئ `db` مباشرة — لا ViewModel | `FiscalPeriodsPage.xaml.cs:23` |
| `MessageBox` للأخطاء الحرجة | `App.xaml.cs:20-23` |
| لا offline/sync |
| إمكانية وصول (a11y) غير مُختبرة |
| UiAudit **خارج CI** | `docs/ROADMAP.md:86-94` |

**تقييم UX: 6.5/10** — وظيفي وواسع؛ ليس enterprise-polished.

---

## 8. مراجعة قاعدة البيانات

### المزودون

- **SQLite** — افتراضي للتطوير (`AppServices.cs:36-37`)
- **PostgreSQL** — إلزامي في Production (`AppEnvironmentConfig.IsProduction`)

### المخطط

- EF Core Migrations: `20260707000538_InitialCreate`, `AddZatcaCsidFields`
- `BrqoshSchemaUpdater` للترقية خارج migrations في بعض المسارات
- **37+ DbSet** في `BrqoshDbContext`

### قيود وسلامة

| القيد | الحالة |
|-------|--------|
| Unique `(CompanyId, Account.Code)` | ✅ |
| Unique `(CompanyId, EntryNumber)` | ✅ |
| `DeleteBehavior.Restrict` على حسابات القيود | ✅ |
| Soft delete query filters | ✅ على عملاء/موردين/حسابات |
| Row-level security | ❌ |
| Check constraints DB-level للتوازن | ❌ — في Domain فقط |
| Foreign keys شاملة | ⚠️ جزئي — بعض العلاقات ضمنية |

### مخاطر

1. **Owned entities** لسطور القيود — صعب partitioning جزئي للسطور.
2. **لا نسخ احتياطي مدمج** — يعتمد على أدوات DB.
3. **Single-tenant schema** — `CompanyId` عمود لكن تطبيق أحادي الشركة.

**تقييم DB: 6.5/10**

---

## 9. مراجعة واجهات البرمجة

### النتيجة

**لا توجد واجهات برمجة (API) في المشروع.**

بحث `Controller|MinimalApi|WebApplication|MapGet` — **صفر ملفات**.

### التأثير

- لا تكامل مع أنظمة خارجية (CRM، بوابات دفع، تطبيقات جوال)
- لا فصل frontend/backend للتوسع
- كل العمليات عبر WPF مباشرة إلى Infrastructure

### التوصية

إضافة `Brqosh.Api` (ASP.NET Core Minimal API أو gRPC) مع JWT + نفس `SecurityContext` — أولوية عالية للمؤسسة.

**تقييم API: 0/10** — غير موجود.

---

## 10. تقييم قابلية التوسع

| البعد | الحالة |
|-------|--------|
| مستخدمون متزامنون | Desktop — مستخدم/جهاز؛ لا تزامن حقيقي |
| شركات متعددة | Schema يدعم؛ التطبيق لا |
| فروع | لا كيان فرع منفصل |
| حجم بيانات | Partitioning يدوي فقط |
| نشر أفقي | غير قابل — monolith desktop |
| توسع وظيفي | Modules projects موجودة لكن غير مستغلة بالكامل |

**تقييم: 3/10** للتوسع المؤسسي.

---

## 11. تقييم المخاطر

| المخاطرة | الاحتمال | الأثر | التخفيف المطلوب |
|----------|----------|-------|-----------------|
| ترحيل بلا صلاحية (null role) | متوسط | حرج | Fail-closed في SecurityContext |
| فاتورة ZATCA بلا قيد (فشل جزئي) | متوسط | حرج | Transaction موحّد |
| إغلاق فترة خاطئ من محاسب | عالي | عالي | صلاحية مدير + workflow |
| فقدان مفتاح ZATCA | متوسط | حرج | HSM / Key Vault |
| SQLite في إنتاج بالخطأ | منخفض | حرج | فحص CI + installer |
| عدم توافق ZATCA مع التحديثات | متوسط | حرج | مراقبة إصدارات الهيئة |
| فقدان بيانات بدون backup | متوسط | حرج | سياسة نسخ احتياطي |

**مستوى المخاطر الإجمالي: مرتفع (High)**

---

## 12. تقرير الديون التقنية

| الدين | الموقع | الجهد التقديري |
|-------|--------|----------------|
| ازدواجية مسار ترحيل القيود | Infrastructure/Accounting vs Sales/Purchasing | 2 أسابيع |
| Static AppServices | `AppServices.cs` | 3 أسابيع |
| عدم DI | كل الصفحات | 4 أسابيع |
| SecurityContext fail-open | `SecurityContext.cs` | 2 أيام |
| God DbContext | `BrqoshDbContext.cs` | 3 أسابيع |
| ROADMAP يبالغ في الجاهزية | `docs/ROADMAP.md` | توثيق |
| اختبارات UI خارج CI | `tools/UiAudit` | 1 أسبوع |
| تنسيق ملفات متقطع (أسطر فارغة) | `SalesInvoicePostingService.cs` | 1 يوم |

**إجمالي الديون: ~3–4 أشهر شخص-عمل** قبل إعادة هيكلة كاملة.

---

## 13. الأخطاء الحرجة

| ID | الوصف | الدليل | الخطورة |
|----|--------|--------|---------|
| C-01 | **SecurityContext fail-open** — `if (CurrentRole is null) return` يتجاوز كل فحوص الصلاحيات | `SecurityContext.cs:37,45,53,60` | 🔴 |
| C-02 | **إغلاق/إعادة فتح فترة مالية** بدون تحقق صلاحية على مستوى الخدمة | `FiscalPeriodService.cs:11-31` | 🔴 |
| C-03 | **ZATCA generation/submission خارج transaction** الترحيل — احتمال عدم اتساق محاسبي/ضريبي | `SalesInvoicePostingService.cs:45-66` | 🔴 |
| C-04 | **لا API ولا طبقة عزل** — أي أداة تستدعي Infrastructure مباشرة تتجاوز UI RBAC | بنية المشروع | 🔴 |
| C-05 | **مفاتيح ZATCA وكلمات مرور DB** مخزنة نصياً على القرص | `database.json`, `ZatcaSettings` | 🔴 |

---

## 14. الأخطاء ذات الأولوية العالية

| ID | الوصف | الدليل |
|----|--------|--------|
| H-01 | إنشاء فترة مالية تلقائياً عند الترحيل دون موافقة | `JournalPostingService.cs:34-39` |
| H-02 | إقرار VAT ناقص — لا تسويات ولا reverse charge | `VatReturnQuery.cs` |
| H-03 | لا إقفال سنوي — أرباح جارية في حساب اصطناعي 3999 | `BalanceSheetQuery.cs:50-60` |
| H-04 | لا قفل حساب بعد محاولات فاشلة | `UserAuthService.cs` |
| H-05 | `FiscalPeriodsPage` — N+1 استعلامات | `FiscalPeriodsPage.xaml.cs:30-35` |
| H-06 | اختبار ZATCA الحي اختياري ويُتخطى في CI | `ZatcaLiveCsidTests`, `docs/ZATCA_LIVE_CSID.md` |
| H-07 | ازدواجية `entry.Post()` خارج `JournalPostingService` | `SalesInvoicePostingService.cs:175-177` |
| H-08 | لا تحقق من اكتمال الترحيل قبل إغلاق الفترة | `FiscalPeriod.Close()` |

---

## 15. الأخطاء ذات الأولوية المتوسطة

| ID | الوصف | الدليل |
|----|--------|--------|
| M-01 | مجمع الإهلاك كـ Asset لا contra-asset | `SaudiChartOfAccounts.cs:48` |
| M-02 | مخصص الديون المشكوك فيها كـ Asset | `SaudiChartOfAccounts.cs:31` |
| M-03 | سياسة كلمة مرور ضعيفة (8 أحرف) | `PasswordPolicy.cs:8` |
| M-04 | `HttpClient` static بدون `IHttpClientFactory` | `ZatcaSubmissionService.cs:50` |
| M-05 | `docs/ROADMAP.md` يذكر 115 اختبار — الفعلي 133 (توثيق قديم) | تشغيل dotnet test |
| M-06 | لا MFA / لا session timeout | `App.xaml.cs` |
| M-07 | معالج أخطاء عام يبتلع stack trace | `App.xaml.cs:20-23` |
| M-08 | Payroll يستخدم أكواد حسابات hardcoded `"5203"` | `PayrollPostingService.cs:21` |

---

## 16. الأخطاء ذات الأولوية المنخفضة

| ID | الوصف |
|----|--------|
| L-01 | تنسيق كود غير متسق (أسطر فارغة مفرطة) في بعض الملفات |
| L-02 | `UserRole` 4 فقط — لا تخصيص صلاحيات granular |
| L-03 | لا تصدير Excel للتقارير (QuestPDF فقط) |
| L-04 | أيقونات emoji في حالة الفترة المالية `🔒` |
| L-05 | `EnsureDefaultAdminAsync` throws دائماً — dead code path |
| L-06 | Modules projects شبه فارغة مقارنة بـ Infrastructure |

---

## 17. الميزات الناقصة

| الميزة | الأهمية للمؤسسة |
|--------|------------------|
| API REST/GraphQL | حرجة |
| تعدد الشركات/الفروع | حرجة |
| إقفال سنوي ونقل أرباح | حرجة |
| إقرار زكاة | عالية (سعودي) |
| عملات متعددة وأسعار صرف | عالية |
| workflow موافقات (فواتير، سندات) | عالية |
| تكامل بنكي (Open Banking) | متوسطة |
| نسخ احتياطي مجدول | عالية |
| تطبيق جوال / ويب | متوسطة |
| BI / لوحة تحكم متقدمة | متوسطة |
| إدارة عقود إيجار (IFRS 16) | منخفضة |
| دعم فواتير مشتريات ZATCA (وارد) | عالية |

---

## 18. منطق الأعمال الخاطئ

| # | الوصف | الدليل |
|---|--------|--------|
| 1 | POS Mixed يُقيَّد على الصندوق لا البنك | `SalesInvoicePostingService.cs:231-235` — قد لا يعكس التوزيع الفعلي نقد/بطاقة |
| 2 | إنشاء فترة مالية ضمنياً = «مفتوحة دائماً» حتى يُغلقها أحد | `JournalPostingService.cs` |
| 3 | ترخيص تجريبي 30 يوم في غير الإنتاج قد يُنسى تفعيله | `LicenseStore.cs:69-71` |
| 4 | `BRQOSH_UITEST=1` يتجاوز الترخيص بالكامل | `LicenseStore.cs:56-57` |

---

## 19. المنطق المحاسبي الخاطئ

| # | الوصف | الدليل | الخطورة |
|---|--------|--------|---------|
| 1 | حساب 3999 اصطناعي خارج الدليل | `BalanceSheetQuery.cs:55-59` | 🟡 |
| 2 | مجمع الإهلاك يزيد في جانب الأصول | `SaudiChartOfAccounts.cs:48` — عرض الميزانية | 🟡 |
| 3 | لا قيد افتتاحي رسمي عند بداية السنة | غير موجود | 🟠 |
| 4 | VAT Net Payable (`2107`) قد لا يُحدَّث آلياً من الإقرار | لا خدمة ترحيل تسوية VAT | 🟠 |
| 5 | فواتير مشتريات: خصم VAT يعتمد على flag يدوي `VatDeductible` فقط | `VatReturnQuery.cs:65` | 🟡 |

---

## 20. المتطلبات السعودية الناقصة

| المتطلب | الحالة |
|---------|--------|
| ZATCA Phase 2 — إرسال فعلي معتمد | ⚠️ غير مُختبر إلزامياً في CI |
| رقم ضريبي 15 رقماً — تحقق | جزئي في Domain |
| هوية وطنية/سجل تجاري للعملاء | غير واضح في `Customer` |
| الفوترة الإلكترونية للمشتريات (وارد) | ❌ |
| إقرار VAT بصيغة الهيئة | ❌ — تقرير داخلي فقط |
| الزكاة والضريبة | حسابات فقط — لا إقرار |
| التأمينات (GOSI) | رواتب أساسية — لا ملفات GOSI |
| شيكات سعودية / تسوية | ✅ موجود `ChequesPage` |
| دليل محاسبي سعودي | ✅ `SaudiChartOfAccounts` |
| مراكز تكلفة للمقاولات | ✅ |
| محتجزات مقاولات | ✅ مع اختبارات |

---

## 21. نقاط ضعف واجهة المستخدم

1. **لا MVVM** — منطق في code-behind يصعب الصيانة.
2. **صلاحيات UI فقط** — مستخدم تقني يمكنه استدعاء الخدمات (مع C-01 أخطر).
3. **لا بحث عام** عبر المستندات.
4. **جداول بدون pagination** في صفحات كثيرة — بطء محتمل.
5. **لا dark mode**.
6. **رسائل خطأ تقنية** أحياناً بدون إرشاد إجرائي.
7. **معالج ZATCA** معقد لغير التقنيين.
8. **لا onboarding** تفاعلي بعد الإعداد الأولي.

---

## 22. اختناقات الأداء

| الاختناق | التأثير | الحل المثالي |
|----------|---------|--------------|
| `VatReturnQuery.ToListAsync()` | ذاكرة + بطء مع آلاف الفواتير | `SUM()` على DB |
| عدد القيود لكل فترة — حلقة | بطء شاشة الفترات | استعلام واحد `GROUP BY` |
| 95 اختبار Application ~94s | CI بطيء | parallelization + in-memory DB مشترك |
| تقارير PDF متزامنة | تجميد UI | `Task.Run` + progress |
| لا read replicas | ضغط على PostgreSQL الوحيد | replica للتقارير |

---

## 23. الثغرات الأمنية

| الثغرة | OWASP / معيار | الخطورة |
|--------|---------------|---------|
| Fail-open authorization | A01 Broken Access Control | 🔴 |
| Secrets in plaintext files | A02 Cryptographic Failures | 🔴 |
| No audit on login failure | A09 Logging | 🟠 |
| No TLS pinning for ZATCA | A02 | 🟡 |
| Desktop app — DLL tampering | A08 Software Integrity | 🟠 |
| `BRQOSH_UITEST` bypass license | Business logic | 🟡 |
| Legacy SHA-256 passwords still accepted until login | A02 | 🟡 |

---

## 24. رائحة الكود

1. **`new Service(db)` في كل مكان** — عدم DI.
2. **Static mutable state** — `AppServices`, `SecurityContext`.
3. **God classes** — `BrqoshDbContext`, `MainWindow.xaml.cs` (switch ضخم).
4. **Magic strings** — `"5203"`, `"3999"`, `"QA-AUDIT"`.
5. **تكرار `await using var db = AppServices.CreateDb()`** في كل صفحة.
6. **Obsolete methods** لا تُزال — `UserAuthService.EnsureDefaultAdminAsync`.
7. **Comments بالعربية والإنجليزية مختلطة** — ليس خطأ لكن غير موحّد.

---

## 25. فرص إعادة الهيكلة

| الفرصة | الفائدة |
|--------|---------|
| `IJournalPostingGateway` موحّد | مسار ترحيل واحد + أمان واحد |
| MediatR للأوامر | فصل أوامر الترحيل/الإلغاء |
| MVVM + CommunityToolkit | UI قابل للاختبار |
| `IHttpClientFactory` لـ ZATCA | اختبار ومرونة |
| Repository خفيف فوق DbContext | تقليل حجم DbContext |
| Domain Events | فصل ZATCA post-posting |
| Policy-based authorization | بديل `SecurityContext` static |

---

## 26. الهندسة المعمارية الموصى بها

```
┌─────────────────────────────────────────────────────────┐
│  Brqosh.Desktop (WPF)  │  Brqosh.Web (مستقبلي)         │
│  MVVM + Auth Session   │  Blazor/React                   │
└────────────┬────────────┴────────────┬──────────────────┘
             │                         │
┌────────────▼─────────────────────────▼──────────────────┐
│  Brqosh.Api (ASP.NET Core)                               │
│  JWT + Policy Authorization + Rate Limiting              │
└────────────┬────────────────────────────────────────────┘
             │
┌────────────▼────────────────────────────────────────────┐
│  Brqosh.Application                                        │
│  Commands/Queries (MediatR) + Validators (FluentValidation)│
└────────────┬────────────────────────────────────────────┘
             │
┌────────────▼──────────┬──────────────────────────────────┐
│  Brqosh.Domain        │  Brqosh.Infrastructure            │
│  Aggregates, Rules    │  EF, ZATCA, PDF, External APIs    │
└───────────────────────┴──────────────────────────────────┘
             │
┌────────────▼────────────────────────────────────────────┐
│  PostgreSQL (Primary) + Read Replica + Key Vault           │
└───────────────────────────────────────────────────────────┘
```

**مبادئ:**

- **Fail-closed security** على كل command.
- **Outbox pattern** لـ ZATCA بعد الترحيل.
- **Multi-tenant** بـ `CompanyId` + middleware.
- **Event sourcing** اختياري لسجل القيود (مراجعة SOCPA).

---

## 27. خطة إعادة البناء خطوة بخطوة

### المرحلة 0 — إيقاف النزيف (أسبوع 1–2)

1. إصلاح `SecurityContext` → fail-closed.
2. `FiscalPeriodService` → `EnsureAdministrator` للإغلاق/الإعادة.
3. دمج ZATCA في transaction أو outbox.
4. إضافة اختبارات regression للثغرات C-01 إلى C-03.

### المرحلة 1 — توحيد الترحيل (أسبوع 3–6)

1. `JournalPostingGateway` — كل القيود تمر منه.
2. إزالة `entry.Post()` المباشر من خدمات الفواتير.
3. منع إنشاء الفترات التلقائي — شاشة إعداد فترات إلزامية.

### المرحلة 2 — أمان وامتثال (أسبوع 7–12)

1. Key Vault / DPAPI لمفاتيح ZATCA.
2. إقرار VAT كامل + تصدير.
3. اختبار ZATCA حي في pipeline staging.
4. Audit شامل + login failure logging.

### المرحلة 3 — API وقابلية التوسع (شهر 4–6)

1. `Brqosh.Api` مع نفس منطق Application.
2. Multi-company في UI.
3. PostgreSQL partitioning آلي.

### المرحلة 4 — جودة مؤسسية (شهر 7–9)

1. MVVM refactor تدريجي.
2. UiAudit في CI.
3. Penetration test خارجي.
4. توثيق CEEF محدّث بأدلة.

---

## 28. خارطة طريق التطوير

| الربع | الهدف | المخرجات |
|-------|-------|----------|
| Q3 2026 | استقرار أمني ومحاسبي | إغلاق C-01–C-05، gateway موحّد |
| Q4 2026 | امتثال ZATCA كامل | CSID حي، إرسال Production، وارد |
| Q1 2027 | API + تكامل | REST API، webhooks |
| Q2 2027 | توسع مؤسسي | Multi-tenant، فروع، workflow |
| Q3 2027 | منصة | Web client، mobile POS |

*(مبني على `docs/ROADMAP.md` مع تصحيح توقعات الجاهزية)*

---

## 29. استراتيجية الاختبار

### الوضع الحالي

| الطبقة | العدد | الملاحظة |
|--------|-------|----------|
| Domain | 38 | قواعد JournalEntry, COA, lifecycle |
| Application | 95 | دورات مبيعات/مشتريات/ZATCA/تقارير |
| UI (FlaUI) | ~167 فحص | **خارج CI** |
| Full Scenario | `tools/FullScenarioAudit` | يحتاج DB حقيقية |
| تغطية الكود | **غير مقاسة** | لا coverlet في CI |

### الاستراتيجية المستهدفة

```
        ┌─────────────┐
        │  E2E UiAudit │  5% — nightly
        ├─────────────┤
        │ Integration │  25% — PostgreSQL Testcontainers
        ├─────────────┤
        │  Application │  40% — خدمات الترحيل
        ├─────────────┤
        │   Domain     │  30% — invariants
        └─────────────┘
```

- **هدف تغطية:** 80% على `Brqosh.Domain` + `Infrastructure/Accounting|Sales|Zatca`.
- **اختبارات عقود ZATCA** ضد sandbox في كل PR إلى `main`.
- **Mutation testing** على `JournalEntry` و`SecurityContext`.

---

## 30. خطة اختبار الانحدار

### مجموعات إلزامية قبل كل إصدار

| المجموعة | الأمر | المعيار |
|----------|-------|---------|
| Unit+Integration | `dotnet test Brqosh.sln -c Release` | 0 فشل |
| ZATCA Golden | `--filter ZatcaGolden` | XML مطابق |
| Full Scenario | `tools/FullScenarioAudit` | كل الخطوات OK |
| UI Audit | `tools/UiAudit/run-audit.ps1` | 0 فشل |
| Trial Balance | FB = FC | توازن |
| Balance Sheet | Assets = Liab + Equity | `IsBalanced` |
| VAT | Net = Output - Input | مطابق يدوي |

### حالات حرجة للانحدار

1. ترحيل فاتورة → قيد متوازن → رصيد عميل.
2. إلغاء فاتورة مع إشعار دائن → مرفوض.
3. Viewer → ترحيل مرفوض (`SecurityContextPostingTests`).
4. فترة مقفلة → ترحيل مرفوض.
5. عكس قيد مستند → مرفوض يدوياً.
6. POS نقدي → حساب صندوق لا ذمم.

---

## 31. خطة النشر

### القنوات الحالية

| القناة | الملف | الحالة |
|--------|-------|--------|
| Portable | `installer/publish.ps1` | ✅ |
| MSI WiX | `installer/build-msi.ps1` | ✅ يحتاج WiX CLI |
| CI | `.github/workflows/ci.yml` | ✅ build+test فقط |

### خطة نشر Production موصى بها

1. **بيئة Staging** — PostgreSQL + ZATCA simulation.
2. **بيئة Production** — PostgreSQL 15+، `AppEnvironment=Production`.
3. **قائمة تحقق ما قبل النشر:**
   - [ ] `database.json` — PostgreSQL فقط
   - [ ] ترخيص RSA موقّع
   - [ ] CSID Production مفعّل
   - [ ] نسخ احتياطي مجدول
   - [ ] 0 فشل في FullScenario + UiAudit
4. **تحديثات:** MSI delta أو Squirrel — **غير موجود حالياً**.
5. **مراقبة:** إضافة Application Insights — skill موجود لكن **غير مُدمج**.

---

## 32. استراتيجية الصيانة

| النشاط | التكرار | المسؤول |
|--------|---------|---------|
| ترقية .NET | سنوي | فريق المنصة |
| مراجعة ZATCA | عند تحديث الهيئة | امتثال |
| Migrations review | كل PR | مطور DB |
| نسخ احتياطي PostgreSQL | يومي | عمليات |
| تدوير شهادات CSID | قبل انتهاء | امتثال |
| مراجعة أمنية | ربع سنوي | أمن |
| تحديث `SaudiChartOfAccounts` | عند تغيير سياسة | محاسبة |

**إصدارات:** Semantic Versioning — حالياً لا `CHANGELOG.md` رسمي.

---

## 33. خطة التوسع المستقبلية

### أفقي (Scale Out)

1. فصل **قراءة التقارير** إلى read replica.
2. **API layer** لعدة عملاء Desktop/Web.
3. **Message queue** (RabbitMQ/Azure Service Bus) لـ ZATCA submission.
4. **Partitioning** آلي لـ `journal_entries` عند 5M+ سجل.

### عمودي (Scale Up)

1. **Multi-company SaaS** — tenant isolation بـ RLS في PostgreSQL.
2. **فروع** — `BranchId` على المستندات والمخازن.
3. **تكامل:** GOSI API، البنوك، منصات التجارة.

### органizational

- فريق دعم L2 للمحاسبة السعودية.
- شريك مراجعة خارجي قبل أول عميل مؤسسي.

---

## ملحق أ — إحصائيات المشروع (8 يوليو 2026)

| المقياس | القيمة |
|---------|--------|
| مشاريع الحل | 9 (4 أساسية + 3 modules + 2 test) |
| صفحات WPF | ~50 |
| اختبارات ناجحة | **133** (38 Domain + 95 Application) |
| Migrations EF | 2 |
| ملفات CI | 1 (`ci.yml`) |
| أدوات تدقيق | `FullScenarioAudit`, `UiAudit` |

## ملحق ب — الملفات المرجعية الرئيسية

| الموضوع | المسار |
|---------|--------|
| ترحيل القيود | `src/Brqosh.Infrastructure/Accounting/JournalPostingService.cs` |
| كيان القيد | `src/Brqosh.Domain/Accounting/JournalEntry.cs` |
| ميزان المراجعة | `src/Brqosh.Infrastructure/Reporting/TrialBalanceQuery.cs` |
| المركز المالي | `src/Brqosh.Infrastructure/Reporting/BalanceSheetQuery.cs` |
| إقرار VAT | `src/Brqosh.Infrastructure/Reporting/VatReturnQuery.cs` |
| ZATCA | `src/Brqosh.Infrastructure/Zatca/` |
| الصلاحيات UI | `src/Brqosh.Desktop/Security/PermissionService.cs` |
| الصلاحيات خدمات | `src/Brqosh.Infrastructure/Security/SecurityContext.cs` |
| المصادقة | `src/Brqosh.Infrastructure/Security/UserAuthService.cs` |
| المخطط | `src/Brqosh.Infrastructure/Persistence/BrqoshDbContext.cs` |
| خارطة الطريق | `docs/ROADMAP.md` |
| إطار CEEF | `docs/CEEF/00_MASTER_SYSTEM.md` |

## ملحق ج — حكم الجاهزية وفق CEEF

وفق `docs/CEEF/00_MASTER_SYSTEM.md`: *«النظام جاهز للإنتاج فقط عند تحقق: المحاسبة، سلامة البيانات، الأمان، الأداء، التوسع، UX، الامتثال السعودي، التوثيق، الاختبارات، وجاهزية النشر — كلها بأدلة.»*

| البوابة | الحكم |
|---------|-------|
| المحاسبة | ⚠️ جزئي |
| سلامة البيانات | ⚠️ جزئي |
| الأمان | ❌ غير مجتاز |
| الأداء | ⚠️ غير مُختبر تحت حمل |
| التوسع | ❌ |
| UX | ⚠️ |
| الامتثال السعودي | ⚠️ |
| التوثيق | ✅ جيد نسبياً |
| الاختبارات | ⚠️ بدون تغطية/UI في CI |
| جاهزية النشر | ⚠️ MSI موجود؛ عمليات ناقصة |

**الحكم النهائي: غير جاهز للإنتاج المؤسسي — يتطلب المعالجات في الأقسام 13–27.**

---

*نهاية التقرير — أُعدّ بناءً على فحص الكود المصدري وتشغيل الاختبارات بتاريخ 8 يوليو 2026.*
