using System.Windows.Controls;
using Brqosh.Desktop.Automation;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>G-31 دعم عملاء — MVP in-repo (SLA + قنوات + placeholder تذاكر).</summary>
public partial class CustomerSupportPage : UserControl
{
    public CustomerSupportPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Bind();
    }

    private void Bind()
    {
        SlaGrid.ItemsSource = new List<SlaRow>
        {
            new("تجريبي", "48 ساعة عمل", "أحد–خميس 9–17"),
            new("قياسي", "24 ساعة عمل", "أحد–خميس 8–18"),
            new("مؤسسي", "4 ساعات", "24/7 حرج"),
        };

        TicketsGrid.ItemsSource = Array.Empty<TicketRow>();
        UiTestAutomation.PublishGridCount(TicketsGrid, 0);
        TicketsSummaryText.Text = "لا توجد تذاكر مفتوحة — التكامل الحي مع بوابة الدعم في خارطة SaaS.";
    }

    private sealed record SlaRow(string Plan, string Response, string Hours);
    private sealed record TicketRow(string Id, string Subject, string Priority, string Status);
}
