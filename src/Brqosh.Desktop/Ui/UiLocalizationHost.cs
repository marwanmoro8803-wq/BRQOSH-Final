using System.Windows;
using System.Windows.Controls;

namespace Brqosh.Desktop.Ui;

/// <summary>Global hooks — apply XAML localization on every page/dialog load + locale toggle.</summary>
public static class UiLocalizationHost
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded),
            true);

        LocaleManager.Changed += OnLocaleChanged;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject root)
            return;

        var ns = root.GetType().Namespace ?? string.Empty;
        if (!ns.StartsWith("Brqosh.Desktop.Views", StringComparison.Ordinal))
            return;

        if (root is not (UserControl or Window))
            return;

        XamlUiLocalizer.Apply(root);
    }

    private static void OnLocaleChanged()
    {
        if (System.Windows.Application.Current?.MainWindow is not Window main)
            return;

        XamlUiLocalizer.Apply(main);

        if (main.FindName("PageHost") is ContentControl { Content: DependencyObject page })
            XamlUiLocalizer.Apply(page);
    }
}
