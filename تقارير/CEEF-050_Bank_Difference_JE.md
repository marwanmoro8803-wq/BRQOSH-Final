# CEEF-050 — V2/N1: فروقات بنكية → قيد محاسبي

| الحقل | القيمة |
|-------|--------|
| **النطاق** | إغلاق M4.7 · LIFECYCLE-68 |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **N1 ✅ مغلق (أدنى)** |

---

## 1. ما أُغلق

| # | البند | الدليل |
|---|-------|--------|
| N1.1 | `BankStatementDifferencePostingService` | JE متوازن + match + reconcile |
| N1.2 | `IBankStatementDifferencePostingUseCase` | DI adapter |
| N1.3 | offset افتراضي | 5213 رسوم · 4106 إيرادات أخرى |
| N1.4 | اختبارات | +2 (PostDifference · DefaultOffsetCode) |
| N1.5 | UI | زر «قيد فروق» · اختيار سطر معلّق |

### الكود
- `Brqosh.Infrastructure/Treasury/BankStatementDifferencePostingService.cs`
- `BankReconciliationPage` — `PostDifferenceBtn`
- `SaudiChartOfAccounts.SystemCodes.BankCharges` · `OtherIncome`

---

## 2. سلوك

1. سطر كشف **غير مطابق** → قيد يومية Draft
2. إيداع (+): مدين بنك · دائن 4106
3. سحب/رسوم (−): مدين 5213 · دائن بنك
4. ترحيل → `Match` على السطر → `ReconcileAsync`
5. `DocumentPostedEvent` + `JournalEntryPostedEvent`

---

## 3. القياس

| المشروع | PASS |
|---------|------|
| Domain | 58 |
| Application | 179 (+2) |
| Api | 26 |
| Desktop | 12 |
| **المجموع** | **275** |

Release · `BRQOSH_SKIP_STRESS=1` · coverlet ≥75%

---

## 4. مراجع

- [`docs/archive/roadmaps/EXECUTION_ROADMAP_V2.md`](../docs/archive/roadmaps/EXECUTION_ROADMAP_V2.md) — N1 ✅
- V1 M4.7 — مُحدَّث في `EXECUTION_ROADMAP.md`
