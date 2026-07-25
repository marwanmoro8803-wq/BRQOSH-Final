# CEEF-159 — إغلاق D5 FlaUI (أوامر الشورى)

| الحقل | القيمة |
|-------|--------|
| **التاريخ** | 2026-07-11 |
| **السلطة** | council-20260711-193313 · D5 · MEM-028 |
| **النطاق** | FlaUI KPI + صمود UIA · 3 ملفات عمل |

---

## 1. المشكلة

| # | العطل | السبب |
|---|-------|--------|
| F1 | KPI لوحة التحكم FAIL | `KpiGrid` vs `BrqoshKpiGrid` — عدم انتظار تحميل async |
| F3 | nightly بدون مزامنة كلمة مرور | `-SkipPasswordSync` → فشل الدخول لـ 3 ملفات |
| F4 | دخول async + انتظار قصير | `WaitForPostLoginAsync` · `LoginError` UIA id |

---

## 2. الإصلاح

| الملف | التغيير |
|-------|---------|
| `DashboardPage.xaml(.cs)` | توحيد `BrqoshKpiGrid` |
| `UiAutomationHelpers.cs` | `WaitForDashboardKpisAsync` · fallback `KpiGrid` |
| `ComprehensiveAuditor.cs` | try/catch لكل صفحة · انتظار 45s للوحة |
| `Program.cs` | حماية `RunAllPagesAsync` من UIA عابر |

---

## 3. تحقق

```powershell
.\scripts\ops\run-ui-audit-nightly.ps1 -SkipPasswordSync
```

**الدليل:** `dist/evidence/live-ui-audit/shura-nightly-20260711.txt` · `audit-*.txt`

---

## 4. أوامر الشورى المتبقية

| الأمر | الحالة |
|-------|--------|
| D5 FlaUI 3 profiles | يُغلق عند nightly PASS |
| D4 O-01 OTP | ⏭ EXTERNAL — المستخدم |
| D6 G-27 i18n | P2 مؤجّل |
