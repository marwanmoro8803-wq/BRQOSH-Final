using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public class DateInputDialog : Window
{
    private readonly DatePicker _datePicker;

    public DateOnly SelectedDate { get; private set; }

    public DateInputDialog(string title, string prompt, DateOnly defaultDate)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FlowDirection = Brqosh.Desktop.Ui.LocaleManager.Flow;
        FontFamily = System.Windows.Application.Current.TryFindResource("AppFont") as FontFamily
            ?? new FontFamily("Segoe UI");
        Background = System.Windows.Application.Current.TryFindResource("BackgroundBrush") as Brush
            ?? Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        panel.Children.Add(new TextBlock { Text = prompt, FontSize = 14, Margin = new Thickness(0, 0, 0, 8) });

        _datePicker = new DatePicker
        {
            SelectedDate = defaultDate.ToDateTime(TimeOnly.MinValue),
            FontSize = 14,
        };
        panel.Children.Add(_datePicker);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        var ok = new Button { Content = "موافق", Padding = new Thickness(20, 8, 20, 8), IsDefault = true };
        ok.Click += (_, _) =>
        {
            if (_datePicker.SelectedDate is not DateTime dt)
            {
                UiDialog.Show("اختر تاريخاً صالحاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedDate = DateOnly.FromDateTime(dt);
            DialogResult = true;
            Close();
        };
        var cancel = new Button
        {
            Content = "إلغاء",
            Padding = new Thickness(20, 8, 20, 8),
            Margin = new Thickness(10, 0, 0, 0),
            IsCancel = true,
        };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
    }
}
