// مسبار القطاعات (CEEF-230 · ط0-١ · ط0-٣)
//
// ‏السؤال الذي يجيب عنه:
//   ثمانيةُ ملفّاتِ عملٍ تُعرض في شاشة إنشاء المنشأة، وكلُّ ملفٍّ يُعلن مجموعةَ وحدات.
//   **هل كلُّ وحدةٍ مُعلَنةٍ تعمل فعلاً، أم أنّ منها اسماً في قائمة؟**
//
// ‏لماذا لا يكفي أيُّ اختبار قائم:
//   اختباراتُ المستودع كلُّها تُنشئ منشأةً بملفّ `Retail` أو `General` — وهما اثنان من
//   ثمانية. وقد أثبت `م2-١٩` أنّ ملفَّ عملٍ ينقصه وحدةٌ واحدة يُخرج نظاماً يبيع بضاعةً
//   ولا يستطيع شراءها؛ وأثبتت `ط0-١` أنّ ملفّاً يبيع بلا `Zatca` يُصدر فواتيرَ خارج
//   نظام الهيئة. **وكلاهما لا يظهر إلا حين تُنشأ منشأةٌ بذلك الملفّ وتُشغَّل.**
//
// ‏(ط0-٣) ‏وما تغيّر في هذه النسخة — ولماذا:
//   كانت النسخةُ الأولى تُجري **دورةً ثابتة** (شراء ⟵ بيع ⟵ تحصيل ⟵ إقرار). وهي
//   تمسّ سبعَ وحداتٍ من ثلاث عشرة، **وستٌّ لا يمرّ بها شيء**: الأصولُ الثابتة والرواتبُ
//   ونقطةُ البيع والتصنيعُ والمقاولاتُ وإدارةُ العلاقات. والرواتبُ وحدَها **مُعلَنةٌ على
//   القطاعات الثمانية كلِّها** وهي صفرُ تشغيل.
//
//   فانقلب البناء: **سجلٌّ من مسبارٍ لكلّ وحدة**، لا دورةٌ ثابتة. وثلاثةُ قيودٍ تحكمه:
//
//     ١) **حارسُ التغطية**: كلُّ قيمةٍ في `ModuleKey` يلزمها مسبارٌ مسجَّل. ووحدةٌ بلا
//        مسبارٍ تُسقط المسبارَ كلَّه باسمها — **فالصمتُ عن وحدةٍ هو بعينه العطلُ المقصود**.
//     ٢) **الشرطُ هو الوحدةُ وحدَها**: خطوةٌ تُنفَّذ إن كانت وحدتُها محلولةً، بلا شروطٍ
//        مساعدة. فنقصُ مستودعٍ أو حسابٍ **سقوطٌ لا تخطٍّ** — والتبعياتُ مضمونةٌ في
//        `ModuleCatalog` فما نقص بعدها عطلٌ حقيقيّ.
//     ٣) **لا يكفي ألّا تُرمى**: كلُّ مسبارٍ يقيس **أثراً**: صفٌّ يُكتب، أو رصيدُ حسابٍ
//        يتحرّك بالمقدار المتوقَّع. ومسبارٌ يستدعي خدمةً ولا يقيس شيئاً يشهد للعطل.
//
// ‏التشغيل:  dotnet run --project tools/SectorProbe

using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Crm;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Crm;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Npgsql;

const string TestDb = "brqosh_sectorprobe";

// ── حارسُ التغطية: يسبق كلَّ تشغيل ─────────────────────────────────────────
// ‏(ط0-٣) ‏وحدةٌ بلا مسبارٍ لا تُقاس — **وهي بعينها الحالةُ التي جاء هذا البند لها**.
// ‏فلو أُضيفت وحدةٌ جديدة غداً ونُسي مسبارُها لسقط هذا الحارسُ باسمها، ولم تمرّ خضراءَ
// ‏بالصمت. وهو يسبق فتحَ القاعدة كي يُقال العطلُ في ثانية.
var uncovered = Enum.GetValues<ModuleKey>()
    .Where(m => !ModuleProbes.All.Any(p => p.Module == m))
    .ToList();

