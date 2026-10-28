// ADR (i18n step 1, 2026-10): UI strings move from "Arabic text as key" (UiBilingualRegistry) to stable dotted
// keys in Resources/Strings.resx (Arabic, neutral) + Strings.en.resx. To migrate a screen: add the key to BOTH resx
// files, replace the XAML literal with {loc:Str Key=Area.Name} (xmlns:loc="clr-namespace:Brqosh.Desktop.Ui") or the
// code literal with a Loc.T call on the same key; unmigrated text keeps working through the registry fallback.
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

[assembly: NeutralResourcesLanguage("ar")]

namespace Brqosh.Desktop.Ui;

/// <summary>Key-based UI string lookup (resx), with fallback to the Arabic-text registry.</summary>
public static class Loc
{
    private static readonly ResourceManager Strings =
        new("Brqosh.Desktop.Resources.Strings", typeof(Loc).Assembly);

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    /// <summary>The string for <paramref name="key"/> in the current UI language.</summary>
    public static string T(string key) =>
        TryGet(key, LocaleManager.IsEnglish)
        ?? LocaleManager.T(key, UiBilingualRegistry.Lookup(key));

    /// <summary>The resx value for <paramref name="key"/> in the given language, or null if the key is unknown.</summary>
    public static string? TryGet(string key, bool english)
    {
        try
        {
            return Strings.GetString(key, english ? English : CultureInfo.InvariantCulture);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }
}

/// <summary>Binding source behind <see cref="StrExtension"/>: re-reads every key when the language switches.</summary>
public sealed class LocSource : INotifyPropertyChanged
{
    public static LocSource Instance { get; } = new();

    private LocSource() =>
        LocaleManager.Changed += () =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));

    public string this[string key] => Loc.T(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>XAML: <c>Content="{loc:Str Key=Common.Save}"</c>, live on language switch.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class StrExtension : MarkupExtension
{
    public StrExtension() { }

    public StrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocSource.Instance,
            Mode = BindingMode.OneWay,
            FallbackValue = Key,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
