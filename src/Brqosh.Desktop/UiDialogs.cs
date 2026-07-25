using System.Windows;
using Brqosh.Desktop.Views.Pages;

namespace Brqosh.Desktop;

internal static class UiDialogs
{
    /// <summary>يطلب تاريخ عملية محاسبية (عكس، إلغاء، تسوية) — الافتراضي اليوم.</summary>
    public static DateOnly? PromptAccountingDate(Window owner, string title, string prompt)
    {
        var dialog = new DateInputDialog(title, prompt, DateOnly.FromDateTime(DateTime.Today)) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.SelectedDate : null;
    }

    public static string? PromptText(string title, string prompt, string defaultValue = "")
    {
        var dialog = new TextInputDialog(title, prompt, defaultValue)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }
}
