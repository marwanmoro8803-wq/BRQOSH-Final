using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-229) <b>الإقلاعُ يُصلح المستندَ ولا ينظر إلى قيده.</b>
///
/// <para>أُغلق في <c>م2-١١</c> أنّ الإصلاحَ صار <b>داخل معاملةٍ وبأثرِ تدقيق</b>، فلم يعد
/// صامتاً. <b>وبقي أنّه لا يقارن نتيجتَه بالقيد المرحَّل</b> — وسُجّل بنداً مفتوحاً.</para>
///
/// <para><b>والسؤال المقيس هنا:</b> هل يستطيع الإصلاحُ أن يُخرج مستنداً <b>يخالف قيدَه</b>؟
/// فإن استطاع، فالأثرُ يقول «غيّرتُ الوعاءَ من صفرٍ إلى ألف» ولا يقول <b>إنّ الأستاذَ يقول
/// تسعمئة</b> — وهو الخبرُ الذي يعني شيئاً للمحاسب.</para>
///
/// <para><b>ولا تكشفه هويّةٌ قائمة:</b> هـ-١ تقيس مجموعَ أرصدة العملاء بحساب 1104 — وكلاهما
/// من الأستاذ ومن الاستعلام، <b>ولا يمرّان بإجماليات المستند المخزَّنة</b>. فالمستندُ قد
/// يقول غيرَ ما يقوله قيدُه والهويّاتُ الثلاث صادقة.</para>
/// </summary>
public class BootRepairMeetsTheLedgerTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public BootRepairMeetsTheLedgerTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> مستندٌ مرحَّلٌ فقدت إجمالياتُه، <b>وسطرٌ من سطوره تغيّر سعرُه</b>
    /// بعد الترحيل (بترقيعٍ أو هجرةٍ أو يدٍ في القاعدة). فإعادةُ الحساب عند الإقلاع تُنتج
    /// رقماً <b>يخالف القيد</b>.
    ///
    /// <para>والمطلوب: أن يقول الأثرُ ذلك. <b>فإصلاحٌ يُنتج خلافاً ولا يُعلنه أسوأُ من ترك
    /// الصفر</b> — الصفرُ ظاهرٌ للعين، والخلافُ لا.</para>
    /// </summary>
    [Fact]
    public async Task WhenTheRepairDisagreesWithTheEntry_TheTrailSaysSo()
    {
        var (companyId, invoiceId) = await SeedPostedInvoiceAsync();

        // ‏سعرُ سطرٍ تغيّر بعد الترحيل: القيدُ رُحّل على 1,000 والسطورُ تقول 900.
        //
        // ‏**والتعديلُ عبر EF لا بـSQL خام**: أوّلُ صياغةٍ استعملت SQL فلم تأخذ — EF يخزّن
        // ‏`decimal` في SQLite نصّاً، فتسعون عدديّةٌ لا يقرؤها. وهو الدرسُ نفسُه الذي
        // ‏سقطتُ فيه في م2-١١، وسقطتُ فيه هنا ثانيةً.
        await RepriceLinesAsync(invoiceId, 90m);
        await ZeroTotalsAsync(invoiceId);

        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var invoice = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(900m, invoice.Subtotal);

        var trail = await _db.AuditLogEntries.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.EntityKey == invoiceId.ToString())
            .Select(a => a.Details + " | " + (a.Reason ?? ""))
            .ToListAsync();

        Assert.True(trail.Count > 0, "لم يُكتب أثرٌ أصلاً — فالقياسُ أجوف.");

        Assert.True(trail.Any(t => t.Contains("يخالف", StringComparison.Ordinal)
                                   || t.Contains("الأستاذ", StringComparison.Ordinal)),
            "أُعيدت كتابةُ إجماليات مستندٍ مرحَّل فخالفت قيدَه، والأثرُ لا يذكر ذلك — "
            + $"فالمالكُ يقرأ «صُحّح» وهو خلاف. الأثر: {string.Join(" ¶ ", trail)}");
    }

    /// <summary>
    /// <b>والاتّجاه الآخر — وهو ما يمنع الإنذارَ الكاذب.</b> مستندٌ فقدت إجمالياتُه وسطورُه
    /// سليمة ⟵ الإصلاحُ يُطابق القيد، <b>فلا يُذكر خلافٌ لا وجودَ له</b>.
    ///
    /// <para>وحارسٌ يُعلن الخلافَ في كلّ مرّة يُعلّم المالكَ تجاهلَ سجلّ التدقيق.</para>
    /// </summary>
    [Fact]
    public async Task WhenTheRepairAgreesWithTheEntry_NoDisagreementIsClaimed()
    {
        var (companyId, invoiceId) = await SeedPostedInvoiceAsync();
        await ZeroTotalsAsync(invoiceId);

        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var invoice = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(1_000m, invoice.Subtotal);

        var trail = await _db.AuditLogEntries.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.EntityKey == invoiceId.ToString())
            .Select(a => a.Details + " | " + (a.Reason ?? ""))
            .ToListAsync();

        Assert.Single(trail);
        Assert.DoesNotContain(trail, t => t.Contains("يخالف", StringComparison.Ordinal));
    }

    private async Task RepriceLinesAsync(long invoiceId, decimal unitPrice)
    {
        var invoice = await _db.SalesInvoices.Include(i => i.Lines)
            .SingleAsync(i => i.Id == invoiceId);

        foreach (var line in invoice.Lines)
            _db.Entry(line).Property("UnitPrice").CurrentValue = unitPrice;

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task ZeroTotalsAsync(long invoiceId)
    {
        var invoice = await _db.SalesInvoices.SingleAsync(i => i.Id == invoiceId);
        _db.Entry(invoice).Property(i => i.Subtotal).CurrentValue = 0m;
        _db.Entry(invoice).Property(i => i.VatAmount).CurrentValue = 0m;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task<(long CompanyId, long InvoiceId)> SeedPostedInvoiceAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الإقلاع", "310123456700011", BusinessProfile.Retail);

        var customer = Customer.Create(company.Id, "عميل نقدي");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 1));
        invoice.AddLine("بضاعة", 10m, 100m);
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);
        _db.ChangeTracker.Clear();

        return (company.Id, posted.Id);
    }
}
