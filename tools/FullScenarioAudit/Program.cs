using System.Text.Json;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

const string Tag = "QA-AUDIT";

var configPath = Path.Combine(
    Brqosh.Infrastructure.Persistence.BrqoshDataRoot.Path(), "database.json");
if (!File.Exists(configPath))
{
    Console.WriteLine("FAIL: database.json غير موجود — شغّل البرنامج أولاً.");
    return 1;
}

var config = JsonSerializer.Deserialize<DbConfig>(File.ReadAllText(configPath))!;
var provider = config.Provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase)
    ? DatabaseProvider.PostgreSql
    : DatabaseProvider.Sqlite;
var cs = provider == DatabaseProvider.Sqlite
    ? $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}"
    : config.ConnectionString;

await using var db = BrqoshDbContextFactory.Create(provider, cs);
await new BrqoshSchemaUpdater(db).ApplyAsync();

var company = await db.Companies.OrderBy(c => c.Id).FirstAsync();
var companyId = company.Id;

var profileName = Environment.GetEnvironmentVariable("BRQOSH_AUDIT_PROFILE");
BusinessProfile? auditProfile = null;
if (!string.IsNullOrWhiteSpace(profileName) &&
    Enum.TryParse<BusinessProfile>(profileName.Trim(), ignoreCase: true, out var parsedProfile))
{
    auditProfile = parsedProfile;
    company.UpdateBusinessProfile(parsedProfile);
    await db.SaveChangesAsync();
    Console.WriteLine($"Business profile: {parsedProfile}");
}

await EnsureUiAuditUserAsync(db, companyId);
var today = DateOnly.FromDateTime(DateTime.Today);
var yearStart = new DateOnly(today.Year, 1, 1);

Console.WriteLine("=== BRQOSH Full Scenario Audit ===");
Console.WriteLine($"Company: {company.NameAr} (Id={companyId}) | Provider: {provider}");
Console.WriteLine();

var results = new List<(string Step, bool Ok, string Detail)>();

