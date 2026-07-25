# CEEF-028 — ترجمة إقفال + تقارير موحّدة موسّعة (O-08)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | سعر متوسط/إقفال · CTA 3106 · قائمة دخل/مركز مالي موحّدان · API |
| **التاريخ** | 2026-07-08 |
| **الحكم** | **تعميق O-08 مُثبت** — O-08 **ليست** مغلقة بالكامل؛ O-01…O-07 و O-09…O-13 كما هي |

---

## 1. ما أُنجز

### أسعار FX — متوسط وإقفال

| المكوّن | الملف |
|---------|-------|
| `FxRateKind` | Closing · Average |
| `GetAverageRateAsync` | متوسط الفترة أو (افتتاح+إقفال)/2 |
| `ConsolidatedFxRateResolver` | حل موحّد لكل التقارير الموحّدة |

### ترجمة إقفال (CTA)

| المكوّن | الملف |
|---------|-------|
| `FxTranslationCalculator` | CTA = صافي الدخل × (إقفال − متوسط) |
| `FxClosingTranslationService` | قيد ترحيل إلى `3106` احتياطي ترجمة العملات |
| حساب نظامي `3106` | `SaudiChartOfAccounts` + إنشاء تلقائي |

### تقارير موحّدة موسّعة

| التقرير | سعر FX | الملف |
|---------|--------|-------|
| ميزان مراجعة موحّد | إقفال | `ConsolidatedTrialBalanceQuery` (محدّث) |
| قائمة دخل موحّدة | **متوسط** | `ConsolidatedIncomeStatementQuery` |
| مركز مالي موحّد | **إقفال** + CTA توازن | `ConsolidatedBalanceSheetQuery` |

### API

- `GET .../consolidated-income-statement`
- `GET .../consolidated-balance-sheet`
- `POST /api/v1/companies/{id}/fx-closing-translation`
- حماية `TenantGuard` على المجموعات

---

## 2. أدلة تشغيل

| الفحص | النتيجة |
|--------|---------|
| Domain | **49/49 PASS** |
| Application | **157/157 PASS** (`BRQOSH_SKIP_STRESS=1` · `BRQOSH_TEST=1`) |
| Api | **19/19 PASS** |
| **المجموع** | **225/225 PASS** |
| FullScenarioAudit | **13/13 PASS** |
| Desktop Release | **0** أخطاء |
| Security static | **PASS** |

---

## 3. ما لم يُغلق — صريح

| البند | السبب |
|-------|--------|
| O-01…O-05 | OTP · SOCPA · MSI · 1M · ZAP حي |
| O-06 | coverlet ≥75% على كل assembly |
| O-08 كامل | إزالة معاملات بين الشركات · خرائط حسابات · فروق محققة تلقائية · مصادر أسعار حية |
| O-09…O-13 | SaaS/billing · Cloud · BI · LLM · Mobile |
| Desktop UI للتقارير الموحّدة | يمكن CEEF-029 |

---

## 4. الحكم

| الادعاء | الحكم |
|---------|--------|
| ترجمة إقفال CTA + تقارير موحّدة P&L/BS | ✅ داخل المستودع |
| O-08 مغلقة بالكامل | ❌ |
| O-01…O-06 · O-09…O-13 مغلقة | ❌ |

**التالي:** O-01…O-05 (سوق) **أو** إزالة معاملات بين الشركات + واجهة Desktop للتقارير الموحّدة.
