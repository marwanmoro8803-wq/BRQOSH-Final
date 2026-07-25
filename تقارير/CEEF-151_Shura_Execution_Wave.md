# CEEF-151 — Shura Execution Wave (P0/P1 in-repo)

| الحقل | القيمة |
|-------|--------|
| **التاريخ** | 2026-07-11 |
| **السلطة** | RAI-SHURA-CEEF-150 · council-20260711-113023.json |
| **النطاق** | تنفيذ in-repo لأوامر الشورى P1 |

---

## 1. ما نُفّذ (✅ in-repo)

| # | أمر الشورى | التنفيذ | التحقق |
|---|------------|---------|--------|
| 1 | G-31 دعم عملاء | **موجود مسبقاً** — `CustomerSupportPage` + nav + RBAC | Desktop.Tests G-31 keys |
| 2 | G-32 تسعير/باقات | **موجود مسبقاً** — `PricingPlansPage` + `IBillingPlanCatalog` | Desktop.Tests G-32 keys |
| 3 | AUDIT-SEC-005 API SecurityContext | `ApiSecurityContextMiddleware` — X-Api-User / X-Api-Role | Api.Tests 2 PASS |
| 4 | AUDIT-PRT-001 posted-only print | `PrintPostedGuard` + `DocumentPrintService` | Application.Tests 1 PASS |
| 5 | G-47 API rate limits | `ApiRateLimitMiddleware` 120/min | Api.Tests 1 PASS |
| 6 | AUDIT-GATE-001 merge chronicle | `merge-council-chronicle.ps1` graceful SKIP | script exit 0 |

---

## 2. ما بقي (⏭ EXTERNAL أو موجات لاحقة)

| # | البند | السبب |
|---|-------|-------|
| 1 | O-01 ZATCA OTP حي | ⏭ EXTERNAL — يحتاج OTP من المستخدم |
| 2 | O-02..O-05 | ⏭ EXTERNAL |
| 3 | G-27 XAML sweep كامل (1420 سطر) | موجة منفصلة — شهر+ |
| 4 | FlaUI Construction + Retail | يحتاج Desktop حي |
| 5 | Idempotency Redis · webhooks DB | P2 |

---

## 3. الاختبارات

| المقياس | قبل | بعد |
|---------|-----|-----|
| الإجمالي | 482 | **488** (+6) |
| Api | 35 | **38** (+3) |
| Application | 267 | **268** (+1) |
| Desktop | 116 | **118** (+2) |

```powershell
dotnet test Brqosh.sln -c Release --nologo  # 488/488 PASS
```

---

## 4. الملفات المُغيَّرة

- `src/Brqosh.Api/Middleware/ApiSecurityContextMiddleware.cs`
- `src/Brqosh.Api/Middleware/ApiRateLimitMiddleware.cs`
- `src/Brqosh.Api/Program.cs`
- `src/Brqosh.Infrastructure/Reporting/PrintPostedGuard.cs`
- `src/Brqosh.Infrastructure/Reporting/DocumentPrintService.cs`
- `scripts/ops/merge-council-chronicle.ps1`
- `tests/Brqosh.Api.Tests/ApiSecurityContextMiddlewareTests.cs`
- `tests/Brqosh.Api.Tests/ApiRateLimitMiddlewareTests.cs`
- `tests/Brqosh.Application.Tests/Reporting/PrintPostedGuardTests.cs`
- `tests/Brqosh.Desktop.Tests/DesktopUiTests.cs`

---

## 5. الحكم

**✅ موجة Shura P1 in-repo مكتملة** · **⏭ O-01 OTP = الخطوة الحية التالية للمستخدم**
