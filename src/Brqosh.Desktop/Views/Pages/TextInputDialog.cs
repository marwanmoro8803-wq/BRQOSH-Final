using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brqosh.Desktop.Views.Pages;

public class TextInputDialog : Window
{
    private readonly TextBox _inputBox;

    public string Value => _inputBox.Text;

    public TextInputDialog(string title, string prompt, string defaultValue = "")
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FlowDirection = FlowDirection.RightToLeft;
        FontFamily = System.Windows.Application.Current.TryFindResource("AppFont") as FontFamily ?? new FontFamily("Segoe UI");
        Background = System.Windows.Application.Current.TryFindResource("BackgroundBrush") as Brush ?? Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        panel.Children.Add(new TextBlock { Text = prompt, FontSize = 14, Margin = new Thickness(0, 0, 0, 8) });

        _inputBox = new TextBox { Text = defaultValue, FontSize = 14, Padding = new Thickness(10, 8, 10, 8) };
        panel.Children.Add(_inputBox);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var ok = new Button { Content = "موافق", Padding = new Thickness(20, 8, 20, 8), IsDefault = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        var cancel = new Button { Content = "إلغاء", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(10, 0, 0, 0), IsCancel = true };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
    }
}
