# تقرير التدقيق التفصيلي العميق — نظام BRQOSH ERP

**الإصدار:** 2.0 (تدقيق بالمليمتر)  
**التاريخ:** 8 يوليو 2026  
**النطاق:** `C:\Users\qc\Projects\BRQOSH` — تطبيق WPF محاسبي/ERP سعودي  
**المنهجية:** مراجعة evidence-based على 294 ملف C#، 133 اختباراً (تشغيل فعلي 8/7/2026 — 0 فشل)، 42 شاشة، 33 كيان DB، وتوثيق CEEF  
**المراجع:** IFRS / IAS 2 / IFRS 15 / SOCPA / ZATCA Phase 2 / OWASP ASVS L2 / CEEF v1.0 / SAP FI / Odoo Accounting

> **ملاحظة:** هذا التقرير يُكمّل `AUDIT_REPORT_AR.md` ويُعمّقه. لا يفترض صحة أي تنفيذ — كل ادعاء مربوط بملف وسطر.

---

## جدول المحتويات

### الأقسام الرئيسية (33)

1. [الملخص التنفيذي](#1-الملخص-التنفيذي)
2. [تقييم الهندسة المعمارية](#2-تقييم-الهندسة-المعمارية)
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
26. [الهندسة المعمارية الموصى بها](#26-الهندسة-المعمارية-الموصى-بة)
27. [خطة إعادة البناء](#27-خطة-إعادة-البناء)
28. [خارطة طريق التطوير](#28-خارطة-طريق-التطوير)
29. [استراتيجية الاختبار](#29-استراتيجية-الاختبار)
30. [خطة اختبار الانحدار](#30-خطة-اختبار-الانحدار)
31. [خطة النشر](#31-خطة-النشر)
32. [استراتيجية الصيانة](#32-استراتيجية-الصيانة)
33. [خطة التوسع المستقبلية](#33-خطة-التوسع-المستقبلية)



### الملاحق

- [الملحق أ — مصفوفة القيود المحاسبية لكل مستند](#الملحق-أ--مصفوفة-القيود-المحاسبية)
- [الملحق ب — جرد الخدمات والصلاحيات](#الملحق-ب--جرد-الخدمات)
- [الملحق و — تحقيق الخدمات الثمانية بلا EnsureCanPost*](#الملحق-و--تحقيق-الخدمات-الثمانية-بلا-ensurecanpost)
- [الملحق ج — جرد الشاشات (42 شاشة)](#الملحق-ج--جرد-الشاشات)
- [الملحق د — مصفوفة الاختبارات](#الملحق-د--مصفوفة-الاختبارات)
- [الملحق هـ — سجل المخاطر الكامل](#الملحق-هـ--سجل-المخاطر)

---



## 1. الملخص التنفيذي



### 1.1 الحكم النهائي


| البُعد                            | الدرجة  | الحكم                                      |
| --------------------------------- | ------- | ------------------------------------------ |
| صحة القيد المزدوج (مستوى المستند) | 78%     | جيد — التوازن الرياضي محمي في Domain       |
| سلامة التقارير التاريخية          | 35%     | **فاشل** — القيود المعكوسة تُشوّه التقارير |
| الامتثال السعودي (ZATCA/VAT)      | 55%     | جزئي — تكامل موجود لكن به ثغرات حرجة       |
| الأمان والتفويض                   | 30%     | **فاشل** — fail-open + خدمات غير محمية     |
| جودة الواجهة                      | 55%     | متوسط — RTL جيد، UX غير مؤسسي              |
| قابلية التوسع                     | 25%     | ضعيف — monolith أحادي الشركة               |
| جاهزية الإنتاج المؤسسي            | **32%** | **غير جاهز**                               |


**الخلاصة:** BRQOSH نموذج أولي متقدم وظيفياً (Advanced Prototype) بقيمة تجريبية عالية. القيد المزدوج وعكس القيود وربط المستندات **قويان نظرياً**. لكن أخطر الفجوات ليست في جمع المدين والدائن، بل في: **سلامة التقارير عبر الفترات**، **التزامن في الترقيم**، **فصل ZATCA عن المعاملة**، **غياب IFRS 15 للمقاولات**، **WAC-only بلا FIFO**، و**ضوابط الأمان/الفترة الضعيفة**.

### 1.2 إحصائيات المشروع (مُثبتة)


| المقياس                    | القيمة                       | المصدر                      |
| -------------------------- | ---------------------------- | --------------------------- |
| ملفات C# — Desktop         | 94                           | `src/Brqosh.Desktop`        |
| ملفات C# — Infrastructure  | 93                           | `src/Brqosh.Infrastructure` |
| ملفات C# — Domain          | 47                           | `src/Brqosh.Domain`         |
| ملفات C# — Application     | 2                            | `src/Brqosh.Application`    |
| مشاريع Modules.*           | 0 ملف (csproj فارغ)          | `Brqosh.Modules.`*          |
| شاشات WPF                  | 42 مفتاح تنقل + 46 Page.xaml | `NavigationCatalog.cs`      |
| كيانات DbSet               | 33                           | `BrqoshDbContext.cs`        |
| اختبارات Domain            | 38 — 0 فشل                   | `dotnet test` 8/7/2026      |
| اختبارات Application       | 95 — 0 فشل                   | `dotnet test` 8/7/2026      |
| إجمالي `[Fact]`/`[Theory]` | ~125                         | `tests/`                    |
| خدمات ترحيل بـ `EnsureCan*` | **14 من 22** (64%) — **8 بلا فحص** | grep Infrastructure — الملحق و |
| صفحات بفحص صلاحيات داخلي   | **0 من 50**                  | grep Pages                  |




### 1.3 أهم 15 خللاً حرجة (مرتبة بالأثر)


| #   | المعرف    | الخلل                                           | الملف                                  | الأثر                              |
| --- | --------- | ----------------------------------------------- | -------------------------------------- | ---------------------------------- |
| 1   | SEC-001   | `CurrentRole == null` → تجاوز كامل للصلاحيات    | `SecurityContext.cs:37-38`             | أي استدعاء بلا دور يمرّ            |
| 2   | ACC-001   | التقارير تستبعد `Reversed` — تشويه تاريخي       | `TrialBalanceQuery.cs:32`              | ميزان يناير يتغير بعد إلغاء فبراير |
| 3   | ZATCA-001 | منحنى `secp256k1` في CSID vs `P-256` في التوقيع | `ZatcaCsidOnboardingService.cs:25,36`  | شهادات قد تُرفض                    |
| 4   | ACC-002   | ZATCA خارج معاملة الترحيل                       | `SalesInvoicePostingService.cs:57-66`  | GL مرحّل، ZATCA فاشل               |
| 5   | ACC-003   | إهلاك شهري بلا منع تكرار                        | `DepreciationService.cs:28-42`         | إهلاك مضاعف                        |
| 6   | SEC-002   | `PostCoreAsync` يتجاوز `EnsureCanPost`          | `SalesInvoicePostingService.cs:75`     | POS/أي مستدعٍ يرحّل بلا فحص        |
| 7   | ACC-004   | إغلاق/فتح فترات بلا صلاحية مدير                 | `FiscalPeriodService.cs:11-30`         | أي محاسب يغلق السنة                |
| 8   | ACC-005   | إشعار دائن POS يُقيد على 1104 بدل نقدية         | `CreditNotePostingService.cs:147`      | خلل في الذمم                       |
| 9   | ACC-006   | Race condition في ترقيم JV/INV                  | `JournalEntryNumberGenerator.cs`       | أرقام مكررة                        |
| 10  | ACC-007   | إلغاء WO بلا عكس مواد/قيود                      | `WorkOrderService.cs:91-99`            | مخزون/GL خاطئ                      |
| 11  | ZATCA-002 | أسرار CSID في DB بنص صريح                       | `BrqoshDbContext.cs` + `ZatcaSettings` | تسريب عند نسخ احتياطي              |
| 12  | ACC-008   | فائض جرد يُقيد كإيراد 4106                      | `StockMovementPostingService.cs:91-98` | مخالف IAS 2                        |
| 13  | ACC-009   | لا IFRS 15 — إيراد مقاولات كامل بلا WIP         | `ProgressBilling.cs`                   | تقارير مالية مضللة                 |
| 14  | SEC-003   | **8 خدمات ترحيل** بلا `EnsureCanPost*` (انظر الملحق و) | انظر الجدول أدناه              | Viewer/Cashier/null-role يرحّل مباشرة |
| 15  | INFRA-001 | لا API — تكامل خارجي مستحيل                     | بحث `Controller|WebApplication`        | صفر نتائج                          |




### 1.4 التوصية التنفيذية

**لا تُطلق Production** قبل إغلاق P0 (القسم 13). المدة التقديرية للجاهزية المؤسسية: **9–12 شهراً** بفريق 4–6 مطورين + محاسب قانوني + مختبر ZATCA.

---



## 2. تقييم الهندسة المعمارية



### 2.1 البنية الفعلية

```
Brqosh.sln
├── Brqosh.Domain          (47 .cs)  — كيانات + قواعد أعمال نقية ✅
├── Brqosh.Application     (2 .cs)   — Setup فقط ⚠️ طبقة شبه فارغة
├── Brqosh.Infrastructure  (93 .cs)  — EF + خدمات + ZATCA + تقارير
├── Brqosh.Desktop         (94 .cs)  — WPF — كل الواجهات
├── Brqosh.Modules.*       (0 .cs)    — مشاريع فارغة ❌
└── tests/                 (47 .cs)  — 133 اختبار
```



### 2.2 مخالفات معمارية (مُثبتة)


| #   | المخالفة                             | الدليل                                                      | الخطورة | الحل المثالي                                            |
| --- | ------------------------------------ | ----------------------------------------------------------- | ------- | ------------------------------------------------------- |
| 2.1 | Presentation → Infrastructure مباشرة | ~70 ملف `*Page.xaml.cs` يستورد `Brqosh.Infrastructure.*`    | عالي    | طبقة Application/use-cases                              |
| 2.2 | Service Locator static               | `AppServices.cs` — `CreateDb()`, `CurrentUser`, `CompanyId` | عالي    | DI container (Microsoft.Extensions.DependencyInjection) |
| 2.3 | Modules فارغة                        | `Brqosh.Modules.Construction` = csproj فقط                  | متوسط   | ملء أو حذف من الحل                                      |
| 2.4 | لا CQRS/Event Sourcing               | كل العمليات CRUD مباشرة                                     | منخفض   | أحداث مجال للترحيل/الإلغاء                              |
| 2.5 | لا API layer                         | WPF monolith                                                | عالي    | ASP.NET Core Minimal API                                |
| 2.6 | `Application` لا تُنسّق              | `Brqosh.Application` = `AppEnvironment` + `Setup` فقط       | عالي    | نقل orchestration من Desktop                            |




### 2.3 اقتران التبعيات (Dependency Graph)

```
Desktop ──direct──> Infrastructure ──> Domain
   │                      │
   └── AppServices ───────┘ (static, anti-pattern)
```

**مقارنة SAP/Odoo:** فصل Presentation/Application/Domain/Infrastructure صارم + API + message bus.

### 2.4 أنماط التصميم المستخدمة


| النمط             | الاستخدام                              | التقييم           |
| ----------------- | -------------------------------------- | ----------------- |
| Repository (ضمني) | `BrqoshDbContext` مباشرة               | ⚠️ لا abstraction |
| Unit of Work      | `UnitOfWork.ExecuteInTransactionAsync` | ✅ جيد             |
| Domain Events     | غير موجود                              | ❌                 |
| Strategy (تكلفة)  | WAC فقط                                | ⚠️                |
| Factory           | `JournalEntry.CreateDraft`             | ✅                 |
| Service Layer     | Infrastructure services                | ⚠️ بلا واجهات     |


---



## 3. مراجعة المحاسبة



### 3.1 القيد المزدوج — النواة

`JournalEntry.cs` — التحقق عند `Post()`:

```
∀ entry: Σ Debit_i = Σ Credit_i  ⟺  Post() ينجح
شرط إضافي: |Lines| ≥ 2
```


| القاعدة                     | السطر                | الحالة               |
| --------------------------- | -------------------- | -------------------- |
| منع سالب                    | `AddLine`            | ✅                    |
| منع مدين+دائن معاً          | `AddLine`            | ✅                    |
| حساب قابل للترحيل           | `account.IsPostable` | ✅                    |
| حالات Draft→Posted→Reversed | enum                 | ✅                    |
| تسامح هللات                 | `==` صارم            | ⚠️ لا 0.01 tolerance |




### 3.2 مصفوفة القيود لكل مستند (ملخص — التفصيل في الملحق أ)



#### فاتورة مبيعات (`SalesInvoice.Post`)

```
Dr 1104/1101  = GrandTotal
Cr 4101       = Subtotal
Cr 2106       = VatAmount
Dr 5101       = COGS (إن وُجد)
Cr 1107       = COGS
```

**برهان التوازن:** `GrandTotal = Subtotal + Vat` → Dr = Cr ✅

#### فاتورة مشتريات (`PurchaseInvoice.Post`)

```
Dr 5102/1107  = Subtotal (مقسّم)
Dr 1111       = VatAmount (إن قابل للخصم)
Cr 2101       = GrandTotal
```



#### إشعار دائن (`CreditNote.Post`)

```
Dr 4102 + Dr 2106 / Cr 1104 = GrandTotal
Dr 1107 / Cr 5101 = COGS reversal
```



#### مستخلص أعمال (`ProgressBilling.Post`)

```
Dr 1104 = NetBillable + Vat
Dr 1114 = RetentionAmount
Cr 4104 = CurrentWorkAmount
Cr 2106 = VatAmount
```

**برهان:** `Dr = (Work-Ret)+Vat+Ret = Work+Vat = Cr` ✅

#### رواتب (`PayrollRun.Post`)

```
Dr 5201 = Gross
Dr 5203 = GosiEmployer
Cr 2108 = Net
Cr 2109 = GosiEmployee + GosiEmployer
```

**برهان:** `Dr = Gross+GosiEmp = Cr` ✅

### 3.3 خلل التقارير التاريخية — تحليل رياضي مفصّل

**المشكلة:** `TrialBalanceQuery.cs:32` يفلتر `Status == Posted` فقط.

**سيناريو إثبات:**

```
1. يناير: فاتورة بيع 1,150 ر.س → قيد JV-001 (Posted)
2. فبراير: إلغاء الفاتورة → JV-002 (عكسي) + JV-001 → Reversed
3. تقرير ميزان يناير (بعد الإلغاء):
   - JV-001 مستبعد (Reversed)
   - JV-002 في فبراير فقط
   → يناير يظهر بصفر بينما كان فعلياً 1,150
```

**الحل المثالي (SAP):** تقارير "كما كانت" (point-in-time) أو تضمين قيود العكس المرتبطة في نفس الفترة الأصلية.

**نفس الخلل في:** `GeneralLedgerQuery.cs:28-31`, `IncomeStatementQuery`, `BalanceSheetQuery`.

### 3.4 الفترات المالية


| الوظيفة      | الملف                            | المشكلة                                   |
| ------------ | -------------------------------- | ----------------------------------------- |
| إنشاء تلقائي | `JournalPostingService.cs:34-39` | فترة تُنشأ بلا حوكمة                      |
| إغلاق        | `FiscalPeriodService.cs:11-20`   | بلا `EnsureAdministrator`، بلا فحص مسودات |
| إعادة فتح    | `FiscalPeriodService.cs:22-30`   | بلا صلاحية، بلا سبب إلزامي                |
| إقفال سنوي   | غير موجود                        | لا نقل 4xx/5xx → 3103/3104                |




### 3.5 المخزون — WAC فقط

**الصيغة** (`InventoryService.ApplyReceiptCostAsync`):

```
WAC_new = (Q_old × C_old + Q_in × C_in) / (Q_old + Q_in)
```


| المشكلة               | الخطورة | التفصيل                                              |
| --------------------- | ------- | ---------------------------------------------------- |
| لا FIFO/LIFO/Standard | حرج     | بحث `FIFO` = 0 نتائج                                 |
| عكس COGS بـ WAC لحظي  | عالي    | `CreditNotePostingService.cs:131`                    |
| فائض جرد = إيراد 4106 | حرج     | مخالف IAS 2 — `StockMovementPostingService.cs:91-98` |




### 3.6 المقاولات — غياب IFRS 15


| القدرة                      | BRQOSH                   | IFRS 15 / SAP PS         |
| --------------------------- | ------------------------ | ------------------------ |
| إثبات إيراد بالنسبة للإنجاز | ❌ إيراد كامل عند الفوترة | ✅ Performance obligation |
| تكاليف مشروع على WIP 1113   | ❌                        | ✅                        |
| عقد + تعديلات               | جزئي (عقود بسيطة)        | ✅                        |
| محتجزات                     | ✅ 1114/2110              | ✅                        |
| ضريبة على المحتجز           | ⚠️ غير موثّق فقهياً      | يحتاج تأكيد هيئة         |




### 3.7 الدفاتر المساعدة vs GL


| الدفتر           | الصيغة                                                | مطابقة GL             |
| ---------------- | ----------------------------------------------------- | --------------------- |
| عملاء            | `Invoiced + Billings - Credited - Received - Cheques` | ❌ لا تقرير فروقات     |
| موردون           | `Invoiced - Paid - Cheques - Retention`               | ❌                     |
| شيكات في الحافظة | تُخصم من رصيد العميل قبل التحصيل                      | ⚠️ سياسة قابلة للنقاش |


---



## 4. مراجعة الامتثال



### 4.1 ZATCA — Phase 2


| المتطلب          | الحالة               | الدليل                              |
| ---------------- | -------------------- | ----------------------------------- |
| UBL 2.1 XML      | ✅                    | `ZatcaUblInvoiceBuilder.cs`         |
| TLV QR Phase 1/2 | ✅                    | `ZatcaQrGenerator.cs`               |
| XAdES-EPES توقيع | ✅ (إذا المفتاح صحيح) | `ZatcaXmlSigner.cs`                 |
| سلسلة ICV/PIH    | ✅                    | `ZatcaEInvoiceGenerator.cs`         |
| CSID onboarding  | ⚠️ منحنى خاطئ        | `ZatcaCsidOnboardingService.cs:36`  |
| إرسال تلقائي     | ✅                    | `ZatcaAutoSubmissionHelper`         |
| ذرية GL+ZATCA    | ❌                    | خارج المعاملة                       |
| فشل توقيع صامت   | ⚠️ QR Phase 1 فقط    | `ZatcaEInvoiceGenerator.cs:246-265` |
| اختبار حي OTP    | ⚠️ معطّل افتراضياً   | `ZatcaLiveCsidTests.cs`             |




### 4.2 ضريبة القيمة المضافة (15%)


| البند                  | الحالة                           |
| ---------------------- | -------------------------------- |
| ضريبة مخرجات 2106      | ✅                                |
| ضريبة مدخلات 1111      | ✅                                |
| مورد غير مسجل — لا خصم | ✅ `PurchaseInvoice.cs:119-131`   |
| إقرار VAT              | ⚠️ `VatReturnQuery` — تجميع بسيط |
| تسوية شهرية لـ 2107    | ❌ حساب موجود غير مستخدم          |
| ضريبة سطرية ZATCA      | ❌ ضريبة على مستوى الفاتورة       |
| تصدير/صفرية/معفاة      | ❌                                |
| Reverse charge         | ❌                                |




### 4.3 SOCPA / IFRS


| المعيار              | الحالة | الفجوة                          |
| -------------------- | ------ | ------------------------------- |
| IAS 2 (مخزون)        | جزئي   | WAC فقط، فائض جرد كإيراد        |
| IFRS 15 (إيراد)      | ❌      | مقاولات بلا نسبة إنجاز          |
| IAS 16 (أصول)        | جزئي   | إهلاك خط مستقيم فقط، تكرار ممكن |
| IAS 21 (عملات)       | ❌      | SAR فقط                         |
| IAS 36 (انخفاض قيمة) | ❌      |                                 |


---



## 5. مراجعة الأمان



### 5.1 ثغرة fail-open — تحليل عميق

```csharp
// SecurityContext.cs:35-40
public static void EnsureCanPost()
{
    if (CurrentRole is null) return;  // ← ثغرة حرجة
    if (CurrentRole is UserRole.Viewer)
        throw new AccountingException(...);
}
```

**سيناريو الاستغلال:**

1. أي كود يستدعي `CreditNotePostingService.PostAsync` مباشرة
2. `SecurityContext.CurrentRole` غير مضبوط (اختبارات، أدوات، API مستقبلي)
3. الترحيل ينجح بلا أي فحص

**نفس النمط في:** `EnsureCanPostAccounting`, `EnsureCanCancelOrReverse`, `EnsureAdministrator` — كلها `return` عند null.

**الإصلاح:**

```csharp
if (CurrentRole is null)
    throw new AccountingException("جلسة غير صالحة — يجب تسجيل الدخول.");
// أو: if (AppEnvironment.IsTest) return; // صريح للاختبارات فقط
```



### 5.2 جرد حماية الخدمات — نتيجة التحقيق المُثبتة

> **منهجية العد:** `grep EnsureCanPost|EnsureCanPostAccounting|EnsureCanCancelOrReverse` على `src/Brqosh.Infrastructure` — 8 يوليو 2026.

#### أ) خدمات الترحيل/العكس **المحمية** (14 خدمة)

| الخدمة | الفحص المستخدم | الملف:السطر |
|--------|----------------|-------------|
| `JournalPostingService` | `EnsureCanPostAccounting` | `JournalPostingService.cs:17` — **فقط في `PostAsync`** |
| `SalesInvoicePostingService` | `EnsureCanPost` | `SalesInvoicePostingService.cs:43` |
| `PurchaseInvoicePostingService` | `EnsureCanPost` | `PurchaseInvoicePostingService.cs:19` |
| `StockMovementPostingService` | `EnsureCanPost` | `StockMovementPostingService.cs:15` |
| `PayrollPostingService` | `EnsureCanPostAccounting` | `PayrollPostingService.cs:14` |
| `ChequePostingService` | `EnsureCanPostAccounting` | `ChequePostingService.cs:14,32,50,69,99,127` |
| `JournalReversalService` | `EnsureCanCancelOrReverse` + `EnsureCanPostAccounting` | `JournalReversalService.cs:22-24` |
| `SalesInvoiceCancellationService` | `EnsureCanCancelOrReverse` | `SalesInvoiceCancellationService.cs:20` |
| `PurchaseInvoiceCancellationService` | `EnsureCanCancelOrReverse` | `PurchaseInvoiceCancellationService.cs:18` |
| `CreditNoteCancellationService` | `EnsureCanCancelOrReverse` | `CreditNoteCancellationService.cs:20` |
| `ProgressBillingCancellationService` | `EnsureCanCancelOrReverse` | `ProgressBillingCancellationService.cs:18` |
| `StockMovementCancellationService` | `EnsureCanCancelOrReverse` | `StockMovementCancellationService.cs:24` |
| `VoucherReversalService` | `EnsureCanCancelOrReverse` | `VoucherReversalService.cs:15,43` |
| `PayrollReversalService` | `EnsureCanCancelOrReverse` | `PayrollReversalService.cs:16` |

#### ب) خدمات الترحيل **الثمانية بلا أي `EnsureCanPost*`** — ثغرة مؤكدة

| # | الخدمة | الدالة | الملف:السطر | الفحص البديل الموجود | غير كافٍ لأن |
|---|--------|--------|-------------|---------------------|-------------|
| 1 | `CreditNotePostingService` | `PostAsync` | `CreditNotePostingService.cs:35` | `EnsurePeriodOpenAsync` فقط | لا يفحص الدور |
| 2 | `ReceiptVoucherPostingService` | `PostAsync` | `ReceiptVoucherPostingService.cs:14` | `EnsurePeriodOpenAsync` فقط | لا يفحص الدور |
| 3 | `PaymentVoucherPostingService` | `PostAsync` | `PaymentVoucherPostingService.cs:14` | `EnsurePeriodOpenAsync` فقط | لا يفحص الدور |
| 4 | `ProgressBillingPostingService` | `PostAsync` | `ProgressBillingPostingService.cs:13` | `EnsurePeriodOpenAsync` فقط | لا يفحص الدور |
| 5 | `FixedAssetAcquisitionPostingService` | `PostAcquisitionAsync` | `FixedAssetAcquisitionPostingService.cs:12` | `EnsurePeriodOpenAsync` فقط | لا يفحص الدور |
| 6 | `FixedAssetDisposalPostingService` | `PostDisposalAsync` | `FixedAssetDisposalPostingService.cs:13` | `EnsurePeriodOpenAsync` فقط | لا يفحص الدور |
| 7 | `RetentionReleaseService` | `ReleaseAsync` | `RetentionReleaseService.cs:20` | `EnsurePeriodOpenAsync` فقط | لا يفحص الدور |
| 8 | `SubcontractRetentionWithholdService` | `WithholdAsync` | `SubcontractRetentionWithholdService.cs:13` | `EnsurePeriodOpenAsync` فقط | لا يفحص الدور |

**ملاحظة حرجة:** `JournalPostingService.EnsurePeriodOpenAsync` (سطر 28-45) **لا يستدعي** `SecurityContext` — يتحقق من الفترة المالية فقط. الخدمات الثمانية تعتمد عليه وتظن أنها «محمية».

**تناقض أمني:** `CreditNoteCancellationService` **محمية** بـ `EnsureCanCancelOrReverse` بينما `CreditNotePostingService` **غير محمية** — يمكن الترحيل بلا صلاحية لكن الإلغاء ممنوع لـ Viewer.

#### ج) خدمات أخرى بلا `EnsureCan*` (خارج نطاق الثمانية — إدارية/تشغيلية)

`DepreciationService`, `FiscalPeriodService`, `UserManagementService`, `WorkOrderService`, `PurchaseOrderService`, `StockCountAdjustmentService`, `BankAccountService`, `ZatcaEInvoiceGenerator`, `ZatcaSubmissionService`, `PosSaleService.PostCoreAsync` — تحتاج فحوصات منفصلة (`EnsureAdministrator` أو `EnsureCanPostAccounting`).

**التحقيق التفصيلي للثمانية:** انظر [الملحق و](#الملحق-و--تحقيق-الخدمات-الثمانية-بلا-ensurecanpost).

### 5.3 المصادقة


| البند                            | الحالة | الملف                                              |
| -------------------------------- | ------ | -------------------------------------------------- |
| PBKDF2-SHA256 100k iterations    | ✅      | `UserAuthService.cs`                               |
| Salt عشوائي                      | ✅      |                                                    |
| Constant-time compare            | ✅      |                                                    |
| ترقية SHA-256 قديم               | ⚠️     | يقبل legacy                                        |
| قفل حساب / rate limit            | ❌      | `LoginWindow.xaml.cs`                              |
| Session timeout                  | ❌      | `AppServices.CurrentUser` static                   |
| MFA                              | ❌      |                                                    |
| سياسة كلمة مرور عند إنشاء مستخدم | ❌      | `UserManagementService` لا يستدعي `PasswordPolicy` |




### 5.4 الأسرار على القرص


| المسار                                         | المحتوى                  | الخطورة            |
| ---------------------------------------------- | ------------------------ | ------------------ |
| `%APPDATA%\BRQOSH\database.json`               | ConnectionString كامل    | عالي               |
| `%APPDATA%\BRQOSH\zatca\{id}\csid\private.key` | مفتاح ZATCA خاص          | حرج                |
| جدول `zatca_settings`                          | `ApiToken`, `CsidSecret` | حرج                |
| `%APPDATA%\BRQOSH\license.json`                | ترخيص                    | متوسط              |
| `BRQOSH_UITEST=1`                              | تعطيل الترخيص            | عالي في Production |




### 5.5 OWASP ASVS — ملخص


| فئة                | التقييم | أبرز الفجوة            |
| ------------------ | ------- | ---------------------- |
| V1 Architecture    | ⚠️      | لا API، monolith       |
| V2 Authentication  | ⚠️      | لا timeout/lockout     |
| V3 Session         | ❌       | static session         |
| V4 Access Control  | ❌       | fail-open              |
| V5 Validation      | ✅ جزئي  | Domain validation جيد  |
| V6 Cryptography    | ⚠️      | secp256k1 vs P-256     |
| V7 Error Handling  | ⚠️      | exception خام في login |
| V8 Data Protection | ❌       | أسرار بنص صريح         |
| V9 Communications  | ⚠️      | ZATCA HTTPS فقط        |
| V10 Malicious Code | ✅       | لا eval/dynamic SQL    |
| V11 Business Logic | ⚠️      | race في الترقيم        |
| V12 Files          | ⚠️      | XML على القرص          |
| V13 API            | N/A     | لا API                 |
| V14 Configuration  | ⚠️      | CI ضعيف                |


---



## 6. مراجعة الأداء



### 6.1 نقاط القياس


| السيناريو            | التقييم المتوقع      | الدليل                                  |
| -------------------- | -------------------- | --------------------------------------- |
| Cold start WPF       | 3-8 ثوانٍ            | bootstrap + DB + license + login        |
| ميزان مراجعة 10K قيد | جيد (DB aggregation) | `TrialBalanceQuery` GroupBy على DB      |
| أستاذ عام 100K سطر   | ⚠️ بطيء              | `GeneralLedgerQuery` يحمّل في الذاكرة   |
| 100 مستخدم متزامن    | ❌ غير مدعوم          | desktop single-user                     |
| مليون سجل            | ⚠️ غير مُختبر        | لا load tests                           |
| جرد 10K صنف          | ⚠️                   | `StockCountPage` بدون pagination UI     |
| إعادة بناء أرصدة     | موجود                | `InventoryService.RebuildBalancesAsync` |




### 6.2 Race Conditions


| الموضع                        | الخطورة | التفصيل                           |
| ----------------------------- | ------- | --------------------------------- |
| `JournalEntryNumberGenerator` | حرج     | `MAX+1` بلا `UPDLOCK`             |
| `DocumentNumberGenerator`     | حرج     | نفس النمط                         |
| ZATCA ICV                     | ⚠️      | داخل معاملة لكن ZATCA نفسه خارجها |
| WAC تحديث                     | ⚠️      | داخل TX — مقبول لـ single-user    |




### 6.3 اختبارات أداء موجودة

- `InventoryPerformanceTests.cs` — اختبار واحد
- لا load/stress/endurance tests

---



## 7. مراجعة واجهة المستخدم



### 7.1 التقييم الإجمالي: 55/100


| المعيار        | الدرجة | الملاحظة                       |
| -------------- | ------ | ------------------------------ |
| RTL            | 90%    | `FlowDirection=RTL` في 80+ ملف |
| Design System  | 70%    | `BrqoshTheme.xaml` متسق        |
| Error UX       | 25%    | MessageBox في 50+ موضع         |
| Loading states | 15%    | نص فقط في Dashboard            |
| Keyboard       | 0%     | صفر KeyBinding                 |
| Dark mode      | 0%     |                                |
| Accessibility  | 15%    | 3 AutomationProperties         |
| Iconography    | 30%    | Emoji في التنقل                |
| Pagination     | 20%    | `Take(100)` صامت               |
| Permission UX  | 40%    | رفض عند النقر لا إخفاء         |




### 7.2 مقارنة Fiori/Stripe


| معيار               | BRQOSH    | Fiori/Stripe |
| ------------------- | --------- | ------------ |
| Toast notifications | ❌         | ✅            |
| Skeleton loading    | ❌         | ✅            |
| Empty states        | ❌         | ✅            |
| Inline validation   | جزئي      | ✅            |
| Object page layout  | ✅ متكرر   | ✅            |
| Trust & clarity     | ✅ تأكيدات | ✅            |
| Professional icons  | ❌ Emoji   | ✅ SVG        |




### 7.3 ثغرات UX حرجة

1. **Viewer يرى أزرار ترحيل/إلغاء** — يفشل لاحقاً في الخدمة
2. **القائمة الجانبية تُظهر كل الشاشات** — رفض عند النقر فقط
3. `username ?? "admin"` في 15+ صفحة — تزوير سجل تدقيق
4. **لا Hijri DatePicker** — تواريخ ميلادية فقط في التقارير
5. **POS يفتح وردية تلقائياً** — `PosSessionPage.EnsureShiftAsync`

---



## 8. مراجعة قاعدة البيانات



### 8.1 الكيانات (33 DbSet)

Company, Account, JournalEntry, FiscalPeriod, Customer, SalesInvoice, CreditNote, ReceiptVoucher, Supplier, PurchaseInvoice, PaymentVoucher, Product, Warehouse, StockMovement, InventoryBalance, ZatcaSettings, ZatcaEInvoiceDocument, BankAccount, CostCenter, Cheque, ReconciledBankEntry, PurchaseOrder, ConstructionProject, ConstructionContract, ProgressBilling, SubcontractAgreement, BillOfMaterials, WorkOrder, PosShift, AppUser, AuditLogEntry, FixedAsset, PayrollRun

### 8.2 الفهارس والقيود


| النوع                                                    | الحالة                           |
| -------------------------------------------------------- | -------------------------------- |
| Unique (CompanyId, Code)                                 | ✅ للحسابات، المنتجات، المستودعات |
| Unique (CompanyId, InvoiceNumber)                        | ✅                                |
| Unique (CompanyId, DocumentType, SourceDocumentId) ZATCA | ✅                                |
| Soft delete query filters                                | ✅ 10 كيانات                      |
| FK OnDelete Restrict                                     | ✅ جزئي                           |
| Row-Level Security                                       | ❌                                |
| Check constraints (DB level)                             | ❌ — الاعتماد على Domain فقط      |




### 8.3 استراتيجية المخطط

**مشكلة:** مسار هجين — `EnsureCreated` + `Migrate` + `BrqoshSchemaUpdater` + `SchemaSqlGuard`


| Migration                  | التاريخ        |
| -------------------------- | -------------- |
| InitialCreate              | 20260707000538 |
| AddZatcaCsidFields         | 20260707040000 |
| AddFixedAssetAcquisitionJe | 20260707050000 |
| AddFixedAssetDisposalJe    | 20260707120000 |
| AddSoftDeleteDeletedAtUtc  | 20260707130000 |


**التوصية:** مسار واحد — EF Migrations فقط في Production.

---



## 9. مراجعة واجهات البرمجة

**الحكم: غير موجود.**

- بحث `Controller`, `WebApplication`, `MapGet`, `MapPost` = **0 نتائج**
- لا REST/GraphQL/gRPC
- لا webhooks
- لا تكامل بنكي API
- لا تكامل موارد بشرية (GOSI API)

**التوصية:** ASP.NET Core Minimal API مع:

- `/api/v1/invoices`, `/api/v1/journal-entries`, `/api/v1/reports/trial-balance`
- OAuth 2.0 / API keys
- Rate limiting
- OpenAPI/Swagger

---



## 10. تقييم قابلية التوسع


| البُعد             | الحالي               | 100 مستخدم | 10,000 مستخدم      |
| ------------------ | -------------------- | ---------- | ------------------ |
| Multi-user         | Desktop single       | ❌          | ❌                  |
| Multi-company      | CompanyId واحد       | ❌          | ❌                  |
| Multi-branch       | ❌                    | ❌          | ❌                  |
| Horizontal scaling | ❌                    | ❌          | ❌                  |
| Database           | SQLite dev / PG prod | PG فقط     | PG + read replicas |
| Caching            | ❌                    | Redis      | Redis cluster      |
| Message queue      | ❌                    | RabbitMQ   | Kafka              |


**الحل المثالي:** Web API + PostgreSQL + Redis + multi-tenant schema.

---



## 11. تقييم المخاطر



### مصفوفة المخاطر (احتمال × أثر)


| المعرف | الخطر                        | احتمال | أثر  | الدرجة | المعالجة            |
| ------ | ---------------------------- | ------ | ---- | ------ | ------------------- |
| R-001  | تقارير مالية خاطئة بعد العكس | عالي   | حرج  | **25** | إصلاح queries       |
| R-002  | تسريب صلاحيات                | عالي   | حرج  | **25** | fail-closed         |
| R-003  | ZATCA رفض شهادات             | متوسط  | حرج  | **15** | P-256               |
| R-004  | أرقام مستندات مكررة          | متوسط  | عالي | **12** | تسلسلات ذرية        |
| R-005  | إهلاك مضاعف                  | متوسط  | عالي | **12** | منع تكرار           |
| R-006  | تسريب أسرار DB/ZATCA         | متوسط  | حرج  | **15** | DPAPI               |
| R-007  | عدم امتثال IFRS 15           | مؤكد   | عالي | **15** | إعادة تصميم مقاولات |
| R-008  | لا API = لا تكامل            | مؤكد   | عالي | **15** | بناء API            |


---



## 12. تقرير الديون التقنية


| #     | الدين                                 | الجهد                           | الأولوية |
| ----- | ------------------------------------- | ------------------------------- | -------- |
| TD-01 | Desktop → Infrastructure مباشرة       | 4-6 أسابيع                      | P1       |
| TD-02 | Application layer فارغة               | 3-4 أسابيع                      | P1       |
| TD-03 | Modules.* فارغة                       | 1 أسبوع (حذف) أو 8 أسابيع (ملء) | P2       |
| TD-04 | MessageBox-centric UX                 | 2-3 أسابيع                      | P2       |
| TD-05 | Schema hybrid (EnsureCreated+Migrate) | 1-2 أسابيع                      | P1       |
| TD-06 | 50+ صفحة بلا MVVM                     | 8-12 أسبوع                      | P3       |
| TD-07 | لا API                                | 6-8 أسابيع                      | P1       |
| TD-08 | username ?? "admin" في 15+ صفحة       | 2 أيام                          | P0       |
| TD-09 | EnsureCreated في Production           | 3 أيام                          | P0       |
| TD-10 | لا تغطية اختبار مُقاسة                | 1 أسبوع                         | P2       |


**إجمالي الديون التقنية المقدّر:** ~6-9 أشهر شخص-عمل

---



## 13. الأخطاء الحرجة


| #   | المعرف    | الوصف                   | الملف:السطر                            | الإصلاح                  |
| --- | --------- | ----------------------- | -------------------------------------- | ------------------------ |
| 1   | SEC-001   | fail-open عند null role | `SecurityContext.cs:37-38`             | throw بدل return         |
| 2   | ACC-001   | تقارير تستبعد Reversed  | `TrialBalanceQuery.cs:32`              | point-in-time reporting  |
| 3   | ZATCA-001 | secp256k1 vs P-256      | `ZatcaCsidOnboardingService.cs:36`     | `nistP256`               |
| 4   | ACC-002   | ZATCA خارج TX           | `SalesInvoicePostingService.cs:57-66`  | داخل UnitOfWork          |
| 5   | ACC-003   | إهلاك مكرر              | `DepreciationService.cs:28-42`         | `LastDepreciationPeriod` |
| 6   | SEC-002   | PostCoreAsync بلا فحص   | `SalesInvoicePostingService.cs:75`     | EnsureCanPost داخلي      |
| 7   | ACC-004   | إغلاق فترات بلا صلاحية  | `FiscalPeriodService.cs:11`            | EnsureAdministrator      |
| 8   | ACC-005   | CN POS على 1104         | `CreditNotePostingService.cs:147`      | نسخ حساب الأصل           |
| 9   | ACC-006   | race في الترقيم         | `JournalEntryNumberGenerator.cs`       | SEQUENCE/UPDLOCK         |
| 10  | ACC-007   | إلغاء WO بلا عكس        | `WorkOrderService.cs:91-99`            | خدمة إلغاء كاملة         |
| 11  | ACC-008   | فائض جرد = إيراد        | `StockMovementPostingService.cs:91-98` | حساب حقوق ملكية          |
| 12  | ZATCA-002 | أسرار CSID في DB        | `ZatcaSettings`                        | DPAPI                    |
| 13  | SEC-8S    | 8 خدمات ترحيل بلا `EnsureCanPost*` | انظر [الملحق و](#الملحق-و--تحقيق-الخدمات-الثمانية-بلا-ensurecanpost) | `EnsureCanPost*` + إصلاح SEC-001 |


---



## 14. الأخطاء ذات الأولوية العالية


| #   | الوصف                                  | الملف                                   |
| --- | -------------------------------------- | --------------------------------------- |
| 1   | 8 خدمات ترحيل بلا `EnsureCanPost*`     | انظر الملحق و                           |
| 2   | لا إقفال سنوي                          | غير موجود                               |
| 3   | COGS بـ WAC لحظي لا تكلفة البيع        | `CreditNotePostingService.cs:131`       |
| 4   | IFRS 15 — لا WIP مشاريع                | `ProgressBilling.cs`                    |
| 5   | POS Mixed كله على 1101                 | `SalesInvoicePostingService.cs:223-237` |
| 6   | لا session timeout                     | `AppServices.cs`                        |
| 7   | لا lockout login                       | `LoginWindow.xaml.cs`                   |
| 8   | UserManagement بلا EnsureAdministrator | `UserManagementService.cs`              |
| 9   | سياسة كلمة مرور غير مطبّقة             | `UserManagementService.cs:26`           |
| 10  | BRQOSH_UITEST bypass ترخيص             | `LicenseStore.cs:56`                    |
| 11  | database.json بنص صريح                 | `AppServices.cs:99`                     |
| 12  | ZATCA إلغاء خارج TX                    | `SalesInvoiceCancellationService.cs:76` |
| 13  | VAT بلا تسوية 2107                     | `VatReturnQuery.cs`                     |
| 14  | لا تخصيص دفعات                         | سندات القبض/الصرف                       |
| 15  | إنشاء فترات تلقائي                     | `JournalPostingService.cs:34-39`        |


---



## 15. الأخطاء ذات الأولوية المتوسطة


| #   | الوصف                              |
| --- | ---------------------------------- |
| 1   | ضريبة على مستوى الفاتورة لا السطر  |
| 2   | حسابات تناقصية (1207) كنوع Asset   |
| 3   | `username ?? "admin"` في 15+ صفحة  |
| 4   | SHA-256 legacy مقبول               |
| 5   | Audit غير مكتمل للترحيل            |
| 6   | Schema hybrid                      |
| 7   | لا pagination UI                   |
| 8   | لا Hijri dates                     |
| 9   | RetentionSummary يعتمد parsing نصي |
| 10  | MachineId ترخيص قابل للتنبؤ        |
| 11  | CI بدون SAST/Dependabot            |
| 12  | fallback SQLite صامت               |
| 13  | لا 3-way match مشتريات             |
| 14  | شيك في الحافظة يُخصم فوراً         |
| 15  | لا ContraAsset في الدليل           |


---



## 16. الأخطاء ذات الأولوية المنخفضة


| #   | الوصف                                      |
| --- | ------------------------------------------ |
| 1   | تعليق AppUser يذكر SHA-256 والتنفيذ PBKDF2 |
| 2   | PasswordHash محدود 128 حرف                 |
| 3   | DispatcherUnhandledException يبتلع أخطاء   |
| 4   | لا Row-Level Security                      |
| 5   | فهارس مركبة ناقصة لتقارير                  |
| 6   | لا tolerance 0.01 في التوازن               |
| 7   | Emoji في التنقل                            |
| 8   | FocusVisualStyle=null في DataGrid          |
| 9   | لا تصدير Excel موحّد                       |
| 10  | خط Segoe UI بدل Noto Naskh                 |


---



## 17. الميزات الناقصة



### محاسبة

- [x] إقفال سنوي (Year-end close)
- [x] قيود افتتاحية آلية
- [x] قيود متكررة
- [x] ميزانيات
- [x] تعدد عملات + فروقات صرف
- [x] تخصيص دفعات (payment allocation)
- [x] مطابقة بنكية آلية (bank import CSV/MT940)
- [x] FIFO/LIFO/Standard costing
- [x] Landed cost
- [x] Debit notes



### ERP

- [x] تعدد فروع (BranchId)
- [x] تعدد شركات (multi-tenant)
- [x] Workflow موافقات
- [x] API REST
- [x] تكامل GOSI
- [x] تكامل بنكي
- [x] BI/تقارير تفاعلية
- [x] Workflow engine



### سعودي

- [x] تقويم هجري كامل
- [x] إقرار VAT بصيغة الهيئة
- [x] Reverse charge
- [x] تصدير/استيراد بضائع
- [x] WHT (ضريبة استقطاع)

---



## 18. منطق الأعمال الخاطئ


| #   | السيناريو                          | السلوك الحالي               | المتوقع             |
| --- | ---------------------------------- | --------------------------- | ------------------- |
| 1   | إلغاء فاتورة POS نقدية بإشعار دائن | يُقيد على 1104 عملاء        | نقدية 1101          |
| 2   | POS دفع مختلط                      | كله على 1101 صندوق          | تفصيل نقد/بطاقة     |
| 3   | فائض جرد                           | إيراد 4106                  | حقوق ملكية/مخزون    |
| 4   | إلغاء أمر إنتاج                    | بلا عكس مواد                | عكس كامل            |
| 5   | شيك في الحافظة                     | يُخصم من رصيد العميل        | سياسة قابلة للتكوين |
| 6   | محتجز باطن                         | يُعامل كمدفوع في كشف المورد | إظهار منفصل         |
| 7   | إفراج محتجز                        | بتاريخ Today                | تاريخ القيد الفعلي  |


---



## 19. المنطق المحاسبي الخاطئ


| #   | الخلل                    | المعيار المخالف         | البرهان                       |
| --- | ------------------------ | ----------------------- | ----------------------------- |
| 1   | تقارير تاريخية بعد العكس | SOCPA/IFRS presentation | سيناريو يناير/فبراير أعلاه    |
| 2   | إيراد مقاولات كامل       | IFRS 15.39              | `ProgressBilling.Post`        |
| 3   | فائض جرد كإيراد          | IAS 2.34                | `StockMovementPostingService` |
| 4   | لا إقفال حسابات مؤقتة    | SOCPA closing           | غير موجود                     |
| 5   | COGS عكس بـ WAC لاحق     | IAS 2 matching          | `CreditNotePostingService`    |
| 6   | مجمع إهلاك كنوع Asset    | SOCPA presentation      | `SaudiChartOfAccounts.cs:48`  |
| 7   | ضريبة على المحتجز        | ZATCA policy            | غير موثّق                     |


---



## 20. المتطلبات السعودية الناقصة


| المتطلب            | الحالة                | الأولوية |
| ------------------ | --------------------- | -------- |
| ZATCA Phase 2 كامل | 70%                   | P0       |
| ض.ق.م 15%          | 85%                   | —        |
| دليل حسابات سعودي  | 90%                   | —        |
| تقويم هجري         | 5% (ar-SA header فقط) | P1       |
| RTL                | 95%                   | —        |
| SOCPA إقفال سنوي   | 0%                    | P0       |
| GOSI رواتب         | 60% (قيد أساسي)       | P1       |
| شيكات سعودية       | 80%                   | —        |
| محتجزات مقاولات    | 75%                   | P1       |
| فروع متعددة        | 0% (وعد SetupWizard)  | P2       |
| إقرار VAT رسمي     | 30%                   | P1       |


---



## 21. نقاط ضعف واجهة المستخدم

1. MessageBox في 50+ موضع — لا toast/strip
2. لا loading overlay منهجي
3. لا empty states في الجداول
4. لا keyboard shortcuts
5. لا TabIndex منهجي
6. لا dark mode
7. Emoji في التنقل بدل SVG
8. `Take(100)` صامت بدون pagination UI
9. Viewer يرى أزرار كتابة
10. تواريخ `yyyy-MM-dd` vs `ar-SA` في الهيدر
11. لا Hijri DatePicker
12. Accessibility ضعيف (3 AutomationProperties)
13. POS لا barcode Enter shortcut
14. لا تصدير Excel موحّد

---



## 22. اختناقات الأداء


| الموضع                        | المشكلة                  | الحل                   |
| ----------------------------- | ------------------------ | ---------------------- |
| `GeneralLedgerQuery`          | تحميل كل القيود السابقة  | pagination + DB cursor |
| `JournalEntryNumberGenerator` | MAX+1 بلا lock           | SEQUENCE               |
| `StockCountPage`              | كل الأصناف في الذاكرة    | virtual scroll         |
| `DashboardPage`               | استعلامات متعددة متسلسلة | parallel + cache       |
| لا connection pooling config  | افتراضي EF               | تكوين صريح             |
| SQLite في dev                 | انحراف عن prod           | PG في dev أيضاً        |


---



## 23. الثغرات الأمنية

(انظر القسم 5 للتفصيل — ملخص)


| المعرف    | OWASP | الشدة | الحالة |
| --------- | ----- | ----- | ------ |
| SEC-001   | A01   | حرج   | مفتوح  |
| SEC-002   | A01   | حرج   | مفتوح  |
| SEC-003   | A02   | حرج   | مفتوح  |
| SEC-004   | A02   | حرج   | مفتوح  |
| SEC-005   | A07   | عالي  | مفتوح  |
| SEC-006   | A07   | عالي  | مفتوح  |
| SEC-007   | A01   | عالي  | مفتوح  |
| SEC-008   | A07   | عالي  | مفتوح  |
| SEC-009   | A04   | عالي  | مفتوح  |
| SEC-010   | A02   | عالي  | مفتوح  |
| ZATCA-001 | A02   | حرج   | مفتوح  |
| ZATCA-002 | A04   | متوسط | مفتوح  |


---



## 24. رائحة الكود


| #   | الرائحة                                               | الموضع              |
| --- | ----------------------------------------------------- | ------------------- |
| 1   | God class — `BrqoshDbContext` 500+ سطر                | Persistence         |
| 2   | Static service locator                                | `AppServices`       |
| 3   | `username ?? "admin"`                                 | 15+ Page            |
| 4   | تكرار نمط ToolbarCard+DataGrid                        | 42 صفحة             |
| 5   | `new Service(db)` في كل صفحة                          | coupling            |
| 6   | Magic strings لأكواد الحسابات                         | "1104", "4101"      |
| 7   | `catch (AccountingException) { throw; }` عديم الفائدة | SalesInvoicePosting |
| 8   | Modules فارغة                                         | Solution clutter    |
| 9   | تعليقات عربية + إنجليزية مختلطة                       | عام                 |
| 10  | لا interfaces للخدمات                                 | testability         |


---



## 25. فرص إعادة الهيكلة


| #   | الفرصة                                   | الأثر       | الجهد    |
| --- | ---------------------------------------- | ----------- | -------- |
| 1   | `IAccountCodeResolver` بدل magic strings | صيانة       | 1 أسبوع  |
| 2   | `IPostingService<T>` موحّد               | اتساق       | 2 أسابيع |
| 3   | Application layer use-cases              | فصل طبقات   | 4 أسابيع |
| 4   | MVVM للصفحات الرئيسية                    | testability | 8 أسابيع |
| 5   | Domain Events للترحيل                    | تدقيق       | 2 أسابيع |
| 6   | `CostingStrategy` interface              | FIFO/WAC    | 3 أسابيع |
| 7   | `IReportQuery` مع caching                | أداء        | 2 أسابيع |
| 8   | NotificationService بدل MessageBox       | UX          | 1 أسبوع  |


---



## 26. الهندسة المعمارية الموصى بها

```
┌─────────────────────────────────────────────────┐
│  Brqosh.Api          (ASP.NET Core Minimal API) │
│  Brqosh.Desktop      (WPF — يستهلك Api أو App)  │
├─────────────────────────────────────────────────┤
│  Brqosh.Application  (Use Cases / Commands)      │
│    ├── Commands: PostInvoice, ClosePeriod...     │
│    ├── Queries: TrialBalance, GL...              │
│    └── Interfaces: IPostingService, IReport...   │
├─────────────────────────────────────────────────┤
│  Brqosh.Domain       (Entities, Value Objects)   │
├─────────────────────────────────────────────────┤
│  Brqosh.Infrastructure                           │
│    ├── Persistence (EF Core + Migrations)      │
│    ├── Zatca, Reporting, Security                │
│    └── External (Bank, GOSI)                     │
├─────────────────────────────────────────────────┤
│  Brqosh.Modules.*    (Construction, Mfg, Retail) │
│    └── كل وحدة: Domain + Application + Infra     │
└─────────────────────────────────────────────────┘
         PostgreSQL  │  Redis  │  RabbitMQ
```

---



## 27. خطة إعادة البناء



### المرحلة 0 — إيقاف النزيف (أسبوع 1-2)

- [x] SEC-001: fail-closed
- [x] ACC-004: EnsureAdministrator على FiscalPeriod
- [x] TD-08: إزالة `username ?? "admin"`
- [x] ZATCA-001: P-256
- [x] EnsureCanPost على كل خدمات الترحيل



### المرحلة 1 — سلامة محاسبية (أسبوع 3-6)

- [x] ACC-001: إصلاح queries التقارير
- [x] ACC-003: منع إهلاك مكرر
- [x] ACC-006: تسلسلات ذرية
- [x] ACC-002: ZATCA داخل TX
- [x] ACC-005: CN POS حساب صحيح
- [x] Year-end close service



### المرحلة 2 — أمان وامتثال (أسبوع 7-10)

- [x] DPAPI لأسرار ZATCA/DB
- [x] Session timeout + lockout
- [x] Audit logging كامل
- [x] CI: CodeQL + Dependabot + PG job
- [x] ZATCA live testing



### المرحلة 3 — معمارية (أسبوع 11-18)

- [ ] Application layer
- [ ] API REST v1
- [ ] DI container
- [ ] إزالة/ملء Modules.*



### المرحلة 4 — ERP مؤسسي (أسبوع 19-36)

- [x] IFRS 15 construction
- [x] FIFO costing
- [x] Multi-branch
- [x] Bank import
- [x] Hijri dates
- [x] UX overhaul (toast, loading, keyboard)

---



## 28. خارطة طريق التطوير


| الربع   | الهدف                   | المخرجات              |
| ------- | ----------------------- | --------------------- |
| Q3 2026 | إغلاق P0                | أمان + تقارير + ZATCA |
| Q4 2026 | API + Application layer | REST v1, DI           |
| Q1 2027 | IFRS 15 + FIFO          | مقاولات + مخزون       |
| Q2 2027 | Multi-branch + Hijri    | ERP سعودي كامل        |
| Q3 2027 | UX polish + BI          | واجهة مؤسسية          |
| Q4 2027 | Load testing + pen test | جاهزية Production     |


---



## 29. استراتيجية الاختبار



### الحالي

- 133 اختبار (38 Domain + 95 Application)
- 0 فشل
- لا قياس تغطية
- لا UI tests في CI
- لا load tests
- لا security tests



### المستهدف


| الطبقة         | الأداة                          | التغطية المستهدفة |
| -------------- | ------------------------------- | ----------------- |
| Domain         | xUnit                           | 90%+              |
| Application    | xUnit + Moq                     | 85%+              |
| Infrastructure | Integration (Testcontainers PG) | 75%+              |
| API            | WebApplicationFactory           | 80%+              |
| UI             | FlaUI (موجود جزئياً)            | مسارات حرجة       |
| ZATCA          | Golden files + live OTP         | 100% مسارات       |
| Security       | OWASP ZAP                       | سنوي              |
| Performance    | k6 / NBomber                    | 1M records        |


---



## 30. خطة اختبار الانحدار



### مجموعات إلزامية قبل كل إصدار

1. **دورة مبيعات كاملة:** عميل → فاتورة → ترحيل → ZATCA → إشعار دائن → إلغاء
2. **دورة مشتريات:** مورد → PO → فاتورة → ترحيل → سند صرف
3. **مخزون:** استلام → بيع → WAC → جرد → تسوية
4. **مقاولات:** مشروع → عقد → مستخلص → محتجز → إفراج
5. **رواتب:** دورة → ترحيل → عكس
6. **أصول:** اقتناء → إهلاك → استبعاد
7. **تقارير:** TB → P&L → BS → توازن
8. **أمان:** 4 أدوار × 42 شاشة × عمليات كتابة
9. **ZATCA:** توليد → توقيع → إرسال → إلغاء
10. **فترات:** ترحيل → إغلاق → محاولة ترحيل → رفض

---



## 31. خطة النشر



### الحالي

- MSI installer (`installer/brqosh.wxs`)
- SQLite dev / PostgreSQL prod
- CI: build + test فقط



### المستهدف

1. **CI/CD:** GitHub Actions → build → test → SAST → sign → MSI
2. **بيئات:** Dev (SQLite) → Staging (PG) → Prod (PG)
3. **نسخ احتياطي:** pg_dump مجدول + اختبار restore شهري
4. **ترقية:** EF Migrations فقط — لا EnsureCreated
5. **مراقبة:** Application Insights / Serilog
6. **توقيع:** Code signing certificate للـ MSI

---



## 32. استراتيجية الصيانة


| النشاط                     | التكرار           | المسؤول          |
| -------------------------- | ----------------- | ---------------- |
| تحديث تبعيات NuGet         | أسبوعي            | DevOps           |
| مراجعة أمنية               | ربع سنوي          | Security         |
| اختبار ZATCA live          | عند كل تحديث هيئة | ZATCA specialist |
| نسخ احتياطي + restore test | شهري              | Ops              |
| مراجعة دليل حسابات         | سنوي              | محاسب            |
| تحديث CEEF compliance      | ربع سنوي          | Architect        |


---



## 33. خطة التوسع المستقبلية



### قصير المدى (6 أشهر)

- Web API + PostgreSQL
- 10-50 مستخدم (terminal server)



### متوسط المدى (12 شهر)

- Multi-branch
- SaaS multi-tenant
- Mobile app (قراءة تقارير)



### طويل المدى (24 شهر)

- 1000+ مستخدم
- Microservices للوحدات الثقيلة (ZATCA, Reporting)
- AI: تصنيف قيود، كشف احتيال، توقع تدفقات

---



## الملحق أ — مصفوفة القيود المحاسبية


| المستند           | مدين                                | دائن                                     | الملف                                          |
| ----------------- | ----------------------------------- | ---------------------------------------- | ---------------------------------------------- |
| فاتورة بيع        | 1104/1101 = Total                   | 4101 = Sub, 2106 = VAT, 5101/1107 = COGS | `SalesInvoice.cs:104-115`                      |
| فاتورة شراء       | 5102/1107 = Sub, 1111 = VAT         | 2101 = Total                             | `PurchaseInvoice.cs:105-131`                   |
| إشعار دائن        | 4102 = Sub, 2106 = VAT, 1107 = COGS | 1104 = Total, 5101 = COGS                | `CreditNote.cs:93-104`                         |
| سند قبض           | صندوق/بنك                           | 1104                                     | `ReceiptVoucher.cs:99-101`                     |
| سند صرف           | 2101                                | صندوق/بنك                                | `PaymentVoucher.cs:87-89`                      |
| مستخلص            | 1104, 1114                          | 4104, 2106                               | `ProgressBilling.cs:94-100`                    |
| رواتب             | 5201, 5203                          | 2108, 2109                               | `PayrollRun.cs:102-108`                        |
| شيك وارد (استلام) | 1103                                | 1104                                     | `Cheque.cs`                                    |
| شيك صادر          | 2101                                | 2103                                     | `Cheque.cs`                                    |
| اقتناء أصل        | أصل                                 | 1101                                     | `FixedAssetAcquisitionPostingService.cs:27`    |
| إهلاك             | 5211                                | 1207                                     | `DepreciationService.cs:36-37`                 |
| فائض جرد          | 1107                                | 4106 ⚠️                                  | `StockMovementPostingService.cs:91-98`         |
| نقص جرد           | 5215                                | 1107                                     | `StockMovementPostingService.cs:100-114`       |
| WIP إنتاج         | 1113/1107                           | 1107/1113                                | `WorkOrderService.cs:64-71`                    |
| إفراج محتجز عميل  | 1104                                | 1114                                     | `RetentionReleaseService.cs:48-56`             |
| محتجز باطن        | 2101                                | 2110                                     | `SubcontractRetentionWithholdService.cs:39-40` |


---



## الملحق ب — جرد الخدمات



### خدمات الترحيل — حالة الأمان


| الخدمة                              | EnsureCan*   | UnitOfWork | Audit |
| ----------------------------------- | ------------ | ---------- | ----- |
| JournalPostingService               | ✅ Accounting | ✅          | ❌     |
| SalesInvoicePostingService          | ✅ Post       | ✅          | ❌     |
| PurchaseInvoicePostingService       | ✅ Post       | ✅          | ❌     |
| StockMovementPostingService         | ✅ Post       | ✅          | ❌     |
| PayrollPostingService               | ✅ Accounting | ✅          | ❌     |
| ChequePostingService                | ✅ Accounting | ✅          | ❌     |
| ReceiptVoucherPostingService        | ❌            | ✅          | ❌     |
| PaymentVoucherPostingService        | ❌            | ✅          | ❌     |
| CreditNotePostingService            | ❌            | ✅          | ❌     |
| ProgressBillingPostingService       | ❌            | ✅          | ❌     |
| FixedAssetAcquisitionPostingService | ❌            | ✅          | ❌     |
| FixedAssetDisposalPostingService    | ❌            | ✅          | ❌     |
| DepreciationService                 | ❌            | ✅          | ✅     |
| RetentionReleaseService             | ❌            | ✅          | ❌     |
| SubcontractRetentionWithholdService | ❌            | ✅          | ❌     |
| WorkOrderService                    | ❌            | ✅          | ❌     |




### خدمات الإلغاء


| الخدمة                             | EnsureCan*   | عكس قيد | عكس مخزون |
| ---------------------------------- | ------------ | ------- | --------- |
| SalesInvoiceCancellationService    | ✅            | ✅       | ✅         |
| PurchaseInvoiceCancellationService | ✅            | ✅       | ✅         |
| CreditNoteCancellationService      | ✅            | ✅       | ⚠️        |
| ProgressBillingCancellationService | ✅            | ✅       | —         |
| StockMovementCancellationService   | ✅            | ✅       | ✅         |
| VoucherReversalService             | ✅            | ✅       | —         |
| PayrollReversalService             | ✅            | ✅       | —         |
| JournalReversalService             | ✅ Accounting | ✅       | —         |


---

## الملحق و — تحقيق الخدمات الثمانية بلا EnsureCanPost*

> **نطاق هذا الملحق:** تحقيق مُستقل لكل خدمة من الثماني خدمات ترحيل تفتقر إلى `SecurityContext.EnsureCanPost()` أو `EnsureCanPostAccounting()`.  
> **تاريخ التحقق:** 8 يوليو 2026 — مراجعة الكود المصدري + مسارات الاستدعاء + الاختبارات.

### ملخص تنفيذي للملحق

| المقياس | القيمة |
|---------|--------|
| خدمات ترحيل إجمالية في Infrastructure | 22 |
| محمية بـ `EnsureCanPost*` | 14 (64%) |
| **بلا `EnsureCanPost*` (هذا الملحق)** | **8 (36%)** |
| اختبارات أمنية تغطي هذه الثمانية | **0** (`SecurityContextPostingTests` يختبر فاتورة مبيعات فقط) |
| شاشات تستدعيها مباشرة | 8 حوارات/صفحات |
| تكامل ZATCA خارج المعاملة | 2 من 8 (`CreditNote`, `ProgressBilling`) |

### مصفوفة المقارنة السريعة

| # | الخدمة | الفحص المطلوب | `EnsureCanPost*` | UnitOfWork | Audit | ZATCA خارج TX | اختبار أمني |
|---|--------|---------------|------------------|------------|-------|---------------|-------------|
| 1 | `CreditNotePostingService` | `EnsureCanPost` | ❌ | ✅ | ❌ | ✅ | ❌ |
| 2 | `ReceiptVoucherPostingService` | `EnsureCanPost` | ❌ | ✅ | ❌ | — | ❌ |
| 3 | `PaymentVoucherPostingService` | `EnsureCanPost` | ❌ | ✅ | ❌ | — | ❌ |
| 4 | `ProgressBillingPostingService` | `EnsureCanPost` | ❌ | ✅ | ❌ | ✅ | ❌ |
| 5 | `FixedAssetAcquisitionPostingService` | `EnsureCanPostAccounting` | ❌ | ✅ | ❌ | — | ❌ |
| 6 | `FixedAssetDisposalPostingService` | `EnsureCanPostAccounting` | ❌ | ✅ | ✅ | — | ❌ |
| 7 | `RetentionReleaseService` | `EnsureCanPostAccounting` | ❌ | ✅ | ✅ | — | ❌ |
| 8 | `SubcontractRetentionWithholdService` | `EnsureCanPostAccounting` | ❌ | ✅ | ✅ | — | ❌ |

---

### SEC-8S-01 — `CreditNotePostingService`

| الحقل | القيمة |
|-------|--------|
| **الملف** | `src/Brqosh.Infrastructure/Sales/CreditNotePostingService.cs` |
| **نقطة الدخول** | `PostAsync` (سطر 35)، `PostCoreAsync` داخلي (سطر 69) — `internal` قابل للاستدعاء من نفس التجميع |
| **الفحص الموجود** | `JournalPostingService.EnsurePeriodOpenAsync` (سطر 75) — **فترة مالية فقط** |
| **الفحص الناقص** | `SecurityContext.EnsureCanPost()` في بداية `PostAsync` |

#### القيود المحاسبية المُولَّدة

```
Dr 4102 (مردودات مبيعات)     = Subtotal
Dr 2106 (ضريبة مخرجات)       = VatAmount
Cr 1104 (عملاء)              = GrandTotal
Dr 1107 (مخزون)              = cogsReversal  [إن وُجد]
Cr 5101 (تكلفة مبيعات)       = cogsReversal
```

**برهان التوازن:** `Subtotal + Vat = GrandTotal` → Dr = Cr ✅

#### مسار الاستدعاء

| الطبقة | الملف:السطر | ملاحظة |
|--------|-------------|--------|
| UI | `CreditNoteDialog.xaml.cs:92` | `await new CreditNotePostingService(db).PostAsync(note)` |
| صلاحية شاشة | `PermissionService` → `sales_returns` | Admin + Accountant فقط |
| فحص داخل الصفحة | ❌ | لا `PermissionService` داخل الحوار |

#### سيناريوهات الاستغلال

1. **`CurrentRole == null` (SEC-001):** الاستدعاء المباشر للخدمة ينجح — لا فحص.
2. **Viewer يصل للخدمة عبر أداة/اختبار/API مستقبلي:** الترحيل ينجح رغم أن الإلغاء (`CreditNoteCancellationService`) ممنوع له — **تناقض أمني**.
3. **`PostCoreAsync` داخلي:** أي كود في نفس التجميع يمكنه استدعاءه بلا `EnsureCanPost`.

#### خلل محاسبي إضافي (مستقل عن الصلاحيات)

| السطر | المشكلة | الخطورة |
|-------|---------|---------|
| 147 | دائماً `AccountsReceivable` (1104) حتى لفواتير POS نقدية | عالي |
| 131 | COGS بـ `WeightedAverageCost` الحالي لا تكلفة البيع الأصلية | عالي |
| 51-59 | ZATCA بعد `Commit` — خارج `UnitOfWork` | عالي |

#### الاختبارات الموجودة (بلا فحص أمني)

`CreditNoteAndReceiptTests.cs` (6 استدعاءات)، `PurchasingCycleTests.cs:157`, `ZatcaTests.cs:139`, `SalesInvoiceCancellationTests.cs:89` — **كلها تستدعي `PostAsync` بلا تعيين `SecurityContext.CurrentRole`**.

#### الإصلاح الموصى به

```csharp
public async Task<CreditNote> PostAsync(CreditNote note)
{
    SecurityContext.EnsureCanPost();  // ← إضافة هنا
    await UnitOfWork.ExecuteInTransactionAsync(db, async ct => { ... });
    // نقل ZATCA داخل المعاملة أو تعويض عند الفشل
}
```

---

### SEC-8S-02 — `ReceiptVoucherPostingService`

| الحقل | القيمة |
|-------|--------|
| **الملف** | `src/Brqosh.Infrastructure/Treasury/ReceiptVoucherPostingService.cs` |
| **نقطة الدخول** | `PostAsync` (سطر 14) |
| **الفحص الناقص** | `SecurityContext.EnsureCanPost()` |

#### القيود المحاسبية

```
Dr صندوق/بنك (1101/1102 أو حساب بنك فرعي)  = Amount
Cr 1104 (عملاء)                            = Amount
```

**المصدر:** `ReceiptVoucher.Post` — `ReceiptVoucher.cs:87-101`

#### مسار الاستدعاء

| الطبقة | الملف:السطر |
|--------|-------------|
| UI | `ReceiptVoucherDialog.xaml.cs:90` |
| صلاحية شاشة | `receipt_vouchers` → Admin + Accountant |

#### سيناريوهات الاستغلال

1. **Cashier** ممنوع من شاشة سندات القبض في UI — لكن استدعاء مباشر للخدمة يرحّل سند قبض ويُخفّض ذمة عميل.
2. **Viewer** يمكنه تحصيل نقدي وهمي عبر الخدمة مباشرة.
3. **لا تخصيص لفاتورة:** السند يُخفّض رصيد العميل الإجمالي بلا ربط بفاتورة محددة — خطر احتيال داخلي.

#### خلل تشغيلي إضافي

| السطر | المشكلة |
|-------|---------|
| 49-53 | `ReceiptMethod.Bank` بدون `bankAccountId` يُحل إلى 1102 عام — لا حساب بنك فرعي إلزامي |

#### الاختبارات

`SubLedgerAccuracyTests.cs:116`, `CreditNoteAndReceiptTests.cs` (3 استدعاءات) — بلا `SecurityContext`.

#### الإصلاح

```csharp
public async Task<ReceiptVoucher> PostAsync(ReceiptVoucher voucher) =>
    await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
    {
        SecurityContext.EnsureCanPost();  // ← أول سطر داخل المعاملة
        ...
    });
```

---

### SEC-8S-03 — `PaymentVoucherPostingService`

| الحقل | القيمة |
|-------|--------|
| **الملف** | `src/Brqosh.Infrastructure/Treasury/PaymentVoucherPostingService.cs` |
| **نقطة الدخول** | `PostAsync` (سطر 14) |
| **الفحص الناقص** | `SecurityContext.EnsureCanPost()` |

#### القيود المحاسبية

```
Dr 2101 (موردون)              = Amount
Cr صندوق/بنك                  = Amount
```

**المصدر:** `PaymentVoucher.Post` — `PaymentVoucher.cs:87-89`

#### مسار الاستدعاء

| الطبقة | الملف:السطر |
|--------|-------------|
| UI | `PaymentVoucherDialog.xaml.cs:90` |
| صلاحية شاشة | `payment_vouchers` → Admin + Accountant |

#### سيناريوهات الاستغلال

1. **صرف نقدي غير مصرّح:** أي مستدعٍ مباشر يُنشئ سند صرف ويُخفّض النقدية.
2. **Cashier/Viewer:** يمكنهما الصرف عبر الخدمة رغم منعهما من الشاشة.
3. **لا موافقة workflow:** صرف فوري بلا مستويات موافقة (مقارنة SAP/Odoo).

#### الاختبارات

`PurchasingCycleTests.cs:130` — بلا `SecurityContext`.

#### الإصلاح

نفس نمط `ReceiptVoucherPostingService` — `EnsureCanPost()` في بداية المعاملة.

---

### SEC-8S-04 — `ProgressBillingPostingService`

| الحقل | القيمة |
|-------|--------|
| **الملف** | `src/Brqosh.Infrastructure/Construction/ProgressBillingPostingService.cs` |
| **نقطة الدخول** | `PostAsync` (سطر 13) |
| **الفحص الناقص** | `SecurityContext.EnsureCanPost()` |

#### القيود المحاسبية

```
Dr 1104 (عملاء)               = NetBillable + VatAmount
Dr 1114 (محتجزات مستحقة)      = RetentionAmount
Cr 4104 (إيراد عقود)          = CurrentWorkAmount
Cr 2106 (ضريبة مخرجات)        = VatAmount
```

**المصدر:** `ProgressBilling.Post` — `ProgressBilling.cs:94-100`

#### مسار الاستدعاء

| الطبقة | الملف:السطر |
|--------|-------------|
| UI | `ProgressBillingDialog.xaml.cs:78` |
| صلاحية شاشة | `progress_billings` → Admin + Accountant |

#### سيناريوهات الاستغلال

1. ترحيل مستخلص بقيمة كاملة بلا صلاحية — يُثبت إيراداً على 4104 فوراً (مخالف IFRS 15).
2. **ZATCA خارج TX** (سطر 43-52): مستخلص مرحّل محاسبياً لكن فاتورة إلكترونية قد تفشل.

#### خلل محاسبي إضافي

| المشكلة | الخطورة |
|---------|---------|
| إيراد كامل دفعةً بلا تكاليف مشروع/WIP | حرج (IFRS 15) |
| ضريبة على المحتجز | متوسط — يحتاج توثيق سياسة |

#### الاختبارات

`ProgressBillingVatTests.cs`, `ConstructionCycleTests.cs` (2), `RetentionReleaseTests.cs:35`, `CreditNoteAndReceiptTests.cs:157` — بلا أمن.

#### الإصلاح

```csharp
public async Task<ProgressBilling> PostAsync(ProgressBilling billing, CancellationToken ct = default)
{
    SecurityContext.EnsureCanPost();
    var posted = await UnitOfWork.ExecuteInTransactionAsync(db, async token => { ... }, ct);
    // ZATCA داخل TX أو saga
    return posted;
}
```

---

### SEC-8S-05 — `FixedAssetAcquisitionPostingService`

| الحقل | القيمة |
|-------|--------|
| **الملف** | `src/Brqosh.Infrastructure/Assets/FixedAssetAcquisitionPostingService.cs` |
| **نقطة الدخول** | `PostAcquisitionAsync` (سطر 12) |
| **الفحص الناقص** | `SecurityContext.EnsureCanPostAccounting()` — عملية محاسبية متقدمة |

#### القيود المحاسبية

```
Dr حساب الأصل (120x)          = AcquisitionCost
Cr 1101 (صندوق)               = AcquisitionCost
```

**سطر 27-28:** دائماً صندوق نقدي — لا بنك/آجل.

#### مسار الاستدعاء

| الطبقة | الملف:السطر |
|--------|-------------|
| UI | `FixedAssetDialog.xaml.cs:103` |
| صلاحية شاشة | `fixed_assets` → Admin + Accountant |

#### سيناريوهات الاستغلال

1. **Cashier** يمكنه تسجيل أصول بملايين عبر استدعاء مباشر — يُخفّض الصندوق ويُضخّم الأصول.
2. **Viewer** يمكنه إنشاء قيود اقتناء — تلاعب بالميزانية.

#### الاختبارات

`FixedAssetAcquisitionTests.cs:57`, `FixedAssetDisposalTests.cs:51` — بلا أمن.

#### الإصلاح

```csharp
SecurityContext.EnsureCanPostAccounting();  // أول سطر في PostAcquisitionAsync
```

---

### SEC-8S-06 — `FixedAssetDisposalPostingService`

| الحقل | القيمة |
|-------|--------|
| **الملف** | `src/Brqosh.Infrastructure/Assets/FixedAssetDisposalPostingService.cs` |
| **نقطة الدخول** | `PostDisposalAsync` (سطر 13) |
| **الفحص الناقص** | `SecurityContext.EnsureCanPostAccounting()` |
| **ملاحظة** | يستورد `Brqosh.Infrastructure.Security` لـ `AuditService` فقط — **ليس** لـ `SecurityContext` |

#### القيود المحاسبية

```
Dr 1207 (مجمع إهلاك)          = AccumulatedDepreciation  [إن > 0]
Dr 1101 (صندوق)               = saleProceeds             [إن > 0]
Cr 4106 (إيرادات أخرى)        = gain                     [إن ربح]
Dr 5215 (مصروف متنوع)         = |loss|                    [إن خسارة]
Cr حساب الأصل                 = AcquisitionCost
```

**سطر 43-51:** قيد استبعاد كامل مع ربح/خسارة.

#### مسار الاستدعاء

| الطبقة | الملف:السطر |
|--------|-------------|
| UI | `FixedAssetsPage.xaml.cs:106` |
| Audit | ✅ `AuditService.LogAsync` (سطر 62-63) |

#### سيناريوهات الاستغلال

1. استبعاد أصل بعائد وهمي — تضخيم إيرادات 4106.
2. **Cashier** يمكنه استبعاد أصول — خطر عالي على الميزانية.

#### الاختبارات

`FixedAssetDisposalTests.cs:60` — بلا أمن.

#### الإصلاح

`EnsureCanPostAccounting()` + الاحتفاظ بـ `AuditService`.

---

### SEC-8S-07 — `RetentionReleaseService`

| الحقل | القيمة |
|-------|--------|
| **الملف** | `src/Brqosh.Infrastructure/Construction/RetentionReleaseService.cs` |
| **نقطة الدخول** | `ReleaseAsync` (سطر 20) |
| **الفحص الناقص** | `SecurityContext.EnsureCanPostAccounting()` |

#### القيود المحاسبية — إفراج عميل

```
Dr 1104 (عملاء)               = amount
Cr 1114 (محتجزات مستحقة)      = amount
```

#### القيود — إفراج مقاول باطن

```
Dr 2110 (محتجزات باطن)        = amount
Cr 2101 (موردون)              = amount
```

**سطر 44-57**

#### ضوابط أعمال موجودة (جيدة — لا تُعوّض الصلاحيات)

| الضابط | السطر |
|--------|-------|
| `amount > 0` | 26-27 |
| لا يتجاوز المحتجز المتاح | 29-32 (`RetentionSummaryQuery`) |
| فترة مفتوحة | 37 |
| Audit | 64-65 |

#### مسار الاستدعاء

| الطبقة | الملف:السطر |
|--------|-------------|
| UI | `RetentionsPage.xaml.cs:60` |
| صلاحية شاشة | `retentions` → Admin + Accountant |

#### سيناريوهات الاستغلال

1. إفراج محتجز بمبلغ ضمن المتاح — **الضوابط المحاسبية تمنع التجاوز** لكن **لا تمنع مستخدماً غير مخوّل** من التنفيذ.
2. **`RetentionSummaryQuery` هش** — يعتمد parsing `SourceDocument` نصياً (مشكلة تكامل منفصلة).

#### الاختبارات

`RetentionReleaseTests.cs:37`, `SubcontractRetentionWithholdTests.cs:98` — بلا أمن.

---

### SEC-8S-08 — `SubcontractRetentionWithholdService`

| الحقل | القيمة |
|-------|--------|
| **الملف** | `src/Brqosh.Infrastructure/Construction/SubcontractRetentionWithholdService.cs` |
| **نقطة الدخول** | `WithholdAsync` (سطر 13) |
| **الفحص الناقص** | `SecurityContext.EnsureCanPostAccounting()` |

#### القيود المحاسبية

```
Dr 2101 (موردون)              = amount
Cr 2110 (محتجزات باطن)        = amount
```

**سطر 39-40**

#### ضوابط أعمال موجودة

| الضابط | السطر |
|--------|-------|
| `amount > 0` | 18-19 |
| عقد باطن `Active` فقط | 24-25 |
| فترة مفتوحة | 27 |
| Audit | 47-48 |

#### مسار الاستدعاء

| الطبقة | الملف:السطر |
|--------|-------------|
| UI | `SubcontractorsPage.xaml.cs:85` |
| صلاحية شاشة | `subcontractors` → Admin + Accountant |

#### سيناريوهات الاستغلال

1. تسجيل محتجز باطن يُخفّض ذمة المورد فوراً — **Cashier/Viewer** يمكنهما التنفيذ عبر الخدمة.
2. يؤثر على `SupplierBalanceQuery` — يُعامل المحتجز كـ «مدفوع» (مقصود لكن خطير بلا صلاحيات).

#### الاختبارات

`SubcontractRetentionWithholdTests.cs` (2), `SubLedgerAccuracyTests.cs:172` — بلا أمن.

---

### خلاصة الملحق و — خطة إصلاح موحّدة

#### الأولوية P0 (يوم واحد)

أضف سطراً واحداً في بداية كل خدمة:

| الخدمة | السطر المطلوب |
|--------|---------------|
| `CreditNotePostingService.PostAsync` | `SecurityContext.EnsureCanPost();` |
| `ReceiptVoucherPostingService` (داخل TX) | `SecurityContext.EnsureCanPost();` |
| `PaymentVoucherPostingService` (داخل TX) | `SecurityContext.EnsureCanPost();` |
| `ProgressBillingPostingService.PostAsync` | `SecurityContext.EnsureCanPost();` |
| `FixedAssetAcquisitionPostingService` | `SecurityContext.EnsureCanPostAccounting();` |
| `FixedAssetDisposalPostingService` | `SecurityContext.EnsureCanPostAccounting();` |
| `RetentionReleaseService` | `SecurityContext.EnsureCanPostAccounting();` |
| `SubcontractRetentionWithholdService` | `SecurityContext.EnsureCanPostAccounting();` |

#### الأولوية P1 (أسبوع) — اختبارات أمنية إلزامية

أضف في `SecurityContextPostingTests.cs` — **8 اختبارات جديدة** (واحد لكل خدمة):

```csharp
[Theory]
[InlineData(typeof(CreditNotePostingService), ...)]
// ...
public async Task Viewer_CannotPost_ServiceX() { ... }
```

#### الأولوية P0 — إصلاح SEC-001 أولاً

حتى مع إضافة `EnsureCanPost*`، **`CurrentRole == null` يتجاوز الفحص**. يجب إصلاح `SecurityContext.cs:37-38` إلى `throw` في Production **قبل** أو **مع** إصلاح الثمانية.

#### التوصية المعمارية

استخراج `PostingGuard` موحّد:

```csharp
internal static class PostingGuard
{
    public static void EnsureOperationalPost() => SecurityContext.EnsureCanPost();
    public static void EnsureAccountingPost() => SecurityContext.EnsureCanPostAccounting();
}
```

يُستدعى من **كل** خدمة ترحيل — مع اختبار تكامل يفحص القائمة كاملة عبر reflection.

---


42 مفتاح في `NavigationCatalog.cs` — 4 أدوار:


| الدور         | عدد الشاشات المسموحة | نسبة |
| ------------- | -------------------- | ---- |
| Administrator | 42                   | 100% |
| Accountant    | ~38                  | 90%  |
| Cashier       | 5                    | 12%  |
| Viewer        | 6                    | 14%  |


**فحص صلاحيات داخلي في الصفحات:** 0/50  
**فحص عند التنقل:** 1/1 (`MainWindow.xaml.cs:94`)  
**فحص `EnsureCanPost*` في خدمات الترحيل:** 14/22 محمية — **8/22 بلا فحص** (36%)

---



## الملحق د — مصفوفة الاختبارات


| المجال                      | ملفات الاختبار                   | عدد تقريبي |
| --------------------------- | -------------------------------- | ---------- |
| Domain — JournalEntry       | `JournalEntryTests.cs`           | 9          |
| Domain — Chart              | `SaudiChartOfAccountsTests.cs`   | 6          |
| Domain — Lifecycle          | `EntityLifecycleTests.cs`        | 5          |
| Domain — SoftDelete         | `SoftDeleteTests.cs`             | 3          |
| Application — Sales         | 4 ملفات                          | ~19        |
| Application — Purchasing    | 2 ملف                            | ~8         |
| Application — Inventory     | 5 ملفات                          | ~9         |
| Application — Construction  | 4 ملفات                          | ~7         |
| Application — ZATCA         | 4 ملفات                          | ~12        |
| Application — Reporting     | 4 ملفات                          | ~9         |
| Application — Security      | `SecurityContextPostingTests.cs` | 2          |
| Application — Persistence   | 4 ملفات                          | ~9         |
| Application — Assets        | 3 ملفات                          | ~4         |
| Application — Treasury      | `ChequeTests.cs`                 | 2          |
| Application — Manufacturing | `ManufacturingTests.cs`          | 3          |
| Application — Retail/POS    | `PosZatcaTests.cs`               | 1          |
| Application — Licensing     | `LicensingTests.cs`              | 3          |
| **المجموع**                 | **44 ملف**                       | **133**    |




### فجوات تغطية الاختبار

- [ ] لا اختبار أمني للخدمات الثمانية بلا `EnsureCanPost*` (الملحق و)
- [ ] `SecurityContextPostingTests` يغطي فاتورة مبيعات فقط (2 اختبار) — لا CN/RV/PV/PB/أصول/محتجزات
- [ ] لا اختبار لتقارير بعد العكس (ACC-001)
- [ ] لا اختبار لإهلاك مكرر
- [ ] لا اختبار لـ race في الترقيم
- [ ] لا اختبار لـ fail-open security (`CurrentRole == null`)
- [ ] لا UI tests في CI
- [ ] لا load tests
- [ ] ZATCA live معطّل افتراضياً

---



## الملحق هـ — سجل المخاطر


| ID    | الوصف                  | الاحتمال | الأثر | التخفيف       | المالك       | الحالة |
| ----- | ---------------------- | -------- | ----- | ------------- | ------------ | ------ |
| R-001 | تقارير خاطئة بعد العكس | 5        | 5     | إصلاح queries | Backend      | مفتوح  |
| R-002 | تجاوز صلاحيات — 8 خدمات بلا EnsureCanPost* | 5        | 5     | الملحق و + fail-closed | Security     | مفتوح  |
| R-003 | ZATCA شهادات مرفوضة    | 3        | 5     | P-256         | ZATCA        | مفتوح  |
| R-004 | أرقام مكررة            | 3        | 4     | SEQUENCE      | Backend      | مفتوح  |
| R-005 | إهلاك مضاعف            | 3        | 4     | LastDepPeriod | Backend      | مفتوح  |
| R-006 | تسريب أسرار            | 3        | 5     | DPAPI         | Security     | مفتوح  |
| R-007 | IFRS 15 غير ممتثل      | 5        | 4     | إعادة تصميم   | Domain       | مفتوح  |
| R-008 | لا تكامل خارجي         | 5        | 4     | API           | Architecture | مفتوح  |
| R-009 | فقدان بيانات SQLite    | 2        | 5     | PG إلزامي     | Ops          | جزئي   |
| R-010 | عدم امتثال VAT         | 3        | 4     | إقرار كامل    | Compliance   | مفتوح  |


---



## خاتمة

هذا التقرير يمثل **أعمق مستوى تدقيق ممكن من الكود المصدري** دون تشغيل على بيئة إنتاج حقيقية أو penetration test خارجي. النظام يمتلك **أساساً محاسبياً صلباً في مبدأ القيد المزدوج** لكنه **غير جاهز للإنتاج المؤسسي** بسبب ثغرات حوكمة وأمان وامتثال تمنع اعتماده دون إعادة هيكلة جوهرية تقدر بـ **9-12 شهراً**.

**الخطوة التالية الموصى بها:** البدء بـ P0 (القسم 13 + المرحلة 0 من القسم 27) — أسبوعان لإيقاف النزيف.

---

*تم إعداد هذا التقرير وفق منهجية CEEF v1.0 — «الحقيقة قبل الإرضاء».*