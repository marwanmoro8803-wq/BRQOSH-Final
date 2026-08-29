using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-229 · م2-٢) <b>هل تُنتج الدورةُ نفسها الدفاترَ نفسها على المزوّدَين؟</b>
///
/// <para><b>لماذا هذا السؤال بالذات:</b> كلُّ اختبارات هذا المستودع تعمل على SQLite، والإنتاج
/// على PostgreSQL. وقد أنتج هذا الانفصامُ أعطالاً حقيقيةً شُحنت ولم يرها اختبار: قيدٌ متوازنٌ
/// يُخزَّن مختلاً لأنّ <c>numeric(18,2)</c> يقرّب كلّ عمودٍ على حدة، ورمزُ تزامنٍ لا يوجد إلا
/// على Npgsql، ومخططٌ ينقص أحد عشر عموداً لا يراه أحد.</para>
///
/// <para><b>وهذه هوية لا اختبارُ ميزة.</b> لا تُعدِّد الفروق بين المزوّدَين — وهي أكثر من أن
/// تُحصى — بل تسأل سؤالاً واحداً يشملها كلَّها: <b>الدورةُ نفسها بالأرقام نفسها، هل تُعطي
/// ميزانَ مراجعةٍ واحداً؟</b> فأيّ فرقٍ في التقريب أو التخزين أو الترتيب يظهر رقماً مختلفاً،
/// أيّاً كان سببه، ودون أن أعرف سببه مسبقاً.</para>
///
/// <para><b>والمقارنة على الأستاذ لا على الشاشة:</b> ميزانُ المراجعة هو ما يوقّعه المحاسب،
/// وما يُبنى منه الإقرار الضريبيّ. فإن تطابق الأستاذان تطابقت الدفاتر.</para>
/// </summary>
public class ProviderParityTests
{
    /// <summary>ما تُخرجه الدورة — كلُّه أرقامٌ تُقارَن، لا كائناتٌ تُفحص.</summary>
    private sealed record BookSnapshot(
        SortedDictionary<string, (decimal Debit, decimal Credit)> TrialBalance,
        decimal OnHand,
        decimal PurchaseGrandTotal,
        decimal SalesGrandTotal,
        decimal WeightedAverageCost);

    // ‏أرقامٌ مقصودة. الشراءُ الأوّل نظيف، والثاني يُنتج متوسّطاً مرجّحاً **غيرَ منتهٍ**:
    // ‏(100×60 + 30×55) ÷ 130 = 58.846153846… — وهنا بالضبط يفترق المزوّدان إن لم يحرس النطاق.
    // ‏فـ PostgreSQL يخزّن العمود numeric(18,4) ويقرّب عنده، وSQLite يحتفظ بـ decimal كاملاً.
    // ‏والذي يجعلهما يتّفقان هو تقريبُ النطاق في Product.SetWeightedAverageCost — لا المزوّد.
    // ‏فهذه الدورة تحرس ذلك التقريب: نزعُه يُفرّق الدفترَين فوراً.
    private const decimal PurchaseQuantity = 100m;
    private const decimal PurchaseUnitCost = 60m;
    private const decimal SecondPurchaseQuantity = 30m;
    private const decimal SecondPurchaseUnitCost = 55m;
    private const decimal SaleQuantity = 40m;
    private const decimal SaleUnitPrice = 100m;

    private static async Task<BookSnapshot> RunScenarioAsync(BrqoshDbContext db)
    {
        TestActor.Administrator();

        var company = await new DatabaseInitializer(db)
            .InitializeAsync("منشأة المقارنة", "310123456700099", BusinessProfile.Retail);
        var companyId = company.Id;

        var supplier = Supplier.Create(companyId, "مورد", "312233344400003");
        db.Suppliers.Add(supplier);
        var customer = Customer.Create(companyId, "عميل", "311122233300003");
        db.Customers.Add(customer);
        var product = Product.Create(companyId, "P-1", "بضاعة", "قطعة", ProductType.Stockable, SaleUnitPrice);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // ── شراء ──────────────────────────────────────────────────────────
        var order = PurchaseOrder.CreateDraft(companyId, supplier.Id, new DateOnly(2026, 3, 1));
        order.AddLine("بضاعة", PurchaseQuantity, PurchaseUnitCost, product.Id);
        await new PurchaseOrderService(db).ApproveAsync(order);

        var purchase = await new PurchaseOrderService(db).CreateInvoiceFromOrderAsync(
            companyId, order.Id, "SUP-1", new DateOnly(2026, 3, 2), vatDeductible: true);

        // ── شراءٌ ثانٍ بسعرٍ مختلف — ليصير المتوسّط المرجّح كسراً غيرَ منتهٍ ──
        var second = PurchaseOrder.CreateDraft(companyId, supplier.Id, new DateOnly(2026, 3, 2));
        second.AddLine("بضاعة", SecondPurchaseQuantity, SecondPurchaseUnitCost, product.Id);
        await new PurchaseOrderService(db).ApproveAsync(second);
        await new PurchaseOrderService(db).CreateInvoiceFromOrderAsync(
            companyId, second.Id, "SUP-2", new DateOnly(2026, 3, 2), vatDeductible: true);

        // ── بيع ───────────────────────────────────────────────────────────
        var sale = SalesInvoice.CreateDraft(companyId, customer.Id, new DateOnly(2026, 3, 3));
        sale.AddLine("بضاعة", SaleQuantity, SaleUnitPrice, product.Id);
        await new SalesInvoicePostingService(db).PostAsync(sale);

        // ── تحصيلٌ جزئيّ ──────────────────────────────────────────────────
        var voucher = ReceiptVoucher.CreateDraft(
            companyId, customer.Id, new DateOnly(2026, 3, 4),
            1000m, ReceiptMethod.Cash, "تحصيل جزئي");
        await new ReceiptVoucherPostingService(db).PostAsync(voucher);

        // ── والهويات الجبرية تصمد على المزوّدَين، وإلا فالمقارنة على دفاترَ مكسورة ──
        await LedgerIdentities.AssertAllAsync(db, companyId);

        var reloaded = await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);

