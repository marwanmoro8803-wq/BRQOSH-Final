# CEEF-046 — إغلاق المرحلة 6: Application Use Cases

| الحقل | القيمة |
|-------|--------|
| **النطاق** | عقود Use Case · تسجيل DI · تقليل الاعتماد المباشر على Infrastructure |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **المرحلة 6 مغلقة (أدنى)** — `AppServices` ما زال في Desktop |

---

## 1. عقود جديدة

| الواجهة | الملف |
|---------|-------|
| `IMonthEndCloseUseCase` | `Application/Accounting/ClosingContracts.cs` |
| `IBankStatementImportUseCase` | `Application/Treasury/TreasuryContracts.cs` |
| `IBankStatementMatchUseCase` |同上 |
| `ICashMovementReportUseCase` |同上 |

### تسجيل DI

`InfrastructureServiceCollectionExtensions.cs` — محولات Adapter لكل واجهة.

### موجود مسبقاً

`ITrialBalanceQuery` · `IFxRateSyncService` · `IFinancialIntelligenceService` · وغيرها.

---

## 2. ما لم يُغلق

- إزالة `AppServices` service locator من Desktop — تدريجي
- Use cases لكل مجال (مبيعات · مشتريات · …)

---

## 3. الحكم

**التالي:** المرحلة 7 — UI Golden Paths (design-system skill).
