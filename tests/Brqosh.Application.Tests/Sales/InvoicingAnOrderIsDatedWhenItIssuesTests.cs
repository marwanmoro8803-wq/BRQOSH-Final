using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٣) <b>فاتورةُ أمرِ البيع كانت تُؤرَّخ بيوم الأمر لا بيوم صدورها.</b>
///
/// <para><b>وتاريخُ الفاتورة الضريبيّة حقلٌ ضريبيٌّ لا وصف</b>: تقع به في إقرارٍ دون إقرار،
/// <b>وتأريخٌ إلى الوراء يُدخل توريداً في فترةٍ قد تكون قُدِّمت إقرارُها</b>. وأمرُ البيع
/// <b>ليس توريداً</b> — قد يسبق الفاتورةَ بشهور.</para>
///
/// <para><b>وأسوأُ حالاته بعد ح-١</b>: أمرٌ من شهرٍ أُقفل <b>يصير غيرَ قابلٍ للفوترة
/// أبداً</b> — إذ تُبنى فاتورتُه في الشهر المقفل فيرفضها حارسُ الفترة. <b>فإغلاقُ عطلٍ
/// كشف عطلاً آخر كان مستوراً خلفه.</b></para>
///
/// <para><b>والتوأمُ يفعلها صواباً منذ نشأته</b>: <c>PurchaseOrderService.CreateInvoiceFromOrderAsync</c>
/// يأخذ <c>invoiceDate</c> وسيطاً <b>ويُلفّ في <c>UnitOfWork.ExecuteInTransactionAsync</c></b>.
/// <b>فهذا نقلُ نمطٍ قائمٍ لا اختراع</b> — وهو الصنفُ نفسُه المسجَّل في ح-٢٣…ح-٣٠.</para>
/// </summary>
public class InvoicingAnOrderIsDatedWhenItIssuesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public InvoicingAnOrderIsDatedWhenItIssuesTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── القياسُ الحاكم ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>الفاتورةُ تحمل يومَ صدورها</b> — والأمرُ في مارس، والفاتورةُ في يونيو.
    /// </summary>
    [Fact]
    public async Task TheInvoice_CarriesTheDayItWasIssued()
    {
        await SetupAsync();
        var order = await ConfirmedOrderAsync(new DateOnly(2026, 3, 1), 1_000m);

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, new DateOnly(2026, 6, 15));

        Assert.Equal(new DateOnly(2026, 6, 15), invoice.InvoiceDate);
    }

    /// <summary>
    /// <b>والأثرُ الذي يهمّ الهيئة: التوريدُ يقع في إقرار الشهر الذي فُوتر فيه.</b>
    ///
    /// <para><b>ويُقاس على تقرير الإقرار نفسِه لا على حقلٍ في صفّ</b> — فهو ما يُقدَّم.
    /// <b>وقبلَه كان يقع في مارس</b>: إقرارٌ قد يكون قُدِّم.</para>
    /// </summary>
    [Fact]
    public async Task TheSupply_FallsInTheReturnOfTheMonthItWasInvoiced()
    {
        await SetupAsync();
        var order = await ConfirmedOrderAsync(new DateOnly(2026, 3, 1), 1_000m);

        await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, new DateOnly(2026, 6, 15));

        Assert.Equal(0m, (await VatAsync(2026, 3)).SalesSubtotal);
        Assert.Equal(1_000m, (await VatAsync(2026, 6)).SalesSubtotal);
    }

    /// <summary>
    /// <b>وأمرٌ من شهرٍ مقفلٍ يبقى قابلاً للفوترة</b> — <b>وكان يصير حبيسَ الإقفال</b>:
    /// فاتورتُه تُبنى في الشهر المقفل فيرفضها الحارس، <b>فلا سبيلَ إلى تحصيل بيعةٍ وقعت</b>.
    /// </summary>
    [Fact]
    public async Task AnOrderFromAClosedMonth_CanStillBeInvoiced()
    {
        await SetupAsync();
        var order = await ConfirmedOrderAsync(new DateOnly(2026, 3, 1), 1_000m);
        await ClosePeriodAsync(new DateOnly(2026, 3, 1));

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, new DateOnly(2026, 6, 15));

        Assert.NotNull(invoice.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 6, 15), invoice.InvoiceDate);
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — الأمرُ يُوسَم مُفوتَراً ويُشير إلى فاتورته.</b>
    /// <b>وهو الطرفُ الثاني الذي كان يقع خارج معاملة الترحيل</b>، فإن سقط بقيت فاتورةٌ
    /// ضريبيّةٌ مُرحَّلةٌ وأمرٌ يقرأ «غيرَ مُفوتر» — <b>فتصدر فاتورتان عن توريدٍ واحد</b>.
    /// </summary>
    [Fact]
    public async Task TheOrder_IsMarkedInvoicedAndPointsAtIt()
    {
        await SetupAsync();
        var order = await ConfirmedOrderAsync(new DateOnly(2026, 3, 1), 1_000m);

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, new DateOnly(2026, 6, 15));

        _db.ChangeTracker.Clear();
        var stored = await _db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Invoiced, stored.Status);
        Assert.Equal(invoice.Id, stored.SalesInvoiceId);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — الفوترةُ مرّةً واحدة:</b> النداءُ الثاني يُرفض.
    /// <b>ولولاه لجاز أن يكون «الإصلاح» قد فتح باب التكرار.</b>
    /// </summary>
    [Fact]
    public async Task AnAlreadyInvoicedOrder_IsRefused()
    {
        await SetupAsync();
        var order = await ConfirmedOrderAsync(new DateOnly(2026, 3, 1), 1_000m);
        var service = new SalesOrderService(_db);

        await service.CreateInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 6, 15));

        await Assert.ThrowsAsync<AccountingException>(() => service
            .CreateInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 6, 16)));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثالث — المبالغُ تعبر كما كانت:</b> الإصلاحُ مسّ التاريخَ والمعاملة
    /// <b>لا الأرقام</b>.
    /// </summary>
    [Fact]
    public async Task TheAmounts_CrossUnchanged()
    {
        await SetupAsync();
        var order = await ConfirmedOrderAsync(new DateOnly(2026, 3, 1), 1_000m);

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, new DateOnly(2026, 6, 15));

        _db.ChangeTracker.Clear();
        var storedOrder = await _db.SalesOrders.Include(o => o.Lines).SingleAsync(o => o.Id == order.Id);
        var stored = await _db.SalesInvoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoice.Id);

        Assert.Equal(storedOrder.Lines.Count, stored.Lines.Count);
        Assert.Equal(storedOrder.VatAmount, stored.VatAmount);
        Assert.Equal(storedOrder.GrandTotal(), stored.GrandTotal());
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task<VatReturnResult> VatAsync(int year, int month) =>
        new VatReturnQuery(_db).ExecuteAsync(_companyId,
            new DateOnly(year, month, 1),
            new DateOnly(year, month, DateTime.DaysInMonth(year, month)));

    private async Task ClosePeriodAsync(DateOnly inside)
    {
        // ‏الفترةُ تُنشأ عند أوّل ترحيلٍ فيها — فتُستحضَر قبل إقفالها (درسُ ح-٣٢).
        await new JournalPostingService(_db).EnsurePeriodOpenAsync(_companyId, inside);

        var period = await _db.FiscalPeriods.SingleAsync(
            p => p.CompanyId == _companyId && p.Code == FiscalPeriod.CodeFor(inside));
        await new FiscalPeriodService(_db).CloseAsync(_companyId, period.Id, "admin");
    }

    private async Task<SalesOrder> ConfirmedOrderAsync(DateOnly orderDate, decimal amount)
    {
        var order = SalesOrder.CreateDraft(_companyId, _customerId, orderDate);
        order.AddLine("توريد", 1m, amount);
        return await new SalesOrderService(_db).ConfirmAsync(order);
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المبيعات", "310123456700135", BusinessProfile.General);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }
}
