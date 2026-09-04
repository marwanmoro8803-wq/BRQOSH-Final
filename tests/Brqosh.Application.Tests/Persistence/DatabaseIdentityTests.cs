using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-236 · م-٢٠) <b>مَن يسكن هذه القاعدة؟ — يُسأل قبل أن يُكتب فيها حرف.</b>
///
/// <para><b>ما فتح الباب:</b> نافذةُ الإنقاذ (م-١٣) قبلت أيَّ سلسلة اتصالٍ يمرّ اختبارُها،
/// <b>واختبارُ الاتصال يُثبت البلوغَ لا الهويّة</b>. وما بعده كان يفعل أحدَ أمرَين بلا سؤال:
/// قاعدةٌ فارغةٌ ⟵ <b>أربعون هجرةً وشركةٌ جديدة فيها</b> باسمٍ من ملفّ الإعداد المحليّ؛
/// وقاعدةٌ فيها منشأةٌ أخرى ⟵ <b>تُتبنّى كما هي</b>، فيُرحَّل على دفترِ غيرِه والترويسةُ
/// تعرض اسمَه هو — لأنّ فحصَ الترخيص والعنوانَ يقرآن الملفَّ المحليَّ لا القاعدة.</para>
///
/// <para>وهذه الحزمةُ تقيس <see cref="DatabaseBootstrapService.InspectAsync"/> على قاعدةٍ
/// حقيقيّةٍ في الحالات الثلاث: بلا مخطَّط، ومخطَّطٌ بلا منشأة، ومنشأةٌ حاضرة.</para>
/// </summary>
public class DatabaseIdentityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public DatabaseIdentityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// قاعدةٌ بلا مخطَّط BRQOSH — <b>الجوابُ «عذراء» لا استثناءٌ يُسقط الإقلاع</b>.
    /// وهي الحالةُ التي كان الإصلاحُ يُنشئ فيها المخطَّطَ كلَّه في قاعدةٍ أجنبيّة.
    /// </summary>
    [Fact]
    public async Task ADatabaseWithNoSchema_IsReportedVirgin_NotThrown()
    {
        var identity = await new DatabaseBootstrapService(_db).InspectAsync();

        Assert.False(identity.SchemaPresent);
        Assert.True(identity.IsVirgin);
        Assert.Null(identity.CompanyId);
    }

    /// <summary>ومخطَّطٌ حاضرٌ بلا منشأة عذراءُ كذلك — فلا يُتبنّى فراغ.</summary>
    [Fact]
    public async Task ASchemaWithoutACompany_IsStillVirgin()
    {
        await _db.Database.EnsureCreatedAsync();

        var identity = await new DatabaseBootstrapService(_db).InspectAsync();

        Assert.True(identity.SchemaPresent);
        Assert.True(identity.IsVirgin);
        Assert.Null(identity.CompanyName);
    }

    /// <summary>ومنشأةٌ حاضرةٌ تُسمّى باسمها ورقمها — فيُعرض للمشغّل مَن يسكن القاعدة.</summary>
    [Fact]
    public async Task AnInhabitedDatabase_NamesItsCompany()
    {
        await new DatabaseInitializer(_db).InitializeAsync(
            "منشأة الدفاتر الحقيقية", "310123456700003", BusinessProfile.General);

        var identity = await new DatabaseBootstrapService(_db).InspectAsync();

        Assert.True(identity.SchemaPresent);
        Assert.False(identity.IsVirgin);
        Assert.Equal("منشأة الدفاتر الحقيقية", identity.CompanyName);
        Assert.Equal("310123456700003", identity.VatNumber);
    }

    /// <summary>
    /// <b>والمطابقةُ هي الحارس:</b> منشأةٌ أخرى في القاعدة تُردّ، ولو تشابه الاسمُ اختلفَ
    /// الرقمُ الضريبيّ — وهو الدليلُ القاطع.
    /// </summary>
    [Fact]
    public async Task Matches_RejectsAnotherCompany_AndADifferentVatNumber()
    {
        await new DatabaseInitializer(_db).InitializeAsync(
            "منشأة الدفاتر الحقيقية", "310123456700003", BusinessProfile.General);
        var identity = await new DatabaseBootstrapService(_db).InspectAsync();

        Assert.True(identity.Matches("منشأة الدفاتر الحقيقية", "310123456700003"));
        Assert.True(identity.Matches("  منشأة الدفاتر الحقيقية  ", "310123456700003"));

        Assert.False(identity.Matches("منشأة أخرى", "310123456700003"));
        // ‏الاسمُ نفسُه ورقمٌ ضريبيٌّ آخر — نسخةُ اختبارٍ أو منشأةٌ شقيقة. تُردّ.
        Assert.False(identity.Matches("منشأة الدفاتر الحقيقية", "399999999900003"));
        // ‏ورقمٌ غيرُ مُعلَنٍ في الإعداد لا يُستعمل حجّةً على الرفض — الاسمُ وحدَه يحكم.
        Assert.True(identity.Matches("منشأة الدفاتر الحقيقية", ""));
    }

    /// <summary>وقاعدةٌ عذراءُ لا تُطابق أحداً مهما كان المتوقَّع — فلا تمرّ بالتساهل.</summary>
    [Fact]
    public async Task AVirginDatabase_MatchesNobody()
    {
        await _db.Database.EnsureCreatedAsync();
        var identity = await new DatabaseBootstrapService(_db).InspectAsync();

        Assert.False(identity.Matches("أيّ منشأة", "310123456700003"));
        Assert.False(identity.Matches("", ""));
    }
}
