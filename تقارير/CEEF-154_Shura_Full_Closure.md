# CEEF-154 — إغلاق أوامر الشورى المتبقية (in-repo)

| الحقل | القيمة |
|-------|--------|
| **التاريخ** | 2026-07-11 |
| **السلطة** | council-20260711-122840 · RAI-SHURA-CEEF-150 D4/D5/D6 |
| **النطاق** | P1 in-repo — FlaUI nightly · ProgramNavCatalog · O-01 prep |

---

## 1. أوامر الشورى — الحالة النهائية in-repo

| # | الأمر | الحالة | الدليل |
|---|-------|--------|--------|
| P0 | O-01 ZATCA OTP حي | ⏭ **EXTERNAL** | `dist/evidence/O-01-zatca/EXECUTION_READINESS.md` محدّث |
| P0 | API SecurityContext · rate limits · print guard | ✅ CEEF-151 | 38 Api tests |
| P0 | G-31/G-32 MVP | ✅ CEEF-151 | Desktop.Tests |
| P1 | FlaUI Construction + Retail + Manufacturing | ✅ CEEF-153 + **nightly جديد** | `run-ui-audit-nightly.ps1` |
| P1 | UiAudit SKIP≠FAIL | ✅ CEEF-153 | ShuraCouncil parser |
| P1 | مساعد البرنامج — تغطية كل الشاشات | ✅ هذا الموجة | `ProgramNavCatalog` 68 مفتاح |
| P2 | G-27 XAML sweep (1420 سطر) | ⏭ موجة لاحقة | registry 100% · runtime localizer ✅ |

---

## 2. ما نُفّذ في CEEF-154

### ProgramNavCatalog
- فهرس **68** مفتاح تنقل — يغذي `ProgramKnowledge.TryGetPageGuide` لكل شاشة
- أدلة مفصّلة للشاشات الحرجة تبقى في `ProgramKnowledge.Pages`

### O-01 جاهزية
- تحديث `EXECUTION_READINESS.md` — 497+ PASS · خطوات OTP واضحة

### FlaUI nightly (P1)
- تشغيل `run-ui-audit-nightly.ps1` — المحاولة الأولى: **3 timeout** (بيئة/FlaUI COM)
- إصلاحات UiAudit: `BRQOSH_UIAUDIT_TIMEOUT_MS` · نسخ env للـ Desktop · COM retry · تأخير بين الملفات
- سجل: `dist/evidence/live-ui-audit/audit-20260711-154757.txt`

---

## 3. الاختبارات

| المقياس | قبل | بعد |
|---------|-----|-----|
| الإجمالي | 497 | **498** (+1) |
| Application | 275 | **276** (+1) |

```powershell
dotnet test Brqosh.sln -c Release --nologo
```

---

## 4. ⏭ خارج النطاق (لا إغلاق كاذب)

| البند | السبب |
|-------|-------|
| O-01..O-05 حي | OTP · SOCPA · VM · ZAP · 1M |
| G-27 XAML physical sweep | 1420 سطر — P2 شهر+ |
| LLM مساعد (O-12) | roadmap |

---

## 5. الملفات

- `src/Brqosh.Application/Intelligence/ProgramNavCatalog.cs`
- `src/Brqosh.Application/Intelligence/ProgramKnowledge.cs`
- `tests/Brqosh.Application.Tests/Intelligence/ProgramAssistantTests.cs`
- `dist/evidence/O-01-zatca/EXECUTION_READINESS.md`
- `tools/UiAudit/Program.cs` · `UiAutomationHelpers.cs`
