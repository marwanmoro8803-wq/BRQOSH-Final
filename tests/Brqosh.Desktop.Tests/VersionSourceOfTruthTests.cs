using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-231 · ط5-٥) <b>رقمُ الإصدار مصدرٌ واحد — ورقمان لشيءٍ واحدٍ يفترقان.</b>
///
/// <para><b>ما كان:</b> ثلاثةُ أرقامٍ غيرُ مرتبطة — <c>installer/brqosh.wxs</c> يُثبّت
/// <c>1.0.0.0</c> بيده، <b>ولا <c>&lt;Version&gt;</c> في أيّ مشروع</b> فتحمل التجميعاتُ
/// الافتراضيَّ، و<c>Program.cs</c> في الواجهة البرمجية يُثبّت <c>"v1"</c> ثالثاً.</para>
///
/// <para><b>وأخطرُ ما فيه ليس التناقض بل الترقية</b>: <c>MajorUpgrade</c> في MSI يعتمد
/// على رقم الحزمة. <b>فإصدارٌ يُشحن بلا تحرير ذلك السطر بيدٍ لا يُرقّي تثبيتاً قائماً</b> —
/// يُنصَّب جنبَه أو يُرفض، <b>ويبقى العميلُ على نسخةٍ فيها عطلٌ أُصلح</b>.</para>
///
/// <para><b>وفحصُ التحديث يقرأ إصدارَ التجميعة</b> — فتجميعةٌ بلا رقمٍ تجعل القناةَ
/// تقارن بـ<c>0.0.0</c> وتُعلن تحديثاً أبداً.</para>
/// </summary>
public class VersionSourceOfTruthTests
{
    /// <summary>
    /// <b>القياسُ المركزيّ: للتجميعة رقمُ إصدارٍ مُعلَن.</b>
    /// </summary>
    [Fact]
    public void TheAssembly_CarriesADeclaredVersion()
    {
        var version = typeof(Brqosh.Domain.Accounting.JournalEntry).Assembly.GetName().Version;

        Assert.NotNull(version);
        Assert.True(version!.Major > 0 || version.Minor > 0 || version.Build > 0,
            $"التجميعة بلا رقم إصدارٍ مُعلَن ({version}) — و«فحص التحديث» يقارن بصفرٍ فيُعلن تحديثاً أبداً.");
    }

    /// <summary>
    /// <b>وملفُّ المثبِّت لا يُثبّت رقماً بيده.</b>
    ///
    /// <para><b>ورقمٌ مكتوبٌ هنا يُنسى عند الإصدار</b> — والنسيانُ لا يُنتج خطأً يُرى،
    /// <b>بل مثبِّتاً لا يُرقّي</b>.</para>
    /// </summary>
    [Fact]
    public void TheInstaller_DoesNotHardcodeAVersion()
    {
        var root = FindRepoDirectory("installer");
        if (root is null) return;   // ‏المصادرُ غيرُ متاحةٍ في هذا التشغيل.

        var wxs = Path.Combine(root, "brqosh.wxs");
        if (!File.Exists(wxs)) return;

        var text = File.ReadAllText(wxs);
        var match = Regex.Match(text, @"Version\s*=\s*""([^""]+)""");

        Assert.True(match.Success, "لا سمةَ Version في ملفّ المثبِّت.");
        Assert.True(match.Groups[1].Value.Contains("$(var.", StringComparison.Ordinal),
            $"المثبِّت يُثبّت الرقم «{match.Groups[1].Value}» بيده — "
            + "وإصدارٌ يُشحن بلا تحريره لا يُرقّي تثبيتاً قائماً.");
    }

    /// <summary>
    /// <b>ومصدرُ الحقيقة موجودٌ ويحمل رقماً صالحاً.</b>
    ///
    /// <para><b>وحارسٌ للحارسين</b>: لو حُذف <c>Directory.Build.props</c> لعادت التجميعاتُ
    /// إلى الافتراضيّ <b>ومرّ الأوّلُ صامتاً</b> — لأنّ الافتراضيَّ <c>1.0.0.0</c> يجتاز
    /// فحصَ «أكبر من صفر».</para>
    /// </summary>
    [Fact]
    public void TheSingleSource_ExistsAndParses()
    {
        var root = FindRepoDirectory("installer");
        if (root is null) return;

        var props = Path.Combine(Path.GetDirectoryName(root)!, "Directory.Build.props");
        Assert.True(File.Exists(props),
            "Directory.Build.props مفقود — ولا مصدرَ حقيقةٍ واحداً لرقم الإصدار.");

        var match = Regex.Match(File.ReadAllText(props), @"<BrqoshVersion>([^<]+)</BrqoshVersion>");
        Assert.True(match.Success, "BrqoshVersion غير معرَّف في Directory.Build.props.");

        Assert.True(System.Version.TryParse(match.Groups[1].Value, out _),
            $"رقمُ إصدارٍ غيرُ صالح في المصدر: «{match.Groups[1].Value}».");
    }

    private static string? FindRepoDirectory(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}
