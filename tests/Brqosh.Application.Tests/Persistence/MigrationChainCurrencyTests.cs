using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-229 · م2-٩) <b>هل ما تزال سلسلةُ الهجرات تصف النموذج؟</b>
///
/// <para><b>العطل:</b> لقطةُ النموذج (<c>BrqoshDbContextModelSnapshot</c>) هي ذاكرةُ EF عن
/// «آخر شكلٍ رُحِّل». وقد تخلّفت عن النموذج، فصار كلّ تعديلٍ لاحقٍ على الكيانات يعيش في
/// <see cref="BrqoshSchemaUpdater"/> — DDL مكتوبٌ باليد يُشغَّل عند كلّ إقلاع — بدل هجرة.
/// والنتيجة أنّ من يكتب <c>dotnet ef migrations add</c> اليوم يحصل على هجرةٍ تُعيد إنشاء
/// ما رقّعه المُرقِّع أصلاً، فترتطم بـ <c>42701 column already exists</c> على كلّ قاعدةٍ
/// قائمة — <b>ويبتلعها</b> <see cref="DatabaseSchemaService.IsIgnorableSchemaConflict"/>
/// فلا يُكتب صفُّ التاريخ، وتُعاد المحاولة عند كلّ إقلاعٍ إلى الأبد بلا شكوى.</para>
///
/// <para><b>ولماذا لم يكشفه شيء:</b> الاختبارات كلّها على SQLite، وSQLite يُهيَّأ بـ
/// <c>EnsureCreated</c> <b>من النموذج نفسه</b> ولا يمرّ على الهجرات إطلاقاً — فالسلسلة قد
/// تتعفّن سنةً كاملةً دون أن يحمرّ اختبارٌ واحد.</para>
///
/// <para><b>ولماذا PostgreSQL هنا رغم أنّ الاختبار لا يتصل بقاعدة:</b> النموذج نفسه يختلف
/// باختلاف المزوّد — <c>xmin</c> رمزَ تزامنٍ لا يُضاف إلا على Npgsql — واللقطة مولَّدةٌ
/// لـ PostgreSQL. فمقارنةُ نموذجٍ SQLite بلقطةٍ PostgreSQL تُظهر انحرافاً دائماً وتجعل
/// الحارس يصرخ بلا سبب. وبناءُ النموذج لا يفتح اتصالاً، فلا حاجة إلى خادمٍ يعمل.</para>
/// </summary>
public class MigrationChainCurrencyTests
{
    /// <summary>مزوّدٌ إنتاجيّ بسلسلة اتصالٍ لا تُفتح — بناءُ النموذج والمقارنة عملٌ في الذاكرة.</summary>
    private static BrqoshDbContext ProductionShapedContext() =>
        BrqoshDbContextFactory.Create(
            DatabaseProvider.PostgreSql,
            "Host=localhost;Port=5433;Database=brqosh_design;Username=postgres;Password=design");

    /// <summary>
    /// <b>القياس المركزيّ.</b> النموذج واللقطة متطابقان — فأيّ تعديلٍ على كيانٍ من اليوم
    /// فصاعداً يُلزم صاحبه بهجرةٍ بدل ترقيعٍ يدويّ في المُرقِّع.
    /// </summary>
    [Fact]
    public void TheModelSnapshot_StillDescribesTheModel()
    {
        using var db = ProductionShapedContext();

        Assert.False(
            db.Database.HasPendingModelChanges(),
            "النموذج تغيّر بعد آخر هجرة. أضِف هجرةً بدل ترقيع BrqoshSchemaUpdater: "
            + "dotnet ef migrations add <اسم> --project src/Brqosh.Infrastructure — "
            + "واجعل DDL الهجرة محتملاً للتكرار (IF NOT EXISTS) لأنّ قواعد العملاء "
            + "قد تكون رُقِّعت مسبقاً.");
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> الهجرات موجودةٌ فعلاً ومقروءة. لو أعاد أحدٌ تسمية مجلّد الهجرات
    /// أو أفرغه لصار <c>HasPendingModelChanges</c> بلا معنى ومرّ الاختبار أعلاه صامتاً.
    /// </summary>
    [Fact]
    public void TheMigrationChain_IsNotEmpty()
    {
        using var db = ProductionShapedContext();

        var migrations = db.Database.GetMigrations().ToList();

        Assert.NotEmpty(migrations);
        Assert.Contains(migrations, m => m.Contains("InitialCreate", StringComparison.Ordinal));
    }
}
