# CEEF-139 — أمن · RBAC · API · الاستمرارية

| الحقل | القيمة |
|-------|--------|
| **النطاق** | محور C + G + D (خطة CEEF-138) |
| **التاريخ** | 2026-07-10 |
| **المنهج** | فحص كود مباشر + تشغيل security-static-scan حي |
| **الحكم** | Desktop RBAC قوي · **API سطح هجوم P0** |

## درجة المحور

| المحور | الدرجة | السبب |
|--------|--------|-------|
| Desktop RBAC | **B+** | PermissionService ~67 pageKey · fail-closed |
| Service guards | **A−** | SecurityContext fail-closed · production bypass off |
| API auth | **D** | مفتاح اختياري · لا أدوار |
| Tenant isolation | **D** | open mode بدون header |
| Persistence dual-DB | **B** | PG gate موجود · CI skip بدون database.json |

## Findings مُثبتة (Top)

### AUDIT-SEC-001 — API مفتوح بدون مفتاح (P0)

```64:87:src/Brqosh.Api/Program.cs
var apiKey = builder.Configuration["BRQOSH_API_KEY"]
             ?? Environment.GetEnvironmentVariable("BRQOSH_API_KEY");
// ...
if (string.IsNullOrWhiteSpace(apiKey) || ctx.Request.Path.StartsWithSegments("/health"))
{
    await next();
    return;
}
```

**التأثير:** أي عميل يصل للـ API يقرأ trial-balance · companies · provision tenant.

### AUDIT-SEC-002 — TenantGuard مفتوح (P0)

```12:13:src/Brqosh.Infrastructure/Companies/TenantGuard.cs
if (TenantContext.CurrentTenantId is null)
    return true;
```

### AUDIT-SEC-003 — لا RBAC على API (P0)

grep `src/Brqosh.Api/` → **صفر** `SecurityContext` / `UserRole` / `Authorize`.

**POST حرجة:** `/api/v1/tenants/provision` · `/api/v1/companies/{id}/fx-closing-translation` · `/api/v1/exchange-rates`

### AUDIT-SEC-005/006 — state في الذاكرة (P1)

- `IdempotencyStore` — ConcurrentDictionary
- `WebhookSubscriptionRegistry` — List in-memory

### AUDIT-SEC-008 — fallback SQLite صامت (P2 · dev فقط)

```146:152:src/Brqosh.Desktop/AppServices.cs
if (_provider == DatabaseProvider.PostgreSql)
{
    _provider = DatabaseProvider.Sqlite;
    _connectionString = $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}";
```

**إنتاج:** يرفض SQLite صراحةً (سطر 140-144).

## Desktop RBAC — نقاط قوة

- `PermissionService.ScreenRoles` يغطي كل مفاتيح `NavigationCatalog`
- `CanAccess` → `false` إذا pageKey غير معرّف (غير Admin)
- `MainWindow` يفلتر التنقل + يمنع `NavigateTo` (سطر 493)

## التحقق الحي

| فحص | نتيجة | الدليل |
|-----|--------|--------|
| security-static-scan | PASS | `dist/evidence/CEEF-138-supreme-audit/external-gates.log` |
| ApiKeyGuardTests | موجود | `tests/Brqosh.Api.Tests/` |
| TenantMiddlewareTests | موجود | `tests/Brqosh.Application.Tests/Companies/TenantGuardTests.cs` |
| SecurityContextProductionGuardTests | PASS | CEEF-125 pattern |

## 4 مستويات

| المستوى | الحالة |
|---------|--------|
| ✅ كود | Desktop guards + static scan |
| ✅ اختبار | Api key + tenant tests جزئية |
| ⏭ حي | ZAP · OTP |
| ⏭ خارجي | O-05 pen test |

## الإصلاح المقترح (P0 backlog)

1. `BRQOSH_API_KEY` إلزامي في Production (fail fast)
2. TenantGuard default-deny على `/api/v1/*`
3. RBAC خفيف على POST/DELETE (Administrator scope minimum)

**الأدلة:** `dist/evidence/CEEF-138-supreme-audit/security-findings.csv`
