using System.Windows;

namespace Brqosh.Desktop.Ui;

/// <summary>تبديل فوري عربي/إنجليزي — bilingual UI foundation.</summary>
public static class LocaleManager
{
    public static bool IsEnglish { get; private set; }

    public static event Action? Changed;

    public static string T(string arabic, string english) =>
        IsEnglish ? english : arabic;

    /// <summary>Lookup by stable resx key (e.g. <c>Common.Save</c>); falls back to the Arabic-text registry. See <see cref="Loc"/>.</summary>
    public static string T(string key) => Loc.T(key);

    public static FlowDirection Flow =>
        IsEnglish ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

    public static string LanguageLabel => IsEnglish ? "ع" : "EN";

    public static void Toggle()
    {
        IsEnglish = !IsEnglish;
        PrintLanguage();
        Changed?.Invoke();
    }

    /// <summary>يضبط اللغة صراحةً (من التفضيلات المحفوظة) — ولا يُطلق <see cref="Changed"/> إن لم تتغيّر.</summary>
    public static void Set(bool english)
    {
        if (IsEnglish == english)
            return;
        IsEnglish = english;
        PrintLanguage();
        Changed?.Invoke();
    }

    /// <summary>المطبوعاتُ تتبع لغةَ الواجهة (الفاتورةُ الضريبيّةُ وحدَها تبقى عربيّةً — الهيئة).</summary>
    private static void PrintLanguage() => Brqosh.Infrastructure.Reporting.PrintLang.Preferred = IsEnglish;

    public static void ApplyTo(FrameworkElement element) =>
        element.FlowDirection = Flow;

    private static bool _windowsRegistered;

    /// <summary>
    /// المصدرُ الوحيدُ لاتجاه الواجهة: كلُّ نافذةٍ تأخذ <see cref="Flow"/> عند تحميلها، وكلُّ النوافذ المفتوحة
    /// تنقلب معاً عند تبديل اللغة. الصفحاتُ والعناصرُ ترث الاتجاهَ من نافذتها، فلا يُكتب <c>FlowDirection="RightToLeft"</c>
    /// على جذر أيّ شاشة (كان مكرّراً ثابتاً في 188 ملفاً ولا يتبع الإنجليزية).
    /// </summary>
    public static void RegisterWindows()
    {
        if (_windowsRegistered)
            return;
        _windowsRegistered = true;

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window)
                    ApplyTo(window);
            }));

        Changed += () =>
        {
            if (System.Windows.Application.Current is not { } app)
                return;
            foreach (Window window in app.Windows)
                ApplyTo(window);
        };
    }
}
