using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٢٦) <b>رصيدٌ يُشتقّ من سطور القيود لا يُرشِّح <c>Posted</c> وحدَه.</b>
///
/// <para><c>JournalEntry.CreateReversal</c> <b>يضع الأصلَ في حالة <c>Reversed</c></b>
/// ويُرحّل العكسيَّ <c>Posted</c>. <b>فمُرشِّحُ <c>Posted</c> وحدَه يُخرج الأصلَ ويُبقي
/// عكسَه</b> — والرصيدُ يصير <b>مقلوبَ القيد الملغى</b>، لا صفراً ولا صحيحاً.</para>
///
/// <para><b>والمستودعُ يعرف هذا منذ <c>ACC-001</c></b>: <c>ReportingJournalEntries.WhereReportable</c>
/// تضمّ الحالتين، وتعليقُها يقول «استبعادُ المعكوسة كان يشوّه التقاريرَ التاريخية بعد
/// الإلغاء لاحقاً». <b>وثمانيةُ قُرّاءٍ يستعملونها — وستّةَ عشرَ موضعاً لم تكن منهم</b>،
/// أخطرُها <c>FxRevaluationService</c> الذي <b>يُقيّد ربحَ صرفٍ من عدم</b> (ح-٢٥).</para>
///
/// <para><b>وهذه البوّابةُ تمنع السادسَ عشرَ أن يعود</b>: كلُّ استعلامٍ على
/// <c>JournalEntries</c> يُرشِّح <c>Posted</c> <b>يجب أن يضمّ <c>Reversed</c> معه</b>،
/// أو يكون <b>مُقَرّاً به بسببٍ مكتوب</b>.</para>
///
/// <para><b>وثلاثةٌ مُقَرٌّ بها — ولكلٍّ سببُه</b>: مواضعُ <b>لا تشتقّ رصيداً</b> بل
/// تسأل «أوُجد قيدٌ قائمٌ لهذا المستند؟». <b>وضمُّ المعكوسة إليها يقلب معناها</b>:
/// مستندٌ عُكس قيدُه يجب أن يُرحَّل ثانيةً، لا أن يُعَدّ مُرحَّلاً.</para>
/// </summary>
public class ReversedEntriesStayInBalancesTests
{
    /// <summary>ترشيحٌ على <c>Posted</c> دون ضمِّ <c>Reversed</c> في السطر نفسِه.</summary>
    private static readonly Regex PostedOnly = new(
        @"e\.Status\s*==\s*JournalEntryStatus\.Posted(?!\s*\|\|)",
        RegexOptions.Compiled);

