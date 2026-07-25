# CEEF-118 — V13 In-Repo Wave (G-69-C · G-54 · G-08 · G-39 · GTM stubs)

**التاريخ:** 2026-07-09  
**النطاق:** إغلاق in-repo لموجة V13 المتبقية (بدون O-01…O-05)

---

## ملخص الإغلاق

| البند | الحالة | الدليل |
|-------|--------|--------|
| **G-69-C** Company Switcher | ✅ in-repo | `MainWindow` CompanyCombo · `OrganizationsPage` · `SwitchToCompanyAsync` |
| **G-69** SetupWizard 8 profiles | ✅ in-repo | `SetupWizardWindow` ComboBox + `ListIndustries()` |
| **G-69 6-B** Login clarification | ✅ in-repo | تبويب «طلب حساب مستخدم» + نص توجيهي |
| **G-54** Cash Flow Statement | ✅ in-repo (مبسّط) | `CashFlowStatementService` · `CashFlowStatementPage` · `CashFlowStatementTests` |
| **G-08 / LIFECYCLE-32** Sales Orders MVP | ✅ in-repo | `SalesOrder` · `SalesOrderService` · UI · `SalesOrderTests` (3) |
| **G-39** Data Migration stub | ✅ in-repo | `IDataMigrationService` · `CsvDataMigrationService` · `DataMigrationTests` (2) |
| **v13-ops-gtm** GTM docs | 🔶 جزئي | `docs/gtm/PRICING_TIERS.md` · `SUPPORT_SLA.md` · `TRAINING_OUTLINE.md` |
| **O-01…O-05** | ⏭ خارجي | لم يُغلق — harness PASS فقط |

---

## اختبارات

```
dotnet build Brqosh.sln -c Release  → PASS
dotnet test Brqosh.sln -c Release     → 414/414 PASS
  App 250 · Domain 58 · Api 26 · Desktop 80
scripts/ops/run-external-gates.ps1    → PASS (O-01…O-05 تبقى خارجية)
```

**اختبارات جديدة (+6):**
- `CashFlowStatementTests` (1)
- `SalesOrderTests` (3)
- `DataMigrationTests` (2)

---

## ملفات رئيسية

### G-69-C
- `src/Brqosh.Desktop/MainWindow.xaml` — CompanyCombo
- `src/Brqosh.Desktop/MainWindow.xaml.cs` — `BindCompaniesAsync` · `SwitchToCompanyAsync`
- `src/Brqosh.Desktop/Views/Pages/OrganizationsPage.*`
- `src/Brqosh.Desktop/Views/Pages/CompanySettingsPage.xaml.cs` — switch after provision

### G-54
- `src/Brqosh.Infrastructure/Reporting/CashFlowStatementService.cs`
- `src/Brqosh.Application/Reporting/ReportingContracts.cs` — `ICashFlowStatementQuery`
- `src/Brqosh.Desktop/Views/Pages/CashFlowStatementPage.*`

### G-08
- `src/Brqosh.Domain/Sales/SalesOrder.cs`
- `src/Brqosh.Infrastructure/Sales/SalesOrderService.cs`
- `src/Brqosh.Infrastructure/Persistence/BrqoshDbContext.cs` + `BrqoshSchemaUpdater.cs` — `sales_orders`
- `src/Brqosh.Desktop/Views/Pages/SalesOrdersPage.*` · `SalesOrderDialog.*`

### G-39
- `src/Brqosh.Application/Setup/DataMigrationContracts.cs`
- `src/Brqosh.Infrastructure/Setup/CsvDataMigrationService.cs`

### GTM
- `docs/gtm/PRICING_TIERS.md`
- `docs/gtm/SUPPORT_SLA.md`
- `docs/gtm/TRAINING_OUTLINE.md`

---

## مؤجّل (خارج النطاق)

| البند | السبب |
|-------|--------|
| LIFECYCLE-33 حجز مخزون | يحتاج reservation domain |
| GOSI/Mudad/WPS/POS cert | تكامل خارجي |
| v13-ops-gtm كامل | helpdesk حي · فيديوهات · Stripe |
| O-01…O-05 | OTP · SOCPA · VM · 1M · ZAP |

---

## معيار القبول

- [x] Build Release PASS
- [x] 414 tests PASS
- [x] External gates harness PASS
- [x] Schema SQLite + PG (`sales_orders` في SchemaUpdater)
- [x] لا ادعاء إغلاق O-01…O-05