if (uncovered.Count > 0)
{
    Console.WriteLine($"[FAIL] {uncovered.Count} وحدةً بلا مسبار — فهي **خارج القياس** تماماً:");
    foreach (var m in uncovered) Console.WriteLine($"       • {m}");
    Console.WriteLine();
    Console.WriteLine("       ووحدةٌ تُعلَن في ملفّ عملٍ ولا يمسّها مسبارٌ تمرّ خضراءَ بالصمت،");
    Console.WriteLine("       وهو ما يُخرج «كلُّ القطاعات تعمل» من أداةٍ لم تشغّل نصفَها.");
    return 3;
}

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
    await using var conn = new NpgsqlConnection(admin);
    await conn.OpenAsync();
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
Console.WriteLine("══ مصفوفةُ القطاعات × الوحدات ══");
Console.WriteLine();
Console.WriteLine("القطاع".PadRight(16)
                  + string.Concat(ModuleProbes.All.Select(p => Short(p.Module).PadRight(10))));

foreach (var r in report)
    Console.WriteLine(r.Profile.PadRight(16)
                      + string.Concat(ModuleProbes.All.Select(p => Mark(r.Results[p.Module]).PadRight(10))));

Console.WriteLine();
var ran = report.Sum(r => r.Results.Count(x => x.Value == StepResult.Passed));
var off = report.Sum(r => r.Results.Count(x => x.Value == StepResult.Disabled));
Console.WriteLine($"       {ran} خطوةً نُفِّذت وقيس أثرُها · {off} مُعطَّلةً بقرارٍ مُعلَن "
                  + $"· {report.Count} قطاعاً × {ModuleProbes.All.Count} وحدة.");
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

