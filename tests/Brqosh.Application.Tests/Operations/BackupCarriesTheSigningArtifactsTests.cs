using System.IO.Compression;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٥) <b>النسخةُ الاحتياطيّة كانت القاعدةَ وحدَها.</b>
///
/// <para><b>البند:</b> أرشيفُ فواتير ZATCA ومفتاحُ التوقيع وشهادتُه <b>ملفّاتٌ على القرص
/// لا صفوفٌ في جدول</b>. فاستعادةٌ كاملةُ النجاح كانت تُنتج نظاماً <b>لا يستطيع التوقيع
/// ولا إعادةَ إرسال المعلّق</b>، وقد فقد <b>الأرشيفَ الذي يُلزِم به النظام</b>.</para>
///
/// <para><b>والمفتاحُ الخاصُّ لا يُولَّد من جديد:</b> ضياعُه ليس عطلاً يُصلَح بل
/// <b>تسجيلٌ جديدٌ لدى الهيئة</b> — وما بين الضياع والتسجيل لا تُصدَر فاتورةٌ واحدة.</para>
///
/// <para><b>ولم يكن يشكو شيء:</b> النسخةُ تُكتب وتُتحقَّق وتُنقل باسمها، والمُجدوِل يقول
/// «محميّة». <b>فالعطلُ في ما لا تحمله لا في ما تحمله.</b></para>
/// </summary>
public sealed class BackupCarriesTheSigningArtifactsTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "brqosh-companion-" + Guid.NewGuid().ToString("N")[..8]);

    private static string ZatcaRoot => Path.Combine(BrqoshDataRoot.Path(), "zatca");

    public BackupCarriesTheSigningArtifactsTests()
    {
        Directory.CreateDirectory(_folder);
        if (Directory.Exists(ZatcaRoot)) Directory.Delete(ZatcaRoot, recursive: true);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
        try { if (Directory.Exists(ZatcaRoot)) Directory.Delete(ZatcaRoot, recursive: true); }
        catch (IOException) { /* ‏كذلك */ }
        GC.SuppressFinalize(this);
    }

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> النسخةُ تحمل المفتاحَ والشهادةَ وملفَّ الفاتورة.
    /// </summary>
    [Fact]
    public async Task ABackup_CarriesTheSigningKeyAndTheInvoiceArchive()
    {
        PlantSigningArtifacts();

        var result = await TakeABackupAsync("brqosh_2026-06-01_1200.db");
        var companion = result.Path + BackupService.CompanionSuffix;

        Assert.True(File.Exists(companion),
            "‏النسخةُ خرجت بلا رفيق — فالمفتاحُ والأرشيفُ خارجَ الحماية.");

        using var zip = ZipFile.OpenRead(companion);
        var entries = zip.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("7/csid/private.key", entries);
        Assert.Contains("7/csid/csid.crt.pem", entries);
        Assert.Contains("7/INV-001.xml", entries);
    }

    /// <summary>
    /// <b>والاستعادةُ تُعيدها فعلاً</b> — لا أن يكون الرفيقُ ملفّاً يُكتب ولا يُقرأ.
    /// فحُذفت الشجرةُ كلُّها بعد النسخ، ثمّ استُعيدت.
    /// </summary>
    [Fact]
    public async Task ARestore_BringsTheSigningArtifactsBack()
    {
        PlantSigningArtifacts();
        var result = await TakeABackupAsync("brqosh_2026-06-02_1200.db");

        Directory.Delete(ZatcaRoot, recursive: true);
        Assert.False(Directory.Exists(ZatcaRoot));

        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={Path.Combine(_folder, "live.db")}");
        await new BackupService(db).RestoreAsync(result.Path);

        Assert.Equal("KEY", File.ReadAllText(Path.Combine(ZatcaRoot, "7", "csid", "private.key")));
        Assert.Equal("<Invoice/>", File.ReadAllText(Path.Combine(ZatcaRoot, "7", "INV-001.xml")));
    }

    /// <summary>
    /// <b>والرفيقُ يُحذف مع نسختِه.</b> وإلّا نما بلا حدٍّ على القرص نفسِه الذي حُدّت
    /// النسخُ من أجله — <b>وبقي يدّعي نسخةً لم تعد موجودة</b>.
    /// </summary>
    [Fact]
    public async Task PruningABackup_RemovesItsCompanionToo()
    {
        PlantSigningArtifacts();

        // ‏نسخةٌ زائدةٌ عن الحدّ، ولها رفيقُها — فالمُنتظَرُ أن يذهبا معاً.
        var doomed = Path.Combine(_folder, "brqosh_2020-01-01_0000.db");
        File.WriteAllBytes(doomed, new byte[1024]);
        File.WriteAllBytes(doomed + BackupService.CompanionSuffix, new byte[512]);
        File.SetLastWriteTimeUtc(doomed, new DateTime(2020, 1, 1));

        for (var i = 0; i < BackupService.LocalBackupsKept; i++)
        {
            var filler = Path.Combine(_folder, $"brqosh_2025-01-{i + 1:D2}_0000.db");
            File.WriteAllBytes(filler, new byte[1024]);
            File.SetLastWriteTimeUtc(filler, new DateTime(2025, 1, i + 1));
        }

        await TakeABackupAsync("brqosh_2026-06-03_1200.db");

        Assert.False(File.Exists(doomed), "‏النسخةُ الزائدةُ لم تُحذف — فالتهيئةُ نفسُها فاسدة.");
        Assert.False(File.Exists(doomed + BackupService.CompanionSuffix),
            "‏حُذفت النسخةُ وبقي رفيقُها يتيماً على القرص.");
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبط:</b> منشأةٌ لم تُسجَّل بعدُ لدى الهيئة — لا مفاتيحَ ولا أرشيف.
    /// فالنسخةُ تنجح <b>ولا يُكتب رفيقٌ فارغٌ يدّعي محتوًى</b>.
    /// </summary>
    [Fact]
    public async Task WithNoZatcaArtifacts_TheBackupStillSucceeds_AndWritesNoCompanion()
    {
        var result = await TakeABackupAsync("brqosh_2026-06-04_1200.db");

        Assert.True(File.Exists(result.Path));
        Assert.False(File.Exists(result.Path + BackupService.CompanionSuffix));
    }

    /// <summary>
    /// <b>وجذرُ البيانات يُبنى في موضعٍ واحد.</b>
    ///
    /// <para><c>ZatcaCsidOnboardingService</c> كان يركّب <c>%APPDATA%\BRQOSH</c> بيده،
    /// <b>فلا يرى <c>BRQOSH_DATA_ROOT</c></b> — بينما أرشيفُ XML يستعمل الجذر. فمفتاحُ
    /// التوقيع وأرشيفُه في جذرين متى وُجِّه الجذر، <b>ونسخةٌ تمسح جذراً واحداً تفقد
    /// الآخر</b>. وهو أيضاً ما جعل الاختباراتِ تكتب مفاتيحَ في ملفّ المستخدم الحقيقيّ —
    /// وهو بعينه ما وُجد <c>TestDataRootInitializer</c> (CEEF-202) لمنعه.</para>
    ///
    /// <para><b>وحارسٌ بنيويٌّ لا فحصُ قيمة</b>: كلُّ موضعٍ جديدٍ يركّب الجذرَ بيده يُسقط
    /// هذه الحالة.</para>
    /// </summary>
    [Fact]
    public void TheDataRoot_IsBuiltInExactlyOnePlace_InsideInfrastructure()
    {
        var infrastructure = Path.Combine(RepositoryRoot(), "src", "Brqosh.Infrastructure");

        var offenders = Directory
            .GetFiles(infrastructure, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains(
                "SpecialFolder.ApplicationData", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f)
            .ToList();

        // ‏شاهدُ ضبطٍ موجب: التقنيةُ تجد الموضعَ المشروعَ حيث هو — فالصفرُ أدناه نفيٌ مقيس.
        Assert.Contains("BrqoshDataRoot.cs", offenders);

        Assert.Equal(new[] { "BrqoshDataRoot.cs" }, offenders);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <summary>يزرع شجرةَ ZATCA كما يكتبها المنتج: مفتاحٌ وشهادةٌ تحت الشركة، وفاتورةٌ بجوارهما.</summary>
    private static void PlantSigningArtifacts()
    {
        var csid = Path.Combine(ZatcaRoot, "7", "csid");
        Directory.CreateDirectory(csid);
        File.WriteAllText(Path.Combine(csid, "private.key"), "KEY");
        File.WriteAllText(Path.Combine(csid, "csid.crt.pem"), "CERT");
        File.WriteAllText(Path.Combine(ZatcaRoot, "7", "INV-001.xml"), "<Invoice/>");
    }

    private async Task<BackupService.BackupResult> TakeABackupAsync(string fileName)
    {
        var live = Path.Combine(_folder, "live.db");
        if (!File.Exists(live))
        {
            await using var seed = BrqoshDbContextFactory.Create(
                DatabaseProvider.Sqlite, $"Data Source={live}");
            await seed.Database.EnsureCreatedAsync();
        }

        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={live}");

        return await new BackupService(db).CreateAsync(Path.Combine(_folder, fileName));
    }

    /// <summary>جذرُ المستودع — يُصعَد من مجلّد التشغيل حتى يُرى <c>src</c>.</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new DirectoryNotFoundException("‏لم يُعثر على جذر المستودع من مجلّد التشغيل.");
    }
}
