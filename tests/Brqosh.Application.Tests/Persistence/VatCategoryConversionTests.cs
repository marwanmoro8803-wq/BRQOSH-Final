using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (م-٥ · ن-٥١) <b>تحويلُ عمود الفئة الضريبيّة على قاعدةٍ قائمة — ونجاحُه ليس اختياريّاً.</b>
///
/// <para><b>لمن هذا المسار:</b> قاعدةٌ قديمةٌ يُقرّر لها <c>DatabaseSchemaService</c>
/// <c>coreTableExists &amp;&amp; appliedCount == 0</c> <b>لا تُطبَّق عليها هجرةٌ أبداً</b> —
/// يُستدعى المُرقِّعُ ثمّ يُرجَع. فالمُرقِّعُ هو طريقُ إصلاحها الوحيد.</para>
///
/// <para><b>والخطرُ الذي تحرسه هذه الحالات:</b> <c>TrySchemaSqlAsync</c> يبتلع كلَّ فشل
/// ويُسجّله في شاشةٍ لا يفتحها أحد. فلو ابتُلع فشلُ <b>هذا</b> التحويل لصار النموذجُ يقرأ
/// نصّاً والقاعدةُ تخزّن عدداً: <b>إقلاعٌ أخضرُ ثمّ <c>InvalidCastException</c> عند أوّل
/// عرضِ سعرٍ يفتحه المستخدم</b>. <b>والتوقّفُ الصريحُ عند الإقلاع أرحمُ</b>: يقع حيث يوجد
/// مَن يقرأ، ويُستأنف بإزالة السبب.</para>
/// </summary>
public class VatCategoryConversionTests
{
    private const string Table = "sales_quotation_lines";

    /// <summary>يُعيد العمودَ إلى شكله قبل ن-٥١ — <b>فالقاعدةُ القديمةُ تُصنَع لا تُفترَض</b>.</summary>
    private static async Task MakeLegacyIntegerColumnAsync(BrqoshDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync($"""
            ALTER TABLE {Table} ALTER COLUMN "VatCategory" DROP DEFAULT;
            ALTER TABLE {Table} ALTER COLUMN "VatCategory" TYPE integer
                USING (CASE "VatCategory"
                           WHEN 'Standard' THEN 0
                           WHEN 'ZeroRated' THEN 1
                           WHEN 'Exempt' THEN 2
                           WHEN 'OutOfScope' THEN 3
                           ELSE 0 END);
            ALTER TABLE {Table} ALTER COLUMN "VatCategory" SET DEFAULT 0;
            """);
    }

    private static async Task<string?> ColumnTypeAsync(BrqoshDbContext db) =>
        await db.Database.SqlQuery<string>($"""
            SELECT data_type AS "Value" FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = {Table} AND column_name = 'VatCategory'
            """).SingleOrDefaultAsync();

