# CEEF-011 — التحقق من «تحليل شامل للبرنامج» والتنفيذ على BRQOSH

> **التاريخ**: 8 يوليو 2026  
> **المدخل**: `تحليل-شامل-للبرنامج.md` (جذر المستودع)  
> **المنهجية**: تحقق حرفي من كل نقطة مقابل الكود الفعلي في `c:\Users\qc\Projects\BRQOSH`، ثم تنفيذ ما ينطبق  
> **نتيجة الاختبارات بعد التنفيذ**: `132/132` ناجح (`dotnet test -c Release`)

---

## 1. الحكم الأول — خطأ جوهري في المستهدف

التقرير المُدخل **لا يصف المشروع الموجود في هذا المستودع**.

| ما يقوله التقرير | الواقع في المستودع الحالي |
|---|---|
| Python 3 + FastAPI | **.NET 9** (C#) |
| Frontend: `app.js` SPA (2092 سطر) | **WPF Desktop** (`Brqosh.Desktop`) |
| المسار: `Desktop\برنامج المحاسبه\...\BRQOSH_ERP` | `C:\Users\qc\Projects\BRQOSH` |
| 537 ملف Python، 201 اختبار pytest | **14 مشروع .csproj**، **132 اختبار xUnit** |
| `brqosh_erp/web_ui/`, `sessionStorage`, JWT | **لا يوجد** — تسجيل دخول WPF محلي عبر `UserAuthService` |
| `page_size: 1000` في JavaScript | استعلامات EF Core في C# (`Take(100)`–`Take(500)` سابقاً) |

**الاستنتاج الصريح**: 100% من الأدلة المذكورة (`app.js:84`، `transactions.py`، إلخ) **غير قابلة للتحقق هنا** لأن الملفات غير موجودة. التقرير يبدو مُعدّاً لنسخة ويب قديمة/منفصلة من BRQOSH، وليس لنسخة WPF الحالية.

**ما يفعله هذا المستند**: يعيد تحليل كل بند **كمكافئ وظيفي** في BRQOSH Desktop، مع حالة التحقق والتنفيذ.

---

## 2. البنية الفعلية لـ BRQOSH (مرجع التحقق)

```
Brqosh.Desktop (WPF)
    ↓ يستدعي مباشرة
Brqosh.Infrastructure (Orchestrators, Queries, ZATCA, EF Core)
    ↓
Brqosh.Domain (كيانات، قواعد محاسبية)
    ↓
PostgreSQL / SQLite
```

- **لا يوجد** API HTTP عام للواجهة (تطبيق سطح مكتب).
- **RBAC**: `PermissionService` + `SecurityContext` على مستوى الخدمات.
- **ZATCA**: `ZatcaEInvoiceGenerator`, `ZatcaXmlSigner`, `ZatcaSettingsPage`.
- **اختبارات**: `Brqosh.Domain.Tests` (38) + `Brqosh.Application.Tests` (94).

---

## 3. التحقق نقطة بنقطة — الشاشات الـ 16

### 3.1 تسجيل الدخول

| بند التقرير | الحالة في BRQOSH | الدليل |
|---|---|---|
| JWT في sessionStorage | **غير منطبق** | `LoginWindow.xaml.cs` — مصادقة محلية، لا JWT |
| CSRF | **غير منطبق** | لا HTTP SPA |
| لا «تذكرني» | **صحيح جزئياً** | جلسة `AppServices.CurrentUser` حتى إغلاق التطبيق |
| لا «نسيت كلمة المرور» | **صحيح** | غير موجود |
| إجبار تغيير كلمة المرور | **مُنفَّذ سابقاً** | `MustChangePasswordOnNextLogin` + `PasswordChangeWindow` |

### 3.2 لوحة التحكم (Dashboard)

| بند التقرير | الحالة | الدليل |
|---|---|---|
| 6 طلبات + 4 تقارير ثقيلة كل تحميل | **غير موجود** | `DashboardPage` يستدعي `DashboardQuery.ExecuteAsync` **مرة واحدة** |
| تناقض KPI نقدي | **غير مثبت** | مصدر واحد للبيانات |
| JSON خام للمستخدم | **غير موجود** | بطاقات KPI فقط |
| لا skeleton loading | **صحيح** | تحسين UX مستقبلي (P2) |
| معالجة خطأ | **مُحسَّن** | `try/catch` يعرض رسالة في البطاقة |

### 3.3 العملاء

| بند التقرير | الحالة | الدليل / الإجراء |
|---|---|---|
| إعادة تحميل 200 عميل عند كل حفظ | **مختلف** | كان `Take(500)` كامل — **أُصلِح**: بحث على الخادم + `Take(200)` + debounce 300ms |
| لا تحقق VAT في الواجهة | **كان صحيحاً** | Domain يتحقق — **أُضيف** تحقق مبكر في `CustomerDialog` |
| credit_limit سالب | **غير منطبق** | لا حقل `credit_limit` في نموذج العميل الحالي |
| بحث بدون debounce | **كان صحيحاً** | **أُصلِح** بـ `SearchDebouncer` |

### 3.4 الموردون

| بند التقرير | الحالة |
|---|---|
| لا contact_person/category | **صحيح** — الحقول غير موجودة في `Supplier` |
| لا ربط مباشر بفاتورة شراء من شاشة المورد | **صحيح** — يُنشأ من `PurchaseOrdersPage` |

### 3.5 المنتجات

| بند التقرير | الحالة | الإجراء |
|---|---|---|
| `page_size: 1000` | **مكافئ**: كان `Take(500)` | **أُصلِح**: `Take(200)` + فلترة DB + debounce |
| سعر بيع سالب | **مُعالَج في Domain** | `Product.Create` يرفض `salePrice < 0` |
| لا purchase_cost مباشر | **صحيح** | `WeightedAverageCost` من حركات المخزون |
| لا صورة/barcode | **barcode موجود**؛ الصورة غير موجودة |

### 3.6 المخزون

| بند التقرير | الحالة |
|---|---|
| لا API مخزون — دمج client-side | **غير منطبق** | `inventory_balances` + `StockBalanceQuery` + `StockMovementsPage` |
| balance_known: false | **غير منطبق** | أرصدة من جدول `InventoryBalance` |
| reorder_level على products | **غير منطبق** | لا حقل reorder في النموذج الحالي |
| لا سجل حركات من الشاشة | **خطأ في التقرير الأصلي** | `StockMovementsPage` موجود |

### 3.7 فواتير المبيعات

| بند التقرير | الحالة |
|---|---|
| POST دائماً بدون UPDATE مسودة | **مختلف** | `SalesInvoiceDialog` ينشئ ويرحّل مباشرة — لا مسودة منفصلة في الواجهة |
| حذف بلا تأكيد | **غير صحيح هنا** | الإلغاء يتطلب تأكيد + `SalesInvoiceCancellationService` |
| customer اختياري | **غير صحيح** | `SalesInvoiceDialog` يفرض اختيار عميل |
| لا مقارنة سعر بالمنتج | **مفتوح P2** | auto-fill موجود؛ تنبيه انحراف السعر لم يُنفَّذ بعد |
| لا VAT تلقائي في السطر | **غير صحيح** | `SalesInvoice.SaudiVatRate` يُحسب في `UpdateTotals` |

### 3.8 فواتير الشراء

| بند التقرير | الحالة |
|---|---|
| raw_material_name مهدر | **غير منطبق** |
| لا ربط بأمر شراء | **غير صحيح** | `PurchaseOrdersPage` → إنشاء فاتورة من أمر |
| مورد مطلوب | **مُطبَّق** في `PurchaseInvoiceDialog` وDomain |

### 3.9 الشيكات

| بند التقرير | الحالة | الإجراء |
|---|---|---|
| actor_username من client | **مختلف** | `AppServices.CurrentUser` بعد login — ليس حقل HTTP قابل للتزوير بنفس سهولة SPA |
| currency حر | **غير منطبق** | عملة الشركة افتراضياً |
| لا فلتر due_date / ترقيم | **جزئي** | `Take(200)` — فلترة تاريخية P2 |
| **تاريخ العمليات تلقائي اليوم** | **كان صحيحاً** | **أُصلِح**: `ChequesPage` يطلب تاريخاً عبر `DateInputDialog` |

### 3.10 الحضور

| بند | الحالة |
|---|---|
| كل البنود | **غير منطبق** — لا وحدة حضور في BRQOSH WPF |

### 3.11 الموافقات

| بند | الحالة |
|---|---|
| كل البنود | **غير منطبق** — لا workflow موافقات |

### 3.12 المحاسبة / القيود

| بند التقرير | الحالة | الإجراء |
|---|---|---|
| **عكس القيد بتاريخ اليوم تلقائياً** | **كان صحيحاً** | **أُصلِح** في `JournalEntriesPage` + سندات قبض/صرف + رواتب + إلغاءات |
| 3 API متوازية | **غير منطبق** | صفحة واحدة EF |
| integrity أول 5 فقط | **غير منطبق** |
| لا قيد يدوي | **غير صحيح** | `JournalEntryDialog` موجود |
| قيود المصدر لا تُعكس مباشرة | **مُطبَّق** | `JournalReversalService` يمنع عكس قيود `SourceDocument` |

### 3.13 ربط الحسابات

| بند | الحالة |
|---|---|
| PUT كامل للجدول | **غير منطبق** — `SaudiChartOfAccounts` + `ChartOfAccountsPage` |
| role_key | **غير منطبق** |

### 3.14 التقارير المالية

| بند التقرير | الحالة |
|---|---|
| 4 تقارير كل زيارة بدون cache | **مختلف** | كل تقرير يُحمَّل عند الطلب مع **فلترة تاريخ** (`FromDate`/`ToDate`) |
| لا PDF/Excel | **جزئي** | PDF لفواتير المبيعات؛ التقارير العامة P2 |
| دائماً كل التاريخ | **غير صحيح** | `TrialBalancePage`, `GeneralLedgerPage`, إلخ تدعم الفترة |
| bs.balanced بدون فارق | **P2** — عرض الفارق اختياري |

### 3.15 ZATCA

| بند التقرير | الحالة |
|---|---|
| dry_run افتراضي checked | **غير منطبق** | لا dry_run في WPF — بيئة Simulation/Production في `ZatcaSettingsPage` |
| response_payload قابل للتعديل | **غير منطبق** |
| invoice_id حر | **غير منطبق** — التوليد من فاتورة مرحّلة |
| لا إعادة تهيئة | **غير صحيح** | `ZatcaCsidWizardWindow` موجود |
| XAdES / C14N / Phase2 QR | **قيد العمل** | `ZatcaXmlSigner` + حزمة `System.Security.Cryptography.Xml` — **بوابة إنتاج ZATCA ما زالت مغلقة** |

### 3.16 الإدارة

| بند التقرير | الحالة |
|---|---|
| base_currency حر | **غير منطبق** — ريال سعودي ضمني |
| لا تعديل شركة | **غير صحيح** | `CompanySettingsPage` يعدّل الاسم والرقم الضريبي |
| تغيير سياق بلا تأكيد | **غير منطبق** — شركة واحدة لكل قاعدة بيانات محلية |

---

## 4. التحليل العام — إعادة التقييم

### 4.1 المعمارية

| نقطة التقرير | BRQOSH |
|---|---|
| Frontend monolith 2000 سطر JS | **لا ينطبق** — عشرات صفحات XAML منفصلة |
| تكرار operations*.py | **لا ينطبق** |
| Repository pattern | **EF Core DbContext + خدمات تطبيق** |
| RBAC Backend | **نعم** — `SecurityContext` + أدوار `AppUser` |

### 4.2 الاختبارات

| التقرير | BRQOSH |
|---|---|
| 201 ملف guard tests | 94 تطبيق + 38 domain — **اختبارات سلوكية** (ZATCA، مخزون، WAC، إلغاءات) |
| لا E2E | **صحيح** — `UiAudit`/`FullScenarioAudit` أدوات يدوية/شبه آلية |
| لا frontend tests | **صحيح** — لا اختبارات WPF آلية |

### 4.3 الأمان

| التقرير | BRQOSH |
|---|---|
| JWT/XSS | **غير منطبق** |
| Rate limiting login | **غير منطبق** (سطح مكتب) |
| actor_username | **أقل خطورة** — جلسة محلية؛ التحسين المستقبلي: استخراج الاسم داخل الخدمة من `SecurityContext` |
| SQL Injection | **محمي** — EF parameterized |

### 4.4 الأداء

| التقرير | BRQOSH بعد CEEF-011 |
|---|---|
| page_size 1000 | خُفِّض إلى 200 مع بحث DB |
| debounce | **مُنفَّذ** للعملاء والمنتجات |
| تقارير dashboard ثقيلة | **غير موجود** — استعلام KPI واحد |

---

## 5. التوصيات الست العاجلة — مصيرها في BRQOSH

| # | توصية التقرير | BRQOSH |
|---|---|---|
| 1 | JWT → httpOnly cookie | **N/A** |
| 2 | إزالة page_size 1000 | **مُعالَج** (منتجات/عملاء) |
| 3 | إصلاح openAttendance(index) | **N/A** (لا حضور) |
| 4 | **إجبار reversal_date** | **✅ مُنفَّذ** — `DateInputDialog` + `UiDialogs.PromptAccountingDate` |
| 5 | actor_username من الخلفية | **جزئي** — الجلسة محلية؛ RBAC على الخدمات |
| 6 | dry_run ZATCA unchecked | **N/A** — لا dry_run |

---

## 6. ما نُفِّذ في هذه الجولة (كود)

| الملف | التغيير |
|---|---|
| `DateInputDialog.cs` | حوار اختيار تاريخ محاسبي |
| `UiDialogs.cs` | `PromptAccountingDate` |
| `JournalEntriesPage`, `PayrollPage` | تاريخ عكس يدوي |
| `SalesInvoicesPage`, `PurchaseInvoicesPage`, `CreditNotesPage`, `ProgressBillingsPage`, `StockMovementsPage` | تاريخ إلغاء يدوي |
| `ReceiptVouchersPage`, `PaymentVouchersPage` | تاريخ عكس سند |
| `ChequesPage` | تاريخ إيداع/تحصيل/ارتجاع/إلغاء |
| `CustomersPage`, `ProductsPage` | debounce + فلترة DB + حد 200 |
| `CustomerDialog` | تحقق VAT مبكر |
| `Brqosh.Infrastructure.csproj` | `System.Security.Cryptography.Xml` لإصلاح بناء ZATCA |
| `ZatcaQrGenerator.cs` | alias `GenerateTlvBase64` → Phase1 |

---

## 7. الفجوات المتبقية (صريحة)

| الأولوية | البند |
|---|---|
| **P0** | ZATCA إنتاج: XAdES كامل، QR Phase2 بعد التوقيع، CSID OTP حي، UBL BillingReference |
| **P1** | RBAC على كل خدمات الإلغاء (جزئي اليوم) |
| **P1** | UiAudit = 0 فشل (غير مُثبت) |
| **P1** | ترخيص PEM / MSI على VM نظيفة |
| **P2** | ترقيم صفحات كامل لكل القوائم |
| **P2** | تنبيه انحراف سعر البيع في `SalesInvoiceDialog` |
| **P2** | skeleton loading، كاش تقارير |
| **P2** | نسيت كلمة المرور، E2E آلية |

---

## 8. التقييم المُحدَّث

| المحور | تقرير المُدخل (ويب) | BRQOSH WPF (بعد CEEF-011) |
|---|---|---|
| Backend | قوي نسبياً | **قوي** — Domain + معاملات + اختبارات |
| Frontend | **نقطة ضعف حرجة** | **متوسط–جيد** — WPF منظم؛ تحسينات UX متبقية |
| ZATCA | مخاطر dry_run | **محاكاة جاهزة**؛ **إنتاج غير مفتوح** |
| جاهزية إنتاج | غير جاهز (6 بنود عاجلة) | **تجريبي محاسبي** — بوابة إنتاج ZATCA + تدقيق UI مغلقة |

**الدرجة التقريبية لـ BRQOSH WPF**: **~68/100** (كانت ~66 قبل إصلاح التواريخ والأداء).

---

## 9. توصية المجلس

1. **أرشفة أو تصحيح** `تحليل-شامل-للبرنامج.md` — إما نقله لمجلد «نسخة ويب قديمة» أو إعادة كتابته على أساس `Brqosh.Desktop`.
2. **عدم** تطبيق بنود JWT/CSRF/app.js على هذا المستودع.
3. **متابعة** P0 ZATCA وP1 RBAC/UiAudit كما في CEEF-009/010.
4. **الاحتفاظ** بإصلاح تاريخ العكس/الإلغاء — كان مطابقاً لخلل حقيقي في WPF وليس فقط في التقرير الويب.

---

*تم التحقق من المستودع `C:\Users\qc\Projects\BRQOSH` — لا يوجد `brqosh_erp/web_ui/app.js`.*
