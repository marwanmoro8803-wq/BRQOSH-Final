using System.Text.RegularExpressions;
using Brqosh.Application.Intelligence;
using Brqosh.Desktop.Navigation;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · المساعد) <b>المساعدُ يعرف كلَّ شاشةٍ في القائمة — لا نحو ثلثيها.</b>
///
/// <para><b>ما كان:</b> فهرسُ المساعد ٧٠ شاشةً والقائمةُ ١٠٧: على ٣٧ شاشةً (الرواتب والعقارات والرعاية الصحية والضيافة واللوجستيات
/// وأعمار الديون والنسخ الاحتياطي…) كان «ماذا أفعل هنا؟» يعرض «أنت على الشاشة «<c>payslips</c>»» — مفتاحَ الكود نفسَه — بلا شرحٍ.
/// وكان الاختبارُ الوحيدُ يعدّ الفهرسَ <c>≥ 60</c> فلا يرى الفجوة.</para>
/// </summary>
public class TheAssistantKnowsEveryScreenTests
{
    public static IEnumerable<object[]> EveryNavigationKey() =>
        NavigationCatalog.Groups.SelectMany(g => g.Pages).Select(p => new object[] { p.Key }).DistinctBy(a => (string)a[0]);

    [Theory]
    [MemberData(nameof(EveryNavigationKey))]
    public void EveryScreenInTheMenuHasARealGuide(string key)
    {
        var guide = ProgramKnowledge.TryGetPageGuide(key);

        Assert.NotNull(guide);
        Assert.True(guide!.GuideAr.Length > 40, $"{key}: دليلٌ أقصرُ من أن يشرح شيئاً.");
        Assert.True(guide.GuideEn.Length > 40, $"{key}: الدليلُ الإنجليزيّ ناقص.");
        // ‏الدليلُ العامُّ المولَّد لشاشةٍ بلا دليلٍ خاصٍّ («أنت على «…»…») ليس دليلاً: يمرّ بالطول ولا يشرح شيئاً.
        Assert.False(guide.GuideAr.StartsWith("أنت على «", StringComparison.Ordinal), $"{key}: دليلٌ عامٌّ مولَّدٌ لا شرحَ فيه.");
        Assert.DoesNotContain(key, guide.GuideAr, StringComparison.Ordinal);
        Assert.NotEqual(key, guide.TitleAr);
        Assert.NotEqual(key, guide.TitleEn);
    }

    [Fact]
    public void TheAssistantCatalogHasNoScreenTheMenuLacks()
    {
        var menu = NavigationCatalog.Groups.SelectMany(g => g.Pages).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        var extra = ProgramNavCatalog.Keys.Where(k => !menu.Contains(k)).ToList();

        Assert.True(extra.Count == 0, "شاشاتٌ يعرفها المساعد وليست في القائمة: " + string.Join("، ", extra));
    }

    /// <summary>وسومٌ داخليّةٌ (رقمُ فجوة، «MVP»، معرّفُ دليلٍ تشغيليّ، اسمُ حقلٍ في الكود) لا تصل إلى المستخدم في كلام المساعد.</summary>
    private static readonly Regex Internal = new(
        @"\bG-\d+|\bMVP\b|in-repo|placeholder|\bO-\d{2}\b|BranchId|MustChangePassword|\bworkflow\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void NoGuideSpeaksInInternalTags()
    {
        var offenders = new List<string>();
        foreach (var key in ProgramNavCatalog.Keys)
        {
            var guide = ProgramKnowledge.TryGetPageGuide(key)!;
            foreach (var text in new[] { guide.GuideAr, guide.GuideEn })
            {
                var m = Internal.Match(text);
                if (m.Success)
                    offenders.Add($"{key}: «{m.Value}»");
            }
        }

        foreach (var fixedText in new[]
                 {
                     ProgramKnowledge.ZatcaGuide(), ProgramKnowledge.PostingGuide(), ProgramKnowledge.RbacGuide("مدير"),
                     ProgramKnowledge.ProgramHelp(), ProgramKnowledge.FallbackPage("x") is { } f
                         ? new FinancialAssistantReply(f.GuideAr, f.GuideEn, "x") : null!,
                 })
        {
            foreach (var text in new[] { fixedText.AnswerAr, fixedText.AnswerEn })
            {
                var m = Internal.Match(text);
                if (m.Success)
                    offenders.Add($"عام: «{m.Value}»");
            }
        }

        Assert.True(offenders.Count == 0, "وسومٌ داخليّة في كلام المساعد:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void AnUnknownScreenIsNeverNamedByItsCodeKey()
    {
        var fallback = ProgramKnowledge.FallbackPage("some_internal_key");

        Assert.DoesNotContain("some_internal_key", fallback.GuideAr, StringComparison.Ordinal);
        Assert.DoesNotContain("some_internal_key", fallback.GuideEn, StringComparison.Ordinal);
    }
}
