# CEEF-140 — محاسبة · ZATCA · الترحيل

| الحقل | القيمة |
|-------|--------|
| **النطاق** | محور E + F |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **Financial core قوي in-repo** · **امتثال حي ⏭** |

## درجة المحور

| المحور | الدرجة |
|--------|--------|
| GL integrity / Golden paths | **A−** |
| Posting policy | **A−** |
| ZATCA in-repo | **B+** |
| ZATCA production | **⏭** (O-01) |
| Payroll/GOSI integration | **C** (حسابات فقط) |
| Lifecycle depth (100 steps) | **C+** (~41% ✅ · ~37% 🔶 · ~17% 🔜) |

## نقاط قوة مُثبتة

### Golden integrity (8 facts)

`tests/Brqosh.Application.Tests/Accounting/GoldenAccountingIntegrityTests.cs` — 8 `[Fact]` tests covering debit=credit, reversal, period close.

### FullScenarioAudit (تشغيل حي)

`run-external-gates.ps1` → FullScenarioAudit **13/13 PASS** (سجل في `external-gates.log`).

### Posting policy

`SaudiPostingPolicy` + `PostingOrchestrator` — `PostingPolicyTests` PASS.

### GOSI — محاسبة فقط لا API

```63:63:src/Brqosh.Domain/Accounting/SaudiChartOfAccounts.cs
new("2109", "التأمينات الاجتماعية المستحقة", "GOSI Payable", ...
```

`PayrollPostingService` يرحّل GOSI — **لا** تكامل GOSI/Mudad/Qiwa/WPS API (grep: صفر Mudad/WPS/Qiwa في `src/`).

## فجوات lifecycle مُثبتة (grep صفر)

| القدرة | دليل الغياب |
|--------|-------------|
| Accrual | `grep Accrual` → 0 في `src/` |
| Batch post | `grep BatchPost` → 0 |
| Serial/lot | `grep SerialLot` → 0 |
| Stock reservation | `grep StockReservation` → 0 |

**المرجع:** `dist/evidence/CEEF-138-supreme-audit/lifecycle-gap-matrix.csv`

## ZATCA

| المستوى | الحالة | الدليل |
|---------|--------|--------|
| محاكاة + CSID wizard | ✅ كود | `ZatcaTests` · `ZatcaCsidWizardWindow` |
| إنتاج OTP | ⏭ | `zatca-live-attempt.log`: OTP unset |
| 10 فواتير حية | ⏭ | `dist/evidence/O-01-zatca/` فارغ |

## Gulf VAT (G-11…15)

```16:21:src/Brqosh.Domain/Tax/VatJurisdictionProfile.cs
new(TaxJurisdiction.SaudiArabia, "SA", "SAR", 0.15m, true, "ZATCA"),
new(TaxJurisdiction.UnitedArabEmirates, "AE", "AED", 0.05m, false, "FTA"),
// ... EInvoicingInRepo: false for non-SA
```

**مصفوفة VAT ✅** · **e-invoicing حي ⏭** لكل دولة غير SA.

## 4 مستويات

| المستوى | الحالة |
|---------|--------|
| ✅ كود | Golden + posting + ZATCA sim |
| ✅ اختبار | Golden 8/8 · Zatca suite |
| ⏭ حي | O-01 OTP |
| ⏭ خارجي | GOSI API · WPS · FTA |
