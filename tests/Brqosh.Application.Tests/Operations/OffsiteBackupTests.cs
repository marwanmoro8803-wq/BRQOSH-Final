using System.Security.Cryptography;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (CEEF-231 · ط5-٤) <b>النسخةُ الاحتياطية خارج الجهاز — والقرصُ الواحد ليس نسخة.</b>
///
/// <para><b>ما كان:</b> <c>BackupService</c> يكتب ملفّاً إلى مجلّدٍ <b>على القرص نفسِه</b>،
/// وشاشةُ النسخ تنصح المالكَ نصّاً بأن «يحفظها على قرصٍ غير قرص الخادم». <b>ونصيحةٌ ليست
/// ميزة</b>: القرصُ الذي تتلف عليه القاعدةُ تتلف عليه نسختُها، <b>والفديةُ التي تُشفّر
/// المجلّدَ تُشفّر مجلّدَ النسخ الذي بجواره</b>. فمنشأةٌ تظنّ نفسَها محميّةً وهي عارية.</para>
///
/// <para><b>وهويّةُ ح-٧:</b> نسخةٌ لم تُتحقَّق ببصمتها ليست نسخة — <b>ووجودُ ملفٍّ بالحجم
/// نفسِه لا يكفي</b>: النقلُ عبر الشبكة يقطع، والقرصُ التالف يكتب أصفاراً بالطول نفسِه.</para>
/// </summary>
public class OffsiteBackupTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "brqosh-offsite-" + Guid.NewGuid().ToString("N")[..8]);

    public OffsiteBackupTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    // ── ح-٧: النسخةُ تُتحقَّق ببصمتها ────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: النسخةُ تصل كاملةً ومطابقةً للأصل بصمةً.</b>
    /// </summary>
    [Fact]
    public async Task AReplicatedBackup_MatchesTheSourceByHash()
    {
        var source = WriteBackup("brqosh_2026-09-03_0100.db", 4096);
        var destination = Path.Combine(_root, "nas");

        var results = await new OffsiteBackupService().ReplicateAsync(source, [destination]);

        var result = Assert.Single(results);
        Assert.True(result.Succeeded, result.Failure);

        Assert.Equal(Hash(source), Hash(result.CopiedPath!));
    }

    /// <summary>
    /// <b>ولا يبقى ملفٌّ مؤقَّتٌ باسمٍ ناقص.</b>
    ///
    /// <para><b>ونسخٌ يُقطع في منتصفه يترك ملفّاً بالاسم الصحيح وبمحتوىً ناقص</b> —
    /// ويُقرأ نسخةً سليمةً حين يُحتاج إليها. فيُكتب إلى اسمٍ مؤقّتٍ ثمّ يُنقل.</para>
    /// </summary>
    [Fact]
    public async Task NoPartialFile_IsLeftBehind()
    {
        var source = WriteBackup("brqosh_2026-09-03_0200.db", 2048);
        var destination = Path.Combine(_root, "nas2");

        await new OffsiteBackupService().ReplicateAsync(source, [destination]);

        Assert.Empty(Directory.GetFiles(destination, "*.partial"));
    }

    /// <summary>
    /// <b>وملفٌّ بالطول نفسِه ومحتوىً مختلفٍ يُرفض.</b>
    ///
    /// <para><b>وهذا فرعُ الفشل مقيساً مباشرةً</b>: لا يقع في نسخٍ محلّيٍّ سليم،
    /// <b>وحارسٌ يمرّ لأنّ الفرعَ لا يُبلَغ لا يحرس شيئاً</b> — وقد قِيس ذلك بطفرةٍ نجت:
    /// استُبدل حسابُ بصمة المنسوخ ببصمة المصدر <b>فبقيت الحزمةُ خضراء</b>.</para>
    ///
    /// <para><b>والطولُ متساوٍ عمداً</b>: القرصُ التالف يكتب أصفاراً بالطول نفسِه،
    /// <b>وفحصٌ بالحجم يقبله</b>.</para>
    /// </summary>
    [Fact]
    public async Task AFileOfTheSameLengthButDifferentContent_FailsVerification()
    {
        var source = WriteBackup("brqosh_verify_source.db", 1024);
        var corrupted = Path.Combine(_root, "corrupted.db");
        await File.WriteAllBytesAsync(corrupted, new byte[1024]);   // ‏أصفارٌ بالطول نفسِه

        Assert.Equal(new FileInfo(source).Length, new FileInfo(corrupted).Length);

        var expected = await OffsiteBackupService.FileHashAsync(source);

        Assert.True(await OffsiteBackupService.MatchesAsync(source, expected));
        Assert.False(await OffsiteBackupService.MatchesAsync(corrupted, expected));
    }

    // ── الوجهةُ تُوصَف بصدق ─────────────────────────────────────────────────

    /// <summary>
    /// <b>ووجهةٌ على القرص نفسِه تُنسَخ إليها ويُقال إنّها ليست خارج الجهاز.</b>
    ///
    /// <para><b>ولا تُرفض</b>: قرصٌ ثانٍ داخليٌّ أفضلُ من لا شيء. <b>والادّعاءُ بأنّه
    /// حمايةٌ من تلف الجهاز كذب</b> — فيُقال الفرقُ ويراه المالك.</para>
    /// </summary>
    [Fact]
    public async Task ADestinationOnTheSameVolume_SucceedsButIsNotCalledOffMachine()
    {
        var source = WriteBackup("brqosh_2026-09-03_0300.db", 1024);
        var destination = Path.Combine(_root, "same-disk");

        var result = Assert.Single(
            await new OffsiteBackupService().ReplicateAsync(source, [destination]));

        Assert.True(result.Succeeded);
        Assert.False(result.IsOffMachine);
    }

    /// <summary>
    /// <b>ومسارُ شبكةٍ خارج الجهاز بحكم تعريفه.</b>
    /// </summary>
    [Fact]
    public void ANetworkPath_IsOffMachineByDefinition()
    {
        Assert.True(OffsiteBackupService.IsOffMachine(
            Path.Combine(_root, "x.db"), @"\\nas\backups"));

        Assert.False(OffsiteBackupService.IsOffMachine(
            Path.Combine(_root, "x.db"), Path.Combine(_root, "other")));
    }

    // ── الفشلُ يُسمّى بوجهته ────────────────────────────────────────────────

    /// <summary>
    /// <b>ووجهةٌ تفشل لا تُسقط البقية — ويُسمّى فشلُها بوجهته.</b>
    ///
    /// <para>منشأةٌ لها وجهتان وواحدةٌ مفصولةٌ تبقى محميّةً بالأخرى. <b>وسقوطٌ عند أوّل
    /// فشلٍ يُفقد ما كان سينجح</b>، و«تعذّر النسخ» بلا اسمٍ يترك المالكَ يظنّ الكلَّ فشل.</para>
    /// </summary>
    [Fact]
    public async Task AFailingDestination_DoesNotStopTheOthers()
    {
        var source = WriteBackup("brqosh_2026-09-03_0400.db", 512);
        var good = Path.Combine(_root, "good");

        // ‏وجهةٌ لا تصلح مساراً — كقرصٍ مفصولٍ أو حرفٍ لا وجودَ له.
        var bad = "Z:\u0000\u0000/invalid";

        var results = await new OffsiteBackupService().ReplicateAsync(source, [bad, good]);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(results[0].Failure));
        Assert.True(results[1].Succeeded, results[1].Failure);
    }

    /// <summary>
    /// <b>ونسخةُ مصدرٍ غير موجودةٍ تُرفض قبل أيّ عمل.</b>
    /// </summary>
    [Fact]
    public async Task AMissingSource_IsRefused()
    {
        await Assert.ThrowsAsync<AccountingException>(() =>
            new OffsiteBackupService().ReplicateAsync(
                Path.Combine(_root, "nope.db"), [Path.Combine(_root, "d")]));
    }

    /// <summary>
    /// <b>وبلا وجهةٍ يُرفض باسمه</b> — ونسخةٌ على القرص نفسِه ليست نسخة.
    /// </summary>
    [Fact]
    public async Task NoDestination_IsRefusedByName()
    {
        var source = WriteBackup("brqosh_2026-09-03_0500.db", 256);

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new OffsiteBackupService().ReplicateAsync(source, []));

        Assert.Contains("وجهة", error.Message, StringComparison.Ordinal);
    }

    // ── التنظيفُ بعد التحقّق ────────────────────────────────────────────────

    /// <summary>
    /// <b>ويُبقي العددَ المطلوب ويحذف الأقدم.</b>
    ///
    /// <para><b>والحذفُ بعد التحقّق لا قبله</b>: تنظيفُ القديم ثمّ فشلُ الجديد يترك
    /// المنشأةَ <b>بلا نسخةٍ واحدة</b>.</para>
    /// </summary>
    [Fact]
    public async Task OldCopies_ArePrunedAfterTheNewOneIsVerified()
    {
        var destination = Path.Combine(_root, "rotating");
        Directory.CreateDirectory(destination);

        // ‏ثلاثُ نسخٍ قديمةٍ في الوجهة، وأزمنتُها مرتَّبة.
        for (var i = 1; i <= 3; i++)
        {
            var old = Path.Combine(destination, $"brqosh_old_{i}.db");
            await File.WriteAllBytesAsync(old, new byte[64]);
            File.SetLastWriteTimeUtc(old, new DateTime(2026, 1, i, 0, 0, 0, DateTimeKind.Utc));
        }

        var source = WriteBackup("brqosh_2026-09-03_0600.db", 128);

        var result = Assert.Single(await new OffsiteBackupService()
            .ReplicateAsync(source, [destination], keepPerDestination: 2));

        Assert.True(result.Succeeded, result.Failure);

        var remaining = Directory.GetFiles(destination, "brqosh_*.db");
        Assert.Equal(2, remaining.Length);

        // ‏والجديدةُ من الباقيات — وتنظيفٌ يحذفها يُفقد ما جيء لأجله.
        Assert.Contains(remaining, f => Path.GetFileName(f) == "brqosh_2026-09-03_0600.db");
    }

    /// <summary>
    /// <b>و<c>keep = 0</c> لا يحذف شيئاً.</b>
    /// </summary>
    [Fact]
    public async Task KeepZero_PrunesNothing()
    {
        var destination = Path.Combine(_root, "keep-all");
        Directory.CreateDirectory(destination);
        await File.WriteAllBytesAsync(Path.Combine(destination, "brqosh_old.db"), new byte[8]);

        var source = WriteBackup("brqosh_2026-09-03_0700.db", 64);

        var result = Assert.Single(await new OffsiteBackupService()
            .ReplicateAsync(source, [destination], keepPerDestination: 0));

        Assert.Equal(0, result.Pruned);
        Assert.Equal(2, Directory.GetFiles(destination, "brqosh_*.db").Length);
    }

    /// <summary>
    /// <b>ونسخةٌ تفشل لا تحذف شيئاً — والتنظيفُ بعد التحقّق لا قبله.</b>
    ///
    /// <para><b>وهذا أخطرُ ترتيبٍ في هذا الملفّ</b>: تنظيفُ القديم ثمّ فشلُ الجديد
    /// <b>يترك المنشأةَ بلا نسخةٍ واحدة</b> — وهو أسوأُ من عدم التنظيف بمراحل.</para>
    ///
    /// <para><b>والفشلُ يُصنَع بمجلَّدٍ يحمل اسمَ الملفّ الهدف</b>: النقلُ إليه يرمي،
    /// <b>وقياسٌ بنجاحٍ لا يستطيع التفريقَ بين الترتيبين</b> — وقد قِيس ذلك بطفرةٍ نجت.</para>
    /// </summary>
    [Fact]
    public async Task AFailedCopy_PrunesNothing()
    {
        var destination = Path.Combine(_root, "fail-prune");
        Directory.CreateDirectory(destination);

        for (var i = 1; i <= 3; i++)
        {
            var old = Path.Combine(destination, $"brqosh_old_{i}.db");
            await File.WriteAllBytesAsync(old, new byte[32]);
            File.SetLastWriteTimeUtc(old, new DateTime(2026, 1, i, 0, 0, 0, DateTimeKind.Utc));
        }

        var source = WriteBackup("brqosh_2026-09-03_0800.db", 96);

        // ‏مجلَّدٌ باسم الملفّ الهدف — فالنقلُ إليه يرمي، والنسخةُ تفشل.
        Directory.CreateDirectory(Path.Combine(destination, "brqosh_2026-09-03_0800.db"));

        var result = Assert.Single(await new OffsiteBackupService()
            .ReplicateAsync(source, [destination], keepPerDestination: 1));

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.Pruned);

        // ‏**والقديماتُ الثلاثُ باقيات** — ولو نُظّف قبل التحقّق لبقيت واحدةٌ والجديدةُ لم تصل.
        Assert.Equal(3, Directory.GetFiles(destination, "brqosh_old_*.db").Length);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private string WriteBackup(string name, int bytes)
    {
        var path = Path.Combine(_root, "backups", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var payload = new byte[bytes];
        RandomNumberGenerator.Fill(payload);
        File.WriteAllBytes(path, payload);
        return path;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
