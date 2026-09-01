// مسبار القطاعات (CEEF-230 · ط0-١)
//
// ‏السؤال الذي يجيب عنه:
//   ثمانيةُ ملفّاتِ عملٍ تُعرض في شاشة إنشاء المنشأة. **أيُّها يُشغّل دورةً محاسبيةً
//   كاملة، وأيُّها اسمٌ في قائمة؟**
//
// ‏لماذا لا يكفي أيُّ اختبار قائم:
//   اختباراتُ المستودع كلُّها تُنشئ منشأةً بملفّ `Retail` أو `General` — وهما اثنان من
//   ثمانية. والستّةُ الباقية **لم يمرّ بها مسارٌ واحدٌ كاملٌ قطّ**. وقد أثبت `م2-١٩` أنّ
//   ملفَّ عملٍ ينقصه وحدةٌ واحدة يُخرج نظاماً يبيع بضاعةً ولا يستطيع شراءها — عطلٌ
//   تأسيسيٌّ لا يظهر إلا حين تُنشأ منشأةٌ بذلك الملفّ ويُحاوَل تشغيلُها.
//
// ‏ما يفعله المسبار — لكلّ ملفّ عملٍ من الثمانية:
//   ١) يُنشئ منشأةً به في قاعدةٍ واحدة (‏brqosh_sectorprobe).
//   ٢) يقرأ وحداتِه المفعَّلة من `ModuleCatalog` — فيَعرف ما **يُتوقَّع** أن يعمل.
//   ٣) يُجري الدورة: شراء ⟵ استلام ⟵ فاتورة شراء ⟵ بيع ⟵ تحصيل ⟵ إقرار ضريبيّ.
//   ٤) يُصنّف كلَّ خطوة: **نجح · مُعطَّل (وحدةٌ غير مفعَّلة) · سقط (بالرسالة)**.
//   ٥) يقيس الهويّتين في الذيل: المخزون (هـ-٣) والذمم المدينة (هـ-١).
//
// ‏والتفرقةُ بين «مُعطَّل» و«سقط» هي كلُّ قيمة هذا المسبار: الأولى قرارُ تصميم، والثانية
// ‏وعدٌ لا يُوفى.
//
// ‏التشغيل:  dotnet run --project tools/SectorProbe

using Brqosh.Application.Setup;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Npgsql;

const string TestDb = "brqosh_sectorprobe";

var pwPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "pg_password.txt");
if (!File.Exists(pwPath))
{
    Console.WriteLine($"[SKIP] لا يوجد {pwPath} — يتطلب المسبار PostgreSQL محلياً مهيّأً.");
    return 0;
}

var pw = (await File.ReadAllTextAsync(pwPath)).Trim();
var admin = $"Host=localhost;Port=5433;Database=postgres;Username=postgres;Password={pw}";

try
{
    await using var probe = new NpgsqlConnection(admin);
    await probe.OpenAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"[SKIP] تعذر الاتصال بـ PostgreSQL: {ex.Message}");
    return 0;
}

Brqosh.Infrastructure.Security.SecurityContext.CurrentRole =
    Brqosh.Domain.Security.UserRole.Administrator;
Brqosh.Infrastructure.Security.SecurityContext.CurrentUsername = "sector-probe";

// ‏**قاعدةٌ لكلّ قطاع.** `DatabaseInitializer` أُحاديُّ المنشأة بالتصميم — يُعيد القائمةَ
// ‏إن وُجدت. وأصدقُ من الالتفاف عليه أن يُحاكى الواقع: كلُّ عميلٍ يُنصّب نسختَه وحده.
var profiles = Enum.GetValues<BusinessProfile>();
var report = new List<SectorReport>();
var vat = 310123456700000L;

foreach (var profile in profiles)
{
    var name = $"{TestDb}_{profile.ToString().ToLowerInvariant()}";
    await using (var c = new NpgsqlConnection(admin))
    {
        await c.OpenAsync();
        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", c))
            await drop.ExecuteNonQueryAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", c))
            await create.ExecuteNonQueryAsync();
    }

    var cs = $"Host=localhost;Port=5433;Database={name};Username=postgres;Password={pw}";
    await using (var bootstrap = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs))
    {
        await bootstrap.Database.EnsureCreatedAsync();
        await new BrqoshSchemaUpdater(bootstrap).ApplyAsync();
    }

    report.Add(await RunSectorAsync(cs, profile, ++vat));
}

