using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (CEEF-229 · م2-١٠ / D-6) <b>كودٌ محجوزٌ لصفٍّ محذوف — ولا أحدَ يقول لماذا.</b>
///
/// <para>ثمانيةُ كيانات تجمع <b>الحذفَ الناعم</b> مع <b>فهرسٍ فريد على الكود</b>. والصفُّ
/// المحذوف يبقى في الجدول ويحجز كودَه إلى الأبد، <b>ولا يظهر في أيّ شاشة</b> — فيصطدم
/// الحفظُ بانتهاك تفرّدٍ من قاعدة البيانات ويُعرض نصُّ <c>DbUpdateException</c>
/// <b>الإنجليزيّ</b> كما هو داخل واجهةٍ عربية.</para>
///
/// <para><b>والفهرسُ لم يُجزَّأ عمداً</b> — قرارٌ مكتوبٌ في <c>م2-٢٧</c>: كودان متطابقان
/// أحدهما محذوف يجعلان سجلَّ الحركة القديم <b>ملتبساً بين صنفَين</b>، والالتباسُ في الدفاتر
/// أسوأ من رفضٍ مفهوم. <b>فالعلاجُ رسالةٌ تشرح، لا فهرسٌ يتساهل.</b></para>
///
/// <para><b>وما كان ناقصاً:</b> الشرحُ بُني للأصناف وحدها. والسبعةُ الباقية تُخرج نصَّ
/// المحرّك الإنجليزيّ — <b>فالعطلُ واحدٌ والعلاجُ لواحد</b>.</para>
///
/// <para>وهذا حارسُ نصٍّ لا سلوك: يُثبت أنّ لكلّ كيانٍ من الثمانية فحصَ إتاحةٍ يُفسّر
/// الرفض. <b>وسلوكُ الفحص نفسِه مقيسٌ في <c>CodeAvailabilityTests</c>.</b></para>
/// </summary>
public class DeletedCodeExplanationTests
{
    /// <summary>الكياناتُ التي تجمع الحذفَ الناعم مع فهرسٍ فريد على الكود.</summary>
    private static readonly string[] Entities =
    [
        "Product", "Account", "Warehouse", "BankAccount",
        "CostCenter", "ConstructionProject", "BillOfMaterials", "AppUser",
    ];

    [Fact]
    public void EverySoftDeletableCodedEntity_HasAnAvailabilityExplanation()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "src", "Brqosh.Infrastructure", "Platform", "CodeAvailabilityQuery.cs"));

        var missing = Entities
            .Where(e => !source.Contains($"CodeAvailabilityScope.{e}", StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0,
            "‏كياناتٌ يحجز صفُّها المحذوف كودَه بلا شرح — فيرى المالكُ نصَّ المحرّك الإنجليزيّ:\n  "
            + string.Join("\n  ", missing));
    }

    /// <summary>
    /// <b>وحارسٌ للوصلة.</b> فحصٌ لا يستدعيه أحدٌ لا يشرح شيئاً — وهو بعينه ما وقع في
    /// <c>م1-٢٢</c> حيث كانت الشاشةُ تتجاوز الخدمةَ الحارسة.
    ///
    /// <para><b>ويقيس تغطيةَ الكيانات لا عددَ الملفّات</b>: ملفّان يفحصان الصنفَ نفسَه
    /// لا يساويان ملفّين يفحصان كيانين. <b>والسقفُ مِرصادٌ متصاعد</b> — يرتفع كلّما وُصل
    /// كيانٌ جديد، ولا ينزل.</para>
    /// </summary>
    [Fact]
    public void AtLeastFourEntities_AreActuallyCheckedBeforeSaving()
    {
        const int Wired = 4;

        var root = RepositoryRoot();
        var covered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.EndsWith("CodeAvailabilityQuery.cs", StringComparison.Ordinal))
                continue;

            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"CodeAvailabilityScope\.(\w+)"))
                covered.Add(m.Groups[1].Value);
        }

        Assert.True(covered.Count >= Wired,
            $"‏فحصُ إتاحة الكود موصولٌ بـ{covered.Count} كياناتٍ فقط ({string.Join("، ", covered.OrderBy(c => c))}) — "
            + "فبقيّةُ الشاشات تترك المحرّكَ يتحدّث بلغته.");
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
