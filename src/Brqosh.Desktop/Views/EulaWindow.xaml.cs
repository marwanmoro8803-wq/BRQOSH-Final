using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Desktop.Views;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>installer-deployment-ops#13</c>) <b>اتّفاقيّةُ الترخيص قبل الإعداد</b> — تُعرض حتى يقبل المستخدمُ نصَّها
/// بعينه (<see cref="EulaAcceptance"/>)، و«لا أوافق» يُغلق البرنامج.
/// </summary>
public partial class EulaWindow : Window
{
    private readonly string _text;

    /// <param name="readOnly">
    /// ‏للاطّلاع بعد القبول (من «تفضيلات البرنامج»): لا خانةَ قبولٍ ولا «أوافق» — زرُّ إغلاقٍ وحده، ولا يُسجَّل قبولٌ جديد.
    /// </param>
    public EulaWindow(string text, bool readOnly = false)
    {
        InitializeComponent();
        _text = text;
        EulaBox.Document = BuildDocument(text);
        if (readOnly)
        {
            AgreeCheck.Visibility = Visibility.Collapsed;
            AcceptButton.Visibility = Visibility.Collapsed;
            DeclineButton.Content = "إغلاق";
        }
    }

    private static readonly Regex Arabic = new(@"[\u0600-\u06FF]", RegexOptions.Compiled);
    private static readonly Regex Heading = new(@"^\d+\.\s", RegexOptions.Compiled);

    /// <summary>فقرةٌ لكلّ سطر: اتجاهُها بحسب أوّل حرفٍ ذي معنى (عربيّ ⟵ RTL وإلا LTR)، والعناوينُ المرقَّمةُ بخطٍّ غليظ.</summary>
    internal static FlowDocument BuildDocument(string text)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(0), LineHeight = 22 };
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var paragraph = new Paragraph(new Run(line))
            {
                Margin = new Thickness(0, 0, 0, line.Length == 0 ? 4 : 6),
                FlowDirection = Arabic.IsMatch(line) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                FontWeight = Heading.IsMatch(line) ? FontWeights.Bold : FontWeights.Normal,
            };
            doc.Blocks.Add(paragraph);
        }
        return doc;
    }

    private void OnAgreeChanged(object sender, RoutedEventArgs e) => AcceptButton.IsEnabled = AgreeCheck.IsChecked == true;

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        EulaAcceptance.Accept(_text);
        DialogResult = true;
    }

    private void OnDecline(object sender, RoutedEventArgs e) => DialogResult = false;
}
