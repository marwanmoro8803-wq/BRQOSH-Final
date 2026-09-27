using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Support;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// دعمُ العملاء — <b>القنواتُ ومستوياتُ الخدمة كما اعتمدها المورّد في <c>config/support.json</c></b>، وبدونه «لم تُعتمد»
/// (<c>commercial-readiness#7</c>: كانت الشاشةُ تعرض هاتفاً مؤقّتاً وباقاتٍ مكتوبةً في الشيفرة تخالف مسودّتَي التسويق).
/// </summary>
public partial class CustomerSupportPage : UserControl
{
    public CustomerSupportPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Bind();
    }

    private void Bind()
    {
        var support = SupportContact.LoadBeside(AppContext.BaseDirectory);

        SlaGrid.ItemsSource = support.Plans.Select(p => new SlaRow(p.Plan, p.Response, p.Hours)).ToList();
        SlaGrid.Visibility = support.Plans.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        EmailText.Text = support.Email ?? "";
        EmailRow.Visibility = support.Email is null ? Visibility.Collapsed : Visibility.Visible;
        PhoneText.Text = support.Phone ?? "";
        PhoneRow.Visibility = support.Phone is null ? Visibility.Collapsed : Visibility.Visible;

        if (support.Problem is not null)
            SupportStateText.Text = support.Problem;
        else if (!support.IsConfigured)
            SupportStateText.Text = "لم تعتمد قنوات الدعم ولا مستويات الخدمة لهذه النسخة بعد.";
        SupportStateText.Visibility = support.IsConfigured ? Visibility.Collapsed : Visibility.Visible;

        TicketsGrid.ItemsSource = Array.Empty<TicketRow>();
        UiTestAutomation.PublishGridCount(TicketsGrid, 0);
        TicketsSummaryText.Text = "تذاكر الدعم لا تدار من داخل البرنامج في هذه النسخة.";
    }

    private sealed record SlaRow(string Plan, string Response, string Hours);
    private sealed record TicketRow(string Id, string Subject, string Priority, string Status);
}
