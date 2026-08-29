using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-229 · م2-٢) <b>سعرُ وحدةٍ بثلاث خانات — والفاتورةُ المطبوعة كانت لا تجمع نفسها.</b>
///
/// <para><b>الحالة واقعية لا مفتعلة:</b> كرتونٌ فيه ٢٤ حبّة بـ ٤٥.٠٠ ريال ⟵ سعرُ الحبّة
/// <c>1.875</c>. ولم يكن على خاناته حارسٌ إطلاقاً.</para>
///
/// <para><b>وهذا ما قِيس فعلاً قبل الإصلاح</b>، بتشغيل الدورة نفسها على المزوّدَين:</para>
/// <code>
/// SQLite     : السعر 1.875 · الإجماليّ 45.00 · مجموع السطور 45.0000   ← متّسق
/// PostgreSQL : السعر 1.88  · الإجماليّ 45.00 · مجموع السطور 45.1200   ← يناقض نفسه
/// </code>
///
/// <para>أي أنّ الفاتورة الضريبية المطبوعة — التي يُبنى عمودُ وعائها من السطور ويُطبع
/// إجماليُّها من الحقل المخزَّن — <b>لا يساوي عمودُها إجماليَّها</b>. وعلى الإنتاج وحده:
/// على SQLite حيث تعمل الاختبارات كلُّها كان الرقمان متطابقَين، فلم يكن للعطل أثرٌ يُرى.</para>
///
/// <para><b>والأخطر أنّ الملفّ الموقَّع لهيئة الزكاة يُبنى وقت الترحيل من الكائن المتعقَّب في
/// الذاكرة</b> — أي بالسعر غير المقرَّب — فيكون متّسقاً في نفسه، بينما الصفُّ المخزَّن الذي
/// يُعاد طبعُه لاحقاً <b>يخالفه</b>. فالورقةُ التي بيد العميل تخالف ما عند الهيئة.</para>
///
/// <para><b>والإصلاح رفضٌ لا تقريبٌ صامت</b> — انظر <see cref="DocumentLineScale"/>.</para>
/// </summary>
public class UnitPriceScaleParityTests
{
    private const decimal CartonUnits = 24m;
    private const decimal SubHalalaPrice = 1.875m;   // ٤٥.٠٠ ÷ ٢٤
    private const decimal RoundedPrice = 1.88m;

    /// <summary>سعرُ الوحدة كما استقرّ في القاعدة، ومجموعُ السطور المُعاد قراءتُه.</summary>
    private sealed record LineFacts(decimal StoredUnitPrice, decimal StoredSubtotal, decimal RecomputedFromLines);

