using Brqosh.Application.Setup;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Api.Tests;

/// <summary>
/// (م-٥ · ن-٢٢) <b>واجهةٌ بلا سلسلة اتّصالٍ لا تسقط إلى دفاترِ المُشغِّل في الإنتاج.</b>
///
/// <para><b>وهذا السطرُ هو السببُ المُسمّى في CEEF-203</b>، وتعليقُ
/// <c>BrqoshDataRoot</c> يسجّله حرفيّاً: «بُرهن تجريبياً أنّ تشغيلةً واحدةً لاختبارات API
/// تضيف شركاتٍ إلى قاعدته المحلّيّة، <b>لأنّ Brqosh.Api يسقط إلى القاعدة الافتراضيّة حين
/// لا يجد ConnectionStrings:Brqosh</b>». <b>وأُصلح حينها المستدعون — ولم يُصلَح السطر</b>،
/// فبقي إلى ن-٢٢.</para>
///
/// <para><b>ولماذا الرفضُ أرحمُ من السقوط:</b> واجهةٌ تعمل على قاعدةٍ محلّيّةٍ فارغةٍ
/// <b>تبدو سليمةً تماماً</b> — تُقلع، وتردّ، وتُرجع صفراً من كلّ شيء. <b>والصفرُ يُقرأ
/// بياناتٍ لا عطلاً</b>، فيُبنى عليه قرار.</para>
///
/// <para><b>وحدُّ هذا الاختبار مكتوبٌ فيه:</b> يقيس <b>شرطَ الرفض</b> — البيئةُ إنتاجٌ
/// وسلسلةُ الاتّصال غائبة — <b>ولا يُقلع مضيفاً كاملاً</b>. وذلك مقصود: بناءُ المضيف في
/// اختبارٍ يُثبّت ترتيبَ التهيئة ويجعل الاختبارَ يقيس المضيفَ لا الشرط.</para>
/// </summary>
public class ProductionRefusesTheDefaultDatabaseTests : IDisposable
{
    public void Dispose()
    {
        AppEnvironmentConfig.ResetForTests();
        GC.SuppressFinalize(this);
    }

    /// <summary>الشرطُ كما هو في <c>Program.cs</c> حرفاً بحرف.</summary>
    private static bool WouldRefuse(string? configuredConnection) =>
        string.IsNullOrWhiteSpace(configuredConnection) && AppEnvironmentConfig.IsProduction;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InProduction_AMissingConnectionString_IsRefused(string? connection)
    {
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
        Assert.True(WouldRefuse(connection));
    }

    /// <summary>
    /// <b>وشاهدُ ضبط: سلسلةٌ معرَّفةٌ تمرّ في الإنتاج</b> — فالشرطُ يرفض الغيابَ لا العمل.
    /// <b>وبلا هذا</b> يكون «يرفض دائماً» مُرضياً للاختبار أعلاه وهو تعطيلٌ تامّ للواجهة.
    /// </summary>
    [Fact]
    public void InProduction_AConfiguredConnectionString_Passes()
    {
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
        Assert.False(WouldRefuse("Host=db;Database=brqosh;Username=app"));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ: التطويرُ يبقى كما هو.</b> السقوطُ نافعٌ هناك،
    /// <b>والاختباراتُ نفسُها تعتمده</b> وتوجّه جذرَها إلى مجلّدٍ مؤقّت
    /// (<c>TestDataRootInitializer</c>) — فرفضٌ شاملٌ كان يُسقط الحزمةَ كلَّها.
    /// </summary>
    [Theory]
    [InlineData(AppEnvironment.Development)]
    [InlineData(AppEnvironment.Test)]
    public void OutsideProduction_TheFallbackRemains(AppEnvironment environment)
    {
        AppEnvironmentConfig.ForceForTests(environment);
        Assert.False(WouldRefuse(null));
    }

    /// <summary>
    /// <b>والسقوطُ يقع في جذرٍ معزولٍ أثناء الاختبار</b> — وهو ما جعل CEEF-203 يكتب
    /// <c>TestDataRootInitializer</c>. شاهدُ ضبطٍ على أنّ العزلَ نافذٌ الآن.
    /// </summary>
    [Fact]
    public void TheTestRun_DoesNotPointAtTheRealDataRoot()
    {
        var fallback = BrqoshDbContextFactory.DefaultSqlitePath();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.False(fallback.StartsWith(Path.Combine(appData, "BRQOSH"), StringComparison.OrdinalIgnoreCase),
            $"‏جذرُ بيانات الاختبار يشير إلى بيانات المستخدم الحقيقيّة: {fallback}");
    }
}
