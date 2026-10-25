using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brqosh.Domain.Companies;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (علمُ كلّ عملة) <b>علمُ الدولة الممثِّلة لعملةٍ مع رمز الدولة</b> في قوائم اختيار العملة (ISO 4217 ⟵ ISO 3166 من <see cref="CurrencyCatalog.CountryCode"/>).
/// الأعلامُ صورُ PNG مضمَّنةٌ في البرنامج (<c>assets/flags</c> ⟵ <c>Flags/</c>) — فإيموجي الأعلام لا يُرسَم في ويندوز (يظهر حرفين) فلا يصلح.
/// </summary>
public sealed class CurrencyFlagConverter : IValueConverter
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var country = CurrencyCatalog.CountryCode(value as string).ToLowerInvariant();
        if (country.Length == 0)
            return null;

        lock (Cache)
        {
            if (Cache.TryGetValue(country, out var cached))
                return cached;

            ImageSource? image = null;
            try
            {
                var bitmap = new BitmapImage(new Uri($"pack://application:,,,/Brqosh.Desktop;component/Flags/{country}.png", UriKind.Absolute));
                bitmap.Freeze();
                image = bitmap;
            }
            catch (Exception ex) when (ex is IOException or UriFormatException or NotSupportedException)
            {
                // ‏علمٌ مفقودٌ لا يُسقط قائمةً: يبقى الرمزُ بلا صورة ويُسجَّل الفشل (ويحرس وجودَ الأعلام اختبارٌ).
                CrashLog.Write(ex, "CurrencyFlagConverter");
            }

            Cache[country] = image;
            return image;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>رمزُ الدولة (SA) المكتوب بجوار رمز العملة (SAR).</summary>
public sealed class CurrencyCountryConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        CurrencyCatalog.CountryCode(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