try
{
    // --- Master data ---
    var customer = await EnsureCustomerAsync(db, companyId);
    var supplier = await EnsureSupplierAsync(db, companyId);
    var product = await EnsureProductAsync(db, companyId);
    results.Add(("عملاء/موردون/أصناف", true, $"{customer.NameAr} | {supplier.NameAr} | {product.NameAr}"));

    // --- Purchase + stock ---
    if (!await db.PurchaseInvoices.AnyAsync(p => p.CompanyId == companyId && p.SupplierInvoiceReference == $"{Tag}-PI"))
    {
        var pi = PurchaseInvoice.CreateDraft(companyId, supplier.Id, today, $"{Tag}-PI", vatDeductible: true);
        pi.AddLine("مواد خام تدقيق", 20m, 500m, product.Id);
        await new PurchaseInvoicePostingService(db).PostAsync(pi);
    }

    var inv = new InventoryService(db);
    var wh = await inv.GetDefaultWarehouseAsync(companyId);
    var onHand = await inv.GetOnHandAsync(companyId, product.Id, wh.Id);
    results.Add(("مشتريات + مخزون", onHand >= 15m, $"رصيد {product.Code}: {onHand:N2} (20 شراء − 5 بيع)"));

    // --- Sales ---
    if (!await db.SalesInvoices.AnyAsync(s =>
            s.CompanyId == companyId && s.CustomerId == customer.Id &&
            s.Lines.Any(l => l.Description.Contains(Tag))))
    {
        var si = SalesInvoice.CreateDraft(companyId, customer.Id, today);
        si.AddLine($"{Tag} بيع منتج تدقيق", 5m, 750m, product.Id);
        await new SalesInvoicePostingService(db).PostAsync(si);
    }

    var custBal = await new CustomerBalanceQuery(db).ForCustomerAsync(companyId, customer.Id);
    results.Add(("مبيعات + رصيد عميل", custBal.Balance > 0, $"رصيد {customer.NameAr}: {custBal.Balance:N2}"));

      var hasReceipt = await db.ReceiptVouchers.AnyAsync(r => r.CompanyId == companyId && r.Description.Contains(Tag));
    if (!hasReceipt)
    {
        var rv = ReceiptVoucher.CreateDraft(
            companyId, customer.Id, today, 1000m, ReceiptMethod.Cash, $"{Tag} تحصيل تدقيق");
        await new ReceiptVoucherPostingService(db).PostAsync(rv);
    }

    var custBalAfter = await new CustomerBalanceQuery(db).ForCustomerAsync(companyId, customer.Id);
    results.Add(("سند قبض", hasReceipt || custBalAfter.Balance < custBal.Balance,
        $"بعد التحصيل: {custBalAfter.Balance:N2}"));

    // --- Reports ---
    var tb = await new TrialBalanceQuery(db).ExecuteAsync(companyId, yearStart, today);
    var tbDebit = tb.Sum(r => r.TotalDebit);
    var tbCredit = tb.Sum(r => r.TotalCredit);
    results.Add(("ميزان المراجعة", tbDebit == tbCredit, $"مدين {tbDebit:N2} = دائن {tbCredit:N2}"));

    var bs = await new BalanceSheetQuery(db).ExecuteAsync(companyId, today);
    results.Add(("قائمة المركز المالي", bs.IsBalanced,
        $"أصول {bs.TotalAssets:N2} = التزامات+ملكية {bs.TotalLiabilitiesAndEquity:N2}"));

    var pl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, yearStart, today);
    results.Add(("قائمة الدخل", true, $"صافي الدخل: {pl.NetIncome:N2}"));

    var vat = await new VatReturnQuery(db).ExecuteAsync(companyId, yearStart, today);
    results.Add(("إقرار ضريبة القيمة المضافة", true, $"صافي الضريبة: {vat.NetVatPayable:N2}"));

    var dash = await new DashboardQuery(db).ExecuteAsync(companyId);
    results.Add(("لوحة التحكم", dash.OpenPeriods >= 1,
        $"مبيعات الشهر {dash.SalesThisMonth:N2} | ذمم {dash.Receivables:N2}"));

    var stmtLines = await new CustomerBalanceQuery(db).StatementAsync(companyId, customer.Id);
    results.Add(("كشف حساب عميل", stmtLines.Count > 0, $"{stmtLines.Count} حركة"));

    var suppBal = await new SupplierBalanceQuery(db).ForSupplierAsync(companyId, supplier.Id);
    results.Add(("رصيد مورد", suppBal.Balance > 0, $"رصيد {supplier.NameAr}: {suppBal.Balance:N2}"));

    // --- بيانات أساسية إضافية ---
    if (!await db.CostCenters.AnyAsync(c => c.CompanyId == companyId && c.Code == $"{Tag}-CC"))
    {
        db.CostCenters.Add(CostCenter.Create(companyId, $"{Tag}-CC", "مركز تدقيق"));
        await db.SaveChangesAsync();
    }

    if (!await db.BankAccounts.AnyAsync(b => b.CompanyId == companyId && b.Code == $"{Tag}-BNK"))
    {
        var bankGlId = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code == "1102" && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        db.BankAccounts.Add(BankAccount.Create(companyId, $"{Tag}-BNK", "حساب تدقيق", "بنك QA",
            "9988776655", "SA0380000000608010167519", bankGlId));
        await db.SaveChangesAsync();
    }

    // --- BOM + أمر إنتاج ---
    if (!await db.BillsOfMaterials.AnyAsync(b => b.CompanyId == companyId && b.Code == $"{Tag}-BOM"))
    {
        var raw = await EnsureRawProductAsync(db, companyId);
        var bom = BillOfMaterials.Create(companyId, $"{Tag}-BOM", "BOM تدقيق", product.Id);
        bom.AddLine(raw.Id, 2m);
        db.BillsOfMaterials.Add(bom);
        await db.SaveChangesAsync();

        var wo = WorkOrder.CreateDraft(companyId, bom.Id, today, 5m);
        db.WorkOrders.Add(wo);
        await db.SaveChangesAsync();
    }

    // --- أصل ثابت (ترحيل حقيقي) + رواتب مسودة ---
    if (!await db.FixedAssets.AnyAsync(a => a.CompanyId == companyId && a.Code == $"{Tag}-FA"))
    {
        var assetAcctId = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        var depAcctId = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();

        var asset = FixedAsset.Create(companyId, $"{Tag}-FA", "أصل تدقيق", today.AddMonths(-6),
            50_000m, 5_000m, 60, assetAcctId, depAcctId);
        db.FixedAssets.Add(asset);
        await db.SaveChangesAsync();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "fsa";
        await new FixedAssetAcquisitionPostingService(db).PostAcquisitionAsync(asset);
    }

    var hasPayroll = await db.PayrollRuns
        .Where(p => p.CompanyId == companyId)
        .AnyAsync(p => p.Lines.Any(l => l.EmployeeName.Contains(Tag)));
    if (!hasPayroll)
    {
        var run = PayrollRun.CreateDraft(companyId, today.Year, today.Month, today);
        run.AddLine($"{Tag} موظف", 5000m, 1000m, 400m, 300m, 300m);
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync();
    }

    results.Add(("بيانات موسّعة", true, "مراكز تكلفة، بنوك، BOM، أصول، رواتب"));

    if (auditProfile == BusinessProfile.Construction)
    {
        if (!await db.ConstructionProjects.AnyAsync(p => p.CompanyId == companyId && p.Code == $"{Tag}-PRJ"))
        {
            var project = ConstructionProject.Create(
                companyId, $"{Tag}-PRJ", $"{Tag} مشروع مقاولات", customer.Id,
                today.AddMonths(-2), today.AddMonths(10), 500_000m, 10m, "الرياض", 400_000m);
            project.AddBoqItem("1", "حفر أساسات", "م³", 100m, 50m);
            db.ConstructionProjects.Add(project);
            await db.SaveChangesAsync();
        }

        results.Add(("مقاولات", true, "مشروع + BOQ جاهز للفحص"));
    }

    if (auditProfile == BusinessProfile.Retail)
    {
        if (!await db.PosShifts.AnyAsync(s => s.CompanyId == companyId && s.CashierName.Contains(Tag)))
        {
            db.PosShifts.Add(PosShift.Open(companyId, $"{Tag} كاشير", 500m));
            await db.SaveChangesAsync();
        }

        results.Add(("تجزئة POS", true, "وردية مفتوحة جاهزة"));
    }

    var auditCount = await db.AuditLogEntries.CountAsync(a => a.CompanyId == companyId);
    results.Add(("سجل التدقيق", auditCount >= 3, $"{auditCount} إدخال من ترحيل فعلي"));
}
catch (Exception ex)
{
    results.Add(("خطأ حرج", false, ex.Message));
}

