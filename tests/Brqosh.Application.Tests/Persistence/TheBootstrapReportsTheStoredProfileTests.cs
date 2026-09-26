using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٦٦ · sector-modules#10) <b>التمهيدُ يُعيد نظامَ المنشأة المخزَّن — لا النظامَ المطلوب.</b>
///
/// <para><b>ما كان:</b> جهازٌ ثانٍ يُعَدّ على قاعدةٍ فيها منشأة. معالجُه يبدأ على أوّل نظامٍ في قائمته،
/// و<c>DatabaseInitializer.InitializeAsync</c> يُعيد المنشأةَ القائمةَ <b>ويتجاهل النظامَ المطلوب</b> — صحيح.
/// لكنّ <c>CompanyBootstrapResult</c> لم يكن يحمل النظامَ أصلاً، فبقي المُقلِعُ على اختيار المعالج وبنى القوائمَ
/// له: <b>منشأةُ مقاولاتٍ تُفتح على جهازها الثاني بلا شاشات المقاولات</b>، وبياناتُها في القاعدة.</para>
///
/// <para>ملفُّ SQLite لا ذاكرة: سياقان على القاعدة نفسِها هما الجهازان.</para>
/// </summary>
public class TheBootstrapReportsTheStoredProfileTests
{
    [Fact]
    public async Task ASecondWorkstationGetsTheCompanysProfile_NotItsWizardsPick()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-profile-{Guid.NewGuid():N}.db");
        try
        {
            await using (var first = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}"))
            {
                var installed = await new DatabaseBootstrapService(first).InitializeAsync(
                    "مقاولات الشرق", "300000000000003", BusinessProfile.Construction);
                Assert.Equal(BusinessProfile.Construction, installed.Profile);
            }

            await using (var second = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}"))
            {
                // ‏معالجُ الجهاز الثاني على «محاسبة عامة» — وهي أوّلُ القائمة.
                var joined = await new DatabaseBootstrapService(second).InitializeAsync(
                    "مقاولات الشرق", "300000000000003", BusinessProfile.General);

                Assert.Equal(BusinessProfile.Construction, joined.Profile);
            }
        }
        finally
        {
            ReleaseSqliteFile(path);
        }
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب:</b> تركيبٌ بكرٌ يأخذ ما اختاره المعالج — وإلّا فالتوكيدُ أعلاه يمرّ على خدمةٍ تُعيد
    /// نظاماً ثابتاً لا نظامَ القاعدة.
    /// </summary>
    [Fact]
    public async Task AFreshInstallKeepsTheProfileItChose()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-profile-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}");
            var installed = await new DatabaseBootstrapService(db).InitializeAsync(
                "عيادة الشفاء", "300000000000004", BusinessProfile.Healthcare);

            Assert.Equal(BusinessProfile.Healthcare, installed.Profile);
        }
        finally
        {
            ReleaseSqliteFile(path);
        }
    }

    private static void ReleaseSqliteFile(string path)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path))
            File.Delete(path);
    }
}
