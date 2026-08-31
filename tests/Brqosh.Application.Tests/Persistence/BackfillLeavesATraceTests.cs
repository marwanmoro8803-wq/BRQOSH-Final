using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-229 · م2-١١ / D-4) <b>الإقلاعُ لا يُعدّل مستنداً مرحَّلاً بلا أثر.</b>
///
/// <para><b>ما بقي من البند:</b> الشقُّ الأدائيّ أُغلق — مرشّحُ <c>Subtotal == 0</c> يمنع
/// إعادةَ لمس المستندات السليمة، فبطل «كلُّ إقلاعٍ يُعيد حسابَ كلّ شيء». <b>والشقُّ المحاسبيّ
/// لم يُمَسّ:</b> لا حارسَ حالةٍ ولا معاملةٍ ولا أثرَ تدقيقٍ ولا نظرةَ إلى قيد المستند.</para>
///
/// <para><b>والأثر:</b> مستندٌ <b>مرحَّل</b> إجمالياتُه صفرٌ في القاعدة تُعاد كتابتُها عند
/// الإقلاع — <b>وقيدُه يبقى كما هو</b>. فإن كان القيدُ قد رُحّل من الأصفار، صار المستندُ
/// يقول شيئاً والأستاذُ يقول غيرَه، <b>بلا سطرٍ واحدٍ يدلّ على أنّ الإقلاع غيّره</b>.</para>
///
/// <para>والسؤالُ ليس «أيُصلح أم يُفسد» — فذلك يتوقّف على بياناتٍ لا تُعرَف. <b>السؤالُ أنّه
/// يفعل ذلك صامتاً.</b> وتعديلٌ صامتٌ على مستندٍ مرحَّل لا يُقبل في دفترٍ محاسبيّ.</para>
///
/// <para><b>ولم يكن اختبارٌ واحد يمرّ بهذا المسار:</b> كلُّ ما يستدعي <c>ApplyAsync</c>
/// يفعله على قاعدةٍ نظيفةٍ لا مستنداتٍ فيها بأصفار.</para>
/// </summary>
public class BackfillLeavesATraceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public BackfillLeavesATraceTests()
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

    /// <summary>
    /// <b>القياس المركزيّ.</b> مستندٌ مرحَّلٌ أصفارُه تُعاد كتابتُها ⟵ <b>سطرُ تدقيقٍ يُسمّيه
    /// ويُسمّي ما تغيّر</b>. فالمالكُ يرى ما فعله الإقلاعُ بدفاتره.
    /// </summary>
    [Fact]
    public async Task RewritingAPostedDocument_LeavesAnAuditTrail()
    {
        var (companyId, invoiceId, number) = await SeedPostedInvoiceAsync();

        await ZeroTotalsInDatabaseAsync(invoiceId);

        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var restored = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(1_000m, restored.Subtotal);

        var trail = await _db.AuditLogEntries
            .Where(a => a.CompanyId == companyId && a.EntityKey == invoiceId.ToString())
            .ToListAsync();

        Assert.True(trail.Count > 0,
            $"أُعيدت كتابةُ إجماليات المستند المرحَّل «{number}» عند الإقلاع بلا أثرٍ واحد — "
            + "فلا يعلم المالكُ أنّ دفترَه تغيّر.");

        Assert.Contains(trail, a => a.Details.Contains("1,000", StringComparison.Ordinal)
                                    || a.Details.Contains("1000", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>وحارسٌ للمرشّح:</b> مستندٌ سليمُ الإجماليات لا يُلمَس ولا يُكتب له أثر — وإلا
    /// امتلأ سجلُّ التدقيق ضجيجاً عند كلّ إقلاع، وضاع فيه ما يهمّ.
    /// </summary>
    [Fact]
    public async Task AHealthyDocument_IsNeitherTouchedNorLogged()
    {
        var (companyId, invoiceId, _) = await SeedPostedInvoiceAsync();

        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var trail = await _db.AuditLogEntries
            .CountAsync(a => a.CompanyId == companyId && a.EntityKey == invoiceId.ToString());

        Assert.Equal(0, trail);
    }

    /// <summary>
    /// <b>والأثرُ لا يُفسد ما جاء ليحرس.</b> سطورُ التدقيق مربوطةٌ بسلسلةٍ مجزّأة، وسطرٌ
    /// يُضاف بلا ربطٍ يكسرها فيصير الدفترُ كلّه «معبوثاً به» عند أوّل تحقّق. فالكتابةُ
    /// تجري عبر <c>AuditService</c> نفسه — وهذا يقيسُ أنّها كذلك، لا يفترضه.
    /// </summary>
    [Fact]
    public async Task TheBootTrail_KeepsTheAuditChainIntact()
    {
        var (companyId, invoiceId, _) = await SeedPostedInvoiceAsync();
        await ZeroTotalsInDatabaseAsync(invoiceId);

        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var chain = await new AuditChainVerifier(_db).VerifyAsync(companyId);

        Assert.True(chain.EntriesChecked > 0, "لم يُكتب سطرُ تدقيقٍ أصلاً — فالقياسُ أجوف.");
        Assert.True(chain.IsIntact,
            $"كُسرت سلسلةُ التدقيق بعد إصلاح الإقلاع — {chain.Breaks.Count} انقطاعاً.");
    }

    /// <summary>
    /// ‏محاكاةُ صفٍّ قديم: إجمالياتُه لم تُحفظ قطّ. ولا سبيلَ إلى بنائه عبر النطاق —
    /// فالنطاقُ يحسبها دائماً.
    ///
    /// <para><b>ولا عبر SQL خام:</b> قِستُ ذلك فوجدتُ أنّ EF يخزّن <c>decimal</c> في
    /// SQLite <b>نصّاً</b>، فصفرٌ عدديٌّ مكتوبٌ بيدي لا يطابق <c>Subtotal == 0</c> في
    /// استعلام EF — فيمرّ الاختبارُ بلا أن يمسّ المسارَ أصلاً. فالتصفيرُ يجري عبر
    /// EF نفسه ليُكتب بالصيغة التي يقرؤها.</para>
    /// </summary>
    private async Task ZeroTotalsInDatabaseAsync(long invoiceId)
    {
        var invoice = await _db.SalesInvoices.SingleAsync(i => i.Id == invoiceId);
        _db.Entry(invoice).Property(i => i.Subtotal).CurrentValue = 0m;
        _db.Entry(invoice).Property(i => i.VatAmount).CurrentValue = 0m;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task<(long CompanyId, long InvoiceId, string Number)> SeedPostedInvoiceAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الإقلاع", "310123456700003", BusinessProfile.Retail);

        var customer = Customer.Create(company.Id, "عميل نقدي");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 1));
        invoice.AddLine("بضاعة", 10m, 100m);
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        return (company.Id, posted.Id, posted.InvoiceNumber!);
    }
}
