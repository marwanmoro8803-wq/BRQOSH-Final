using System.Text.RegularExpressions;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-231 · ط5-١٠) <b>ترقيةُ مخطّطٍ تفشل لا تُقرأ نجاحاً.</b>
///
/// <para><b>ما كان:</b> عشرون موضعاً في <c>BrqoshSchemaUpdater</c> تكتب DDL داخل
/// <c>try { … } catch { }</c> فارغ. <b>والتسامحُ مقصودٌ ولا يُنزَع</b>: جدولٌ قد يُنشئه
/// <c>EnsureCreated</c> بعدُ، وعمودٌ قد يوجد سلفاً.</para>
///
/// <para><b>لكنّ فشلاً حقيقياً كان لا يُميَّز عن ذلك</b>: صلاحيةٌ ناقصة، أو قرصٌ ممتلئ،
/// أو صياغةٌ لا يقبلها إصدارُ المحرّك. <b>فتُقلع القاعدةُ ويُقال «تمّت الترقية»</b>، ثمّ
/// تسقط عند أوّل استعلامٍ يمسّ ما لم يُنشأ — <b>وهي لحظةُ عملٍ لا لحظةُ إقلاع</b>.</para>
///
/// <para><b>وهذا الحارسُ يقيس شيئين</b>: أنّ الفشلَ يُسجَّل باسم خطوته، <b>وأنّ التسامحَ
/// باقٍ</b> — فترقيةٌ تفشل خطوةٌ فيها لا تُسقط ما بعدها.</para>
/// </summary>
public class SchemaFailureVisibilityTests
{
    /// <summary>
    /// <b>القياسُ الحاكم: كلُّ خطوةٍ متسامحةٍ تمرّ بالمُجمِّع — لا بـ<c>catch</c> فارغ.</b>
    ///
    /// <para><b>وقياسٌ نصّيٌّ عمداً</b>: فرعُ الفشل لا يُبلَغ على قاعدةٍ سليمة، <b>وحارسٌ
    /// سلوكيٌّ يمرّ لأنّه لا يصل إليه</b> — وقد قِيس ذلك في هذه الحملة مرّتين. <b>وهذا
    /// يقيس القاعدةَ: لا <c>catch</c> فارغاً في هذا الملفّ.</b></para>
    /// </summary>
    [Fact]
    public void TheSchemaUpdater_HasNoSilentCatch()
    {
        var source = SchemaUpdaterSource();
        if (source is null) return;   // ‏المصادرُ غيرُ متاحةٍ في هذا التشغيل.

        // ‏**والتعليقاتُ تُسقط قبل البحث** — فذِكرُ النمط في توثيقٍ يشرحه ليس وقوعاً له.
        // ‏وهو درسُ `ServiceReachabilityTests` نفسِه: **حارسٌ يقرأ تعليقاً يتّهم بالباطل
        // ‏أو يُبرّئ بالباطل**، وكلاهما يجعله يُتجاهَل.
        var code = StripComments(source);

        // ‏`catch` بلا وسيطٍ ولا جسدٍ يُسجّل.
        var silent = Regex.Matches(code, @"catch\s*(?:\(\s*\))?\s*\{\s*\}");

        Assert.True(silent.Count == 0,
            $"{silent.Count} موضعاً يبتلع فشلَ خطوةِ مخطّطٍ بلا تسجيل — "
            + "وترقيةٌ تفشل تُقرأ نجاحاً، ثمّ تسقط القاعدةُ عند أوّل استعلامٍ يمسّ ما لم يُنشأ.");
    }

    /// <summary>
    /// <b>وحارسٌ للحارس: المُجمِّع مستعمَلٌ فعلاً لا معرَّفٌ وحسب.</b>
    ///
    /// <para>فلو حُذفت كلُّ النداءات وبقي التعريفُ <b>لمرّ الأوّلُ صامتاً</b> — إذ لا
    /// <c>catch</c> فارغ حينها ولا تسجيلَ كذلك.</para>
    /// </summary>
    [Fact]
    public void TheCollector_IsActuallyUsed()
    {
        var source = SchemaUpdaterSource();
        if (source is null) return;

        var calls = Regex.Matches(source, @"await TrySchemaSqlAsync\(");

        Assert.True(calls.Count >= 15,
            $"نداءاتُ المُجمِّع {calls.Count} — والملفُّ يحوي عشرين خطوةً متسامحة، "
            + "فعددٌ أقلُّ يعني أنّ خطواتٍ عادت تبتلع فشلَها بطريقٍ آخر.");
    }

