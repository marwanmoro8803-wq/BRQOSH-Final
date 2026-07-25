# CEEF-124 — إكمال هجرة Desktop DI (Tier P2/P3)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | E1/E2/E3 من CEEF-120 — Phase 2c |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **✅ مغلق (كود + اختبار)** |

## الملخص

هجرة **جميع** صفحات Desktop المتبقية من `new XxxService(db)` إلى `DesktopAccountingFacade` + DI.

```
قبل CEEF-124: ~32 ملفاً مع new XxxService(
بعد CEEF-124:  0 — grep Desktop = صفر
```

## تسجيل DI (CEEF-124 block)

في `InfrastructureServiceCollectionExtensions.cs`:

- مخزون: `InventoryService` · `LandedCostAllocationService` · `StockCountAdjustmentService` · `StockMovementCancellationService` · `GoodsReceiptCancellationService`
- مشتريات/بيع: `PurchaseOrderService` · `SalesOrderService` · `SalesQuotationService`
- تصنيع: `WorkOrderService`
- أصول/رواتب: `DepreciationService` · `PayrollReversalService` · `ProgressBillingCancellationService`
- مقاولات: `ConstructionProjectService` · `ProjectCostService` · `RetentionReleaseService` · `SubcontractRetentionWithholdService`
- ZATCA: `ZatcaSubmissionService` · `ZatcaCsidOnboardingService`
- أمن/إعداد: `UserAuthService` · `UserManagementService` · `OrganizationProvisioningService` · `DatabaseBootstrapService`

## الملفات المُهاجَرة (~32 ملف)

| المجال | ملفات |
|--------|-------|
| App bootstrap | `App.xaml.cs` |
| Login/Auth | `LoginWindow` · `PasswordChangeWindow` |
| Users | `UsersPage` · `UserDialog` |
| Sales | `SalesOrderDialog` · `SalesOrdersPage` · `SalesQuotationDialog` |
| Purchasing | `PurchaseOrderDialog` · `PurchaseOrdersPage` · `GoodsReceiptDialog` · `GoodsReceiptsPage` |
| Inventory | `StockMovementDialog` · `StockMovementsPage` · `StockCountPage` · `LandedCostPage` |
| Fixed assets | `FixedAssetDialog` · `FixedAssetsPage` |
| Payroll | `PayrollPage` |
| Construction | `ProgressBillingDialog` · `ProgressBillingsPage` · `BoqPage` · `ContractsPage` · `ProjectDialog` · `ProjectCostDialog` · `RetentionsPage` · `SubcontractorsPage` |
| Manufacturing | `WorkOrdersPage` |
| ZATCA | `ZatcaInvoicesPage` · `ZatcaCsidWizardWindow` |
| Journal | `JournalEntriesPage` |
| Org | `OrganizationProvisionerWindow` |

## بوابة الانحدار

`DesktopDiGuardTests.cs`:
- **Theory** — 15 ملف P0 (CEEF-123)
- **Fact جديد** — `EntireDesktop_HasZeroDirectServiceConstruction` يمسح كل `*.cs` في Desktop

## الدليل

| المستوى | الحالة | الدليل |
|---------|--------|--------|
| ✅ كود | تم | grep `new \w+Service\(` في Desktop = 0 |
| ✅ اختبار | **436/436 PASS** (Desktop 97 incl. guard) | `dist/evidence/CEEF-124-desktop-di/dotnet-test.log` |
| ✅ حي | — | تشغيل Desktop يدوي |
| ⏭ خارجي | — | — |

## المتسلسل

- CEEF-109 (4 حوارات) → CEEF-122 (DI + Facade) → CEEF-123 (P0) → **CEEF-124 (كامل)**
