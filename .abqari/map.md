# خريطة مشروع BRQOSH ERP

نظام ERP محاسبي سعودي — .NET 9 / WPF (RTL) / EF Core / Clean Architecture. يبدأ باختيار نظام عمل (مقاولات/إنتاج/تسويق) فيفعّل الوحدات عبر `ModuleCatalog`.

## نقاط الدخول
- **سطح المكتب (المنتج الأساسي):** `src/Brqosh.Desktop/App.xaml.cs` → `AppServices` (Service Locator ساكن + حالة عالمية: CompanyId/CurrentUser/الفرع/الوردية). إنشاء السياق: `AppServices.CreateDb()` → `BrqoshDbContextFactory.Create`.
- **API (رأس SaaS):** `src/Brqosh.Api/Program.cs` (~28 نقطة Minimal API، مصادقة X-Api-Key مشترك).
- **DI الأساسي:** `src/Brqosh.Infrastructure/InfrastructureServiceCollectionExtensions.cs` (~120 تسجيلاً).

## الطبقات (حجم فعلي)
- `Brqosh.Domain` (87 ملف) — نقيّ بلا تبعيات. المحاسبة في `Domain/Accounting/` (JournalEntry = القيد المزدوج، VatCalculator، FiscalPeriod، SaudiChartOfAccounts).
- `Brqosh.Application` (24 ملف) — **قشرة عقود**، تسجيل DI فارغ. العقود + 13 واجهة استعلام تقارير.
- `Brqosh.Infrastructure` (192 ملف) — **كل التنفيذ فعلياً هنا**. Persistence/ (DbContext 57 DbSet، UnitOfWork، BrqoshSchemaUpdater 1303 سطر، Migrations/ خمس)، Sales/Purchasing/Treasury/Inventory/Construction/Manufacturing/Payroll/Zatca/Security/Reporting.
- `Brqosh.Desktop` (157 ملف + 124 XAML) — code-behind، **لا ViewModels**. يتخطى Application ويكلم EF مباشرة (156 CreateDb).
- `src/Modules/` الثلاثة — **أصداف فارغة (0 مصدر)**، لا يرجع إليها أحد.

## أين تجد
- **الترحيل المالي:** 15 `*PostingService` في Infrastructure، كلها عبر `UnitOfWork.ExecuteInTransactionAsync` + `PostingOrchestrator`/`SaudiPostingPolicy`.
- **الصلاحيات:** `Desktop/Security/PermissionService.cs` (خريطة ~70 شاشة) + `Domain.Security.SecurityContext` (fail-closed).
- **كلمات المرور:** `Infrastructure/Security/UserAuthService.cs` (PBKDF2 100k).
- **ZATCA:** `Infrastructure/Zatca/` (UBL + XmlSigner + QrGenerator + Submission + CSID).
- **التقارير:** `Infrastructure/Reporting/` (TrialBalance/BalanceSheet/IncomeStatement/GeneralLedger/CashFlow + Consolidated).

## مزوّد القاعدة
مزدوج: Npgsql (إنتاج) + SQLite (تطوير/افتراضي فعلي). الاختيار في `AppServices` + `DbContextFactory`. **الافتراضي Development + فولباك صامت PG→SQLite.**

## البناء والتشغيل
`dotnet build Brqosh.sln` ✅ · `dotnet test` = 510 (509✅ / 1 فشل حارس DI). CI في `.github/workflows/ci.yml` (بوابة تغطية 75%).
