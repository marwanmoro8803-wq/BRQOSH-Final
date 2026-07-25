# CEEF-029 — إزالة معاملات بين الشركات + Desktop موحّد (O-08)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | Intercompany elimination · تقارير موحّدة Desktop · مجموعات شركات |
| **التاريخ** | 2026-07-08 |
| **الحكم** | **تعميق O-08 مُثبت** — O-08 **ليست** مغلقة بالكامل؛ O-01…O-05 و O-09…O-13 كما هي |

---

## 1. ما أُنجز

### إزالة معاملات بين الشركات (Intercompany Elimination)

| المكوّن | الملف |
|---------|-------|
| `JournalEntry.IsIntercompany` · `CounterpartyCompanyId` · `MarkIntercompany()` | `JournalEntry.cs` |
| حساب الإزالة وتطبيقها على التقارير | `IntercompanyEliminationHelper.cs` |
| تقرير إزالة مستقل | `IntercompanyEliminationQuery` |
| خصم الإزالة من TB/P&L/BS موحّد | `ConsolidatedTrialBalanceQuery` · `ConsolidatedIncomeStatementQuery` · `ConsolidatedBalanceSheetQuery` |
| أعمدة EF + ترحيل SQLite/PG | `BrqoshDbContext` · `BrqoshSchemaUpdater` |
| API | `GET .../intercompany-eliminations` |
| `CreateAdditionalCompanyAsync` (اختبارات/مجموعات) | `DatabaseInitializer.cs` |

### واجهة Desktop — تقارير موحّدة

| الصفحة | الغرض |
|--------|--------|
| `ConsolidatedTrialBalancePage` | ميزان مراجعة موحّد |
| `ConsolidatedIncomeStatementPage` | قائمة دخل موحّدة |
| `ConsolidatedBalanceSheetPage` | مركز مالي موحّد |
| `CompanyGroupsPage` | إنشاء/عرض مجموعات الشركات |
| `CompanySettingsPage` | ربط الشركة بمجموعة |
| `ConsolidatedReportPageHelper` | تحميل المجموعات المشتركة |
| `NavigationCatalog` + `PermissionService` | تنقل وصلاحيات `company_groups` · `consolidated_*` |

### إصلاح تشغيلي

| المشكلة | الحل |
|---------|------|
| `SchemaSqlGuard` يرفض `INTEGER NOT NULL DEFAULT 0` | إضافة النوع لقائمة SQLite المسموحة |

---

## 2. أدلة تشغيل

| الفحص | النتيجة |
|--------|---------|
| Domain | **51/51 PASS** |
| Application | **158/158 PASS** (`BRQOSH_SKIP_STRESS=1` · `BRQOSH_TEST=1`) |
| Api | **19/19 PASS** |
| **المجموع** | **228/228 PASS** |
| FullScenarioAudit | **13/13 PASS** |
| Desktop Release | **0** أخطاء |
| Security static | **PASS** |
| Coverlet مجمّع | **80.64%** ≥ 75% |

### اختبارات جديدة

- `IntercompanyEliminationTests` — إزالة مزدوجة بين شركتين في مجموعة
- `JournalEntryIntercompanyTests` — `MarkIntercompany()` على القيد

---

## 3. ما لم يُغلق — صريح

| البند | السبب |
|-------|--------|
| O-01…O-05 | OTP · SOCPA · MSI VM · 1M load · ZAP حي |
| O-06 | coverlet ≥75% على كل assembly |
| O-08 كامل | واجهة تحديد قيود IC في `JournalEntryDialog` · خرائط حسابات IC · مصادر أسعار FX حية · فروق محققة تلقائية |
| O-09…O-13 | SaaS/billing · Cloud · BI · LLM · Mobile |

---

## 4. الحكم

| الادعاء | الحكم |
|---------|--------|
| إزالة معاملات بين الشركات في التقارير الموحّدة | ✅ داخل المستودع |
| Desktop لعرض التقارير الموحّدة ومجموعات الشركات | ✅ داخل المستودع |
| O-08 مغلقة بالكامل | ❌ |
| O-01…O-06 · O-09…O-13 مغلقة | ❌ |

**التالي:** تحضير بوابات O-01…O-05 (`CEEF-030`) — الاعتمادات السوقية تبقى خارجية حتى الأدلة الحية.
