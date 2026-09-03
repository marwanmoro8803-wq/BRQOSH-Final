using System.IO;
using System.Text.RegularExpressions;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-232 · و-١) <b>الإرشادُ يقول ما تفرضه السياسة — لا رقماً كُتب بجانبها.</b>
///
/// <para><b>ما كان:</b> شاشةُ التهيئة وشاشةُ تغيير كلمة المرور تقولان
/// <b>«8 أحرف على الأقل»</b>، و<c>PasswordPolicy.MinLength</c> <b>اثنتا عشرة</b>.
/// <b>فيُرفض المستخدمُ بشرطٍ لم يُقَل له</b>: يكتب ثمانيةً كما أُمر فتردّه الشاشة برسالةٍ
/// تطلب اثنتي عشرة — <b>ولا يفهم أيَّهما يُصدّق</b>.</para>
///
/// <para><b>ولم يُخرجه اختبارٌ ولا مراجعةُ شيفرة</b>: كلا النصّين صحيحٌ نحوياً، والحارسُ
/// الوحيدُ الذي كان يقرؤهما هو حارسُ الترجمة — <b>وهو يسأل «هل تُرجم؟» لا «هل يصدق؟»</b>.
/// <b>أخرجه تشغيلٌ حيٌّ للمعالج</b>: مُلئت كلمةُ مرورٍ من تسعة أحرف كما يسمح النصُّ المكتوب،
/// <b>فردّتها الشاشة</b>.</para>
///
/// <para><b>وشاشةُ تغيير كلمة المرور يمرّ بها كلُّ مستخدمٍ في أوّل دخول</b> — فهي أوّلُ
/// احتكاكٍ بالنظام، وأوّلُ تناقضٍ يراه.</para>
///
/// <para><b>والحارسُ هنا بنيويّ لا نصّيّ:</b> يرفض أيَّ رقمٍ في نصوص الواجهة يدّعي حدّاً
/// أدنى لكلمة المرور ويخالف الثابت. <b>فيسقط يومَ تُرفع السياسةُ ولا يُرفع النصّ</b>.</para>
/// </summary>
public class PasswordPolicyHintTests
{
    /// <summary>
    /// <b>القياسُ الحاكم: لا نصَّ واجهةٍ يعِد بحدٍّ أدنى غيرِ المفروض.</b>
    /// </summary>
    [Fact]
    public void NoDesktopText_PromisesAPasswordLength_OtherThanThePolicy()
    {
        var sources = DesktopText();
        if (sources.Count == 0) return;   // ‏المصادرُ غيرُ متاحةٍ في هذا التشغيل.

        // ‏«<عدد> أحرف/حرفاً على الأقل» — وهي الصيغةُ التي يقرؤها المستخدم.
        var claim = new Regex(@"(\d{1,2})\s*(?:أحرف|حرفاً|حرفًا)\s*على\s*الأقل");

        // ‏**والقيدُ على سياق كلمة المرور وحدَه** — فللمستخدم قواعدُ طولٍ أخرى مشروعة:
        // ‏اسمُ المستخدم ثلاثةُ أحرف، واسمُ المنشأة كذلك. **وحارسٌ يخلطها يصرخ بالباطل
        // ‏فيُدرَّب على تجاهله** — وهو أسوأ من غيابه.
        var offenders = new List<string>();
        foreach (var (file, text) in sources)
        {
            foreach (var line in text.Split('\n'))
            {
                var isPasswordContext = line.Contains("مرور", StringComparison.Ordinal)
                                        || line.Contains("PolicyHintText", StringComparison.Ordinal);
                if (!isPasswordContext) continue;

                foreach (Match m in claim.Matches(line))
                {
                    if (int.TryParse(m.Groups[1].Value, out var stated)
                        && stated != PasswordPolicy.MinLength)
                    {
                        offenders.Add($"{Path.GetFileName(file)} ⟵ «{m.Value}»");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0,
            $"نصوصٌ تعِد بحدٍّ أدنى يخالف السياسة ({PasswordPolicy.MinLength}) — "
            + "فيُرفض المستخدمُ بشرطٍ لم يُقَل له:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// <b>وحارسٌ للحارس: الرقمُ يُشتقّ من الثابت فعلاً.</b>
    ///
    /// <para><b>وبغيره يمرّ الأوّلُ بكتابة «12» حرفاً في النصّ</b> — فيصحّ اليومَ
    /// <b>ويكذب يومَ تُرفع السياسةُ إلى أربعةَ عشر</b>، وهو العطلُ نفسُه مؤجَّلاً.</para>
    /// </summary>
    [Fact]
    public void TheHint_IsDerivedFromTheConstant_NotTyped()
    {
        var sources = DesktopText();
        if (sources.Count == 0) return;

        var derived = sources
            .Where(s => s.Key.EndsWith("SetupWizardWindow.xaml.cs", StringComparison.Ordinal)
                        || s.Key.EndsWith("PasswordChangeWindow.xaml.cs", StringComparison.Ordinal))
            .ToList();

        Assert.True(derived.Count == 2,
            "لم تُعثر الشاشتان اللتان تعرضان إرشادَ كلمة المرور.");

        foreach (var (file, text) in derived)
        {
            Assert.True(text.Contains("PasswordPolicy.MinLength", StringComparison.Ordinal),
                $"{Path.GetFileName(file)} لا يشتقّ الإرشادَ من `PasswordPolicy.MinLength` — "
                + "فرقمٌ مكتوبٌ يصحّ اليومَ ويكذب عند أوّل تشديدٍ للسياسة.");
        }
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <summary>نصوصُ الواجهة — <c>.cs</c> و<c>.xaml</c> معاً، بلا <c>bin</c> و<c>obj</c>.</summary>
    private static Dictionary<string, string> DesktopText()
    {
        var root = FindRepoDirectory("src/Brqosh.Desktop");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root is null) return result;

        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".cs", StringComparison.Ordinal)
                && !file.EndsWith(".xaml", StringComparison.Ordinal))
                continue;

            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            // ‏**والسجلُّ المولَّد يُستثنى**: يحمل نصوصاً تاريخيةً للترجمة لا يراها مستخدم.
            if (file.EndsWith("UiBilingualRegistry.g.cs", StringComparison.Ordinal))
                continue;

            result[file] = File.ReadAllText(file);
        }

        return result;
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
