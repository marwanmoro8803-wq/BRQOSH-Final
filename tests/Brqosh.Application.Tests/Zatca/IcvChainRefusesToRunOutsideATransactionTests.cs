using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (م-٥ · ن-٢٥) <b>توليدُ مستند ZATCA خارج معاملةٍ يُرفض على كلّ مزوّد.</b>
///
/// <para><b>ما كان قبله:</b> <c>AcquireIcvLockAsync</c> يبدأ بـ
/// <c>if (!db.Database.IsNpgsql()) return;</c> — <b>فيتخطّى القفلَ والحارسَ معاً</b>.
/// والقفلُ خاصٌّ بـPostgreSQL بحقّ (<c>pg_advisory_xact_lock</c>)، <b>أمّا شرطُ المعاملة
/// فليس كذلك</b>: قراءةُ العدّاد ثمّ الكتابةُ فوقه عمليّةٌ مركّبة، وبلا معاملةٍ لا تكون
/// ذرّيّةً على أيّ مزوّد.</para>
///
/// <para><b>وأثرُ التخطّي أنّ الحارسَ لا يعمل حيث تُكتب الاختبارات.</b> والحارسُ نفسُه
/// أمسك عطلاً حقيقيّاً من قبل — تعليقُه يسجّله: «كان حيّاً في إلغاء الفاتورة وميّتاً في
/// إلغاء الإشعار الدائن والمستخلص». <b>فمسارٌ يُكتب ويُختبَر على SQLite بلا معاملة يمرّ
/// أخضرَ، ثمّ يرمي عند أوّل تشغيلٍ على PostgreSQL</b> — أي أنّ الخطأ يُكتشف في يد
/// المُشغِّل بدل أن يُكتشف في الحزمة.</para>
///
/// <para><b>وحدُّ هذا الاختبار مكتوبٌ فيه:</b> يحرس <b>وجودَ المعاملة</b> لا صحّةَ القفل.
/// والقفلُ الاستشاريُّ نفسُه يبقى PostgreSQL-only بحقّ، ولا يُقاس هنا.</para>
/// </summary>
public sealed class IcvChainRefusesToRunOutsideATransactionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;

    public IcvChainRefusesToRunOutsideATransactionTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _workDirectory = Path.Combine(Path.GetTempPath(), "n25-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_workDirectory, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة ن-٢٥", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        ZatcaReadySeller.Complete(company);
        await ZatcaReadySeller.CompleteAsync(_db, _companyId);

        var customer = Customer.Create(_companyId, "عميل ن-٢٥", "311111111100003");
        // ‏مشترٍ مكتملُ الهويّة — الفاتورةُ القياسيّة تذهب إلى المقاصّة وطرفُها معرَّف.
        customer.UpdateRegulatoryIdentity(
            "مؤسسة العميل التجارية",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
                additionalNumber: "8765"));
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        // ‏**وبلا هذا لا يُولَّد مستندٌ أصلاً** — وهو ما أسقط شاهدَي الضبط في أوّل تشغيلة:
        // ‏الفوترةُ الإلكترونيّةُ مُطفأةٌ افتراضاً، فكان الاختبارُ يقيس صمتاً لا سلوكاً.
        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
        await _db.SaveChangesAsync();
    }

    private async Task<SalesInvoice> PostInvoiceAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة استشارية", 1m, 1_000m);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> استدعاءُ المولّد بلا معاملةٍ يرمي — على SQLite أيضاً.
    /// </summary>
    [Fact]
    public async Task GeneratingOutsideATransaction_IsRefused()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        // ‏شاهدُ ضبط: لا معاملةَ مفتوحةً الآن — فالرفضُ التالي عن الشرط لا عن حالةٍ أخرى.
        Assert.Null(_db.Database.CurrentTransaction);

        var thrown = await Assert.ThrowsAsync<AccountingException>(
            () => new ZatcaEInvoiceGenerator(_db).TryGenerateCancellationForSalesInvoiceAsync(invoice.Id));

        Assert.Contains("خارج معاملة", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وداخلَ معاملةٍ يمرّ</b> — فالحارسُ يشترط المعاملةَ ولا يمنع العمل.
    /// <b>وبلا هذا الشاهد</b> يكون «يرمي دائماً» مُرضياً للاختبار أعلاه وهو عطلٌ تامّ.
    /// </summary>
    [Fact]
    public async Task GeneratingInsideATransaction_Succeeds()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var document = await new ZatcaEInvoiceGenerator(_db)
            .TryGenerateCancellationForSalesInvoiceAsync(invoice.Id);

        Assert.NotNull(document);
        await transaction.CommitAsync();
    }

    /// <summary>
    /// <b>والمسارُ الإنتاجيُّ يفتح معاملتَه بنفسه</b> — فالحارسُ لا يمسّه.
    /// وهو شاهدُ ضبطٍ ثالث: لو كان الحارسُ يمنع العملَ المشروع لسقط الترحيلُ نفسُه.
    /// </summary>
    [Fact]
    public async Task ThePostingPath_IsUnaffected()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var generated = await _db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == _companyId && d.SourceDocumentId == invoice.Id)
            .ToListAsync();

        Assert.NotEmpty(generated);
    }
}