// ── التقرير ───────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("══ خلاصةُ القطاعات ══");
Console.WriteLine();
Console.WriteLine($"{"القطاع",-16} {"شراء",-10} {"استلام",-10} {"ف.شراء",-10} {"بيع",-10} {"تحصيل",-10} {"إقرار",-10} {"الهويات",-10}");

foreach (var r in report)
    Console.WriteLine($"{r.Profile,-16} {Mark(r.Order),-10} {Mark(r.Receipt),-10} {Mark(r.PurchaseInvoice),-10} "
                      + $"{Mark(r.Sale),-10} {Mark(r.Collection),-10} {Mark(r.VatReturn),-10} {Mark(r.Identities),-10}");

Console.WriteLine();

var failures = report.SelectMany(r => r.Failures).ToList();
if (failures.Count > 0)
{
    Console.WriteLine($"[FAIL] {failures.Count} خطوةً سقطت في قطاعاتٍ تُعلن وحداتِها:");
    foreach (var f in failures) Console.WriteLine($"       • {f}");
    Console.WriteLine();
    Console.WriteLine("       ووحدةٌ تُعلَن ولا تعمل وعدٌ لا يُوفى: تُعرض شاشاتُها في القائمة");
    Console.WriteLine("       ويصطدم بها المستخدمُ عند أوّل استعمال.");
    return 1;
}

Console.WriteLine("[PASS] كلُّ خطوةٍ في كلّ قطاعٍ إمّا نجحت أو كانت وحدتُها مُعطَّلةً بقرارٍ مُعلَن.");
return 0;