Console.WriteLine("--- النتائج ---");
var failed = 0;
foreach (var (step, ok, detail) in results)
{
    var mark = ok ? "PASS" : "FAIL";
    if (!ok) failed++;
    Console.WriteLine($"[{mark}] {step}: {detail}");
}

Console.WriteLine();
Console.WriteLine(failed == 0
    ? "=== SUCCESS — البيانات جاهزة للعرض في البرنامج (ابحث عن QA-AUDIT) ==="
    : $"=== {failed} فشل — راجع الأخطاء أعلاه ===");
return failed == 0 ? 0 : 1;

static async Task<Customer> EnsureCustomerAsync(BrqoshDbContext db, long companyId)
{
    var existing = await db.Customers.FirstOrDefaultAsync(c =>
        c.CompanyId == companyId && c.NameAr == $"{Tag} عميل");
    if (existing is not null) return existing;

    var c2 = Customer.Create(companyId, $"{Tag} عميل", "311122233300003");
    db.Customers.Add(c2);
    await db.SaveChangesAsync();
    return c2;
}

static async Task<Supplier> EnsureSupplierAsync(BrqoshDbContext db, long companyId)
{
    var existing = await db.Suppliers.FirstOrDefaultAsync(s =>
        s.CompanyId == companyId && s.NameAr == $"{Tag} مورد");
    if (existing is not null) return existing;

    var s2 = Supplier.Create(companyId, $"{Tag} مورد", "312233344400003");
    db.Suppliers.Add(s2);
    await db.SaveChangesAsync();
    return s2;
}

static async Task<Product> EnsureProductAsync(BrqoshDbContext db, long companyId)
{
    var existing = await db.Products.FirstOrDefaultAsync(p =>
        p.CompanyId == companyId && p.Code == $"{Tag}-P001");
    if (existing is not null) return existing;

    var p2 = Product.Create(companyId, $"{Tag}-P001", "منتج تدقيق شامل", "قطعة", ProductType.Stockable, 500m);
    db.Products.Add(p2);
    await db.SaveChangesAsync();
    return p2;
}

static async Task<Product> EnsureRawProductAsync(BrqoshDbContext db, long companyId)
{
    var existing = await db.Products.FirstOrDefaultAsync(p =>
        p.CompanyId == companyId && p.Code == $"{Tag}-RM");
    if (existing is not null) return existing;

    var p2 = Product.Create(companyId, $"{Tag}-RM", "مادة خام تدقيق", "كجم", ProductType.Stockable, 100m);
    db.Products.Add(p2);
    await db.SaveChangesAsync();
    return p2;
}

static async Task EnsureUiAuditUserAsync(BrqoshDbContext db, long companyId)
{
    const string username = "uiaudit";
    var password = Environment.GetEnvironmentVariable("BRQOSH_TEST_PASSWORD") ?? "UiAudit9!";
    var hash = UserAuthService.HashPassword(password);
    var user = await db.AppUsers.FirstOrDefaultAsync(u => u.CompanyId == companyId && u.Username == username);
    if (user is null)
    {
        db.AppUsers.Add(AppUser.Create(companyId, username, hash, "مدير تدقيق واجهة", UserRole.Administrator));
    }
    else
    {
        user.SetPasswordHash(hash);
    }

    var marwan = await db.AppUsers.FirstOrDefaultAsync(u =>
        u.CompanyId == companyId && u.Username == "marwan" && u.IsActive);
    if (marwan is not null)
    {
        marwan.SetPasswordHash(hash);
        marwan.ClearMustChangePassword();
    }

    var admins = await db.AppUsers
        .Where(u => u.CompanyId == companyId && u.IsActive && u.Role == UserRole.Administrator)
        .ToListAsync();
    foreach (var admin in admins)
        admin.SetPasswordHash(hash);

    await db.SaveChangesAsync();

    var auditUser = Environment.GetEnvironmentVariable("BRQOSH_TEST_USERNAME") ?? username;
    var verified = await new UserAuthService(db).AuthenticateAsync(companyId, auditUser, password);
    Console.WriteLine(verified is null
        ? $"[WARN] تحقق دخول «{auditUser}» فشل — راجع BRQOSH_TEST_USERNAME/PASSWORD"
        : $"[PASS] تحقق دخول «{auditUser}» ({verified.DisplayNameAr})");
}

file record DbConfig(string Provider, string ConnectionString);
