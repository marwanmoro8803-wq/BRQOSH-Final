using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») خطُّ الأرقام Bahnschrift لاتينيّ: استبدالُ الأرقام العربيّ في ويندوز يحوّل «0.00» إلى «٠٫٠٠» فيصير الفاصلُ العشريّ «٫» (U+066B) مربّعاً فارغاً
/// لغيابه عن الخطّ — قِيس على شاشةٍ حقيقيّةٍ في بطاقات المؤشّرات («• ▯ ••»). فكلُّ نمطٍ يستعمل <c>FigureFont</c> يثبّت الأرقامَ الأوروبيّة.
/// </summary>
public class FigureFontNumbersAreLatinTests
{
    [Theory]
    [InlineData("KpiValueText")]
    [InlineData("TabularText")]
    public void EveryStyleUsingTheFigureFont_PinsEuropeanDigits(string key)
    {
        var (family, substitution) = WpfTestHost.Invoke(() =>
        {
            var style = (Style)System.Windows.Application.Current.FindResource(key);
            var setters = style.Setters.OfType<Setter>().ToList();
            var font = setters.Where(s => s.Property == TextBlock.FontFamilyProperty).Select(s => ((FontFamily)s.Value).Source).FirstOrDefault();
            var sub = setters.Where(s => s.Property == NumberSubstitution.SubstitutionProperty).Select(s => (NumberSubstitutionMethod?)s.Value).FirstOrDefault();
            return (font, sub);
        });

        Assert.StartsWith("Bahnschrift", family);
        Assert.Equal(NumberSubstitutionMethod.European, substitution);
    }
}
