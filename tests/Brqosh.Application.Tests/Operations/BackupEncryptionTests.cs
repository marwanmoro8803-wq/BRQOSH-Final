using System.Security.Cryptography;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (جاهزيّةُ الإنتاج · H · قرارُ المالك 2026-11-14) ‏<b>النسخةُ مشفّرةٌ تشفيراً مغلَّفاً ومفتاحُ الاستعادة يفتحها.</b>
///
/// <para>‏كلُّ حالةٍ تقيس وعداً واحداً: الذهابُ والإيابُ حرفاً بحرف (بما فيه حدودُ القطع)، ومفتاحٌ خاطئٌ يُرفض، وبايتٌ معدَّلٌ في
/// ‏المحتوى أو الرأس يُرفض، وبترُ القطعة الأخيرة يُرفض، وتدويرُ المفتاح يُعيد التغليفَ ولا يمسّ المحتوى، والجهازُ الجديدُ يفتح
/// ‏بالمفتاح وحدَه، والنسخةُ القديمةُ الصريحةُ تُستعاد كما كانت. ‏والجذرُ معزولٌ بـ<c>AsyncLocal</c> لكلّ اختبار.</para>
/// </summary>
public sealed class BackupEncryptionTests : IDisposable
{
    private const string KeyA = "ABCDE-FGHJK-MNPQR-STVWX-YZ234";
    private const string KeyB = "ZYXWV-TSRQP-NMKJH-GFEDC-BA987";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "brqosh-enc-" + Guid.NewGuid().ToString("N")[..10]);

    public BackupEncryptionTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "backups"));
        BackupKeyring.RootOverrideSlot.Value = _root;
    }

    public void Dispose()
    {
        BackupKeyring.RootOverrideSlot.Value = null;
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException ex) { Console.WriteLine($"تنظيف: {ex.Message}"); }
    }

    private string Backups(string name) => Path.Combine(_root, "backups", name);

    private string WritePlain(string name, int length)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, RandomNumberGenerator.GetBytes(length));
        return path;
    }

    private string Seal(string plain, string name)
    {
        var keyring = BackupKeyring.Load()!;
        var target = Backups(name);
        BackupCipher.Encrypt(plain, target, keyring.MasterPoint, keyring.Recovery);
        return target;
    }

    // ‏صفرٌ، وأقلُّ من قطعة، وقطعةٌ بالضبط، وقطعتان ونصف — حدودُ «الأخيرة» كلُّها.
    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(BackupCipher.ChunkSize)]
    [InlineData(BackupCipher.ChunkSize * 2 + 512 * 1024)]
    public void ARoundTrip_GivesBackTheSameBytes_WithTheRecoveryKeyAlone(int length)
    {
        BackupKeyring.OnRecoveryKeyIssued(KeyA);
        var plain = WritePlain("plain.bin", length);
        var sealedPath = Seal(plain, "brqosh_rt.db");

        Assert.True(BackupCipher.IsEncrypted(sealedPath));
        Assert.Equal(BackupCipher.HeaderLength + length + BackupCipher.TagSize * Math.Max(1, (length + BackupCipher.ChunkSize - 1) / BackupCipher.ChunkSize),
            new FileInfo(sealedPath).Length);

        // ‏«جهازٌ جديد»: لا حلقةَ ولا DPAPI — رأسُ الملفّ ومفتاحُ الاستعادة وحدَهما.
        File.Delete(BackupKeyring.PathFor());
        using var master = BackupCipher.OpenWithRecoveryKey(sealedPath, "abcde fghjk mnpqr stvwx yz234");
        var opened = Path.Combine(_root, "opened.bin");
        BackupCipher.Decrypt(sealedPath, opened, master);

        Assert.Equal(File.ReadAllBytes(plain), File.ReadAllBytes(opened));
    }

    [Fact]
    public void AWrongRecoveryKey_IsRefused_InWords()
    {
        BackupKeyring.OnRecoveryKeyIssued(KeyA);
        var sealedPath = Seal(WritePlain("p.bin", 5000), "brqosh_wrong.db");

        var ex = Assert.Throws<AccountingException>(() => BackupCipher.OpenWithRecoveryKey(sealedPath, KeyB));
        Assert.Contains("مفتاحُ الاستعادة غير صحيح", ex.Message, StringComparison.Ordinal);
    }

    // ‏بايتٌ واحدٌ في المحتوى، وفي مفتاح البيانات المغلَّف، وفي حجم القطعة — كلُّها تُرفض، ولا يبقى ناتجٌ جزئيّ.
    [Theory]
    [InlineData(BackupCipher.HeaderLength + 10)]
    [InlineData(BackupCipher.WrappedKeyOffset + 3)]
    [InlineData(BackupCipher.ChunkSizeOffset)]
    [InlineData(BackupCipher.FileIdOffset + 1)]
    public void ATamperedByte_IsRefused_AndLeavesNoPlaintext(int offset)
    {
        BackupKeyring.OnRecoveryKeyIssued(KeyA);
        var sealedPath = Seal(WritePlain("p.bin", 70_000), "brqosh_tamper.db");
        var bytes = File.ReadAllBytes(sealedPath);
        bytes[offset] ^= 0x01;
        File.WriteAllBytes(sealedPath, bytes);

        using var master = BackupKeyring.Load()!.OpenLocal()!;
        var opened = Path.Combine(_root, "opened.bin");
        Assert.Throws<AccountingException>(() => BackupCipher.Decrypt(sealedPath, opened, master));
        Assert.False(File.Exists(opened), "نصٌّ مفكوكٌ جزئيّاً بقي على القرص بعد رفض الوسم.");
    }

    // ‏بترُ القطعة الأخيرة كاملةً يجعل السابقةَ تبدو أخيرة — وعَلَمُ «الأخيرة» في البيانات المرافقة يكشفه.
    [Fact]
    public void DroppingTheLastChunk_IsRefused()
    {
        BackupKeyring.OnRecoveryKeyIssued(KeyA);
        var sealedPath = Seal(WritePlain("p.bin", BackupCipher.ChunkSize + 100), "brqosh_trunc.db");
        var bytes = File.ReadAllBytes(sealedPath);
        File.WriteAllBytes(sealedPath, bytes[..^(100 + BackupCipher.TagSize)]);

        using var master = BackupKeyring.Load()!.OpenLocal()!;
        Assert.Throws<AccountingException>(() => BackupCipher.Decrypt(sealedPath, Path.Combine(_root, "o.bin"), master));
    }

    [Fact]
    public void RotatingTheRecoveryKey_RewrapsExistingBackups_WithoutReencryptingTheirContent()
    {
        BackupKeyring.OnRecoveryKeyIssued(KeyA);
        var plain = WritePlain("p.bin", 300_000);
        var sealedPath = Seal(plain, "brqosh_rot.db");
        var before = File.ReadAllBytes(sealedPath);

        var rewrapped = BackupKeyring.OnRecoveryKeyIssued(KeyB);

        Assert.Equal(1, rewrapped);
        var after = File.ReadAllBytes(sealedPath);
        // ‏ما خارج مقطع الاستعادة لم يتغيّر بايتاً — المحتوى ومفتاحُ بياناته كما هما.
        Assert.Equal(before[..BackupCipher.RecoveryOffset], after[..BackupCipher.RecoveryOffset]);
        Assert.Equal(before[BackupCipher.ChunkSizeOffset..], after[BackupCipher.ChunkSizeOffset..]);
        Assert.NotEqual(before[BackupCipher.RecoveryOffset..BackupCipher.ChunkSizeOffset],
            after[BackupCipher.RecoveryOffset..BackupCipher.ChunkSizeOffset]);

        Assert.Throws<AccountingException>(() => BackupCipher.OpenWithRecoveryKey(sealedPath, KeyA));
        using var master = BackupCipher.OpenWithRecoveryKey(sealedPath, KeyB);
        var opened = Path.Combine(_root, "opened.bin");
        BackupCipher.Decrypt(sealedPath, opened, master);
        Assert.Equal(File.ReadAllBytes(plain), File.ReadAllBytes(opened));

        // ‏والنسخُ الجديدةُ بعد التدوير لنفس المفتاح العامّ — فالمفتاحُ الرئيسيُّ لم يتبدّل.
        Assert.Equal(BackupCipher.MasterPointOf(sealedPath), BackupKeyring.Load()!.MasterPoint);
    }

    // ── عبر الخدمة نفسِها على SQLite ─────────────────────────────────────────

    [Fact]
    public async Task TheService_TakesAnEncryptedBackup_AndRestoresIt()
    {
        BackupKeyring.OnRecoveryKeyIssued(KeyA);
        var live = Path.Combine(_root, "live.db");
        await SeedAsync(live, "قبل");
        var target = Backups("brqosh_20261114_000000.db");

        await using (var db = Open(live))
        {
            var result = await new BackupService(db).CreateAsync(target);
            Assert.True(result.Encrypted);
        }

        Assert.True(BackupCipher.IsEncrypted(target));
        Assert.DoesNotContain("SQLite format 3", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(target)[..64]));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "backups"), "*.partial"));

        await SetProbeAsync(live, "بعد");
        await using (var db = Open(live))
            await new BackupService(db).RestoreAsync(target);

        Assert.Equal("قبل", await ReadProbeAsync(live));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "backups"), "*.partial"));
    }

    // ‏على جهازٍ لا يملك مفتاحَ النسخ: بلا مفتاحٍ يُطلب المفتاح، وبالمفتاح تُستعاد.
    [Fact]
    public async Task OnAnotherMachine_TheServiceAsksForTheRecoveryKey_ThenRestores()
    {
        BackupKeyring.OnRecoveryKeyIssued(KeyA);
        var live = Path.Combine(_root, "live.db");
        await SeedAsync(live, "الأصل");
        var target = Backups("brqosh_20261114_000001.db");
        await using (var db = Open(live))
            await new BackupService(db).CreateAsync(target);

        File.Delete(BackupKeyring.PathFor());
        await SetProbeAsync(live, "تالف");
        Assert.True(BackupService.NeedsRecoveryKey(target));

        await using (var db = Open(live))
        {
            await Assert.ThrowsAsync<BackupRecoveryKeyRequiredException>(() => new BackupService(db).RestoreAsync(target));
            var wrong = await Assert.ThrowsAsync<AccountingException>(() => new BackupService(db).RestoreAsync(target, KeyB));
            Assert.IsNotType<BackupRecoveryKeyRequiredException>(wrong);
        }
        Assert.Equal("تالف", await ReadProbeAsync(live));   // ‏المفتاحُ الخاطئ لم يمسّ القاعدةَ الحيّة.

        await using (var db = Open(live))
            await new BackupService(db).RestoreAsync(target, KeyA);
        Assert.Equal("الأصل", await ReadProbeAsync(live));
    }

    // ‏نسخةٌ قديمةٌ صريحةٌ أُخذت قبل التشفير تُستعاد كما كانت — حتى وقد صار للتنصيب مفتاحُ نسخ.
    [Fact]
    public async Task AnOldUnencryptedBackup_StillRestores()
    {
        var live = Path.Combine(_root, "live.db");
        await SeedAsync(live, "قديم");
        var target = Backups("brqosh_20250101_000000.db");
        await using (var db = Open(live))
        {
            var result = await new BackupService(db).CreateAsync(target);
            Assert.False(result.Encrypted);
        }
        Assert.False(BackupCipher.IsEncrypted(target));

        BackupKeyring.OnRecoveryKeyIssued(KeyA);   // ‏التدويرُ لا يلمس الصريحة.
        Assert.False(BackupCipher.IsEncrypted(target));

        await SetProbeAsync(live, "جديد");
        await using (var db = Open(live))
            await new BackupService(db).RestoreAsync(target);
        Assert.Equal("قديم", await ReadProbeAsync(live));
    }

    private static BrqoshDbContext Open(string live) =>
        BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={live}");

    private static async Task SeedAsync(string live, string value)
    {
        await using var db = Open(live);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS enc_probe (v TEXT NOT NULL)");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM enc_probe");
        await db.Database.ExecuteSqlAsync($"INSERT INTO enc_probe (v) VALUES ({value})");
    }

    private static async Task SetProbeAsync(string live, string value)
    {
        await using var db = Open(live);
        await db.Database.ExecuteSqlAsync($"UPDATE enc_probe SET v = {value}");
        SqliteConnection.ClearAllPools();
    }

    private static async Task<string> ReadProbeAsync(string live)
    {
        await using var db = Open(live);
        var value = await db.Database.SqlQueryRaw<string>("SELECT v AS \"Value\" FROM enc_probe").SingleAsync();
        SqliteConnection.ClearAllPools();
        return value;
    }
}
