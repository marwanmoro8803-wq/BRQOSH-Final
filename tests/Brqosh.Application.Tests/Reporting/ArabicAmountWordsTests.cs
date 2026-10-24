using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// المبلغ كتابةً (التفقيط) على المطبوعات: النصوص المرجعيّة مكتوبةٌ بقواعد العربية يدويّاً
/// لا مولَّدةً من المنفِّذ — فالاختبار يحكم عليه ولا يكرّره.
/// </summary>
public class ArabicAmountWordsTests
{
    [Theory]
    [InlineData(4370, "فقط أربعة آلاف وثلاثمائة وسبعون ريالاً سعودياً لا غير")]
    [InlineData(258750, "فقط مائتان وثمانية وخمسون ألفاً وسبعمائة وخمسون ريالاً سعودياً لا غير")]
    [InlineData(0, "فقط صفر ريال سعودي لا غير")]
    [InlineData(1, "فقط ريال سعودي واحد لا غير")]
    [InlineData(2, "فقط ريالان سعوديان لا غير")]
    [InlineData(3, "فقط ثلاثة ريالات سعودية لا غير")]
    [InlineData(10, "فقط عشرة ريالات سعودية لا غير")]
    [InlineData(11, "فقط أحد عشر ريالاً سعودياً لا غير")]
    [InlineData(100, "فقط مائة ريال سعودي لا غير")]
    [InlineData(1000, "فقط ألف ريال سعودي لا غير")]
    [InlineData(2000, "فقط ألفان ريال سعودي لا غير")]
    [InlineData(200000, "فقط مائتا ألف ريال سعودي لا غير")]
    [InlineData(1_000_000, "فقط مليون ريال سعودي لا غير")]
    [InlineData(2_500_000, "فقط مليونان وخمسمائة ألف ريال سعودي لا غير")]
    public void TheRiyalsAreWrittenInArabicGrammar(decimal amount, string expected) =>
        Assert.Equal(expected, ArabicAmountWords.ForRiyals(amount));

    [Theory]
    [InlineData(0.5, "فقط خمسون هللة لا غير")]
    [InlineData(0.01, "فقط هللة واحدة لا غير")]
    [InlineData(0.05, "فقط خمس هللات لا غير")]
    [InlineData(0.13, "فقط ثلاث عشرة هللة لا غير")]
    [InlineData(10.05, "فقط عشرة ريالات سعودية وخمس هللات لا غير")]
    [InlineData(115.75, "فقط مائة وخمسة عشر ريالاً سعودياً وخمس وسبعون هللة لا غير")]
    public void TheHalalasFollowTheFeminineNoun(decimal amount, string expected) =>
        Assert.Equal(expected, ArabicAmountWords.ForRiyals(amount));

    [Fact]
    public void AThirdDecimalIsRoundedToTheHalalaNotDropped() =>
        Assert.Equal(ArabicAmountWords.ForRiyals(10.01m), ArabicAmountWords.ForRiyals(10.005m));

    [Fact]
    public void ARoundedUpHalalaCarriesIntoTheRiyal() =>
        Assert.Equal(ArabicAmountWords.ForRiyals(1m), ArabicAmountWords.ForRiyals(0.995m));

    [Fact]
    public void ANegativeAmountIsWrittenByItsMagnitude() =>
        Assert.Equal(ArabicAmountWords.ForRiyals(4370m), ArabicAmountWords.ForRiyals(-4370m));

    [Fact]
    public void AnAmountBeyondAnyCompanyDocumentIsRefusedNotPrintedGarbled() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ArabicAmountWords.ForRiyals(1_000_000_000_000m));

    [Fact]
    public void EveryRiyalUpToATenThousandIsWrittenWithoutGaps()
    {
        for (var n = 0; n <= 10_000; n++)
        {
            var words = ArabicAmountWords.ForRiyals(n);
            Assert.StartsWith("فقط ", words);
            Assert.EndsWith(" لا غير", words);
            Assert.DoesNotContain("  ", words);
            Assert.DoesNotContain(" و ", words);
        }
    }
}
