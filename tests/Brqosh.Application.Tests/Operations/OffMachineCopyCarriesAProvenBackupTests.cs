using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// ‏(م-٥ · ن-٤٤) <b>النقلُ خارج الجهاز كان يُرسل «أحدثَ ملفٍّ» لا «نسخةً أُثبتت».</b>
///
/// <para><b>ما كان:</b> <c>BackupPage</c> يختار بـ<c>GetFiles("brqosh_*")</c> — <b>بلا
/// امتداد</b> — وهو الموضعُ الوحيدُ في المستودع الذي يمسح مجلّدَ النسخ هكذا. فيلتقط ما
/// لا يلتقطه <c>brqosh_*.dump</c>/<c>.db</c>، <b>وأخطرُه بقيّةٌ مرحليّةٌ يتيمة</b>
/// (<c>.partial</c>) <b>تكون دائماً الأحدث</b>. ثمّ يُطابق النقلُ بصمةَ المصدر بالوجهة،
/// <b>فتُثبَت النسخةُ مطابقةً لمصدرٍ فاسد</b> وتُطبع ✔ لكلّ وجهة.</para>
///
/// <para><b>ونقض ذلك ثابتاً أعلنه المستودعُ بنفسِه</b> عند <c>StagingSuffix</c>: «بقايا
/// محاولةٍ مقطوعةٍ تصير غيرَ معدودةٍ <b>بالتصميم</b>، لا بفحصٍ يُنسى أو يُخفَّف».</para>
///
/// <para><b>ولمَ القياسُ سلوكيٌّ لا نصّيّ:</b> حارسٌ يقرأ نصَّ المُعالِج <b>يمرّ أخضرَ
/// بتعليقٍ واحدٍ يذكر <c>VerifyAsync</c></b>، والعطلُ قائم. فالاختيارُ نُقل إلى
/// <c>BackupService</c> ويُقاس هنا <b>بملفّاتٍ حقيقيّةٍ في مجلّدٍ مؤقّتٍ خاصٍّ بكلّ اختبار</b>
/// — فلا يُسمّم اختباراً متوازياً، وهو عطلٌ وقع في هذه الحزمة فعلاً.</para>
/// </summary>
public sealed class OffMachineCopyCarriesAProvenBackupTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly string _folder;

    public OffMachineCopyCarriesAProvenBackupTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();

        // ‏مجلّدٌ خاصٌّ بهذا الاختبار — لا مجلّدَ بيانات مشترك.
        _folder = Path.Combine(Path.GetTempPath(), "brqosh-n44-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
    }

    // ── ١: ما يُختار ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>البقيّةُ المرحليّةُ اليتيمةُ هي الأحدثُ دائماً — ولا تُنقَل.</b>
    /// </summary>
    [Fact]
    public async Task AnOrphanedStagingRemnant_IsNeverChosen_EvenThoughItIsNewest()
    {
        var good = Valid("brqosh_2026-09-12_0100.db", TimeSpan.FromHours(-2));
        var orphan = Garbage("brqosh_2026-09-13_1600.db.partial", TimeSpan.Zero);

        // ‏شاهدُ ضبطٍ موجب: اليتيمُ فعلاً هو الأحدثُ على القرص.
        Assert.True(new FileInfo(orphan).LastWriteTimeUtc > new FileInfo(good).LastWriteTimeUtc);

        var chosen = await new BackupService(_db).NewestVerifiedAsync(_folder);

        Assert.Equal(good, chosen);
    }

    /// <summary>
    /// <b>وملفٌّ صحيحُ الاسم فاسدُ المحتوى يُتخطّى إلى ما قبله.</b> فالنمطُ يُسقط ما يُسقطه
    /// اسمُه، <b>والفحصُ يُسقط ما لا يُسقطه اسمٌ أبداً</b> — وهما ثقبان لا ثقبٌ واحد.
    /// </summary>
    [Theory]
    [InlineData("zero")]
    [InlineData("garbage")]
    [InlineData("truncated")]
    public async Task ANewerButUnprovableBackup_IsSkipped(string kind)
    {
        var good = Valid("brqosh_2026-09-12_0100.db", TimeSpan.FromHours(-2));
        var bad = "brqosh_2026-09-13_1600.db";
        _ = kind switch
        {
            "zero" => Zero(bad, TimeSpan.Zero),
            "garbage" => Garbage(bad, TimeSpan.Zero),
            _ => Truncated(bad, TimeSpan.Zero),
        };

        var chosen = await new BackupService(_db).NewestVerifiedAsync(_folder);

        Assert.True(good == chosen, $"‏اختير «{Path.GetFileName(chosen)}» بدل «{Path.GetFileName(good)}».\n{Census()}");
    }

    /// <summary>
    /// ‏جردُ المجلّد وقتَ السقوط — <b>فحارسٌ يقول «فشل» ولا يقول «ماذا رأى» يُكلّف قارئَه
    /// ما كلّفنا اكتشافُه</b>، وهذا الاختبارُ سقط مرّةً تحت الحِمل ولم يقل شيئاً.
    /// </summary>
    private string Census() =>
        $"‏المزوّد: {(_db.Database.IsNpgsql() ? "Npgsql" : "Sqlite")}\n‏ما في المجلّد:\n"
        + string.Join("\n", new DirectoryInfo(_folder).GetFiles()
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => $"  {f.Name} · {f.Length} بايت · {f.LastWriteTimeUtc:HH:mm:ss.fff} · حكمُ الفحص: {Verdict(f.FullName)}"));

    /// <summary>‏ما يقوله الفحصُ عن ملفٍّ بعينِه — <b>فالسؤالُ «لمَ قُبِل» لا «أيُّهما اختير»</b>.</summary>
    private string Verdict(string path)
    {
        try
        {
            new BackupService(_db).VerifyAsync(path).GetAwaiter().GetResult();
            return "قُبِل";
        }
        catch (Exception ex)
        {
            return $"رُفض ({ex.GetType().Name}: {ex.Message.Split('\n')[0][..Math.Min(70, ex.Message.Length)]})";
        }
    }

    /// <summary>‏شاهدُ ضبطٍ موجب: نسخةٌ سليمةٌ وحدَها تُختار — فالحارسُ لا يرفض كلَّ شيء.</summary>
    [Fact]
    public async Task AValidBackup_IsChosen()
    {
        var good = Valid("brqosh_2026-09-13_1600.db", TimeSpan.Zero);

        Assert.Equal(good, await new BackupService(_db).NewestVerifiedAsync(_folder));
    }

    // ── ٢: ما يُقال حين لا يوجد ما يُنقَل ───────────────────────────────────

    /// <summary>
    /// <b>وسقوطُ الجميع يُسمّي كلَّ ساقط.</b> فرسالةٌ تقول «لا نسخةَ صالحة» ولا تقول أيَّ
    /// ملفٍّ ولا لماذا <b>تُكلّف قارئَها ما كلّفنا اكتشافُها</b>.
    /// </summary>
    [Fact]
    public async Task WhenEveryCandidateFails_ItNamesEachOne()
    {
        Zero("brqosh_2026-09-13_1600.db", TimeSpan.Zero);
        Garbage("brqosh_2026-09-12_0100.db", TimeSpan.FromHours(-2));

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new BackupService(_db).NewestVerifiedAsync(_folder));

        Assert.Contains("brqosh_2026-09-13_1600.db", ex.Message, StringComparison.Ordinal);
        Assert.Contains("brqosh_2026-09-12_0100.db", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>‏ومجلّدٌ خالٍ يقول «خُذْ نسخةً» لا «نسخُك فاسدة».</summary>
    [Fact]
    public async Task AnEmptyFolder_SaysTakeABackup()
    {
        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new BackupService(_db).NewestVerifiedAsync(_folder));

        Assert.Contains("خُذْ نسخةً", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والمشيُ محدود.</b> الفحصُ يقرأ الأرشيفَ كاملاً، فمجلّدٌ فيه أربعَ عشرةَ نسخةً
    /// فاسدةً لا يُنفق دقائقَ قبل أن يقول شيئاً — والرسالةُ تقول كم فُحص من كم.
    /// </summary>
    [Fact]
    public async Task TheWalk_IsBounded_AndSaysHowManyItExamined()
    {
        for (var i = 0; i < 6; i++)
            Zero($"brqosh_2026-09-{10 + i:00}_0100.db", TimeSpan.FromHours(-i));

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new BackupService(_db).NewestVerifiedAsync(_folder));

        Assert.Contains("6", ex.Message, StringComparison.Ordinal);

        // ‏**أسماءٌ متمايزة، لا مطابقاتٌ نصّيّة.** أوّلُ صياغةٍ عدّت المطابقات فوجدت ستّاً
        // ‏لثلاثة ملفّات — لأنّ رسالةَ الرفض تحمل **المسارَ الكامل**، فيتكرّر الاسمُ فيها.
        // ‏فعدّادٌ يقيس ما لم يقصده يُسقط حارساً على كودٍ سليم.
        var named = Regex.Matches(ex.Message, @"brqosh_2026-09-\d\d_0100\.db")
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();

        Assert.Equal(BackupService.NewestCandidatesExamined, named);
    }

    // ── ٣: الفحصُ نفسُه — وهو الحارسُ أمام الاستعادة المتلِفة ───────────────

    /// <summary>
    /// <b>وملفٌّ صفريٌّ ليس قاعدةً — وSQLite كان يقول إنّه سليم.</b>
    ///
    /// <para>و<c>VerifyAsync</c> هو الحارسُ الوحيدُ أمام <c>RestoreAsync</c>، و<c>RestoreSqliteAsync</c>
    /// ينسخ فوق القاعدة الحيّة بـ<c>overwrite: true</c> — <b>فملفٌّ صفريٌّ كان يجتاز التحقّقَ
    /// ثمّ يمحو الدفتر</b>.</para>
    /// </summary>
    [Fact]
    public async Task VerifyAsync_RefusesAZeroByteFile()
    {
        var empty = Zero("brqosh_2026-09-13_1600.db", TimeSpan.Zero);

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new BackupService(_db).VerifyAsync(empty));

        Assert.Contains("فارغ", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>‏شاهدُ ضبطٍ موجب: الفحصُ يقبل نسخةً سليمة — فلا يرفض كلَّ شيءٍ فيبدو حارساً.</summary>
    [Fact]
    public async Task VerifyAsync_AcceptsAValidFile() =>
        await new BackupService(_db).VerifyAsync(Valid("brqosh_ok.db", TimeSpan.Zero));

    // ── ٤: اختبارٌ ذاتيّ — أكانت الخوارزميّةُ القديمةُ تختار السيّئ فعلاً؟ ──

    /// <summary>
    /// <b>وحارسٌ يقرأ كوداً صحيحاً لا يُثبت أنّه يرفض كوداً خاطئاً.</b> فتُعاد هنا خوارزميّةُ
    /// ما قبل الإصلاح حرفيّاً — النمطُ العاري ثمّ الأحدث، بلا فحص — ويُطالَب المجلّدُ نفسُه
    /// بأن <b>يُسلّمها البقيّةَ اليتيمة</b>. فلو لم تفعل لكان هذا الاختبارُ كلُّه زينة.
    /// </summary>
    [Fact]
    public void ThePreFixSelector_WouldHaveChosenTheOrphan()
    {
        Valid("brqosh_2026-09-12_0100.db", TimeSpan.FromHours(-2));
        var orphan = Garbage("brqosh_2026-09-13_1600.db.partial", TimeSpan.Zero);

        // ‏نسخةُ `BackupPage.OnReplicate` كما كانت عند dfaca0b.
        var preFix = new DirectoryInfo(_folder)
            .GetFiles("brqosh_*")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        Assert.NotNull(preFix);
        Assert.Equal(orphan, preFix!.FullName);
    }

    /// <summary>‏والنمطُ ذو الامتداد لا يراها — وهو ثابتُ ن-٣١ الذي نقضه موضعٌ واحد.</summary>
    [Fact]
    public void TheExtensionPattern_NeverSeesAStagingRemnant()
    {
        Garbage("brqosh_2026-09-13_1600.db.partial", TimeSpan.Zero);

        Assert.Empty(new DirectoryInfo(_folder).GetFiles("brqosh_*.db"));
        Assert.Single(new DirectoryInfo(_folder).GetFiles("brqosh_*"));
    }

    // ── ٥: ولا يعود النمطُ العاري إلى المستودع ─────────────────────────────

    /// <summary>
    /// <b>تثبيتٌ بنيويّ:</b> لا موضعَ في <c>src</c> يمسح مجلّدَ النسخ بنمطٍ بلا امتداد.
    ///
    /// <para><b>ويُفحَص الملفُّ كاملاً لا سطراً سطراً</b>: تغليفُ السطر أو إعادةُ ترتيبٍ
    /// من المُحرِّر كان يُعمي فحصاً سطريّاً، <b>والشيءُ الوحيدُ الذي وُجد هذا الحارسُ ليمنعه
    /// هو عودةُ ذلك النمط</b>. والتعليقاتُ تُمحى — <b>والنصوصُ الحرفيّةُ لا</b>، لأنّ النمطَ
    /// نفسَه نصٌّ حرفيّ.</para>
    /// </summary>
    [Fact]
    public void NoSourceFile_ScansTheBackupFolderWithoutAnExtension()
    {
        var offenders = BareBackupPatternsIn(SourceRoot());

        Assert.True(offenders.Count == 0,
            "‏مسحٌ لمجلّد النسخ بنمطٍ بلا امتداد — يلتقط `.partial` و`-wal` و`.tmp`:\n  "
            + string.Join("\n  ", offenders));
    }

    [Theory]
    [InlineData("GetFiles(\"brqosh_*\")", true)]
    [InlineData("GetFiles(\n    \"brqosh_*\")", true)]                 // ‏سطران — والفحصُ السطريُّ يعمى
    [InlineData("EnumerateFiles(dir, \"brqosh_*\")", true)]
    [InlineData("GetFiles(\"brqosh_*.db\")", false)]
    [InlineData("GetFiles($\"brqosh_*{extension}\")", false)]
    [InlineData("// GetFiles(\"brqosh_*\")", false)]                   // ‏تعليقٌ ليس نداءً
    [InlineData("/* GetFiles(\"brqosh_*\") */", false)]
    public void TheBarePatternScanner_Discriminates(string snippet, bool expected) =>
        Assert.Equal(expected, BarePattern.IsMatch(BlankComments(snippet)));

    private static readonly Regex BarePattern = new(
        @"(?:GetFiles|EnumerateFiles)\s*\([^)]*""brqosh_\*""", RegexOptions.Compiled);

    private static List<string> BareBackupPatternsIn(string root)
    {
        var found = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            var cleaned = BlankComments(File.ReadAllText(file));
            foreach (Match m in BarePattern.Matches(cleaned))
            {
                var line = cleaned.Take(m.Index).Count(c => c == '\n') + 1;
                found.Add($"{Path.GetRelativePath(root, file)}:{line}");
            }
        }

        return found;
    }

    /// <summary>يمسح التعليقاتِ فقط — <b>والنصوصُ الحرفيّةُ تبقى</b>، فالنمطُ المُبحوثُ عنه نصّ.</summary>
    private static string BlankComments(string source)
    {
        var b = new StringBuilder(source);

        for (var i = 0; i < source.Length - 1; i++)
        {
            int end;
            if (source[i] == '/' && source[i + 1] == '/')
            {
                end = source.IndexOf('\n', i);
                if (end < 0) end = source.Length;
            }
            else if (source[i] == '/' && source[i + 1] == '*')
            {
                end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
            }
            else continue;

            for (var k = i; k < end; k++)
                if (source[k] != '\n' && source[k] != '\r') b[k] = ' ';
            i = end - 1;
        }

        return b.ToString();
    }

    // ── الأدوات ─────────────────────────────────────────────────────────────

    private string Valid(string name, TimeSpan age)
    {
        var path = Path.Combine(_folder, name);
        using (var c = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
        }.ToString()))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "CREATE TABLE ledger(id INTEGER PRIMARY KEY, amount TEXT);"
                            + "INSERT INTO ledger(amount) VALUES('1234.56');";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        return Stamp(path, age);
    }

    private string Zero(string name, TimeSpan age)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, []);
        return Stamp(path, age);
    }

    private string Garbage(string name, TimeSpan age)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, Enumerable.Repeat((byte)'x', 8192).ToArray());
        return Stamp(path, age);
    }

    /// <summary>‏نسخةٌ سليمةٌ قُطعت — وهي الحالةُ التي يسمّيها البند.</summary>
    private string Truncated(string name, TimeSpan age)
    {
        var whole = Valid(name + ".whole", TimeSpan.Zero);
        var bytes = File.ReadAllBytes(whole);
        File.Delete(whole);

        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes.Take(bytes.Length / 2).ToArray());
        return Stamp(path, age);
    }

    private static string Stamp(string path, TimeSpan age)
    {
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow + age);
        return path;
    }

    private static string SourceRoot()
    {
        var root = Path.Combine(FindRepoRoot(), "src");
        Assert.True(Directory.Exists(root), $"‏لم يُعثر على {root} — الحارسُ أعمى، ولا يمرّ أعمى.");
        return root;
    }

    /// <summary>‏رميةٌ لا تخطٍّ صامت: حارسٌ لا يجد ما يحرسه يجب أن يسقط، لا أن يمرَّ أخضر.</summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
