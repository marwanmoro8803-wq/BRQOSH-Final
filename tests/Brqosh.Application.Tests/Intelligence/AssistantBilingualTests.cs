using Brqosh.Application.Intelligence;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Intelligence;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Intelligence;

/// <summary>
/// (O-26، CEEF-225) مساعد برقوش يتكلّم لغة الواجهة.
///
/// كان <c>FinancialInsight</c> يحمل عنواناً وتفصيلاً **بالعربية وحدها**، فكل ردّ
/// يضمّ نصّ رؤية يخرج عربياً **حتى من فرعه الإنجليزيّ**؛ بل مرّر بعضها النصّ العربيّ
/// نفسه إلى الحقلين (<c>SalesReply</c>, <c>FilteredReply</c>, <c>SummaryReply</c>).
/// وبطاقات الرؤى في اللوحة كانت تعرض <c>TitleAr</c> دائماً.
/// المقيس في المسح الحيّ: نصّ عربيّ في لوحة المساعد على **60 صفحة من 60** بالإنجليزية.
///
/// هذا الحارس **يشغّل المحرك فعلاً** على بيانات مزروعة ويفحص كل رؤية يُنتجها —
/// لا قائمةً منسوخة هنا. فأيّ رؤية جديدة تُضاف بلا إنجليزية تُسقطه.
/// </summary>
public class AssistantBilingualTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public AssistantBilingualTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static bool HasArabic(string text) =>
        text.Any(ch => ch is >= '؀' and <= 'ۿ');

    private FinancialIntelligenceService Engine() =>
        new(_db, new DataQualityService(_db));

    private async Task<Company> SeedAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة ثنائية اللغة", "310123456700003", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return company;
    }

    /// <summary>كل رؤية يُنتجها المحرك لها إنجليزية حقيقية — غير فارغة وبلا حرف عربيّ.</summary>
    [Fact]
    public async Task EveryProducedInsight_HasRealEnglish()
    {
        var company = await SeedAsync();

        var report = await Engine().AnalyzeAsync(company.Id, new DateOnly(2026, 7, 25));

        Assert.NotEmpty(report.Insights);

        var broken = report.Insights
            .Where(i => string.IsNullOrWhiteSpace(i.TitleEn)
                        || string.IsNullOrWhiteSpace(i.DetailEn)
                        || HasArabic(i.TitleEn)
                        || HasArabic(i.DetailEn))
            .Select(i => $"{i.Code} → «{i.TitleEn}» / «{i.DetailEn}»")
            .ToList();

        Assert.True(broken.Count == 0,
            $"رؤى بلا إنجليزية سليمة ({broken.Count} من {report.Insights.Count}):\n  - " +
            string.Join("\n  - ", broken));
    }

    /// <summary>وللعربية أيضاً — الحارس يعمل بالاتجاهين فلا تنقلب المشكلة.</summary>
    [Fact]
    public async Task EveryProducedInsight_KeepsItsArabic()
    {
        var company = await SeedAsync();

        var report = await Engine().AnalyzeAsync(company.Id, new DateOnly(2026, 7, 25));

        var missing = report.Insights
            .Where(i => string.IsNullOrWhiteSpace(i.TitleAr) || string.IsNullOrWhiteSpace(i.DetailAr))
            .Select(i => i.Code)
            .ToList();

        Assert.True(missing.Count == 0, "رؤى بلا عربية: " + string.Join(", ", missing));
    }

    /// <summary>
    /// ردود المساعد على كل المواضيع: الإنجليزيّ إنجليزيّ فعلاً.
    /// الأسئلة بكلمات إنجليزية لأن المصنّف يقرأ الكلمات لا اللغة.
    /// </summary>
    [Theory]
    [InlineData("cash flow")]
    [InlineData("vat")]
    [InlineData("receivable")]
    [InlineData("payable")]
    [InlineData("sales")]
    [InlineData("inventory")]
    [InlineData("anomaly")]
    [InlineData("help")]
    [InlineData("give me a summary")]
    public async Task AssistantEnglishAnswer_IsFreeOfArabic(string question)
    {
        var company = await SeedAsync();
        var assistant = new FinancialAssistantService(_db, Engine());

        var reply = await assistant.AskAsync(company.Id, question);

        Assert.False(string.IsNullOrWhiteSpace(reply.AnswerEn), "الردّ الإنجليزيّ فارغ.");
        Assert.False(HasArabic(reply.AnswerEn),
            $"«{question}» ← ردّ إنجليزيّ يحوي عربياً:\n{reply.AnswerEn}");
    }

    /// <summary>والعربيّ يبقى عربياً — لا ينقلب الردّان إلى نسخة واحدة.</summary>
    [Theory]
    [InlineData("التدفق النقدي")]
    [InlineData("الضريبة")]
    [InlineData("ذمم العملاء")]
    [InlineData("المبيعات")]
    public async Task AssistantArabicAnswer_StaysArabic(string question)
    {
        var company = await SeedAsync();
        var assistant = new FinancialAssistantService(_db, Engine());

        var reply = await assistant.AskAsync(company.Id, question);

        Assert.True(HasArabic(reply.AnswerAr), $"«{question}» ← ردّ عربيّ بلا عربية:\n{reply.AnswerAr}");
        Assert.NotEqual(reply.AnswerAr, reply.AnswerEn);
    }

    /// <summary>فحوص جودة البيانات ثنائية أيضاً — كانت إنجليزية وحيدة داخل واجهة عربية.</summary>
    [Fact]
    public async Task DataQualityFindings_AreBilingual()
    {
        var company = await SeedAsync();

        var report = await new DataQualityService(_db).RunAsync(company.Id);

        Assert.NotEmpty(report.Findings);

        var broken = report.Findings
            .Where(f => string.IsNullOrWhiteSpace(f.MessageAr) || string.IsNullOrWhiteSpace(f.MessageEn)
                        || !HasArabic(f.MessageAr) || HasArabic(f.MessageEn))
            .Select(f => $"{f.Code} → ar«{f.MessageAr}» en«{f.MessageEn}»")
            .ToList();

        Assert.True(broken.Count == 0,
            $"فحوص جودة بيانات غير ثنائية ({broken.Count} من {report.Findings.Count}):\n  - " +
            string.Join("\n  - ", broken));
    }
}
