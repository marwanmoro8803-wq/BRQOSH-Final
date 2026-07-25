# CEEF-035 — SaaS Provisioning + Billing Stubs (O-09)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | `BillingPlan` · `TenantSubscription` · `TenantProvisioningService` · API |
| **التاريخ** | 2026-07-09 |
| **الحكم** | stub داخل المستودع — SaaS كامل ⏭ |

## ما أُنجز

| المكوّن | التفاصيل |
|---------|----------|
| Domain | `BillingPlan` · `TenantSubscription` · `Company.CreateForTenant` |
| Infrastructure | `TenantProvisioningService` · `BillingPlanCatalog` · seed TRIAL/STANDARD/ENTERPRISE |
| API | `POST /tenants/provision` · `GET /billing-plans` · `GET /tenants/subscription` |
| Tests | `TenantProvisioningServiceTests` · `TenantProvisioningApiTests` |

## أدلة

| الفحص | النتيجة |
|--------|---------|
| Tests | **245/245 PASS** |

**ما يبقى ⏭:** Stripe · multi-company/tenant · onboarding UI · provisioning DB منفصل.