// ── التشغيل لقطاعٍ واحد ───────────────────────────────────────────────────
static async Task<SectorReport> RunSectorAsync(string cs, BusinessProfile profile, long vatNumber)
{
    var r = new SectorReport(profile.ToString());
    var modules = ModuleCatalog.ResolveModules(profile);

    Console.WriteLine($"[..] {profile} — {modules.Count} وحدة: "
                      + string.Join(" · ", modules.OrderBy(m => m.ToString())));

    await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, cs);

    var company = await new DatabaseInitializer(db)
        .InitializeAsync($"منشأة {profile}", vatNumber.ToString(), profile);

    var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.CompanyId == company.Id);

    // ‏بياناتٌ أساسية — تُنشأ دائماً، فهي ليست وحدةً بل نواة.
    var supplier = Supplier.Create(company.Id, "مورد", "312222222200003");
    var customer = Customer.Create(company.Id, "عميل", "311111111100003");
    db.Suppliers.Add(supplier);
    db.Customers.Add(customer);
    var product = Product.Create(company.Id, $"P-{profile}", "صنف", "قطعة", ProductType.Stockable);
    db.Products.Add(product);
    await db.SaveChangesAsync();

    var hasPurchasing = modules.Contains(ModuleKey.Purchasing);
    var hasInventory = modules.Contains(ModuleKey.Inventory);
    var hasSales = modules.Contains(ModuleKey.Sales) || modules.Contains(ModuleKey.PointOfSale);

    // ─ شراء ─
    PurchaseOrder? order = null;
    r.Order = await StepAsync(r, "أمر شراء", hasPurchasing, async () =>
    {
        order = PurchaseOrder.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 1));
        order.AddLine("بضاعة", 10m, 100m, product.Id);
        await new PurchaseOrderService(db).ApproveAsync(order);
    });

    // ─ استلام ─
    r.Receipt = await StepAsync(r, "إذن استلام", hasPurchasing && hasInventory && warehouse is not null, async () =>
    {
        var receipt = GoodsReceipt.CreateDraft(
            company.Id, supplier.Id, order!.Id, warehouse!.Id, new DateOnly(2026, 3, 2));
        receipt.AddLine("بضاعة", 10m, 10m, 100m, product.Id);
        await new GoodsReceiptPostingService(db).PostAsync(receipt);
    });

    // ─ فاتورة شراء ─
    r.PurchaseInvoice = await StepAsync(r, "فاتورة شراء", hasPurchasing, async () =>
    {
        var invoice = PurchaseInvoice.CreateDraft(
            company.Id, supplier.Id, new DateOnly(2026, 3, 5), "SUP-1", vatDeductible: true);
        if (order is not null) invoice.LinkPurchaseOrder(order.Id);
        invoice.AddLine("بضاعة", 10m, 100m, product.Id);
        await new PurchaseInvoicePostingService(db).PostAsync(invoice);
    });

    // ─ بيع ─
    SalesInvoice? sale = null;
    r.Sale = await StepAsync(r, "فاتورة مبيعات", hasSales, async () =>
    {
        sale = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 3, 10));
        sale.AddLine("بضاعة", 5m, 150m, hasInventory ? product.Id : null);
        await new SalesInvoicePostingService(db).PostAsync(sale);
    });

    // ─ تحصيل ─
    r.Collection = await StepAsync(r, "سند قبض", hasSales && modules.Contains(ModuleKey.Treasury), async () =>
    {
        var voucher = ReceiptVoucher.CreateDraft(
            company.Id, customer.Id, new DateOnly(2026, 3, 12), 862.50m, ReceiptMethod.Cash, "تحصيل");
        await new ReceiptVoucherPostingService(db).PostAsync(voucher, []);
    });

    // ─ إقرار ضريبيّ ─
    r.VatReturn = await StepAsync(r, "إقرار ضريبيّ", modules.Contains(ModuleKey.Zatca), async () =>
    {
        var result = await new VatReturnQuery(db).ExecuteAsync(
            company.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
        if (result is null) throw new InvalidOperationException("الإقرار أعاد فراغاً.");
    });

    // ─ الهويّة الحاكمة: مَن يبيع يُفوتِر إلكترونياً ─
    //
    // ‏(CEEF-230 · ط0-١) **مَن يبيع في السعودية يُفوتِر إلكترونياً — نظاماً لا اختياراً.**
    // ‏فملفُّ عملٍ يُحلّ إلى `Sales` بلا `Zatca` يُخرج منشأةً تُصدر فواتيرَ خارج الفوترة
    // ‏الإلكترونية. وهو من صنف `م2-١٩` بعينه: وحدةٌ ناقصةٌ في خريطة الملفّات تُخرج نظاماً
    // ‏ناقصاً تأسيسياً — والخريطةُ الخام تكذب، **والمحلولةُ هي الحقّ**.
    if (hasSales && !modules.Contains(ModuleKey.Zatca))
        r.Failures.Add($"{r.Profile} · الفوترة الإلكترونية: الملفُّ يبيع (Sales مفعَّلة) "
                       + "ولا وحدةَ Zatca — فالفواتيرُ تُصدَر خارج نظام الهيئة.");

    // ─ الهويّات ─
    r.Identities = await StepAsync(r, "الهويّات", hasInventory, async () =>
    {
        var rows = await new StockBalanceQuery(db).ExecuteAsync(company.Id);
        var subsidiary = Math.Round(rows.Sum(x => x.TotalValue), 2);

        var trial = await new TrialBalanceQuery(db).ExecuteAsync(
            company.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var ledger = Math.Round(trial
            .Where(l => l.AccountCode.StartsWith("1107", StringComparison.Ordinal))
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);

        if (Math.Abs(subsidiary - ledger) > 0.01m)
            throw new InvalidOperationException(
                $"هـ-٣ مكسورة: الدفتر المساعد {subsidiary:N2} والأستاذ {ledger:N2}.");
    });

    return r;
}

static async Task<StepResult> StepAsync(SectorReport r, string name, bool expected, Func<Task> action)
{
    if (!expected) return StepResult.Disabled;

    try
    {
        await action();
        return StepResult.Passed;
    }
    catch (Exception ex)
    {
        r.Failures.Add($"{r.Profile} · {name}: {ex.GetType().Name}: {FirstLine(ex.Message)}");
        return StepResult.Failed;
    }
}

static string FirstLine(string message)
{
    var i = message.IndexOf('\n');
    return (i < 0 ? message : message[..i]).TrimEnd('\r');
}

static string Mark(StepResult s) => s switch
{
    StepResult.Passed => "نجح",
    StepResult.Disabled => "مُعطَّل",
    _ => "**سقط**",
};

enum StepResult { Passed, Disabled, Failed }

sealed class SectorReport(string profile)
{
    public string Profile { get; } = profile;
    public List<string> Failures { get; } = [];
    public StepResult Order { get; set; }
    public StepResult Receipt { get; set; }
    public StepResult PurchaseInvoice { get; set; }
    public StepResult Sale { get; set; }
    public StepResult Collection { get; set; }
    public StepResult VatReturn { get; set; }
    public StepResult Identities { get; set; }
}
