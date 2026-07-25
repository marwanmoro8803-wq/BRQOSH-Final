using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Brqosh.Desktop.Ui;

public static class PasswordFieldHelper
{
    public static void Wire(PasswordBox passwordBox, TextBox plainBox, ToggleButton revealToggle)
    {
        revealToggle.Checked += (_, _) =>
        {
            plainBox.Text = passwordBox.Password;
            plainBox.Visibility = Visibility.Visible;
            passwordBox.Visibility = Visibility.Collapsed;
            plainBox.Focus();
            plainBox.CaretIndex = plainBox.Text.Length;
        };

        revealToggle.Unchecked += (_, _) =>
        {
            passwordBox.Password = plainBox.Text;
            plainBox.Visibility = Visibility.Collapsed;
            passwordBox.Visibility = Visibility.Visible;
            passwordBox.Focus();
        };

        plainBox.TextChanged += (_, _) =>
        {
            if (plainBox.Visibility == Visibility.Visible)
                passwordBox.Password = plainBox.Text;
        };
    }
}
