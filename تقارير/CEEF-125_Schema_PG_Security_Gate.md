# CEEF-125 — بوابة PostgreSQL + حماية BRQOSH_TEST

| الحقل | القيمة |
|-------|--------|
| **النطاق** | E4/E5/E6 من CEEF-120 — Phase 3 |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **✅ مغلق (كود + اختبار)** · PG حي ⏭ عند توفر `database.json` |

## المخرجات

### 1. `scripts/ops/schema-gate-pg.ps1`

- يشغّل `PostgreSqlStartupTests` + `SchemaSqlGuardTests` + `DbStartupProbe` عند وجود PG في `%APPDATA%\BRQOSH\database.json`
- `BRQOSH_REQUIRE_PG=1` → فشل إلزامي لـ PRs التي تمس Persistence
- بدون PG → تحذير + exit 0 (CI SQLite)

### 2. `SecurityContext.cs`

```csharp
private static bool AutomatedTestBypass =>
    !AppEnvironmentConfig.IsProduction &&
    string.Equals(Environment.GetEnvironmentVariable("BRQOSH_TEST"), "1", ...);
```

### 3. `SecurityContextProductionGuardTests.cs`

- يثبت أن `BRQOSH_TEST=1` **لا يُعطّل** RBAC في Production

## الدليل

| المستوى | الحالة | الدليل |
|---------|--------|--------|
| ✅ كود | تم | `schema-gate-pg.ps1` · `SecurityContext.cs` |
| ✅ اختبار | PASS | `SecurityContextProductionGuardTests` في Application 255 |
| ✅ حي PG | ⏭ | يتطلب `database.json` + خادم PG محلي |
| ⏭ خارجي | — | — |

## تشغيل

```powershell
.\scripts\ops\schema-gate-pg.ps1
# PR يمس Persistence:
$env:BRQOSH_REQUIRE_PG='1'; .\scripts\ops\schema-gate-pg.ps1
```
