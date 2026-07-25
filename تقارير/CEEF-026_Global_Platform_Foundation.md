# CEEF-026 — أساس المرحلة 7: Global Platform (داخل المستودع)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | `CompanyGroup` · `TenantId` · ميزان مراجعة موحّد + FX يدوي · API قراءة · `/health/live` · تهيئة مخطط API |
| **التاريخ** | 2026-07-08 |
| **الحكم** | **أساس مرحلة 7 مُثبت داخل المستودع** — O-08…O-13 **ليست** مغلقة؛ O-01…O-06 تبقى ⏭ |

---

## 1. ما أُنجز (ليس إغلاقاً كاملاً لـ O-08…O-13)

### O-08 — أساس تقارير موحّدة (جزء داخلي فقط)

| المكوّن | الملف |
|---------|-------|
| كيان `CompanyGroup` + عملة تقرير | `Brqosh.Domain/Companies/CompanyGroup.cs` |
| ربط شركة بمجموعة | `Company.AssignCompanyGroup()` · `CompanyGroupId` |
| ميزان مراجعة موحّد | `ConsolidatedTrialBalanceQuery` |
| تحويل FX يدوي (معدلات لكل شركة) | `FxConversion` عبر `Dictionary<long, decimal>` |
| API | `GET /api/v1/company-groups` · `GET .../consolidated-trial-balance` |

**ما لم يُغلق:** إعادة تقييم FX تلقائية · ترجمة أرصدة إقفال · تقارير مالية موحّدة كاملة (قائمة دخل/مركز مالي).

### O-09 — أساس عزل مستأجر (stub)

| المكوّن | الملف |
|---------|-------|
| `TenantId` على `Company` | Domain + EF + `EnsureCompanyPlatformAsync` |
| backfill للشركات القديمة | `BackfillCompanyTenantIdsAsync` |
| سياق جلسة SaaS | `TenantContext.CurrentTenantId` (يُضبط لاحقاً من middleware) |

**ما لم يُغلق:** billing · provisioning · عزل صفوف/مخطط per-tenant · onboarding.

### O-10 — مراقبة سحابية (stub)

| المكوّن | الملف |
|---------|-------|
| Liveness probe | `GET /health/live` |
| Readiness موجود مسبقاً | `/health/ready` |

**ما لم يُغلق:** HA · DR · auto-scaling · tracing/alerts منصة.

### O-13 — قراءة API للشركات (stub)

| المكوّن | الملف |
|---------|-------|
| قائمة شركات | `GET /api/v1/companies` |

**ما لم يُغلق:** تطبيق جوال/Blazor · مزامنة offline.

### بنية تحتية إضافية

- جدول `company_groups` + أعمدة `TenantId`/`CompanyGroupId` — `BrqoshSchemaUpdater.EnsureCompanyPlatformAsync`
- `SchemaSqlGuard` — أنواع `UUID` لـ PostgreSQL
- API يستدعي `DatabaseSchemaService.EnsureSchemaAsync` عند الإقلاع (مع fallback آمن)
- `scripts/check-coverage-by-assembly.ps1` — قياس معلوماتي لـ O-06
- `scripts/ops/external-gates-checklist.ps1` — تحضير O-01…O-05

---

## 2. أدلة تشغيل

| الفحص | النتيجة |
|--------|---------|
| Domain | **45/45 PASS** |
| Application | **145/145 PASS** (`BRQOSH_SKIP_STRESS=1`) |
| Api | **16/16 PASS** |
| **المجموع** | **206/206 PASS** |
| Desktop Release build | **0 Error / 0 Warning** |
| FullScenarioAudit | **13/13 PASS** |
| Coverlet (Application.Tests) | **80.64%** ≥ 75% |
| Security static scan | **PASS** |
| `ConsolidatedTrialBalance_ConvertsFunctional_ToReportingCurrency` | USD→SAR @ 3.75 ✅ |

---

## 3. ما لم يُغلق — ولماذا (صريح)

| البند | السبب — لا يُغلق بالكود وحده |
|-------|------------------------------|
| O-01 | OTP هيئة الزكاة + إنتاج حي |
| O-02 | مراجع SOCPA مستقل |
| O-03 | MSI على VM ميداني نظيف |
| O-04 | Load 1M — أجهزة/ساعات |
| O-05 | OWASP ZAP حي — شبكة/ماسح |
| O-06 | Coverlet ≥75% على **كل** assembly في الحل (البوابة الحالية على artifact Application.Tests فقط) |
| O-08 كامل | FX translation آلي + تقارير موحّدة شاملة |
| O-09 كامل | Multi-tenant SaaS · billing · provisioning |
| O-10 كامل | Cloud HA / DR / auto-scaling |
| O-11 | BI warehouse / Power BI embedded |
| O-12 | AI CFO / LLM حي |
| O-13 كامل | Mobile / Blazor companion |

**قاعدة المشروع:** لا نُعلّم ✅ على بنود ⏭ دون دليل خارجي أو منتج كامل.

---

## 4. الحكم

| الادعاء | الحكم |
|---------|--------|
| أساس Global Platform (مجموعة + TB موحّد + TenantId + API stubs) | ✅ داخل المستودع |
| O-08 · O-09 · O-10 · O-13 مغلقة بالكامل | ❌ — stubs فقط |
| O-01…O-06 · O-11 · O-12 مغلقة | ❌ — خارجي/استراتيجي |

**التالي:** O-01…O-05 (اعتمادات سوقية) **أو** تعميق O-08/O-09 (FX آلي · middleware tenant) حسب الأولوية.
