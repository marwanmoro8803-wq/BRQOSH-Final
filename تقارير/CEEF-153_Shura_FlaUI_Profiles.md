# CEEF-153 — Shura FlaUI Profiles + ProgramKnowledge Wave

| الحقل | القيمة |
|-------|--------|
| **التاريخ** | 2026-07-11 |
| **السلطة** | council-20260711-115604.json · RAI-SHURA-CEEF-150 D5/D6 |
| **النطاق** | P1 in-repo — FlaUI profiles · UiAudit metrics · ProgramKnowledge |

---

## 1. أوامر الشورى — الحالة

| # | الأمر | الحالة | الدليل |
|---|-------|--------|--------|
| P0 | O-01 ZATCA OTP حي | ⏭ EXTERNAL | `dist/evidence/O-01-*/` |
| P0 | API SecurityContext · rate limits · print guard | ✅ CEEF-151 | 38 Api tests |
| P0 | G-31/G-32 MVP | ✅ CEEF-151 | Desktop.Tests |
| P1 | FlaUI Construction + Retail | ✅ هذا الموجة | `BRQOSH_AUDIT_ALL_PROFILES=1` · seeds |
| P1 | UiAudit metric fix (SKIP≠FAIL) | ✅ | `UiAuditResult` · ShuraCouncil parser |
| P1 | ProgramKnowledge توسعة | ✅ | +14 شاشة · Construction/Retail/POS |
| P2 | G-27 XAML sweep (1365 سطر) | ⏭ موجة لاحقة | `i18n-coverage-audit.ps1` |

---

## 2. ما نُفّذ

### UiAudit (D5)
- `ProfileAuditContext` — ملف عمل من `BRQOSH_AUDIT_PROFILE`
- `NavKeyModuleMap` — فلترة صفحات حسب `ModuleCatalog`
- `UiAuditResult` — `[SKIP]` لا يُحسب فشلاً؛ ملخص جديد للشورى
- `ResetNavScrollToTop` — إصلاح فقدان أزرار التنقل أثناء FlaUI
- `NavKeyMap` — `customer_support` · `pricing_plans`
- مرجع `Brqosh.Domain` لـ `BusinessProfile`/`ModuleCatalog`

### FullScenarioAudit
- بذور **Construction**: مشروع QA-AUDIT + BOQ
- بذور **Retail**: وردية POS مفتوحة

### ProgramKnowledge (CEEF-152 توسعة)
- 14 صفحة إضافية: projects · pos_session · chart_of_accounts · purchase_invoices · suppliers · income_statement · balance_sheet · company_settings · crm_leads · bom · work_orders · purchase_orders · bank_reconciliation · cash_flow_statement

### ShuraCouncil
- `EvidenceLoader` — يقرأ تنسيق SKIP الجديد

---

## 3. الاختبارات

| المقياس | قبل | بعد |
|---------|-----|-----|
| الإجمالي | 491 | **497** (+6) |
| Application | 271 | **275** (+4) |
| Desktop | 118 | **120** (+2) |

```powershell
dotnet test Brqosh.sln -c Release --nologo  # 497/497 PASS
```

---

## 4. ⏭ خارج النطاق

| البند | السبب |
|-------|-------|
| O-01..O-05 حي | EXTERNAL |
| G-27 XAML sweep كامل | P2 — شهر+ |
| FlaUI حي 90%+ | يحتاج `run-ui-audit-nightly.ps1` على VM |

---

## 5. التحقق الحي الموصى به

```powershell
.\scripts\ops\run-ui-audit-nightly.ps1
# يشغّل Manufacturing + Construction + Retail عبر BRQOSH_AUDIT_ALL_PROFILES=1
```

---

## 6. الملفات

- `tools/UiAudit/UiAuditResult.cs` · `ProfileAuditContext.cs` · `NavKeyModuleMap.cs`
- `tools/UiAudit/ComprehensiveAuditor.cs` · `UiAutomationHelpers.cs` · `NavKeyMap.cs`
- `tools/FullScenarioAudit/Program.cs`
- `tools/ShuraCouncil/EvidenceLoader.cs`
- `src/Brqosh.Application/Intelligence/ProgramKnowledge.cs`
- `tests/Brqosh.Application.Tests/Intelligence/ProgramAssistantTests.cs`
- `tests/Brqosh.Desktop.Tests/DesktopUiTests.cs`