        return new BookSnapshot(
            await TrialBalanceAsync(db, companyId),
            await new InventoryService(db).GetCompanyOnHandAsync(companyId, product.Id),
            purchase.GrandTotal(),
            sale.GrandTotal(),
            reloaded.WeightedAverageCost);
    }

    /// <summary>ميزانُ المراجعة من الأستاذ العام — مدينٌ ودائنٌ لكلّ حساب.</summary>
    private static async Task<SortedDictionary<string, (decimal, decimal)>> TrialBalanceAsync(
        BrqoshDbContext db, long companyId)
    {
        var lines = await db.JournalEntries
            .Where(e => e.CompanyId == companyId)
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.AccountCode)
            .Select(g => new
            {
                Code = g.Key,
                Debit = g.Sum(l => l.Debit),
                Credit = g.Sum(l => l.Credit),
            })
            .ToListAsync();

        var result = new SortedDictionary<string, (decimal, decimal)>(StringComparer.Ordinal);
        foreach (var line in lines) result[line.Code] = (line.Debit, line.Credit);
        return result;
    }

    private static async Task<BookSnapshot> RunOnSqliteAsync()
    {
        var connection = new SqliteConnection("Data Source=parity;Mode=Memory;Cache=Shared");
        await connection.OpenAsync();
        try
        {
            await using var db = BrqoshDbContextFactory.Create(
                DatabaseProvider.Sqlite, "Data Source=parity;Mode=Memory;Cache=Shared");
            return await RunScenarioAsync(db);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> دورةٌ كاملة — شراء ثمّ بيع ثمّ تحصيل — تُنفَّذ مرّتين بالخدمات
    /// نفسها والأرقام نفسها، مرّةً على SQLite ومرّةً على PostgreSQL. والميزانان يجب أن يتطابقا
    /// حساباً بحساب وهللةً بهللة.
    /// </summary>
    [PostgresFact]
    public async Task TheSameCycle_ProducesTheSameBooks_OnBothProviders()
    {
        var sqlite = await RunOnSqliteAsync();

        await using var scope = await PostgresTestDatabase.CreateAsync(nameof(ProviderParityTests));
        var postgres = await RunScenarioAsync(scope.Db);

        // ‏الحسابات نفسها أوّلاً: حسابٌ يظهر على مزوّدٍ دون الآخر انحرافٌ أخطرُ من فرق مبلغ.
        Assert.Equal(sqlite.TrialBalance.Keys, postgres.TrialBalance.Keys);

        foreach (var (code, (debit, credit)) in sqlite.TrialBalance)
        {
            var (pgDebit, pgCredit) = postgres.TrialBalance[code];
            Assert.True(debit == pgDebit && credit == pgCredit,
                $"الحساب {code}: SQLite مدين {debit} دائن {credit} — "
                + $"PostgreSQL مدين {pgDebit} دائن {pgCredit}");
        }

        Assert.Equal(sqlite.OnHand, postgres.OnHand);
        Assert.Equal(sqlite.PurchaseGrandTotal, postgres.PurchaseGrandTotal);
        Assert.Equal(sqlite.SalesGrandTotal, postgres.SalesGrandTotal);
        Assert.True(sqlite.WeightedAverageCost == postgres.WeightedAverageCost,
            $"متوسّط التكلفة المرجّح افترق: SQLite {sqlite.WeightedAverageCost} — "
            + $"PostgreSQL {postgres.WeightedAverageCost}. وهو ما تُبنى عليه تكلفةُ المبيعات.");
    }

    /// <summary>
    /// <b>حارسٌ للحارس.</b> لو كانت المقارنة أعلاه تُنفَّذ على SQLite مرّتين لمرّت دائماً وهي
    /// لا تقيس شيئاً. فهذا يُثبت أنّ السياق الثاني <b>Npgsql فعلاً</b> وأنّ الدورة نُفِّذت عليه
    /// (شركةٌ ودفترٌ موجودان)، لا أنّ الاتصال نجح فحسب.
    /// </summary>
    [PostgresFact]
    public async Task TheSecondProvider_IsReallyPostgreSql_AndReallyRanTheCycle()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("parity_provider_guard");
        await RunScenarioAsync(scope.Db);

        Assert.True(scope.Db.Database.IsNpgsql(), "السياق الثاني ليس Npgsql — المقارنة بلا معنى.");

        var version = await scope.Db.Database
            .SqlQuery<string>($"SELECT version() AS \"Value\"").SingleAsync();
        Assert.Contains("PostgreSQL", version, StringComparison.OrdinalIgnoreCase);

        Assert.True(await scope.Db.JournalEntries.AnyAsync(), "لا قيودَ على PostgreSQL — الدورة لم تُنفَّذ.");
    }
}