Console.WriteLine("[PASS] كلُّ وحدةٍ مُعلَنةٍ في كلّ قطاعٍ نُفِّذ مسارُها وقيس أثرُها.");
Console.WriteLine("       ⚠️ وهذه إشارةٌ على أنّ الوحدةَ **تعمل**، لا على أنّها **كاملة**:");
Console.WriteLine("          المسبارُ يقيس المسارَ الجوهريَّ لكلّ وحدة، ولا يقيس ميزةً لا وجودَ لها.");
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

    // ‏بياناتٌ أساسية — تُنشأ دائماً، فهي نواةٌ لا وحدة.
    var supplier = Supplier.Create(company.Id, "مورد", "312222222200003");
    var customer = Customer.Create(company.Id, "عميل", "311111111100003");
    db.Suppliers.Add(supplier);
    db.Customers.Add(customer);
    // ‏سعرُ البيع جزءٌ من النواة لا زينة: نقطةُ البيع تقرؤه من الصنف، وبلا سعرٍ يخرج
    // ‏إجماليٌّ صفرٌ فترفضه القاعدةُ **بحقّ** — وأوّلُ تشغيلٍ سقط هنا بعطلٍ في المسبار.
    var product = Product.Create(
        company.Id, $"P-{profile}", "صنف", "قطعة", ProductType.Stockable, salePrice: 150m);
    db.Products.Add(product);
    await db.SaveChangesAsync();

    var ctx = new ProbeContext(db, company.Id, supplier, customer, product, modules);

    foreach (var probe in ModuleProbes.All)
    {
        if (!modules.Contains(probe.Module))
        {
            r.Results[probe.Module] = StepResult.Disabled;
            continue;
        }

        try
        {
            await probe.Run(ctx);
            r.Results[probe.Module] = StepResult.Passed;
        }
        catch (Exception ex)
        {
            r.Failures.Add($"{r.Profile} · {probe.Name}: {Explain(ex)}");
            r.Results[probe.Module] = StepResult.Failed;
        }
        finally
        {
            // ‏(CEEF-230 · ط0-٣) **عزلُ الخطوات — وهو تصحيحٌ لعطلٍ في المسبار نفسِه.**
            //
            // ‏أوّلُ تشغيلٍ أخرج **عشرين سقوطاً في ستّ وحدات**، وكلُّها بالسبب نفسِه:
            // ‏`23505` على `PK_payroll_runs`. والحقيقةُ أنّ **واحدةً سقطت** والبواقي
            // ‏تسمّمت: الكيانُ الذي فشل حفظُه يبقى في `ChangeTracker`، فكلُّ
            // ‏`SaveChangesAsync` بعده يُعيد محاولةَ إدراجه ويسقط بسببِ الأولى.
            //
            // ‏**فكدتُ أُبلّغ عن ستّة أعطالٍ وهي واحد** — ولم يكشفه إلا طباعةُ أعمقِ
            // ‏استثناء: اسمُ قيدٍ لا صلةَ له بالخطوة المذكورة.
            //
            // ‏والتصفيةُ هنا **أصدقُ محاكاةً** لا تساهل: الحاوية تُسجّل `DbContext`
            // ‏بعمرِ نطاق، فلكلّ شاشةٍ سياقُها. وسياقٌ واحدٌ لثلاث عشرة وحدة هو الاصطناع.
            ctx.Db.ChangeTracker.Clear();
        }
    }

    // ─ الهويّة الحاكمة: مَن يبيع يُفوتِر إلكترونياً ─
    //
    // ‏(CEEF-230 · ط0-١) **مَن يبيع في السعودية يُفوتِر إلكترونياً — نظاماً لا اختياراً.**
    // ‏فملفُّ عملٍ يُحلّ إلى `Sales` بلا `Zatca` يُخرج منشأةً تُصدر فواتيرَ خارج الفوترة
    // ‏الإلكترونية. وهو من صنف `م2-١٩` بعينه: وحدةٌ ناقصةٌ في خريطة الملفّات تُخرج نظاماً
    // ‏ناقصاً تأسيسياً — والخريطةُ الخام تكذب، **والمحلولةُ هي الحقّ**.
    if (modules.Contains(ModuleKey.Sales) && !modules.Contains(ModuleKey.Zatca))
        r.Failures.Add($"{r.Profile} · الفوترة الإلكترونية: الملفُّ يبيع (Sales مفعَّلة) "
                       + "ولا وحدةَ Zatca — فالفواتيرُ تُصدَر خارج نظام الهيئة.");

    return r;
}

/// <summary>
/// ‏(CEEF-230 · ط0-٣) <b>السببُ من أعمق استثناء، لا من أعلاه.</b>
///
/// <para>أوّلُ تشغيلٍ أخرج عشرين سقوطاً كلُّها بنصٍّ واحد: <c>DbUpdateException: An error
/// occurred while saving the entity changes. See the inner exception for details.</c>
/// — <b>فسمّى الخطوةَ ولم يُسمِّ العطل</b>. ومسبارٌ يقول «سقط» ولا يقول «لماذا» يُحوّل
/// القياسَ إلى تخمين. فيُطبع الآن نوعُ الاستثناء الخارجيّ ورسالةُ أعمقِ داخليّ.</para>
/// </summary>
static string Explain(Exception ex)
{
    var deepest = ex;
    while (deepest.InnerException is not null) deepest = deepest.InnerException;

    var head = ex.GetType().Name;
    return ReferenceEquals(deepest, ex)
        ? $"{head}: {FirstLine(ex.Message)}"
        : $"{head} ⟵ {deepest.GetType().Name}: {FirstLine(deepest.Message)}";
}

static string FirstLine(string message)
{
    var i = message.IndexOf('\n');
    return (i < 0 ? message : message[..i]).TrimEnd('\r');
}

static string Mark(StepResult s) => s switch
{
    StepResult.Passed => "نجح",
    StepResult.Disabled => "—",
    _ => "**سقط**",
};

