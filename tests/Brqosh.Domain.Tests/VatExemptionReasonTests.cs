using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;

namespace Brqosh.Domain.Tests;

/// <summary>
/// (CEEF-229 · م1-١ب الشقّ الثاني) <b>سببُ الإعفاء — الشرطُ الذي كانت الشاشةُ تعرض بابَه
/// وتترك خلفه فراغاً.</b>
///
/// <para><c>BR-KSA-CL-06</c>: فئةٌ <c>Z</c> أو <c>E</c> أو <c>O</c> بلا رمز سبب إعفاء (BT-121)
/// من قائمة السعودية ⟵ ترفضها الهيئة. وشاشةُ المبيعات تعرض الفئات الأربع منذ اليوم الأول،
/// ولم يكن في المستودع كلِّه ذكرٌ لـ<c>TaxExemption</c>.</para>
/// </summary>
public class VatExemptionReasonTests
{
    private static SalesInvoice Draft() => SalesInvoice.CreateDraft(1, 1, new DateOnly(2026, 7, 9));

    /// <summary><b>معيارُ القبول المكتوب في الخطة:</b> سطرٌ بفئة <c>E</c> بلا سبب إعفاء ⟵ يرمي.</summary>
    [Fact]
    public void AnExemptLine_WithoutAReason_Throws()
    {
        var ex = Assert.Throws<AccountingException>(
            () => Draft().AddLine("إيجار سكني", 1m, 1_000m, null, VatCategory.Exempt));

        Assert.Contains("سبب إعفاء", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>والصفريّ مثلُه — والقاعدة تشمل <c>Z</c> و<c>E</c> و<c>O</c> جميعاً.</summary>
    [Theory]
    [InlineData(VatCategory.ZeroRated)]
    [InlineData(VatCategory.Exempt)]
    [InlineData(VatCategory.OutOfScope)]
    public void EveryNonStandardCategory_DemandsAReason(VatCategory category)
    {
        Assert.True(VatExemptionReason.IsRequiredFor(category));
        Assert.Throws<AccountingException>(
            () => Draft().AddLine("صنف", 1m, 100m, null, category));
    }

    /// <summary>
    /// <b>والأساسيّ لا يُطالَب</b> — ولولا ذلك لتعطّل كلُّ بيعٍ عاديّ. وهذا هو الحارسُ الذي
    /// يمنع الحارسَ من أن يبتلع الحالةَ السائدة.
    /// </summary>
    [Fact]
    public void AStandardLine_IsAskedForNothing()
    {
        var invoice = Draft();
        invoice.AddLine("صنف", 1m, 100m);

        Assert.False(VatExemptionReason.IsRequiredFor(VatCategory.Standard));
        Assert.Single(invoice.Lines);
    }

    /// <summary>وسطرٌ بسببٍ صحيح يمرّ — وإلا لَما أمكن بيعُ صادراتٍ أصلاً.</summary>
    [Fact]
    public void AZeroRatedLine_WithAValidReason_Passes()
    {
        var invoice = Draft();
        invoice.AddLine("تصدير", 1m, 1_000m, null, VatCategory.ZeroRated, "VATEX-SA-32");

        Assert.Equal("VATEX-SA-32", invoice.Lines[0].TaxExemptionReasonCode);
        Assert.Empty(VatExemptionGaps.For(invoice.Lines));
    }

    /// <summary>
    /// <b>ورمزُ فئةٍ في فئةٍ أخرى يُرفض</b> — <c>VATEX-SA-32</c> (تصدير سلع، فئة Z) على سطرٍ
    /// معفى يمرّ على أيّ فحصِ وجود، ويسقط عند الهيئة. فالحارسُ يقرأ الرمزَ لا وجودَه.
    /// </summary>
    [Fact]
    public void AReasonFromAnotherCategory_IsRefused()
    {
        var ex = Assert.Throws<AccountingException>(
            () => Draft().AddLine("خدمة", 1m, 100m, null, VatCategory.Exempt, "VATEX-SA-32"));

        Assert.Contains("VATEX-SA-32", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ورمزٌ مخترَع يُرفض كذلك — فالقائمةُ مغلقة.</summary>
    [Fact]
    public void AnInventedReason_IsRefused() =>
        Assert.Throws<AccountingException>(
            () => Draft().AddLine("خدمة", 1m, 100m, null, VatCategory.Exempt, "VATEX-SA-999"));

    /// <summary>
    /// <b>وفئةٌ واحدةٌ بسببين مختلفين تُرفض</b> — لأنّ الهيئةَ تقرأ السببَ من
    /// <c>cac:TaxSubtotal</c> وهو وعاءٌ واحدٌ لكلّ فئة. ولو أُصدرت لَحملت أحدَهما وكذبت
    /// على الآخر. وهذا عطلٌ لا يظهر في سطرٍ منفرد — لا يمسكه إلا فحصُ المستند كلِّه.
    /// </summary>
    [Fact]
    public void TwoDifferentReasons_InOneCategory_AreRefused()
    {
        var invoice = Draft();
        invoice.AddLine("تصدير سلع", 1m, 1_000m, null, VatCategory.ZeroRated, "VATEX-SA-32");
        invoice.AddLine("تصدير خدمات", 1m, 500m, null, VatCategory.ZeroRated, "VATEX-SA-33");

        var gaps = VatExemptionGaps.For(invoice.Lines);

        Assert.Single(gaps);
        Assert.Contains("سببين مختلفين", gaps[0], StringComparison.Ordinal);
    }

    /// <summary>وفئتان مختلفتان بسببين مختلفين تمرّان — فلكلٍّ وعاؤها.</summary>
    [Fact]
    public void TwoCategories_EachWithItsOwnReason_Pass()
    {
        var invoice = Draft();
        invoice.AddLine("تصدير", 1m, 1_000m, null, VatCategory.ZeroRated, "VATEX-SA-32");
        invoice.AddLine("إيجار سكني", 1m, 500m, null, VatCategory.Exempt, "VATEX-SA-30");
        invoice.AddLine("صنف عاديّ", 1m, 200m);

        Assert.Empty(VatExemptionGaps.For(invoice.Lines));
        Assert.Equal(3, LineDocumentTotalsCalculator.ComputeSubtotals(invoice.Lines).Count);
    }

    /// <summary>
    /// <b>و«خارج النطاق» سببُها نصٌّ يكتبه المكلَّف</b> — رمزُها واحدٌ ثابت، والمعيارُ يقول
    /// صراحةً إنّ النصَّ حرٌّ يُقدَّم حالةً بحالة. فالرمزُ وحده لا يكفي.
    /// </summary>
    [Fact]
    public void OutOfScope_DemandsWrittenText_NotJustTheCode()
    {
        var invoice = Draft();
        invoice.AddLine("منحة", 1m, 100m, null, VatCategory.OutOfScope, VatExemptionReason.OutOfScopeCode);

        var gaps = VatExemptionGaps.For(invoice.Lines);
        Assert.Single(gaps);
        Assert.Contains("سبباً مكتوباً", gaps[0], StringComparison.Ordinal);

        var withText = Draft();
        withText.AddLine("منحة", 1m, 100m, null, VatCategory.OutOfScope,
            VatExemptionReason.OutOfScopeCode, "منحة حكومية خارج نطاق الضريبة");
        Assert.Empty(VatExemptionGaps.For(withText.Lines));
    }

    /// <summary>
    /// <b>حارسٌ للقائمة نفسها:</b> كلُّ فئةٍ تطلب سبباً يجب أن تملك سبباً واحداً على الأقلّ
    /// معروضاً — وإلا كان الحارسُ بابَاً مغلقاً بلا مفتاح، يمنع البيع ولا يهدي إلى حلّ.
    /// </summary>
    [Fact]
    public void EveryDemandingCategory_HasAtLeastOneOfferedReason()
    {
        foreach (var category in Enum.GetValues<VatCategory>().Where(VatExemptionReason.IsRequiredFor))
            Assert.True(VatExemptionReason.ForCategory(category).Count > 0,
                $"فئة «{category.UblCode()}» تطلب سبباً ولا سببَ معروضاً لها — بابٌ بلا مفتاح.");
    }

    /// <summary>
    /// <b>والقائمةُ منقولةٌ لا مخترَعة:</b> كلُّ رمزٍ يبدأ بالبادئة النظامية، وله نصٌّ
    /// إنجليزيّ (هو ما يُكتب في <c>cbc:TaxExemptionReason</c>) ونصٌّ عربيّ للشاشة.
    /// </summary>
    [Fact]
    public void TheCatalogue_IsWellFormed()
    {
        Assert.Equal(16, VatExemptionReason.Catalogue.Count);

        foreach (var reason in VatExemptionReason.Catalogue)
        {
            Assert.StartsWith("VATEX-SA-", reason.Code, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(reason.EnglishText), reason.Code);
            Assert.False(string.IsNullOrWhiteSpace(reason.ArabicText), reason.Code);
            Assert.NotEqual(VatCategory.Standard, reason.Category);
        }

        Assert.Equal(
            VatExemptionReason.Catalogue.Count,
            VatExemptionReason.Catalogue.Select(r => r.Code).Distinct(StringComparer.Ordinal).Count());
    }
}
