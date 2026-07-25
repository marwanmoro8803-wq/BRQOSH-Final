# CEEF-132 — G-10 CRM MVP

| الحقل | القيمة |
|-------|--------|
| **النطاق** | G-10 Leads · Opportunities · Activities |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **✅ مغلق MVP in-repo** |

## المخرجات

| طبقة | المكوّن |
|------|---------|
| Domain | `CrmLead` · `CrmOpportunity` · `CrmActivity` |
| Infra | `CrmService` · جداول `crm_*` · schema V14 |
| Desktop | `CrmLeadsPage` · `CrmOpportunitiesPage` · `CrmActivitiesPage` |
| Module | `ModuleKey.Crm` في كل profiles مع Sales |

## الاختبارات

- `CrmServiceTests` — lead convert · opportunity · activity
- Desktop markup + nav catalog `crm_*`

## ⏭ خارج النطاق

حملات تسويق · SLA · تكامل بريد — G-10 «كامل» enterprise
