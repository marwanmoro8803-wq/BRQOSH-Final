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
                $"تدفق نقدي إيجابي هذا الشهر: {netCash:N2} {CurrencyLabel.Short}",
                $"Positive cash flow this month: {CurrencyLabel.Code} {netCash:N2}")
            : LocaleManager.T(
                $"تنبيه: تدفق نقدي سلبي {netCash:N2} {CurrencyLabel.Short}",
                $"Alert: negative cash flow {CurrencyLabel.Code} {netCash:N2}");

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

    /// <summary>
    /// (CEEF-216) لوحة رسم بمحاور وقيم. <paramref name="asBars"/> للمقارنة بين فئات:
    /// الخط يوحي بتدرّج زمنيّ، فرسمُ إيراد/مصروف/ربح كخطّ متصل مضلِّل.
    /// </summary>
    public static UIElement BuildChartPanel(FrameworkElement host, string titleAr, string titleEn,
        IReadOnlyList<ChartPoint> series, string legendAr, string legendEn, bool asBars = false)
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
            Height = 152,
            Background = UiResources.BrushOf(host, "BackgroundBrush"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Child = asBars
                ? ChartBuilder.CreateBars(
                    series, 520, 132,
                    UiResources.BrushOf(host, "ElectricCyanBrush"),
                    UiResources.BrushOf(host, "PrimarySoftBrush"),
                    UiResources.BrushOf(host, "TextPrimaryBrush"),
                    UiResources.BrushOf(host, "TextMutedBrush"))
                : ChartBuilder.CreateLine(
                    series, 520, 132,
                    UiResources.BrushOf(host, "ElectricCyanBrush"),
                    UiResources.BrushOf(host, "PrimarySoftBrush"),
                    UiResources.BrushOf(host, "TextPrimaryBrush"),
                    UiResources.BrushOf(host, "TextMutedBrush")),
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
            LocaleManager.T($"ذمم العملاء {k.Receivables:N0} {CurrencyLabel.Short} — راجع أعمار الديون.", $"AR {CurrencyLabel.Code} {k.Receivables:N0} — review aging."),
            LocaleManager.T($"صافي ضريبة {k.NetVatThisMonth:N2} {CurrencyLabel.Short} هذا الشهر — راجعه قبل الإقرار.", $"Net VAT {CurrencyLabel.Code} {k.NetVatThisMonth:N2} this month — review before filing."),
            LocaleManager.T($"مخزون {k.InventoryValue:N0} {CurrencyLabel.Short} — تحقق من بطء الحركة.", $"Inventory {CurrencyLabel.Code} {k.InventoryValue:N0} — slow movers."),
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = LocaleManager.T("مؤشرات سريعة", "Quick indicators"),
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
            Text = LocaleManager.T("قواعد محاسبية قابلة للشرح على بيانات شركتك — لا نموذج لغوي", "Explainable accounting rules on your company data — not a language model"),
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

    /// <summary>
    /// «ابدأ هنا» — مراحلُ البدء التي لم تكتمل، كلٌّ منها ينقل إلى شاشتها. <c>null</c> إن اكتملت كلُّها أو لم يبقَ ما يستطيعه هذا
    /// الدور (الصرّافُ لا يُطالَب بإكمال بيانات المنشأة). والإنجازُ يُقرأ من الدفاتر (<see cref="OnboardingQuery"/>) لا من علامة.
    /// </summary>
    public static UIElement? BuildGettingStarted(FrameworkElement host, IReadOnlyList<OnboardingStep> steps)
    {
        var mine = steps
            .Where(s => Brqosh.Desktop.Security.PermissionService.CanAccess(AppServices.CurrentUser, s.PageKey))
            .ToList();
        if (mine.Count == 0 || mine.All(s => s.Done))
            return null;

        var done = mine.Count(s => s.Done);
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = LocaleManager.T("ابدأ هنا", "Get started"),
            Style = UiResources.StyleOf(host, "FormSectionTitle"),
            Margin = new Thickness(0, 0, 0, 4),
        });
        stack.Children.Add(new TextBlock
        {
            Text = LocaleManager.T($"{done} من {mine.Count} مراحل مكتملة — تختفي هذه اللوحة حين تكتمل كلّها.",
                $"{done} of {mine.Count} steps complete — this panel disappears when all are done."),
            FontSize = 11,
            Foreground = UiResources.BrushOf(host, "TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 10),
        });

        foreach (var step in mine)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = true };
            var mark = new TextBlock
            {
                Text = step.Done ? "✓" : "○",
                FontWeight = FontWeights.Bold,
                Width = 20,
                Foreground = UiResources.BrushOf(host, step.Done ? "SuccessBrush" : "TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(mark, Dock.Right);
            row.Children.Add(mark);

            if (!step.Done)
            {
                var open = new Button
                {
                    Content = LocaleManager.T("افتح", "Open"),
                    Style = UiResources.StyleOf(host, "SecondaryButton"),
                    Padding = new Thickness(14, 4, 14, 4),
                    Margin = new Thickness(8, 0, 0, 0),
                };
                var key = step.PageKey;
                open.Click += (_, _) => Brqosh.Desktop.Navigation.AppNavigator.Go(key);
                System.Windows.Automation.AutomationProperties.SetAutomationId(open, "GettingStarted_" + step.Code);
                DockPanel.SetDock(open, Dock.Left);
                row.Children.Add(open);
            }

            row.Children.Add(new TextBlock
            {
                Text = LocaleManager.T(step.TitleAr, step.TitleEn),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = UiResources.BrushOf(host, step.Done ? "TextMutedBrush" : "TextPrimaryBrush"),
                TextDecorations = step.Done ? TextDecorations.Strikethrough : null,
            });
            stack.Children.Add(row);
        }

        return new Border { Style = UiResources.StyleOf(host, "GlassPanel"), Child = stack };
    }

    public static UIElement BuildQuickActions(FrameworkElement host)
    {
        // ‏(أسئلةُ المالك 2026-10-24) ‏كانت أربعَ بطاقاتٍ تُرسَم ولا تفعل شيئاً. صارت تفتح شاشاتِها — وبمفتاح الشاشة تمرّ بفحوص
        // ‏الصلاحية والترخيص نفسِها كالقائمة، ولا تظهر لمن لا يملك الصلاحية أصلاً.
        var actions = new (string Ar, string En, string Code, string PageKey)[]
        {
            ("فاتورة مبيعات", "Sales Invoice", "SI", "sales_invoices"),
            ("قيد يومية", "Journal Entry", "JE", "journal_entries"),
            ("سند قبض", "Receipt", "RC", "receipt_vouchers"),
            ("تقرير VAT", "VAT Report", "VT", "vat_return"),
        };

        var wrap = new WrapPanel();
        foreach (var (ar, en, code, pageKey) in actions)
        {
            if (!Brqosh.Desktop.Security.PermissionService.CanAccess(AppServices.CurrentUser, pageKey))
                continue;

            var chip = new Border
            {
                Style = UiResources.StyleOf(host, "QuickActionChip"),
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = true,
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
            };
            var key = pageKey;
            chip.MouseLeftButtonUp += (_, _) => Brqosh.Desktop.Navigation.AppNavigator.Go(key);
            chip.KeyDown += (_, e) =>
            {
                if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space)
                {
                    e.Handled = true;
                    Brqosh.Desktop.Navigation.AppNavigator.Go(key);
                }
            };
            System.Windows.Automation.AutomationProperties.SetName(chip, LocaleManager.T(ar, en));
            System.Windows.Automation.AutomationProperties.SetAutomationId(chip, "QuickAction_" + key);
            wrap.Children.Add(chip);
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
            LocaleManager.T($"تحصيلات: {k.CollectedThisMonth:N2} {CurrencyLabel.Short}", $"Collections: {CurrencyLabel.Code} {k.CollectedThisMonth:N2}"),
            LocaleManager.T($"مدفوعات: {k.PaidThisMonth:N2} {CurrencyLabel.Short}", $"Payments: {CurrencyLabel.Code} {k.PaidThisMonth:N2}"),
            LocaleManager.T($"مبيعات: {k.SalesThisMonth:N2} {CurrencyLabel.Short}", $"Sales: {CurrencyLabel.Code} {k.SalesThisMonth:N2}"),
            LocaleManager.T($"مشتريات: {k.PurchasesThisMonth:N2} {CurrencyLabel.Short}", $"Purchases: {CurrencyLabel.Code} {k.PurchasesThisMonth:N2}"),
        };

        var list = new StackPanel();
        list.Children.Add(new TextBlock
        {
            Text = LocaleManager.T("حركة الشهر", "This month's movement"),
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
        // ‏(أسئلةُ المالك 2026-10-24 · المؤشّرات) ‏**الأرقامُ نفسُها التي في البطاقات أعلاه** — من قائمة الدخل.
        // ‏كان هذا الملخّصُ يحسب الإيرادَ من مبيعات المستندات شاملةً الضريبة، والمصروفَ مشتريات + سدادات (فيزدوج: شراءٌ ثمّ سدادُه)،
        // ‏والربحَ مبيعات − مشتريات — وهي الصيغةُ التي أُسقطت من البطاقات في CEEF-214. فكانت الشاشةُ الواحدةُ تعرض رقمَين لكلّ مؤشّر.
        var revenue = k.RevenueThisMonth;
        var expenses = k.ExpensesThisMonth;
        var net = k.NetProfitThisMonth;
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
                Text = CurrencyLabel.Format(value),
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
