using System.Diagnostics;
using System.Globalization;
using System.Text;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Intelligence;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Performance;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل يستحمل خمس سنواتٍ من البيانات؟ وهل في الصفحات بطء؟») <b>قياسُ شاشاتِ القراءة فوق دفاترَ لخمس سنوات.</b>
///
/// <para><b>ما كان مقيساً:</b> سرعةُ <i>الكتابة</i> فقط — ثمنُ ترحيل القيد (<see cref="JournalStressLoadTests"/>). أمّا <i>القراءة</i> — اللوحةُ
/// وميزانُ المراجعة وقائمةُ الدخل ودفترُ الأستاذ وكشفُ العميل وأعمارُ الديون وإقرارُ الضريبة — فعلى دفاترَ ممتلئةٍ لم تُقَس قطّ
/// (<c>PERFORMANCE_BUDGETS.md</c> يقول ذلك صراحةً).</para>
///
/// <para><b>كيف:</b> يُرحَّل شهراً بشهرٍ بخدمات البرنامج نفسِها (فواتير بيع وشراء، سندات قبض وصرف، قيود يدويّة) على قاعدةٍ في ملفّ —
/// ثم تُقاس الاستعلاماتُ التي تقف خلف شاشات القراءة، <b>ثلاثاً لكلٍّ ويُنشَر الوسيط</b>. الافتراضيُّ شهران (دخانٌ سريع يثبت أنّ الأداة تعمل)؛
/// والخمسُ سنوات: <c>BRQOSH_SCALE_MONTHS=60</c>. المزوّد: <c>BRQOSH_LOAD_PROVIDER=postgres</c> وإلّا SQLite في ملفّ.</para>
///
/// <para>الحدُّ المؤكَّد عشرُ ثوانٍ للاستعلام الواحد — فوقه شاشةٌ يظنّها المستخدمُ معلَّقة. وإن زاد استعلامٌ فالعلاجُ في الاستعلام
/// لا في رفع الحدّ.</para>
/// </summary>
public class FiveYearsOfBooksTests(Xunit.Abstractions.ITestOutputHelper output)
{
    public const string MonthsVariable = "BRQOSH_SCALE_MONTHS";
    public const string ReportVariable = "BRQOSH_SCALE_REPORT";

    internal static int MonthsFrom(string? value) =>
        int.TryParse(value, out var months) && months >= 1 ? Math.Min(months, 120) : 2;

    private const double BudgetSeconds = 10;

    private sealed record Sample(string Name, double MedianMs, double MaxMs, string Note);

