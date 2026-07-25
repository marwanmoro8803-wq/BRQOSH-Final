using System.IO;
using System.Windows;

namespace Brqosh.Desktop.Views.Pages;

public partial class XmlViewerDialog : Window
{
    public XmlViewerDialog(string title, string filePath)
    {
        InitializeComponent();
        TitleText.Text = title;
        ContentBox.Text = File.Exists(filePath) ? File.ReadAllText(filePath) : "الملف غير موجود.";
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ContentBox.Text);
        MessageBox.Show("تم النسخ.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
