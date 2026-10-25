using System.IO;
using System.Text.Json;

namespace Brqosh.Desktop.Ui;

/// <summary>تفضيلاتُ الواجهة لمستخدم ويندوز هذا: المظهرُ واللغة.</summary>
public sealed class UiPreferences
{
    /// <summary>المظهرُ الداكن (الأصل) أو الفاتح.</summary>
    public bool Dark { get; set; } = true;

    /// <summary>الإنجليزيّةُ؟ — وإلّا فالعربيّة (الأصل).</summary>
    public bool English { get; set; }

    /// <summary>كثافةُ الجداول المضغوطة؟ — وإلّا المريحةُ (الأصل).</summary>
    public bool Compact { get; set; }
}

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · الثيمات) <b>اختيارُ المظهر واللغة يُحفظ ويعود عند كلّ تشغيل.</b>
///
/// <para><b>ما كان:</b> زرٌّ في الترويسة يبدّل الثيمَ واللغة، <b>والبرنامج يبدأ داكناً عربيّاً دائماً</b> — فمن يفضّل الفاتحَ يبدّله كلَّ صباح،
/// ولا مكانَ في أيّ شاشة إعدادات يُختار منه المظهر. <b>الآن</b> يُحفظ الاختيارُ في ملفٍّ صغيرٍ في بيانات هذا المستخدم
/// (<c>%APPDATA%\BRQOSH\ui-prefs.json</c>)، ويُقرأ عند فتح النافذة الرئيسة.</para>
///
/// <para>الملفُّ التالفُ أو الغائبُ ⟵ الأصل، <b>ولا يُسقط الإقلاع</b>؛ وفشلُ الكتابة يُهمَل (تفضيلٌ لا بيانات).</para>
/// </summary>
public static class UiPreferencesStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "ui-prefs.json");

    public static UiPreferences Load(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (!File.Exists(path))
                return new UiPreferences();
            return JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(path)) ?? new UiPreferences();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new UiPreferences();
        }
    }

    /// <summary>يحفظ التفضيلات. <c>true</c> إن كُتبت؛ وعلى الفشل <c>false</c> بلا استثناء.</summary>
    public static bool Save(UiPreferences prefs, string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(prefs, Options));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