    /// <summary>
    /// <b>والفشلُ يُسجَّل باسمه — ويُقاس بخطوةٍ تفشل عمداً.</b>
    ///
    /// <para><b>وهذا هو فرعُ الفشل مبلوغاً</b>: يُطلب DDL لا يقبله المحرّك، <b>فيُسجَّل
    /// ولا يُرمى</b>. وحارسٌ يقيس المسارَ الناجحَ وحدَه لا يحرس هذا — <b>وقد قِيس ذلك
    /// في هذه الحملة ثلاثَ مرّات بطفراتٍ نجت</b>.</para>
    ///
    /// <para><b>ويُستدعى المُجمِّعُ مباشرةً لا عبر <c>ApplyAsync</c></b>: ترقيةُ قاعدةٍ
    /// سليمةٍ لا تُفشل خطوةً، <b>وقاعدةٌ فارغةٌ تماماً تُسقط <c>ApplyAsync</c> نفسَه</b>
    /// عند مسارٍ غيرِ متسامحٍ قبل أن تبلغ المُجمِّع. <b>وحدُّ هذا القياس مُعلَن</b>: يقيس
    /// أنّ المُجمِّع يُسجّل ولا يرمي، <b>ويقيس الحارسان أعلاه أنّه هو الطريقُ الوحيد</b>.</para>
    /// </summary>
    [Fact]
    public async Task AFailingStep_IsRecordedAndDoesNotThrow()
    {
        TestActor.Administrator();

        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, "Data Source=:memory:");
        db.Database.OpenConnection();
        await db.Database.EnsureCreatedAsync();

        var updater = new BrqoshSchemaUpdater(db);

        // ‏خطوتان تفشلان عمداً — **والثانيةُ تُثبت أنّ الأولى لم تُوقف ما بعدها**.
        var first = await updater.TrySchemaSqlForTests(
            "Probe.BadSyntax", "THIS IS NOT SQL", CancellationToken.None);

        var second = await updater.TrySchemaSqlForTests(
            "Probe.MissingTable", "UPDATE table_that_does_not_exist SET x = 1", CancellationToken.None);

        Assert.False(first);
        Assert.False(second);

        Assert.Equal(2, updater.SuppressedFailures.Count);
        Assert.Equal("Probe.BadSyntax", updater.SuppressedFailures[0].Step);
        Assert.Equal("Probe.MissingTable", updater.SuppressedFailures[1].Step);

        // ‏**وكلُّ سطرٍ يحمل نوعَه ورسالتَه** — ورسالةُ المحرّك وحدَها لا تقول أيَّ خطوةٍ طلبتها.
        Assert.All(updater.SuppressedFailures, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.ExceptionType));
            Assert.False(string.IsNullOrWhiteSpace(f.Message));
        });
    }

    /// <summary>
    /// <b>وخطوةٌ تنجح لا تُسجَّل — فوجودُ السطر هو الخبر.</b>
    ///
    /// <para><b>وسجلٌّ يمتلئ في كلّ إقلاعٍ سليمٍ يُدرَّب المستخدمُ على تجاهله</b>، فلا
    /// يقرؤه يومَ تفشل ترقيةٌ حقيقية.</para>
    /// </summary>
    [Fact]
    public async Task ASucceedingStep_IsNotRecorded()
    {
        TestActor.Administrator();

        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, "Data Source=:memory:");
        db.Database.OpenConnection();
        await db.Database.EnsureCreatedAsync();

        var updater = new BrqoshSchemaUpdater(db);

        var ok = await updater.TrySchemaSqlForTests(
            "Probe.Valid", "CREATE TABLE IF NOT EXISTS probe_ok (id INTEGER)", CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(updater.SuppressedFailures);
    }

    /// <summary>يُسقط التعليقات — <b>فذِكرُ النمط في شرحٍ له ليس وقوعاً له</b>.</summary>
    private static string StripComments(string source)
    {
        var text = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*?$", " ", RegexOptions.Multiline);
    }

    private static string? SchemaUpdaterSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName,
                "src", "Brqosh.Infrastructure", "Persistence", "BrqoshSchemaUpdater.cs");

            if (File.Exists(path)) return SchemaUpdaterSourceFiles.ReadAll(dir.FullName);
            dir = dir.Parent;
        }

        return null;
    }
}