    private static async Task<LineFacts> RunAsync(BrqoshDbContext db, decimal unitPrice)
    {
        TestActor.Administrator();

        var company = await new DatabaseInitializer(db)
            .InitializeAsync("منشأة الكرتون", "310123456700099", BusinessProfile.Retail);

        var customer = Customer.Create(company.Id, "عميل", "311122233300003");
        db.Customers.Add(customer);
        var product = Product.Create(company.Id, "P-1", "حبّة", "حبة", ProductType.Service);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 4, 1));
        invoice.AddLine("حبّة من كرتون", CartonUnits, unitPrice, product.Id);
        await new SalesInvoicePostingService(db).PostAsync(invoice);

        // ‏القراءةُ الباردة هي جوهرُ القياس: داخل السياق نفسه يُعيد EF الكائنَ المتعقَّب
        // ‏بسعره غير المقرَّب، فيمرّ التأكيد على المزوّدَين — وهذا بالضبط سببُ عدم كشفه.
        db.ChangeTracker.Clear();
        var reloaded = await db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id);

        return new LineFacts(
            reloaded.Lines.Single().UnitPrice,
            reloaded.Subtotal,
            reloaded.Lines.Sum(l => l.LineTotal));
    }

    private static async Task<LineFacts> OnSqliteAsync(decimal unitPrice)
    {
        var connection = new SqliteConnection("Data Source=pricescale;Mode=Memory;Cache=Shared");
        await connection.OpenAsync();
        try
        {
            await using var db = BrqoshDbContextFactory.Create(
                DatabaseProvider.Sqlite, "Data Source=pricescale;Mode=Memory;Cache=Shared");
            return await RunAsync(db, unitPrice);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> سعرٌ صالحٌ بخانتَين يستقرّ متطابقاً على المزوّدَين، والفاتورةُ
    /// تجمع نفسها على كليهما: مجموعُ السطور == الإجماليّ المخزَّن.
    /// </summary>
    [PostgresFact]
    public async Task AValidPrice_StoresIdentically_AndTheInvoiceAddsUpOnBothProviders()
    {
        var sqlite = await OnSqliteAsync(RoundedPrice);

        await using var scope = await PostgresTestDatabase.CreateAsync("price_scale");
        var postgres = await RunAsync(scope.Db, RoundedPrice);

        Assert.True(sqlite.StoredUnitPrice == postgres.StoredUnitPrice,
            $"سعرُ الوحدة استقرّ مختلفاً: SQLite {sqlite.StoredUnitPrice} — PostgreSQL {postgres.StoredUnitPrice}");

        Assert.True(postgres.RecomputedFromLines == postgres.StoredSubtotal,
            $"على PostgreSQL: مجموعُ السطور {postgres.RecomputedFromLines} لا يساوي "
            + $"الإجماليَّ المخزَّن {postgres.StoredSubtotal} — الفاتورة المطبوعة لا تجمع نفسها.");

        Assert.True(sqlite.RecomputedFromLines == sqlite.StoredSubtotal,
            $"على SQLite: مجموعُ السطور {sqlite.RecomputedFromLines} لا يساوي "
            + $"الإجماليَّ المخزَّن {sqlite.StoredSubtotal}.");
    }

    /// <summary>
    /// <b>الحارس نفسه.</b> سعرٌ بثلاث خانات يُرفض عند الإدخال — قبل أن يصل عموداً بخانتين
    /// فيفترق المزوّدان. والرسالةُ تدلّ على المخرج الصحيح: العبوة سطرٌ واحد بسعرها الكامل.
    /// </summary>
    [Fact]
    public void ASubHalalaUnitPrice_IsRefusedWithAWayOut()
    {
        var invoice = SalesInvoice.CreateDraft(1, 1, new DateOnly(2026, 4, 1));

        var ex = Assert.Throws<AccountingException>(
            () => invoice.AddLine("حبّة من كرتون", CartonUnits, SubHalalaPrice));

        Assert.Contains("خانتَي الهللة", ex.Message);
        Assert.Contains("سطراً واحداً", ex.Message);
    }

    /// <summary>
    /// <b>والحارس معمَّم لا موضعيّ.</b> ثمانيةُ مستنداتٍ تتقاسم سطرَ الوثيقة نفسه، وإصلاحُ
    /// المبيعات وحدها كان سيترك الشراءَ — وهو الطرف الذي جاءت منه الحالة أصلاً (كرتونُ مورّد).
    /// </summary>
    [Fact]
    public void TheGuardCoversPurchaseAndQuotationToo_NotJustSales()
    {
        var purchase = PurchaseInvoice.CreateDraft(1, 1, new DateOnly(2026, 4, 1), "SUP-1", vatDeductible: true);
        Assert.Throws<AccountingException>(
            () => purchase.AddLine("حبّة من كرتون", CartonUnits, SubHalalaPrice));

        var order = PurchaseOrder.CreateDraft(1, 1, new DateOnly(2026, 4, 1));
        Assert.Throws<AccountingException>(
            () => order.AddLine("حبّة من كرتون", CartonUnits, SubHalalaPrice));

        var quotation = SalesQuotation.CreateDraft(1, 1, new DateOnly(2026, 4, 1));
        Assert.Throws<AccountingException>(
            () => quotation.AddLine("حبّة من كرتون", CartonUnits, SubHalalaPrice));
    }

    /// <summary>وحارسٌ للحارس: السعرُ الصالح يمرّ — وإلا لكان الحارس يمنع كلّ بيع.</summary>
    [Fact]
    public void AValidPrice_PassesTheGuard()
    {
        var invoice = SalesInvoice.CreateDraft(1, 1, new DateOnly(2026, 4, 1));
        invoice.AddLine("حبّة", CartonUnits, RoundedPrice);

        Assert.Equal(RoundedPrice, invoice.Lines.Single().UnitPrice);
    }
}
