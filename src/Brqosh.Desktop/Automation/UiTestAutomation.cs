using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Automation;

/// <summary>
/// نقاط اختبار UIA لـ FlaUI — لا تؤثر على سلوك المستخدم.
/// </summary>
public static class UiTestAutomation
{
    public const string MainGridId = "BrqoshMainGrid";
    public const string ReportSummaryId = "BrqoshReportSummary";
    public const string KpiGridId = "BrqoshKpiGrid";

    public static void PublishGridCount(DataGrid grid, int count, string automationId = MainGridId, string? pageKey = null)
    {
        EmptyStateHelper.Sync(grid, count, pageKey: pageKey);
        AutomationProperties.SetAutomationId(grid, automationId);
        AutomationProperties.SetItemStatus(grid, count.ToString());
        AutomationProperties.SetName(grid, $"GridRows:{count}");
        PublishPageProbe(grid, automationId, $"GridRows:{count}", count.ToString());
    }

    public static void PublishSummary(FrameworkElement element, string text)
    {
        AutomationProperties.SetAutomationId(element, ReportSummaryId);
        AutomationProperties.SetName(element, text);
    }

    public static void PublishKpiPanel(Panel panel, int cardCount)
    {
        AutomationProperties.SetAutomationId(panel, KpiGridId);
        AutomationProperties.SetItemStatus(panel, cardCount.ToString());
        AutomationProperties.SetName(panel, $"KpiCount:{cardCount}");
        PublishPageProbe(panel, KpiGridId, $"KpiCount:{cardCount}", cardCount.ToString());
    }

    private static void PublishPageProbe(FrameworkElement anchor, string automationId, string name, string text)
    {
        var page = FindAncestor<UserControl>(anchor);
        if (page is null)
            return;

        if (page.Tag is not TextBlock probe)
        {
            probe = new TextBlock
            {
                Visibility = Visibility.Collapsed,
                Width = 0,
                Height = 0,
                IsHitTestVisible = false,
            };

            if (page.Content is Panel panel)
                panel.Children.Add(probe);
            else if (page.Content is ScrollViewer { Content: Panel scrollPanel })
                scrollPanel.Children.Add(probe);
            else if (page.Content is Grid grid)
                grid.Children.Add(probe);

            page.Tag = probe;
        }

        probe.Text = text;
        AutomationProperties.SetAutomationId(probe, automationId);
        AutomationProperties.SetName(probe, name);
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
                return match;
            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }
}