    /// <summary>
    /// <b>مواضعُ لا تشتقّ رصيداً — ولكلٍّ سببُه المكتوب.</b>
    ///
    /// <para><b>وإضافةُ سطرٍ هنا إقرارٌ بأنّ الموضعَ يسأل عن وجودِ قيدٍ لا عن مبلغ</b>،
    /// لا تهرّبٌ من فشل.</para>
    /// </summary>
    private static readonly Dictionary<string, string> Declared = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WorkOrderService.cs"] =
            "‏بحثٌ عن قيدِ أمرِ الإنتاج نفسِه لمنع الترحيل مرّتين — لا اشتقاقَ رصيد. "
            + "وضمُّ المعكوس يجعل أمراً عُكس قيدُه يبدو مُرحَّلاً فلا يُعاد ترحيلُه.",
        ["PosShiftVariancePostingService.cs"] =
            "‏حارسُ تكرارٍ لقيد فرق الوردية — سؤالٌ عن وجودٍ لا عن مبلغ. "
            + "وضمُّ المعكوس يمنع إعادةَ ترحيلِ ورديةٍ عُكس قيدُها.",
        // ‏(جاهزيّةُ الإطلاق · ح-٣٨ ⟵ ح-٤٧) **وضمُّ المعكوس هنا يُعيد العطلَ بعينه.**
        // ‏كان الإقرارُ على `MonetaryRevaluationService` — **ثمّ صار لسؤال «القائم» مالكٌ واحد**
        // ‏يستعمله هو و`VatReturnQuery` والإقفالُ السنويّ، فانتقل الإقرارُ إلى المالك.
        ["StandingJournalEntries.cs"] =
            "‏مالكُ سؤال «القائم»: مرحّلٌ لا يُسمّيه قيدٌ عكساً له — **سؤالُ وجودٍ** لإعادة التقييم "
            + "والإقفال السنويّ، **ويُسقط طرفَي زوج العكس معاً** في الإقرار الضريبيّ فلا يقلب رصيداً "
            + "كما يقلبه مُرشِّحُ Posted وحدَه (الذي يُبقي العكس). وضمُّ المعكوس يجعل إقفالاً أو تقييماً "
            + "أُلغي يبدو قائماً **فيُمنع من إعادته أبداً** — الحلقةُ المغلقةُ في ح-٣٨ وح-٤٧ بعينها.",
        ["DataQualityService.cs"] =
            "‏عدُّ القيود غير المتوازنة بين المُرحَّل القائم — فحصُ جودةٍ على ما هو نافذٌ "
            + "اليوم، لا رصيدٌ تاريخيّ يُشتقّ من السطور.",
    };

    private static string InfrastructureRoot()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Infrastructure"));
        Assert.True(Directory.Exists(root), $"Infrastructure root not found: {root}");
        return root;
    }

    private static Dictionary<string, int> Scan()
    {
        var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(InfrastructureRoot(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            // ‏يُسقَط التعليقُ أوّلاً — **فشرحُ النمط ليس وقوعاً له**، وهذا الدرسُ كلّفني
            // ‏ثلاث مرّاتٍ في هذه الحملة (ح-٢٢ · ح-٢٥ · وهذه).
            var text = Regex.Replace(File.ReadAllText(file), @"^\s*//.*$", "", RegexOptions.Multiline);
            var count = PostedOnly.Matches(text).Count;
            if (count > 0) found[Path.GetFileName(file)] = count;
        }

        return found;
    }

    // ── البوّابة ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>لا موضعَ جديدٌ يُخرج المعكوسةَ من رصيد.</b>
    /// </summary>
    [Fact]
    public void NoBalanceQuery_ExcludesReversedEntries()
    {
        var offenders = Scan().Keys
            .Where(f => !Declared.ContainsKey(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(offenders.Count == 0,
            "استعلامٌ يُرشِّح Posted وحدَه: القيدُ المعكوسُ يخرج **ويبقى عكسُه**، "
            + "فالرصيدُ مقلوبُ القيد الملغى. استعمل WhereReportable أو اضمم Reversed:\n  "
            + string.Join("\n  ", offenders));
    }

    // ── حارسا الحارس ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>والمسحُ يرى الشكلَ فعلاً</b> — الثلاثةُ المُقَرُّ بها <b>موجودةٌ ويُعثر عليها</b>.
    /// <b>وصفرُ نتائجَ يعني انكسارَ الاستخراج لا نظافةَ المستودع.</b>
    /// </summary>
    [Fact]
    public void TheScan_StillSeesTheDeclaredSites()
    {
        var found = Scan();

        foreach (var declared in Declared.Keys)
            Assert.True(found.ContainsKey(declared),
                $"‏{declared} مُقَرٌّ به ولم يعثر عليه المسحُ — انكسر الاستخراج، "
                + "أو نُظِّف الموضعُ ولم يُحدَّث الإقرار.");
    }

    /// <summary>
    /// <b>وصيغةُ العلاج يراها المسحُ علاجاً.</b>
    ///
    /// <para><c>FxRevaluationService</c> عولج في ح-٢٥ و<c>BankReconciliationQuery</c>
    /// في ح-٢٦ — <b>فغيابُهما عن النتائج هو الدليلُ الحيُّ</b> على أنّ البوّابةَ تُفرّق
    /// بين المُرشِّح الناقص والتامّ، <b>لا أنّها لا ترى شيئاً</b>.</para>
    /// </summary>
    [Fact]
    public void TheCorrectedSites_AreNotReported()
    {
        var found = Scan();

        Assert.DoesNotContain("FxRevaluationService.cs", found.Keys);
        Assert.DoesNotContain("BankReconciliationQuery.cs", found.Keys);
        Assert.DoesNotContain("SubcontractSupplierRetentionQuery.cs", found.Keys);
    }

    /// <summary>
    /// <b>وكلُّ إقرارٍ يحمل سبباً يصمد</b> — وسطرٌ يُكتب لتسكيت الحارس هو العطلُ نفسُه.
    /// </summary>
    [Fact]
    public void EveryDeclaration_CarriesAWrittenReason()
    {
        var weak = Declared
            .Where(d => d.Value.Trim().Length < 60
                        || d.Value.Contains("TODO", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Key)
            .ToList();

        Assert.True(weak.Count == 0,
            "إقراراتٌ بلا سببٍ يصمد:\n  " + string.Join("\n  ", weak));
    }
}
