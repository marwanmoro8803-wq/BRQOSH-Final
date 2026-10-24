using System.Windows;

namespace Brqosh.Desktop.Ui;

/// <summary>تبديل فوري عربي/إنجليزي — bilingual UI foundation.</summary>
public static class LocaleManager
{
    public static bool IsEnglish { get; private set; }

    public static event Action? Changed;

    public static string T(string arabic, string english) =>
        IsEnglish ? english : arabic;

    public static FlowDirection Flow =>
        IsEnglish ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

    public static string LanguageLabel => IsEnglish ? "ع" : "EN";

    public static void Toggle()
    {
        IsEnglish = !IsEnglish;
        Changed?.Invoke();
    }

    /// <summary>يضبط اللغة صراحةً (من التفضيلات المحفوظة) — ولا يُطلق <see cref="Changed"/> إن لم تتغيّر.</summary>
    public static void Set(bool english)
    {
        if (IsEnglish == english)
            return;
        IsEnglish = english;
        Changed?.Invoke();
    }

    public static void ApplyTo(FrameworkElement element) =>
        element.FlowDirection = Flow;
}