    /// <summary>يزرع عرضَ سعرٍ بسطرٍ معفًى ويُعيد معرّفَه — عبر EF، والعمودُ ما زال نصّاً.</summary>
    private static async Task<long> SeedExemptQuotationAsync(BrqoshDbContext db)
    {
        TestActor.Accountant();

        var company = await new DatabaseInitializer(db)
            .InitializeAsync("شركة ن٥١", "300000000000001", Brqosh.Domain.Modules.BusinessProfile.General);

        var customer = Customer.Create(company.Id, "عميل", "300000000000002");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var quote = SalesQuotation.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 3, 1));
        quote.AddLine("خدمة معفاة", 1m, 1000m, null, VatCategory.Exempt,
            "VATEX-SA-29", "Financial services mentioned in Article 29 of the VAT Regulations");
        db.SalesQuotations.Add(quote);
        await db.SaveChangesAsync();

        return quote.Id;
    }

    [PostgresFact]
    public async Task A_legacy_integer_column_is_converted_and_its_rows_survive()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("n51_convert_ok");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();

        var quotationId = await SeedExemptQuotationAsync(scope.Db);
        await MakeLegacyIntegerColumnAsync(scope.Db);

        // ‏شواهدُ ضبط: القاعدةُ صارت فعلاً «قديمة»، والصفُّ فيها فعلاً عددٌ صحيحٌ قيمتُه ٢.
        Assert.Equal("integer", await ColumnTypeAsync(scope.Db));
        var before = await scope.Db.Database
            .SqlQueryRaw<int>($"""SELECT "VatCategory" AS "Value" FROM {Table}""").ToListAsync();
        Assert.Equal([2], before);

        await using var boot = scope.OpenIndependentContext();
        var updater = new BrqoshSchemaUpdater(boot);
        await updater.ApplyAsync();

        Assert.Equal("character varying", await ColumnTypeAsync(scope.Db));

        var suppressed = updater.SuppressedFailures
            .Where(f => f.Step.StartsWith("EnsureVatCategoryIsTextAsync", StringComparison.Ordinal))
            .ToList();
        Assert.True(suppressed.Count == 0,
            "‏ابتُلع فشلٌ في التحويل: " + string.Join(" · ", suppressed.Select(f => $"{f.Step} ⟵ {f.Message}")));

        // ‏**والقيمةُ محفوظةٌ لا مُقرَّبة**: ٢ صار 'Exempt' لا 'Standard' ولا '2'.
        var after = await scope.Db.Database
            .SqlQueryRaw<string>($"""SELECT "VatCategory" AS "Value" FROM {Table}""").ToListAsync();
        Assert.Equal(["Exempt"], after);

        await using var reader = scope.OpenIndependentContext();
        var quote = await reader.SalesQuotations.Include(q => q.Lines)
            .SingleAsync(q => q.Id == quotationId);
        Assert.Equal(VatCategory.Exempt, quote.Lines[0].VatCategory);
    }

    [PostgresFact]
    public async Task Running_it_twice_changes_nothing()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("n51_convert_twice");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();

        await SeedExemptQuotationAsync(scope.Db);
        await MakeLegacyIntegerColumnAsync(scope.Db);
        Assert.Equal("integer", await ColumnTypeAsync(scope.Db));

        for (var run = 1; run <= 3; run++)
        {
            await using var boot = scope.OpenIndependentContext();
            var updater = new BrqoshSchemaUpdater(boot);
            await updater.ApplyAsync();

            Assert.Equal("character varying", await ColumnTypeAsync(scope.Db));
            Assert.True(updater.SuppressedFailures.Count == 0,
                $"‏الجولةُ {run} ابتلعت فشلاً: "
                + string.Join(" · ", updater.SuppressedFailures.Select(f => $"{f.Step} ⟵ {f.Message}")));
        }
    }

    /// <summary>
    /// <b>الحالةُ التي تُثبت أنّ الإقلاعَ يتوقّف بدل أن يكذب.</b>
    ///
    /// <para>عرضٌ معتمدٌ على العمود يجعل <c>ALTER … TYPE</c> يفشل بـ<c>0A000</c>. وقبل هذا
    /// البند كان <c>ApplyAsync</c> <b>يعود بسلام</b> والعمودُ عددٌ صحيح، ثمّ ترمي أوّلُ قراءةٍ
    /// في يد المستخدم. <b>والمطلوبُ أن يُرمى هنا، والرسالةُ تُسمّي الجدولَ والسبب.</b></para>
    /// </summary>
    [PostgresFact]
    public async Task A_swallowed_failure_stops_the_boot_instead_of_breaking_every_read()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("n51_convert_blocked");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();

        await SeedExemptQuotationAsync(scope.Db);
        await MakeLegacyIntegerColumnAsync(scope.Db);

        await scope.Db.Database.ExecuteSqlRawAsync(
            $"""CREATE VIEW n51_blocker AS SELECT "VatCategory" FROM {Table};""");

        // ‏شاهدُ ضبط: العمودُ عددٌ صحيحٌ والعرضُ قائمٌ قبل أيّ ادّعاء.
        Assert.Equal("integer", await ColumnTypeAsync(scope.Db));

        await using var boot = scope.OpenIndependentContext();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new BrqoshSchemaUpdater(boot).ApplyAsync());

        Assert.Contains(Table, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("0A000", thrown.Message, StringComparison.Ordinal);

        // ‏والقاعدةُ تُركت كما هي — لا نصفَ تحويل.
        Assert.Equal("integer", await ColumnTypeAsync(scope.Db));
    }

    /// <summary>
    /// <b>والقاعدةُ النصّيّةُ أصلاً لا تدخل الحلقةَ، فلا تُرمى أبداً.</b> شرطُ ما بعدُ يرمي —
    /// <b>وحدُّ رميِه أن يكون ثَمّ ما يُحوَّل</b>. لولا هذا لكان عرضٌ على قاعدةٍ سليمة كافياً
    /// لإسقاط كلّ إقلاع.
    /// </summary>
    [PostgresFact]
    public async Task A_view_on_an_already_text_column_does_not_stop_the_boot()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("n51_convert_notext");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();

        await SeedExemptQuotationAsync(scope.Db);
        await scope.Db.Database.ExecuteSqlRawAsync(
            $"""CREATE VIEW n51_harmless AS SELECT "VatCategory" FROM {Table};""");

        Assert.Equal("character varying", await ColumnTypeAsync(scope.Db));

        await using var boot = scope.OpenIndependentContext();
        await new BrqoshSchemaUpdater(boot).ApplyAsync();

        Assert.Equal("character varying", await ColumnTypeAsync(scope.Db));
    }
}
