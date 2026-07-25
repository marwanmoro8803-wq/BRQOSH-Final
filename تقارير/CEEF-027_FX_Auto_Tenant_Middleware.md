# CEEF-027 — تعميق O-08/O-09: FX آلي + Tenant Middleware

| الحقل | القيمة |
|-------|--------|
| **النطاق** | `ExchangeRate` · `ExchangeRateService` · إعادة تقييم مجمّعة · حل FX تلقائي في TB موحّد · `TenantContext` AsyncLocal · `X-Tenant-Id` middleware |
| **التاريخ** | 2026-07-08 |
| **الحكم** | **تعميق O-08/O-09 مُثبت داخل المستودع** — O-08/O-09 **ليستا** مغلقتين بالكامل؛ O-01…O-06 تبقى ⏭ |

---

## 1. O-08 — FX آلي (داخل المستودع)

| المكوّن | الملف |
|---------|-------|
| كيان `ExchangeRate` | `Brqosh.Domain/Treasury/ExchangeRate.cs` |
| خدمة أسعار + بحث تاريخي + عكس الزوج | `ExchangeRateService` |
| حل تلقائي في TB موحّد | `ConsolidatedTrialBalanceQuery` → `GetRateAsync` عند غياب القاموس اليدوي |
| إعادة تقييم بسعر مخزّن | `FxRevaluationService.RevalueWithStoredRateAsync` |
| إعادة تقييم مجمّعة للبنوك الأجنبية | `FxRevaluationBatchService` |
| API | `POST /api/v1/exchange-rates` · `GET /api/v1/exchange-rates/lookup` |
| مخطط | جدول `exchange_rates` — `EnsureExchangeRatesTableAsync` |

**ما لم يُغلق:** ترجمة إقفال · تقارير موحّدة كاملة (قائمة دخل/مركز مالي) · فروق محققة تلقائياً · مصادر أسعار خارجية حية.

---

## 2. O-09 — Tenant Middleware (داخل المستودع)

| المكوّن | الملف |
|---------|-------|
| `TenantContext` بـ `AsyncLocal<Guid?>` | `Infrastructure/Companies/TenantContext.cs` |
| Middleware | `X-Tenant-Id` في `Program.cs` (بعد API key) |
| حارس عزل | `TenantGuard.IsCompanyAllowedAsync` · `IsGroupAllowedAsync` |
| حماية API | كل مسارات `companyId` + فلترة `companies`/`company-groups` |

**سلوك:** بدون `X-Tenant-Id` → توافق خلفي كامل (لا عزل). مع الرأس → **fail-closed** 403 `tenant_forbidden`.

**ما لم يُغلق:** billing · provisioning · onboarding · EF global filters · multi-company-per-tenant.

---

## 3. O-01…O-05 — تحضير خارجي (لم يُغلق)

| البند | الإجراء داخل المستودع |
|-------|------------------------|
| O-01…O-05 | `scripts/ops/external-gates-checklist.ps1` — نقاط دخول واضحة |
| O-01 | `docs/ZATCA_LIVE_CSID.md` |
| O-05 | `security-static-scan.ps1` PASS — ZAP حي يحتاج شبكة |

---

## 4. أدلة تشغيل

| الفحص | النتيجة |
|--------|---------|
| Domain | **47/47 PASS** |
| Application | **153/153 PASS** (`BRQOSH_SKIP_STRESS=1`) |
| Api | **19/19 PASS** |
| **المجموع** | **219/219 PASS** |
| FullScenarioAudit | **13/13 PASS** |
| Desktop Release | **0** أخطاء |
| Security static | **PASS** |
| `ExchangeRateService` lookup + inverse | ✅ |
| `FxRevaluationBatchService` | ✅ |
| `TenantMiddlewareTests` | ✅ |

---

## 5. الحكم

| الادعاء | الحكم |
|---------|--------|
| FX آلي (تخزين + حل + TB موحّد + batch) | ✅ داخل المستودع |
| Tenant middleware على API | ✅ داخل المستودع |
| O-08 · O-09 مغلقتان بالكامل | ❌ |
| O-01…O-06 · O-10…O-13 مغلقة | ❌ — خارجي/استراتيجي |

**التالي:** O-01…O-05 (أدلة سوقية) **أو** تقارير موحّدة موسّعة + ترجمة إقفال (O-08).
