using System.IO.Compression;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (م-٥ · ن-٤٢) <b>حزمةُ الدعم — ملفٌّ واحدٌ يُرسَل بدل مجلّدٍ يُفتَح.</b>
///
/// <para><b>وحدُّ هذه الاختبارات مكتوبٌ فيها:</b> تحرس <b>البناءَ</b> — ما الذي يدخل
/// الأرشيف وبأيّ اسم — <b>ولا تحرس الزرَّ ولا حوارَ الحفظ</b>. ولذلك وُضع البناءُ في
/// <c>Brqosh.Infrastructure</c> لا في الـcode-behind: <b>ما يُكتب في شاشةٍ لا يُقاس إلّا
/// بتشغيل الشاشة</b>.
/// </para>
/// </summary>
public sealed class SupportBundleTests : IDisposable
{
    private readonly string _work =
        Path.Combine(Path.GetTempPath(), "n42-" + Guid.NewGuid().ToString("N"));

    public SupportBundleTests() => Directory.CreateDirectory(_work);

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
        GC.SuppressFinalize(this);
    }

    private string Logs(params (string Name, string Body)[] files)
    {
        var folder = Path.Combine(_work, "logs");
        Directory.CreateDirectory(folder);
        foreach (var (name, body) in files)
            File.WriteAllText(Path.Combine(folder, name), body);
        return folder;
    }

    private static List<string> EntriesOf(string zip)
    {
        using var archive = ZipFile.OpenRead(zip);
        return archive.Entries.Select(entry => entry.FullName).OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void ItCarriesTheReportAndEveryLogFile()
    {
        var logs = Logs(
            ("crash-2026-09-14_101112.000__Posting__7.log", "‏مكدّسُ العطل"),
            ("schema-2026-09-14_101113.log", "‏خطوةُ ترقيةٍ ابتُلع فشلُها"));

        var zip = Path.Combine(_work, "bundle.zip");
        var (added, unreadable) = SupportBundle.Create(
            zip, "BRQOSH — حالة النظام\nإصدار البرنامج: 1.2.3", logs);

        Assert.Equal(2, added);
        // ‏**وما تعذّر يُعدّ ويُعاد** — فحزمةٌ ناقصةٌ تقول ما ينقصها، ولا تُبتلع صامتةً.
        Assert.Equal(0, unreadable);
        Assert.True(File.Exists(zip));

        var entries = EntriesOf(zip);
        Assert.Contains(SupportBundle.ReportEntryName, entries);
        Assert.Contains("logs/crash-2026-09-14_101112.000__Posting__7.log", entries);
        // ‏**وتقريرُ المخطّط يُضمّ أيضاً** — وهو نصفُ الدليل في عطل ترقية (ن-٤٣).
        Assert.Contains("logs/schema-2026-09-14_101113.log", entries);
    }

    /// <summary>
    /// <b>والتقريرُ يُقرأ كما كُتب</b> — فحزمةٌ تصل بنصٍّ مُشوَّهٍ لا تُغني عن مكالمة.
    /// </summary>
    [Fact]
    public void TheReportSurvivesTheRoundTrip()
    {
        const string report = "BRQOSH — حالة النظام\nمحرك قاعدة البيانات: PostgreSQL\nآخر عطل مسجّل: لا شيء";
        var zip = Path.Combine(_work, "bundle.zip");

        SupportBundle.Create(zip, report, Logs());

        using var archive = ZipFile.OpenRead(zip);
        using var reader = new StreamReader(archive.GetEntry(SupportBundle.ReportEntryName)!.Open());
        Assert.Equal(report, reader.ReadToEnd());
    }

    /// <summary>
    /// <b>وأرشيفٌ سابقٌ يُستبدَل لا يُضاف إليه</b> — وإلّا قرأ الفنّيُّ سجلَّ مكالمةٍ ماضية
    /// وظنّه الحاليّ، <b>وهو أسوأُ من ألّا يصل شيء</b>.
    /// </summary>
    [Fact]
    public void ASecondBundle_ReplacesTheFirst()
    {
        var zip = Path.Combine(_work, "bundle.zip");

        SupportBundle.Create(zip, "أوّل", Logs(("crash-old.log", "قديم")));
        Assert.Contains("logs/crash-old.log", EntriesOf(zip));

        var fresh = Path.Combine(_work, "logs2");
        Directory.CreateDirectory(fresh);
        File.WriteAllText(Path.Combine(fresh, "crash-new.log"), "جديد");

        SupportBundle.Create(zip, "ثانٍ", fresh);

        var entries = EntriesOf(zip);
        Assert.Contains("logs/crash-new.log", entries);
        Assert.DoesNotContain("logs/crash-old.log", entries);
    }

    /// <summary>
    /// <b>ومجلّدُ سجلّاتٍ غائبٌ لا يُسقط الحزمة</b> — فتقريرُ الحالة وحدَه أنفعُ من لا شيء،
    /// وأوّلُ تركيبٍ نظيفٍ لا سجلّاتِ فيه أصلاً.
    /// </summary>
    [Fact]
    public void AMissingLogFolder_StillProducesAReport()
    {
        var zip = Path.Combine(_work, "bundle.zip");

        var (added, _) = SupportBundle.Create(zip, "تقرير", Path.Combine(_work, "does-not-exist"));

        Assert.Equal(0, added);
        Assert.Equal([SupportBundle.ReportEntryName], EntriesOf(zip));
    }

    /// <summary>
    /// <b>والاسمُ المقترحُ يُفرّق مكالمةً عن مكالمة</b> — يحمل الجهازَ والختم.
    /// </summary>
    [Fact]
    public void TheSuggestedName_CarriesMachineAndStamp()
    {
        var name = SupportBundle.SuggestedFileName(new DateTime(2026, 9, 14, 10, 11, 0, DateTimeKind.Local));

        Assert.StartsWith("brqosh-support-", name, StringComparison.Ordinal);
        Assert.EndsWith(".zip", name, StringComparison.Ordinal);
        Assert.Contains(Environment.MachineName, name, StringComparison.Ordinal);
        Assert.Contains("2026-09-14_1011", name, StringComparison.Ordinal);
    }
}
