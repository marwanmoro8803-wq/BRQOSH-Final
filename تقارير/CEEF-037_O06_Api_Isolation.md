# CEEF-037 — O-06 + عزل API Tests

| الحقل | القيمة |
|-------|--------|
| **النطاق** | coverlet exclude migrations · اختبارات إضافية · `IsolatedApiFactory` |
| **التاريخ** | 2026-07-09 |
| **الحكم** | تعميق O-06 — بوابة 75% لكل assembly ⏭ |

## ما أُنجز

- `test.runsettings` — استبعاد `Migrations` من coverlet
- Domain: `VatCalculatorTests` · `IntercompanyAccountMapTests` · `BillingPlanTests`
- Api: `ApiExchangeRateEndpointTests` · `IsolatedApiFactory` · `ApiTestFixture` · تعطيل parallelization
- Application: `SamaFxRateProviderTests` · `TenantProvisioningServiceTests`

## أدلة

| الفحص | النتيجة |
|--------|---------|
| **المجموع** | **245/245 PASS** (58 Domain · 164 Application · 23 Api) |
| Coverlet مجمّع | ≥ 75% (بوابة CI) |

**ما يبقى ⏭:** Desktop.Tests · Infrastructure queries إضافية · فرض per-assembly في CI (ليس informational فقط).
