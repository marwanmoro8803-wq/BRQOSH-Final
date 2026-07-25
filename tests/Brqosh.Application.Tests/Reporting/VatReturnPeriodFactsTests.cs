using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// O-21 (CEEF-219) — الإقرار الضريبي يعكس **وقائع الفترة** لا حالة المستند اليوم.
///
/// كان يُرشّح على <c>Status == Posted</c> وحده: فاتورة صدرت في الربع الأول وأُلغيت في
/// الثاني تصبح Cancelled فتختفي من إعادة تشغيل إقرار الربع الأول — بأرقام أقلّ مما
/// قُدِّم للهيئة فعلاً، ومخالفة لقيدها الباقي في الأستاذ لتلك الفترة. خطر فروقات
/// مباشر عند الفحص الضريبي.
///
/// الصواب: إقرار فترة الإصدار يبقى كما قُدِّم، وأثر الإلغاء يظهر في فترة الإلغاء
/// كتسوية سالبة — تماماً كما يفعل إشعار الدائن.
/// </summary>
public class VatReturnPeriodFactsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    // ربعان متتاليان من سنة واحدة.
    private static readonly DateOnly Q1From = new(2026, 1, 1);
    private static readonly DateOnly Q1To = new(2026, 3, 31);
    private static readonly DateOnly Q2From = new(2026, 4, 1);
    private static readonly DateOnly Q2To = new(2026, 6, 30);

    public VatReturnPeriodFactsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة الإقرار", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل الإقرار", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }

    private async Task<SalesInvoice> PostSaleAsync(DateOnly date, decimal net)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, date);
        invoice.AddLine("خدمة", 1m, net);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private Task<VatReturnResult> ReturnFor(DateOnly from, DateOnly to) =>
        new VatReturnQuery(_db).ExecuteAsync(_companyId, from, to);

    /// <summary>
    /// جوهر البند: فاتورة الربع الأول تبقى في إقرار الربع الأول بعد إلغائها في الثاني —
    /// إعادة التشغيل تُنتج نفس ما قُدِّم للهيئة.
    /// </summary>
    [Fact]
    public async Task InvoiceCancelledLater_StaysInItsOriginalPeriodReturn()
    {
        await SetupAsync();
        var invoice = await PostSaleAsync(new DateOnly(2026, 2, 10), 10_000m);   // ضريبة 1,500

        var asFiled = await ReturnFor(Q1From, Q1To);
        Assert.Equal(1_500m, asFiled.OutputVat);

        // أُلغيت في الربع الثاني
        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, invoice.Id, new DateOnly(2026, 5, 15), "admin");

        var reRun = await ReturnFor(Q1From, Q1To);

        Assert.Equal(asFiled.OutputVat, reRun.OutputVat);          // لم تختفِ
        Assert.Equal(asFiled.NetVatPayable, reRun.NetVatPayable);
        Assert.Equal(0m, reRun.CancelledOutputVat);                // ولا تسوية في فترة الإصدار
    }

    /// <summary>وأثر الإلغاء يظهر في فترة الإلغاء — لا يضيع بين الفترات.</summary>
    [Fact]
    public async Task CancellationEffect_AppearsInCancellationPeriod()
    {
        await SetupAsync();
        var invoice = await PostSaleAsync(new DateOnly(2026, 2, 10), 10_000m);

        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, invoice.Id, new DateOnly(2026, 5, 15), "admin");

        var q2 = await ReturnFor(Q2From, Q2To);

        Assert.Equal(1_500m, q2.CancelledOutputVat);
        Assert.Equal(-1_500m, q2.NetOutputVat);      // تسوية سالبة كإشعار دائن
    }

    /// <summary>
    /// الحارس الحاسم: مجموع الفترتين يساوي صفراً — الضريبة لا تُحتسب مرتين ولا تضيع.
    /// </summary>
    [Fact]
    public async Task AcrossBothPeriods_CancellationNetsToZero()
    {
        await SetupAsync();
        var invoice = await PostSaleAsync(new DateOnly(2026, 2, 10), 10_000m);

        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, invoice.Id, new DateOnly(2026, 5, 15), "admin");

        var q1 = await ReturnFor(Q1From, Q1To);
        var q2 = await ReturnFor(Q2From, Q2To);

        Assert.Equal(0m, q1.NetOutputVat + q2.NetOutputVat);
    }

    /// <summary>
    /// إلغاء داخل نفس فترة الإصدار: لا تسوية منفصلة — المستند يخرج من الإقرار مباشرة،
    /// وإلا خُصمت الضريبة مرتين.
    /// </summary>
    [Fact]
    public async Task CancelledInSamePeriod_IsSimplyExcluded_NotDoubleCounted()
    {
        await SetupAsync();
        var invoice = await PostSaleAsync(new DateOnly(2026, 2, 10), 10_000m);

        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, invoice.Id, new DateOnly(2026, 3, 20), "admin");

        var q1 = await ReturnFor(Q1From, Q1To);

        Assert.Equal(0m, q1.OutputVat);              // خرجت من الإقرار
        Assert.Equal(0m, q1.CancelledOutputVat);     // ولا تسوية مكرّرة
        Assert.Equal(0m, q1.NetOutputVat);
    }

    /// <summary>فاتورة سارية لا تتأثر بشيء من هذا.</summary>
    [Fact]
    public async Task ActiveInvoice_IsUnaffected()
    {
        await SetupAsync();
        await PostSaleAsync(new DateOnly(2026, 2, 10), 4_000m);   // ضريبة 600

        var q1 = await ReturnFor(Q1From, Q1To);

        Assert.Equal(600m, q1.OutputVat);
        Assert.Equal(600m, q1.NetOutputVat);
        Assert.Equal(0m, q1.CancelledOutputVat);
    }

    /// <summary>تاريخ الإلغاء يُحفظ فعلاً — بدونه يستحيل وضع الأثر في فترته.</summary>
    [Fact]
    public async Task CancellationDate_IsPersisted()
    {
        await SetupAsync();
        var invoice = await PostSaleAsync(new DateOnly(2026, 2, 10), 1_000m);
        var cancelledOn = new DateOnly(2026, 5, 15);

        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, invoice.Id, cancelledOn, "admin");

        var stored = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(SalesInvoiceStatus.Cancelled, stored.Status);
        Assert.Equal(cancelledOn, stored.CancelledAt);
    }
}