static string Short(ModuleKey m) => m switch
{
    ModuleKey.GeneralLedger => "أستاذ",
    ModuleKey.Inventory => "مخزون",
    ModuleKey.Purchasing => "شراء",
    ModuleKey.Sales => "بيع",
    ModuleKey.Treasury => "خزينة",
    ModuleKey.Zatca => "فوترة",
    ModuleKey.Reports => "تقارير",
    ModuleKey.FixedAssets => "أصول",
    ModuleKey.Payroll => "رواتب",
    ModuleKey.PointOfSale => "نقطةبيع",
    ModuleKey.Manufacturing => "تصنيع",
    ModuleKey.ProjectsConstruction => "مقاولات",
    ModuleKey.Crm => "علاقات",
    _ => m.ToString(),
};

enum StepResult { Passed, Disabled, Failed }

sealed class SectorReport(string profile)
{
    public string Profile { get; } = profile;
    public List<string> Failures { get; } = [];
    public Dictionary<ModuleKey, StepResult> Results { get; } = [];
}

/// <summary>‏الحالةُ المشتركةُ بين مسابر الوحدات في القطاع الواحد.</summary>
sealed class ProbeContext(
    BrqoshDbContext db, long companyId, Supplier supplier, Customer customer, Product product,
    IReadOnlySet<ModuleKey> modules)
{
    public BrqoshDbContext Db { get; } = db;
    public long CompanyId { get; } = companyId;
    public Supplier Supplier { get; } = supplier;
    public Customer Customer { get; } = customer;
    public Product Product { get; } = product;
    public IReadOnlySet<ModuleKey> Modules { get; } = modules;

    /// <summary>
    /// ‏رصيدُ حسابٍ من ميزان المراجعة.
    ///
    /// <para><b>وقيدٌ مُعلَن:</b> كلُّ التأكيدات تمرّ بـ<c>TrialBalanceQuery</c>، فلو كان
    /// هو المعطوبَ لكذبت كلُّها معاً. ويحرسه جزئياً مسبارُ <c>Reports</c>: يتحقّق أنّ
    /// مجموع المدين يساوي مجموع الدائن وأنّ الميزانَ ليس فارغاً — <b>وهو حارسٌ جزئيّ
    /// لا كامل</b>.</para>
    /// </summary>
    public async Task<decimal> BalanceAsync(string accountCode)
    {
        var trial = await new TrialBalanceQuery(Db).ExecuteAsync(
            CompanyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial
            .Where(l => l.AccountCode.StartsWith(accountCode, StringComparison.Ordinal))
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    public async Task<long> AccountIdAsync(string code) =>
        (await Db.Accounts.AsNoTracking()
            .SingleAsync(a => a.CompanyId == CompanyId && a.Code == code)).Id;

    /// <summary>
    /// ‏المستودعُ الافتراضيّ — <b>وغيابُه سقوطٌ لا تخطٍّ</b>: هذه الدالّة لا تُستدعى إلا
    /// من وحدةٍ محلولةٍ تحتاج مخزوناً، فمنشأةٌ بلا مستودعٍ حينها عطلُ تهيئةٍ حقيقيّ.
    /// </summary>
    public async Task<Warehouse> WarehouseAsync() =>
        await Db.Warehouses.FirstOrDefaultAsync(w => w.CompanyId == CompanyId)
        ?? throw new InvalidOperationException(
            "لا مستودعَ في منشأةٍ وحدةُ المخزون فيها مفعَّلة.");

    public async Task<decimal> OnHandAsync(long productId) =>
        (await new StockBalanceQuery(Db).ExecuteAsync(CompanyId))
        .Where(x => x.ProductId == productId).Sum(x => x.Quantity);

    public static void Expect(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}

sealed record ModuleProbe(ModuleKey Module, string Name, Func<ProbeContext, Task> Run);

/// <summary>
/// ‏(CEEF-230 · ط0-٣) <b>مسبارٌ لكلّ وحدة — وكلُّ مسبارٍ يقيس أثراً.</b>
///
/// <para>الترتيبُ مقصود: ما تعتمد عليه وحدةٌ يسبقها. والشراءُ يسبق المخزونَ لأنّه يُدخل
/// البضاعة، والمخزونُ يسبق التصنيعَ ونقطةَ البيع لأنّهما يستهلكانها.</para>
/// </summary>
static class ModuleProbes
{
    public static IReadOnlyList<ModuleProbe> All { get; } =
    [
        // ─ الأستاذ العام: قيدٌ يدويٌّ يُرحَّل ويتحرّك به الحساب ─
        new(ModuleKey.GeneralLedger, "الأستاذ العام — قيدٌ يدويّ", async c =>
        {
            var cash = await c.Db.Accounts.SingleAsync(
                a => a.CompanyId == c.CompanyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);
            var equity = await c.Db.Accounts.SingleAsync(
                a => a.CompanyId == c.CompanyId && a.Code == "3101");

            var before = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);

            var entry = JournalEntry.CreateDraft(c.CompanyId, new DateOnly(2026, 1, 5), "رأسُ مالٍ افتتاحيّ");
            entry.AddLine(cash, 50_000m, 0m);
            entry.AddLine(equity, 0m, 50_000m);
            await new JournalPostingService(c.Db).PostAsync(entry);

            var after = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
            ProbeContext.Expect(after - before == 50_000m,
                $"القيدُ رُحِّل ولم يتحرّك النقد: قبل {before:N2} وبعد {after:N2}.");
        }),

        // ─ الشراء: أمرٌ ⟵ استلامٌ ⟵ فاتورةٌ، وذمّةُ المورّد ترتفع ─
        new(ModuleKey.Purchasing, "الشراء — أمرٌ واستلامٌ وفاتورة", async c =>
        {
            var warehouse = await c.WarehouseAsync();
            var before = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable);

            var order = PurchaseOrder.CreateDraft(c.CompanyId, c.Supplier.Id, new DateOnly(2026, 3, 1));
            order.AddLine("بضاعة", 100m, 100m, c.Product.Id);
            await new PurchaseOrderService(c.Db).ApproveAsync(order);

            var receipt = GoodsReceipt.CreateDraft(
                c.CompanyId, c.Supplier.Id, order.Id, warehouse.Id, new DateOnly(2026, 3, 2));
            receipt.AddLine("بضاعة", 100m, 100m, 100m, c.Product.Id);
            await new GoodsReceiptPostingService(c.Db).PostAsync(receipt);

            var invoice = PurchaseInvoice.CreateDraft(
                c.CompanyId, c.Supplier.Id, new DateOnly(2026, 3, 5), "SUP-1", vatDeductible: true);
            invoice.LinkPurchaseOrder(order.Id);
            invoice.AddLine("بضاعة", 100m, 100m, c.Product.Id);
            await new PurchaseInvoicePostingService(c.Db).PostAsync(invoice);

            var after = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable);
            ProbeContext.Expect(before - after == 11_500m,
                $"ذمّةُ المورّد لم ترتفع بقيمة الفاتورة: الفرق {before - after:N2} والمنتظر 11,500.00.");
        }),

        // ─ المخزون: جردٌ يُصحّح الرصيد، وهـ-٣ تصمد ─
        new(ModuleKey.Inventory, "المخزون — جردٌ وهـ-٣", async c =>
        {
            var warehouse = await c.WarehouseAsync();
            var before = await c.OnHandAsync(c.Product.Id);

            await new StockCountAdjustmentService(c.Db).PostVariancesAsync(
                c.CompanyId, warehouse.Id, new DateOnly(2026, 3, 3),
                [(c.Product.Id, before, before + 10m, 100m)], "sector-probe");

            var after = await c.OnHandAsync(c.Product.Id);
            ProbeContext.Expect(after - before == 10m,
                $"الجردُ رُحِّل ولم يتحرّك الرصيد: قبل {before:N4} وبعد {after:N4}.");

            var rows = await new StockBalanceQuery(c.Db).ExecuteAsync(c.CompanyId);
            var subsidiary = Math.Round(rows.Sum(x => x.TotalValue), 2);
            var ledger = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.Inventory);
            ProbeContext.Expect(Math.Abs(subsidiary - ledger) <= 0.01m,
                $"هـ-٣ مكسورة: الدفترُ المساعد {subsidiary:N2} والأستاذ {ledger:N2}.");
        }),

        // ─ المبيعات: فاتورةٌ ترفع ذمّةَ العميل ─
        new(ModuleKey.Sales, "المبيعات — فاتورةٌ وذمّة", async c =>
        {
            var before = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable);

            var sale = SalesInvoice.CreateDraft(c.CompanyId, c.Customer.Id, new DateOnly(2026, 3, 10));
            sale.AddLine("بضاعة", 5m, 150m, c.Modules.Contains(ModuleKey.Inventory) ? c.Product.Id : null);
            await new SalesInvoicePostingService(c.Db).PostAsync(sale);

            var after = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
            ProbeContext.Expect(after - before == 862.50m,
                $"ذمّةُ العميل لم ترتفع بقيمة الفاتورة: الفرق {after - before:N2} والمنتظر 862.50.");
        }),

        // ─ الخزينة: سندُ قبضٍ يُنقص الذمّةَ ويرفع النقد ─
        new(ModuleKey.Treasury, "الخزينة — سندُ قبض", async c =>
        {
            var arBefore = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
            var cashBefore = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);

            var voucher = ReceiptVoucher.CreateDraft(
                c.CompanyId, c.Customer.Id, new DateOnly(2026, 3, 12), 500m, ReceiptMethod.Cash, "تحصيل");
            await new ReceiptVoucherPostingService(c.Db).PostAsync(voucher, []);

            var arAfter = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
            var cashAfter = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);

            ProbeContext.Expect(arBefore - arAfter == 500m,
                $"الذمّةُ لم تنخفض بالمحصَّل: الفرق {arBefore - arAfter:N2}.");
            ProbeContext.Expect(cashAfter - cashBefore == 500m,
                $"النقدُ لم يرتفع بالمحصَّل: الفرق {cashAfter - cashBefore:N2}.");
        }),

        // ─ الفوترة الإلكترونية: الإقرارُ يوافق الأستاذ ─
        new(ModuleKey.Zatca, "الفوترة — إقرارٌ ضريبيّ", async c =>
        {
            var result = await new VatReturnQuery(c.Db).ExecuteAsync(
                c.CompanyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
            ProbeContext.Expect(result is not null, "الإقرارُ أعاد فراغاً.");

            var output = -await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput);
            ProbeContext.Expect(Math.Abs(result!.OutputVat - output) <= 0.01m,
                $"الإقرارُ يخالف الأستاذ: الإقرار {result.OutputVat:N2} والأستاذ {output:N2}.");
        }),

        // ─ التقارير: الميزانُ متوازنٌ والمركزُ المالي متوازن ─
        new(ModuleKey.Reports, "التقارير — توازنُ الميزان والمركز", async c =>
        {
            var trial = await new TrialBalanceQuery(c.Db).ExecuteAsync(
                c.CompanyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            var debit = Math.Round(trial.Sum(l => l.TotalDebit), 2);
            var credit = Math.Round(trial.Sum(l => l.TotalCredit), 2);
            ProbeContext.Expect(Math.Abs(debit - credit) <= 0.01m,
                $"ميزانُ المراجعة غيرُ متوازن: مدين {debit:N2} ودائن {credit:N2}.");
            ProbeContext.Expect(debit > 0m, "ميزانُ المراجعة فارغ — فما قِيس شيء.");

            var sheet = await new BalanceSheetQuery(c.Db).ExecuteAsync(
                c.CompanyId, new DateOnly(2026, 12, 31));
            ProbeContext.Expect(Math.Abs(sheet.TotalAssets - sheet.TotalLiabilitiesAndEquity) <= 0.01m,
                $"المركزُ المالي غيرُ متوازن: أصول {sheet.TotalAssets:N2} "
                + $"ومطلوبات وحقوق {sheet.TotalLiabilitiesAndEquity:N2}.");
        }),

        // ─ الأصول الثابتة: اقتناءٌ ثمّ إهلاكٌ شهريّ ─
        new(ModuleKey.FixedAssets, "الأصول الثابتة — اقتناءٌ وإهلاك", async c =>
        {
            var assetAccount = await c.AccountIdAsync("1203");
            var accumAccount = await c.AccountIdAsync("1207");
            var before = await c.BalanceAsync("1203");

            var asset = FixedAsset.Create(c.CompanyId, "FA-1", "آلة", new DateOnly(2026, 1, 1),
                acquisitionCost: 120_000m, salvageValue: 0m, usefulLifeMonths: 60,
                glAssetAccountId: assetAccount, glDepreciationAccountId: accumAccount);
            c.Db.FixedAssets.Add(asset);
            await c.Db.SaveChangesAsync();

            await new FixedAssetAcquisitionPostingService(c.Db).PostAcquisitionAsync(asset);
            ProbeContext.Expect(await c.BalanceAsync("1203") - before == 120_000m,
                "حسابُ الأصل لم يرتفع بتكلفة الاقتناء.");

            var count = await new DepreciationService(c.Db)
                .RunMonthlyAsync(c.CompanyId, new DateOnly(2026, 2, 28), "sector-probe");
            ProbeContext.Expect(count > 0, "الإهلاكُ الشهريّ لم يُنتج قيداً واحداً.");

            var accumulated = await c.BalanceAsync("1207");
            ProbeContext.Expect(accumulated < 0m,
                $"مجمّعُ الإهلاك لم يتحرّك: {accumulated:N2}.");
        }),

        // ─ الرواتب: دورةٌ تُرحَّل فيرتفع مصروفُ الرواتب ─
        //
        // ‏وهي **الوحدةُ المُعلَنة على القطاعات الثمانية كلِّها** ولم يمرّ بها مسارٌ قطّ
        // ‏قبل هذا البند — وهي بعينها الحالةُ التي بُني لها ط0-٣.
        new(ModuleKey.Payroll, "الرواتب — دورةٌ تُرحَّل", async c =>
        {
            var before = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalariesExpense);

            var run = PayrollRun.CreateDraft(c.CompanyId, 2026, 3, new DateOnly(2026, 3, 28));
            run.AddLine("موظّف أوّل", basic: 8_000m, housing: 2_000m, transport: 500m,
                gosiEmployee: 900m, gosiEmployer: 1_100m);
            run.AddLine("موظّف ثانٍ", basic: 5_000m, housing: 1_250m, transport: 400m);

            // ‏**ولا تُضاف الدورةُ هنا**: `PostAsync` يُضيفها بنفسه — ومَن يُضيفها قبله
            // ‏يُدرجها مرّتين فيسقط بـ`23505`. وهو ما فعله أوّلُ تشغيل.
            await new PayrollPostingService(c.Db).PostAsync(run, "sector-probe");

            var after = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalariesExpense);
            ProbeContext.Expect(after - before > 0m,
                $"دورةُ الرواتب رُحِّلت ولم يتحرّك مصروفُ الرواتب: قبل {before:N2} وبعد {after:N2}.");
        }),

        // ─ التصنيع: قائمةُ موادّ ⟵ أمرُ تشغيلٍ ⟵ إتمامٌ يُدخل المنتجَ التامّ ─
        new(ModuleKey.Manufacturing, "التصنيع — أمرُ تشغيلٍ يُتمّ", async c =>
        {
            var finished = Product.Create(
                c.CompanyId, $"FG-{c.CompanyId}", "منتج تامّ", "قطعة", ProductType.Stockable);
            c.Db.Products.Add(finished);
            await c.Db.SaveChangesAsync();

            var bom = BillOfMaterials.Create(c.CompanyId, "BOM-1", "قائمةُ موادّ", finished.Id, 1m);
            bom.AddLine(c.Product.Id, 2m);
            c.Db.BillsOfMaterials.Add(bom);
            await c.Db.SaveChangesAsync();

            var order = WorkOrder.CreateDraft(c.CompanyId, bom.Id, new DateOnly(2026, 3, 15), 3m);
            c.Db.WorkOrders.Add(order);
            await c.Db.SaveChangesAsync();

            var service = new WorkOrderService(c.Db);
            await service.ReleaseAsync(c.CompanyId, order.Id);
            await service.CompleteAsync(c.CompanyId, order.Id, 3m, "sector-probe");

            ProbeContext.Expect(await c.OnHandAsync(finished.Id) == 3m,
                "أمرُ التشغيل أُتمّ ولم يدخل المنتجُ التامّ المخزون.");
        }),

        // ─ نقطة البيع: ورديةٌ تُفتح وبيعةٌ ترفع النقد ─
        new(ModuleKey.PointOfSale, "نقطة البيع — ورديةٌ وبيعة", async c =>
        {
            var cashBefore = await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);

            var shift = PosShift.Open(c.CompanyId, "أمين الصندوق", 0m);
            c.Db.PosShifts.Add(shift);
            await c.Db.SaveChangesAsync();

            var sale = await new PosSaleService(c.Db).SellAsync(
                c.CompanyId, shift.Id, null, [(c.Product.Id, 2m)],
                isVatRegistered: true, PosPaymentMethod.Cash, "sector-probe");

            ProbeContext.Expect(sale is not null, "بيعةُ نقطة البيع أعادت فراغاً.");
            ProbeContext.Expect(await c.BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand) > cashBefore,
                "بيعةُ نقطة البيع لم ترفع النقد.");
        }),

        // ─ المقاولات: مشروعٌ يُنشأ وتكلفةٌ تُستوعب ─
        new(ModuleKey.ProjectsConstruction, "المقاولات — مشروعٌ وتكلفة", async c =>
        {
            var project = ConstructionProject.Create(
                c.CompanyId, "PRJ-1", "مشروعُ القياس", c.Customer.Id,
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
                contractValue: 1_000_000m, retentionPercent: 10m,
                location: "الرياض", estimatedTotalCost: 800_000m);
            await new ConstructionProjectService(c.Db).CreateAsync(project);

            await new ProjectCostService(c.Db).RecordAsync(
                c.CompanyId, project.Id, new DateOnly(2026, 3, 20), "تكلفةُ تنفيذ",
                75_000m, "sector-probe");

            var incurred = await new ProjectCostService(c.Db).IncurredCostAsync(c.CompanyId, project.Id);
            ProbeContext.Expect(incurred == 75_000m,
                $"التكلفةُ سُجّلت ولم تُستوعب في المشروع: {incurred:N2} والمنتظر 75,000.00.");
        }),

        // ─ إدارة العلاقات: عميلٌ محتملٌ يُحوَّل إلى عميل ─
        new(ModuleKey.Crm, "إدارة العلاقات — تحويلُ عميلٍ محتمل", async c =>
        {
            var before = await c.Db.Customers.CountAsync(x => x.CompanyId == c.CompanyId);

            var lead = CrmLead.Create(c.CompanyId, "عميلٌ محتمل", "0500000000", "lead@example.sa");
            var service = new CrmService(c.Db);
            await service.CreateLeadAsync(lead);
            lead.MarkQualified();
            await c.Db.SaveChangesAsync();

            var converted = await service.ConvertLeadAsync(c.CompanyId, lead.Id);
            ProbeContext.Expect(converted is not null, "التحويلُ أعاد فراغاً.");

            var after = await c.Db.Customers.CountAsync(x => x.CompanyId == c.CompanyId);
            ProbeContext.Expect(after == before + 1,
                $"العميلُ المحتمل حُوِّل ولم يظهر عميلٌ جديد: قبل {before} وبعد {after}.");
        }),
    ];
}
