using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») الترويسةُ لا تدهس العنوان عند الحدّ الأدنى للنافذة (١٠٢٤ ⟵ ٧٥٢ للترويسة؛ ومع لوحة المساعد ٤٥٢ فتُحجَب اللوحةُ تلقائيّاً بـ`ApplyAiPanelPolicy`). مجموعُ عروض الأدوات في كلّ درجةٍ + ٢٠٠ للعنوان + ٥٦ حشوٌ ≤ العرض.
/// </summary>
public class HeaderFitTests
{
    // ‏عروضُ الأدوات الثابتة: ثلاثةُ أزرار (44+44+52 + هوامش 24) = 164، التاريخ ≈170+18، الشارة ≈120، تسميتا الحقلين ≈ 90، هوامش القائمتين 32.
    private static double NeededWidth(HeaderFit.Tier t) =>
        t.CompanyWidth + t.BranchWidth + 32 + 164
        + (t.ShowLabelsAndDate ? 188 + 90 : 0) + (t.ShowUserBadge ? 120 : 0) + 200 + 56;

    [Theory]
    [InlineData(1400)]
    [InlineData(1240)]
    [InlineData(1239)]
    [InlineData(960)]
    [InlineData(959)]
    [InlineData(752)]
    [InlineData(740)]
    [InlineData(739)]
    [InlineData(680)]
    public void EveryTier_FitsTheWidthItIsChosenFor(double width)
    {
        Assert.True(NeededWidth(HeaderFit.For(width)) <= width, $"عند {width}: المطلوب {NeededWidth(HeaderFit.For(width))}");
    }

    [Fact]
    public void TheWidestTier_KeepsEverything_AndTheNarrowestDropsExtrasFirst()
    {
        var wide = HeaderFit.For(1500);
        Assert.True(wide.ShowLabelsAndDate && wide.ShowUserBadge);
        var mid = HeaderFit.For(1000);
        Assert.False(mid.ShowLabelsAndDate);
        Assert.True(mid.ShowUserBadge);
        var narrow = HeaderFit.For(500);
        Assert.False(narrow.ShowUserBadge);
        Assert.True(narrow.CompanyWidth < wide.CompanyWidth);
    }
}
