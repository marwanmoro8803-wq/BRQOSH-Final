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
    /// <b>ومصدرُ الحقيقة موجودٌ ويحمل رقماً صالحاً — ورقمُ البناء في الحقل الثالث.</b>
    ///
    /// <para><b>وحارسٌ للحارسين</b>: لو حُذف <c>Directory.Build.props</c> لعادت التجميعاتُ
    /// إلى الافتراضيّ <b>ومرّ الأوّلُ صامتاً</b> — لأنّ الافتراضيَّ <c>1.0.0.0</c> يجتاز
    /// فحصَ «أكبر من صفر».</para>
    ///
    /// <para>(م-١ · ن-٠١) <b>وحُدِّث هذا الحارسُ لأنّ العقدَ تطوّر — لا لأنّه أخطأ.</b>
    /// كان الرقمُ نصّاً حرفيّاً (<c>1.0.0</c>) فيُحلَّل مباشرة. وصار مركّباً
    /// (<c>$(BrqoshVersionPrefix).$(BrqoshBuildNumber)</c>) لأنّ رقماً مجمَّداً يجعل
    /// الترقيةَ مستحيلةً: <c>MajorUpgrade</c> لا يرى إصدارين متساويين.</para>
    ///
    /// <para><b>والحارسُ الآن أقوى لا أضعف</b>: يشترط أن يكون رقمُ البناء في <b>الحقل
    /// الثالث</b>. والسببُ ليس ذوقاً: <b>Windows Installer يقارن ثلاثةَ حقولٍ من
    /// <c>ProductVersion</c> ويُهمل الرابع</b> — فترقيمٌ يرفع الرابعَ وحدَه يبدو متغيّراً
    /// للعين وهو ثابتٌ عند المُثبِّت، <b>وتعود الترقيةُ إلى استحالتها بلا أن يشتكيَ شيء</b>.</para>
    /// </summary>
    [Fact]
    public void TheSingleSource_ExistsAndComposesABuildNumberInTheThirdField()
    {
        var root = FindRepoDirectory("installer");
        if (root is null) return;

        var props = Path.Combine(Path.GetDirectoryName(root)!, "Directory.Build.props");
        Assert.True(File.Exists(props),
            "Directory.Build.props مفقود — ولا مصدرَ حقيقةٍ واحداً لرقم الإصدار.");

        var text = File.ReadAllText(props);

        var prefix = Regex.Match(text, @"<BrqoshVersionPrefix>([^<]+)</BrqoshVersionPrefix>");
        Assert.True(prefix.Success, "BrqoshVersionPrefix غير معرَّف — ولا بادئةَ دلاليّةً للإصدار.");
        Assert.Matches(@"^\d+\.\d+$", prefix.Groups[1].Value);

        var build = Regex.Match(text, @"<BrqoshBuildNumber[^>]*>([^<]+)</BrqoshBuildNumber>");
        Assert.True(build.Success, "BrqoshBuildNumber غير معرَّف — فلا افتراضَ للبناء المحلّيّ.");

        var composed = Regex.Match(text, @"<BrqoshVersion>([^<]+)</BrqoshVersion>");
        Assert.True(composed.Success, "BrqoshVersion غير معرَّف في Directory.Build.props.");

        // ‏الشرطُ الجوهريّ: البادئةُ حقلان ورقمُ البناء ثالثُها — فالمقارنةُ عند MSI تراه.
        Assert.Equal("$(BrqoshVersionPrefix).$(BrqoshBuildNumber)", composed.Groups[1].Value.Trim());

        // ‏وأنّ التركيبَ يُنتج رقماً صالحاً فعلاً — لا مجرّد نصٍّ يبدو صحيحاً.
        // ‏(الإصدارُ يتحرّك مع كلّ تعديل) ‏رقمُ البناء يُقرأ من version.txt في الجذر — فيُقاس الرقمُ الفعليّ منه.
        var buildText = build.Groups[1].Value.Trim();
        if (buildText.Contains("version.txt", StringComparison.Ordinal))
            buildText = File.ReadAllText(Path.Combine(Path.GetDirectoryName(root)!, "version.txt")).Trim();
        var effective = $"{prefix.Groups[1].Value}.{buildText}";
        Assert.True(System.Version.TryParse(effective, out var parsed),
            $"التركيبُ لا يُنتج رقماً صالحاً: «{effective}».");
        Assert.True(parsed!.Build >= 0,
            "رقمُ البناء ليس في الحقل الثالث — وWindows Installer يُهمل الرابع، فالترقيةُ تعود مستحيلة.");
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
