using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brqosh.Application.Intelligence;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Desktop.Ui;

public static class ExecutiveDashboardBuilder
{
    public static UIElement BuildAlertStrip(FrameworkElement host, DashboardKpis k)
    {
        var netCash = k.CollectedThisMonth - k.PaidThisMonth;
        var msg = netCash >= 0
            ? LocaleManager.T(
                $"تدفق نقدي إيجابي هذا الشهر: {netCash:N2} ر.س",
                $"Positive cash flow this month: SAR {netCash:N2}")
            : LocaleManager.T(
                $"تنبيه: تدفق نقدي سلبي {netCash:N2} ر.س",
                $"Alert: negative cash flow SAR {netCash:N2}");

        var dot = new Border
        {
            Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
            Background = UiResources.BrushOf(host, "ElectricCyanBrush"),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(dot, Dock.Right);

        var panel = new DockPanel { LastChildFill = true };
        panel.Children.Add(dot);
        panel.Children.Add(new TextBlock
        {
            Text = msg,
            Foreground = UiResources.BrushOf(host, "TextPrimaryBrush"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return new Border { Style = UiResources.StyleOf(host, "AlertStrip"), Child = panel };
    }

    public static UIElement BuildChartPanel(FrameworkElement host, string titleAr, string titleEn,
        IReadOnlyList<decimal> series, string legendAr, string legendEn)
    {
        var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = LocaleManager.T(titleAr, titleEn),
            Style = UiResources.StyleOf(host, "FormSectionTitle"),
        };
        Grid.SetColumn(titleBlock, 0);

        var legendPanel = new StackPanel { Orientation = Orientation.Horizontal };
        legendPanel.Children.Add(new Border
        {
            Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
            Background = UiResources.BrushOf(host, "ElectricCyanBrush"),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        legendPanel.Children.Add(new TextBlock
        {
            Text = LocaleManager.T(legendAr, legendEn),
            Style = UiResources.StyleOf(host, "KpiCurrencySuffixText"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(legendPanel, 1);
        header.Children.Add(titleBlock);
        header.Children.Add(legendPanel);

        var chartHost = new Border
        {
            Height = 128,
            Background = UiResources.BrushOf(host, "BackgroundBrush"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = SparklineBuilder.Create(
                series, 480, 112,
                UiResources.BrushOf(host, "ElectricCyanBrush"),
                UiResources.BrushOf(host, "PrimarySoftBrush")),
        };

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(chartHost);

        return new Border
        {
            Style = UiResources.StyleOf(host, "ChartPanel"),
            Margin = new Thickness(0, 0, 0, 14),
            Child = body,
        };
    }

    public static UIElement BuildAiInsightsPanel(FrameworkElement host, DashboardKpis k)
    {
        var items = new[]
        {
            LocaleManager.T($"ذمم العملاء {k.Receivables:N0} ر.س — راجع أعمار الديون.", $"AR SAR {k.Receivables:N0} — review aging."),
            LocaleManager.T($"صافي ضريبة {k.NetVatThisMonth:N2} ر.س — جاهز للإقرار.", $"Net VAT SAR {k.NetVatThisMonth:N2} — filing ready."),
            LocaleManager.T($"مخزون {k.InventoryValue:N0} ر.س — تحقق من بطء الحركة.", $"Inventory SAR {k.InventoryValue:N0} — slow movers."),
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = LocaleManager.T("رؤى الذكاء الاصطناعي", "AI Insights"),
            Style = UiResources.StyleOf(host, "FormSectionTitle"),
            Margin = new Thickness(0, 0, 0, 12),
        });
        foreach (var text in items)
            stack.Children.Add(BuildAiCard(host, text));

        return new Border
        {
            Style = UiResources.StyleOf(host, "GlassPanel"),
            Margin = new Thickness(0, 0, 0, 14),
            Child = stack,
        };
    }

    public static UIElement BuildRuleEngineInsightsPanel(FrameworkElement host, FinancialIntelligenceReport report)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = LocaleManager.T("دعم القرار المالي", "Financial Decision Support"),
            Style = UiResources.StyleOf(host, "FormSectionTitle"),
            Margin = new Thickness(0, 0, 0, 4),
        });
        stack.Children.Add(new TextBlock
        {
            Text = report.Engine,
            FontSize = 11,
            Foreground = UiResources.BrushOf(host, "TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 12),
        });

        foreach (var insight in report.Insights.Take(5))
            stack.Children.Add(AiAssistantPanelBuilder.BuildInsightCard(host, insight));

        return new Border
        {
            Style = UiResources.StyleOf(host, "GlassPanel"),
            Margin = new Thickness(0, 0, 0, 14),
            Child = stack,
        };
    }

    public static UIElement BuildQuickActions(FrameworkElement host)
    {
        var actions = new (string Ar, string En, string Code)[]
        {
            ("فاتورة مبيعات", "Sales Invoice", "SI"),
            ("قيد يومية", "Journal Entry", "JE"),
            ("سند قبض", "Receipt", "RC"),
            ("تقرير VAT", "VAT Report", "VT"),
        };

        var wrap = new WrapPanel();
        foreach (var (ar, en, code) in actions)
        {
            wrap.Children.Add(new Border
            {
                Style = UiResources.StyleOf(host, "QuickActionChip"),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new Border
                        {
                            Width = 28, Height = 28, CornerRadius = new CornerRadius(8),
                            Background = UiResources.BrushOf(host, "PrimarySoftBrush"),
                            Child = new TextBlock
                            {
                                Text = code, FontSize = 9, FontWeight = FontWeights.Bold,
                                Foreground = UiResources.BrushOf(host, "PrimaryDarkBrush"),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                            },
                        },
                        new TextBlock
                        {
                            Text = LocaleManager.T(ar, en),
                            Margin = new Thickness(8, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = UiResources.BrushOf(host, "TextPrimaryBrush"),
                            FontSize = 13,
                        },
                    },
                },
            });
        }

        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(new TextBlock
        {
            Text = LocaleManager.T("إجراءات سريعة", "Quick Actions"),
            Style = UiResources.StyleOf(host, "FormSectionTitle"),
            Margin = new Thickness(0, 0, 0, 10),
        });
        panel.Children.Add(wrap);
        return panel;
    }

    public static UIElement BuildActivityFeed(FrameworkElement host, DashboardKpis k)
    {
        var rows = new[]
        {
            LocaleManager.T($"تحصيلات: {k.CollectedThisMonth:N2} ر.س", $"Collections: SAR {k.CollectedThisMonth:N2}"),
            LocaleManager.T($"مدفوعات: {k.PaidThisMonth:N2} ر.س", $"Payments: SAR {k.PaidThisMonth:N2}"),
            LocaleManager.T($"مبيعات: {k.SalesThisMonth:N2} ر.س", $"Sales: SAR {k.SalesThisMonth:N2}"),
            LocaleManager.T($"مشتريات: {k.PurchasesThisMonth:N2} ر.س", $"Purchases: SAR {k.PurchasesThisMonth:N2}"),
        };

        var list = new StackPanel();
        list.Children.Add(new TextBlock
        {
            Text = LocaleManager.T("النشاط الأخير", "Recent Activity"),
            Style = UiResources.StyleOf(host, "FormSectionTitle"),
            Margin = new Thickness(0, 0, 0, 10),
        });
        foreach (var row in rows)
        {
            list.Children.Add(new Border
            {
                Style = UiResources.StyleOf(host, "ActivityFeedItem"),
                Child = new TextBlock
                {
                    Text = row, FontSize = 13,
                    Foreground = UiResources.BrushOf(host, "TextPrimaryBrush"),
                },
            });
        }

        return new Border { Style = UiResources.StyleOf(host, "GlassPanel"), Child = list };
    }

    public static UIElement BuildPerformanceSummary(FrameworkElement host, DashboardKpis k)
    {
        var revenue = k.SalesThisMonth;
        var expenses = k.PurchasesThisMonth + k.PaidThisMonth;
        var net = k.SalesThisMonth - k.PurchasesThisMonth;
        var wc = k.Receivables - k.Payables;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        void AddMetric(int col, int row, string labelAr, string labelEn, decimal value, string color)
        {
            var sp = new StackPanel { Margin = new Thickness(8) };
            sp.Children.Add(new TextBlock
            {
                Text = LocaleManager.T(labelAr, labelEn),
                FontSize = 11,
                Foreground = UiResources.BrushOf(host, "TextMutedBrush"),
            });
            sp.Children.Add(new TextBlock
            {
                Text = value.ToString("N2"),
                Style = UiResources.StyleOf(host, "TabularText"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!),
                Margin = new Thickness(0, 4, 0, 0),
            });
            Grid.SetColumn(sp, col);
            Grid.SetRow(sp, row);
            grid.Children.Add(sp);
        }

        AddMetric(0, 0, "الإيرادات", "Revenue", revenue, "#22D3EE");
        AddMetric(1, 0, "المصروفات", "Expenses", expenses, "#F87171");
        AddMetric(0, 1, "صافي الربح", "Net Profit", net, "#D4AF37");
        AddMetric(1, 1, "رأس المال العامل", "Working Capital", wc, "#34D399");

        var wrap = new StackPanel();
        wrap.Children.Add(new TextBlock
        {
            Text = LocaleManager.T("ملخص الأداء", "Performance Summary"),
            Style = UiResources.StyleOf(host, "FormSectionTitle"),
            Margin = new Thickness(0, 0, 0, 8),
        });
        wrap.Children.Add(grid);
        return new Border
        {
            Style = UiResources.StyleOf(host, "ChartPanel"),
            Margin = new Thickness(0, 0, 0, 0),
            Child = wrap,
        };
    }

    private static Border BuildAiCard(FrameworkElement host, string text) =>
        new()
        {
            Style = UiResources.StyleOf(host, "AiInsightCard"),
            Margin = new Thickness(0, 0, 0, 8),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = UiResources.BrushOf(host, "TextPrimaryBrush"),
            },
        };
}