    [Fact]
    public async Task EveryReadScreenStaysUnderTheBudget_OverTheYears()
    {
        TestActor.Administrator();
        var months = MonthsFrom(Environment.GetEnvironmentVariable(MonthsVariable));

        if (JournalStressLoadTests.UsesPostgres(Environment.GetEnvironmentVariable(JournalStressLoadTests.ProviderVariable)))
        {
            await using var scope = await PostgresTestDatabase.CreateAsync("five-years");
            await RunAsync(scope.Db, scope.OpenIndependentContext, months, "PostgreSQL", scope.OpenReadContext);
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"brqosh-five-years-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite($"Data Source={path}").Options;
            await using var db = new BrqoshDbContext(options);
            db.Database.EnsureCreated();
            await RunAsync(db, () => new BrqoshDbContext(options), months, "SQLite (ملف)");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var f in new[] { path, path + "-wal", path + "-shm" })
                try { File.Delete(f); } catch (IOException) { }
        }
    }

    private async Task RunAsync(BrqoshDbContext db, Func<BrqoshDbContext> fresh, int months, string provider, Func<BrqoshDbContext>? readFresh = null)
    {
        var company = await new DatabaseInitializer(db).InitializeAsync("شركة السنوات", "310123456700003", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(db).ApplyAsync();
        var companyId = company.Id;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));

        var seedWatch = Stopwatch.StartNew();
        var counts = await SeedAsync(db, fresh, companyId, first, months);
        seedWatch.Stop();
        output.WriteLine($"[تهيئة] {provider} · {months} شهراً · {counts} · {seedWatch.Elapsed.TotalSeconds:N0}s");

        // ‏أثقلُ عميلٍ في الدفاتر (ثلاثون في المئة من المبيعات) — كشفُه هو الأسوأ.
        var heavyCustomer = (await db.Customers.Where(c => c.CompanyId == companyId).OrderBy(c => c.Id).FirstAsync()).Id;
        var cashAccount = "1101";
        var fiveYears = first;
        var thisYear = new DateOnly(today.Year, 1, 1);
        var lastQuarter = today.AddMonths(-3);
        var samples = new List<Sample>();

        async Task Measure(string name, Func<BrqoshDbContext, Task<string>> work)
        {
            var times = new List<double>();
            var note = "";
            for (var run = 0; run < 3; run++)
            {
                await using var ctx = (readFresh ?? fresh)();
                var sw = Stopwatch.StartNew();
                note = await work(ctx);
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }

            times.Sort();
            samples.Add(new Sample(name, times[1], times[^1], note));
        }

        await Measure("لوحة التحكم (DashboardQuery)", async c => { var k = await new DashboardQuery(c).ExecuteAsync(companyId); return $"ذمم {k.Receivables:N0}"; });
        await Measure("رؤى القرار المالي (AnalyzeAsync)", async c =>
        {
            var r = await new FinancialIntelligenceService(c, new DataQualityService(c)).AnalyzeAsync(companyId);
            return $"{r.Insights.Count} رؤية";
        });
        await Measure("توقّع النقد ١٣ أسبوعاً", async c => { var f = await new CashFlowForecastService(c).ForecastAsync(companyId); return $"تحصيل {f.TotalProjectedInflows:N0}"; });
        await Measure("ميزان المراجعة — كلّ المدة", async c => { var t = await new TrialBalanceQuery(c).ExecuteAsync(companyId, fiveYears, today); return $"{t.Count} حساباً"; });
        await Measure("ميزان المراجعة — هذا الشهر", async c => { var t = await new TrialBalanceQuery(c).ExecuteAsync(companyId, new DateOnly(today.Year, today.Month, 1), today); return $"{t.Count} حساباً"; });
        await Measure("قائمة الدخل — هذه السنة", async c => { var r = await new IncomeStatementQuery(c).ExecuteAsync(companyId, thisYear, today); return $"صافي {r.NetIncome:N0}"; });
        await Measure("قائمة المركز المالي", async c => { var r = await new BalanceSheetQuery(c).ExecuteAsync(companyId, today); return "تمّت"; });
        await Measure("قائمة التدفقات — هذه السنة", async c => { var r = await new CashFlowStatementService(c).ExecuteAsync(companyId, thisYear, today); return "تمّت"; });
        await Measure("دفتر الأستاذ — حساب الصندوق، كلّ المدة", async c =>
        {
            var r = await new GeneralLedgerQuery(c).ExecuteAsync(companyId, fiveYears, today, cashAccount);
            return $"{r.Sum(a => a.Lines.Count):N0} سطراً";
        });
        await Measure("دفتر الأستاذ — الشهر الأخير (كلّ الحسابات)", async c =>
        {
            var r = await new GeneralLedgerQuery(c).ExecuteAsync(companyId, new DateOnly(today.Year, today.Month, 1), today);
            return $"{r.Count} حساباً";
        });
        await Measure("إقرار الضريبة — الربع الأخير", async c => { var r = await new VatReturnQuery(c).ExecuteAsync(companyId, lastQuarter, today); return $"صافي {r.NetVatPayable:N0}"; });
        await Measure("إقرار الضريبة — كلّ المدة", async c => { var r = await new VatReturnQuery(c).ExecuteAsync(companyId, fiveYears, today); return $"صافي {r.NetVatPayable:N0}"; });
        await Measure("كشف حساب أثقل عميل", async c => { var l = await new CustomerBalanceQuery(c).StatementAsync(companyId, heavyCustomer); return $"{l.Count:N0} سطراً"; });
        await Measure("أرصدة كل العملاء", async c => { var l = await new CustomerBalanceQuery(c).AllBalancesAsync(companyId); return $"{l.Count} عميلاً"; });
        await Measure("أعمار ديون العملاء", async c => { var l = await new ReceivableAgingQuery(c).ExecuteAsync(companyId, today); return $"{l.Count} صفاً"; });
        await Measure("أعمار ديون الموردين", async c => { var l = await new PayableAgingQuery(c).ExecuteAsync(companyId, today); return $"{l.Count} صفاً"; });
        await Measure("قائمة القيود — الصفحة الأولى + العدّ", async c =>
        {
            var total = await c.JournalEntries.Where(e => e.CompanyId == companyId).CountAsync();
            var page = await c.JournalEntries.Where(e => e.CompanyId == companyId)
                .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id).Skip(0).Take(100).ToListAsync();
            return $"{total:N0} قيداً / {page.Count}";
        });
        await Measure("قائمة فواتير المبيعات — الصفحة الأولى + العدّ", async c =>
        {
            var total = await c.SalesInvoices.Where(e => e.CompanyId == companyId).CountAsync();
            var page = await c.SalesInvoices.Where(e => e.CompanyId == companyId)
                .OrderByDescending(e => e.InvoiceDate).ThenByDescending(e => e.Id).Skip(0).Take(100).ToListAsync();
            return $"{total:N0} فاتورة / {page.Count}";
        });

        var table = new StringBuilder();
        table.AppendLine($"| الشاشة / الاستعلام | الوسيط (ms) | الأقصى (ms) | ما عاد |");
        table.AppendLine("|---|---:|---:|---|");
        foreach (var s in samples)
            table.AppendLine($"| {s.Name} | {s.MedianMs.ToString("N0", CultureInfo.InvariantCulture)} | {s.MaxMs.ToString("N0", CultureInfo.InvariantCulture)} | {s.Note} |");
        output.WriteLine($"[قراءة · {provider} · {months} شهراً · {counts}]");
        output.WriteLine(table.ToString());

        if (Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } reportPath)
            File.WriteAllText(reportPath, $"المزوّد: {provider} · المدة: {months} شهراً · {counts} · تهيئة {seedWatch.Elapsed.TotalSeconds:N0}s\n\n{table}");

        var over = samples.Where(s => s.MaxMs > BudgetSeconds * 1000).ToList();
        Assert.True(over.Count == 0,
            $"استعلاماتٌ تجاوزت {BudgetSeconds}s فوق {months} شهراً على {provider}:\n"
            + string.Join("\n", over.Select(s => $"  {s.Name}: وسيط {s.MedianMs:N0}ms · أقصى {s.MaxMs:N0}ms")));
    }

    /// <summary>
    /// يُرحِّل شهراً بشهرٍ بخدمات البرنامج نفسها، <b>سياقاً جديداً لكلّ مستند</b> كما يفعل التطبيق. المزيجُ لمنشأةٍ صغيرةٍ-متوسّطة:
    /// ٦٠ فاتورةَ بيع و٤٠ سندَ قبض و١٠ فواتيرَ شراء و٨ سندات صرف و٤٠ قيداً يدويّاً في الشهر.
    /// </summary>
    private static async Task<string> SeedAsync(BrqoshDbContext db, Func<BrqoshDbContext> fresh, long companyId, DateOnly first, int months)
    {
        for (var i = 0; i < 40; i++)
            db.Customers.Add(Customer.Create(companyId, $"عميل {i:D2}", "3111222333" + i.ToString("D4", CultureInfo.InvariantCulture) + "3"));

        var suppliers = new List<Supplier>();
        for (var i = 0; i < 15; i++)
        {
            var supplier = Supplier.Create(companyId, $"مورد {i:D2}", "3122223333" + i.ToString("D4", CultureInfo.InvariantCulture) + "3");
            db.Suppliers.Add(supplier);
            suppliers.Add(supplier);
        }

        var products = new List<Product>();
        for (var i = 0; i < 30; i++)
        {
            var product = Product.Create(companyId, $"P{i:D3}", $"صنف {i:D3}", "قطعة", ProductType.Stockable, 100m + i);
            db.Products.Add(product);
            products.Add(product);
        }

        await db.SaveChangesAsync();
        var customerIds = await db.Customers.Where(c => c.CompanyId == companyId).OrderBy(c => c.Id).Select(c => c.Id).ToListAsync();
        var supplierIds = suppliers.Select(s => s.Id).ToList();
        var productIds = products.Select(p => p.Id).ToList();

        var random = new Random(20261024);
        long sales = 0, receipts = 0, purchases = 0, payments = 0, manual = 0;

        // ‏مخزونٌ افتتاحيّ لكلّ صنف — فلا يُرفض بيعٌ في الشهر الأوّل لأنّ الصنف لم يُشتَر بعد.
        await using (var opening = fresh())
        {
            var order = PurchaseOrder.CreateDraft(companyId, supplierIds[0], first);
            foreach (var id in productIds)
                order.AddLine("مخزون افتتاحيّ", 3_000m, 60m, id);
            var service = new PurchaseOrderService(opening);
            await service.ApproveAsync(order);
            await service.CreateInvoiceFromOrderAsync(companyId, order.Id, "OPENING", first, vatDeductible: true);
            purchases++;
        }

        for (var m = 0; m < months; m++)
        {
            var month = first.AddMonths(m);
            DateOnly Day() => new(month.Year, month.Month, random.Next(1, 28));

            // ‏الشراءُ أوّلاً ليتوفّر المخزون.
            for (var i = 0; i < 10; i++)
            {
                await using var c = fresh();
                var order = PurchaseOrder.CreateDraft(companyId, supplierIds[random.Next(supplierIds.Count)], Day());
                for (var l = 0; l < 4; l++)
                    order.AddLine("بضاعة", 60m, 60m, productIds[random.Next(productIds.Count)]);
                var service = new PurchaseOrderService(c);
                await service.ApproveAsync(order);
                var invoice = await service.CreateInvoiceFromOrderAsync(companyId, order.Id, $"SUP-{m}-{i}", order.OrderDate, vatDeductible: true);
                purchases++;

                if (i < 8)
                {
                    await using var p = fresh();
                    await new PaymentVoucherPostingService(p).PostAsync(PaymentVoucher.CreateDraft(
                        companyId, invoice.SupplierId, order.OrderDate, Math.Round(invoice.GrandTotal() / 2m, 2),
                        ReceiptMethod.Bank, "سداد", $"PAY-{m}-{i}"));
                    payments++;
                }
            }

            var invoiceTotals = new List<(long Customer, DateOnly Date, decimal Amount)>();
            for (var i = 0; i < 60; i++)
            {
                await using var c = fresh();
                // ‏ثلاثون في المئة من المبيعات لعميلٍ واحد — ليكون كشفُه الأثقل.
                var customer = random.NextDouble() < 0.30 ? customerIds[0] : customerIds[random.Next(customerIds.Count)];
                var date = Day();
                var sale = SalesInvoice.CreateDraft(companyId, customer, date);
                for (var l = 0; l < 3; l++)
                    sale.AddLine("بيع", random.Next(1, 4), 120m + random.Next(0, 60), productIds[random.Next(productIds.Count)]);
                await new SalesInvoicePostingService(c).PostAsync(sale);
                invoiceTotals.Add((customer, date, sale.GrandTotal()));
                sales++;
            }

            foreach (var (customer, date, amount) in invoiceTotals.Take(40))
            {
                await using var c = fresh();
                await new ReceiptVoucherPostingService(c).PostAsync(ReceiptVoucher.CreateDraft(
                    companyId, customer, date.AddDays(3), Math.Round(amount * 0.6m, 2), ReceiptMethod.Bank, "تحصيل", $"RC-{m}-{receipts}"));
                receipts++;
            }

            await using (var c = fresh())
            {
                var cash = await c.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "1101");
                var capital = await c.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == "3101");
                for (var i = 0; i < 40; i++)
                {
                    await using var j = fresh();
                    var draft = JournalEntry.CreateDraft(companyId, Day(), $"قيد يدوي {m}-{i}", sourceDocument: null);
                    var amount = 100m + random.Next(0, 900);
                    draft.AddLine(await j.Accounts.SingleAsync(a => a.Id == cash.Id), amount, 0m);
                    draft.AddLine(await j.Accounts.SingleAsync(a => a.Id == capital.Id), 0m, amount);
                    await new JournalPostingService(j).PostAsync(draft);
                    manual++;
                }
            }
        }

        return $"بيع {sales:N0} · قبض {receipts:N0} · شراء {purchases:N0} · صرف {payments:N0} · يدوي {manual:N0}";
    }
}
