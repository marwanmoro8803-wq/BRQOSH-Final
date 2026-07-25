using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brqosh.Desktop.Ui;

public static class EmptyStateHelper
{
    private const string OverlayTag = "BrqoshEmptyStateOverlay";

    public static void Sync(DataGrid grid, int itemCount, EmptyStateSpec? spec = null, string? pageKey = null)
    {
        try
        {
            var host = grid as FrameworkElement ?? throw new InvalidOperationException("Grid host required.");
            spec ??= EmptyStateCatalog.Resolve(grid.Name, pageKey);
            var panelHost = EnsureHost(grid);
            var overlay = GetOrCreateOverlay(host, panelHost, spec);
            overlay.Visibility = itemCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            grid.Visibility = itemCount == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        catch
        {
            // الحالة الفارغة اختيارية — لا تُعطّل الشاشة
            grid.Visibility = Visibility.Visible;
        }
    }

    private static Grid EnsureHost(DataGrid grid)
    {
        if (grid.Tag is Grid existingHost)
            return existingHost;

        return grid.Parent switch
        {
            Border border => WrapBorderChild(border, grid),
            Grid parentGrid => WrapGridChild(parentGrid, grid),
            DockPanel dockPanel => WrapDockChild(dockPanel, grid),
            Panel panel => WrapPanelChild(panel, grid),
            _ => throw new InvalidOperationException($"Cannot attach empty state to grid {grid.Name}."),
        };
    }

    private static Grid WrapBorderChild(Border border, DataGrid grid)
    {
        var host = new Grid();
        border.Child = host;
        host.Children.Add(grid);
        grid.Tag = host;
        return host;
    }

    private static Grid WrapGridChild(Grid parentGrid, DataGrid grid)
    {
        var host = new Grid();
        var row = Grid.GetRow(grid);
        var col = Grid.GetColumn(grid);
        var rowSpan = Grid.GetRowSpan(grid);
        var colSpan = Grid.GetColumnSpan(grid);

        parentGrid.Children.Remove(grid);
        host.Children.Add(grid);
        parentGrid.Children.Add(host);

        Grid.SetRow(host, row);
        Grid.SetColumn(host, col);
        Grid.SetRowSpan(host, rowSpan);
        Grid.SetColumnSpan(host, colSpan);

        grid.Tag = host;
        return host;
    }

    private static Grid WrapDockChild(DockPanel dockPanel, DataGrid grid)
    {
        var host = new Grid();
        var dock = DockPanel.GetDock(grid);

        dockPanel.Children.Remove(grid);
        host.Children.Add(grid);
        dockPanel.Children.Add(host);
        DockPanel.SetDock(host, dock);

        grid.Tag = host;
        return host;
    }

    private static Grid WrapPanelChild(Panel panel, DataGrid grid)
    {
        var host = new Grid();
        var index = panel.Children.IndexOf(grid);

        panel.Children.RemoveAt(index);
        host.Children.Add(grid);
        panel.Children.Insert(index, host);

        grid.Tag = host;
        return host;
    }

    private static Border GetOrCreateOverlay(FrameworkElement host, Grid panelHost, EmptyStateSpec spec)
    {
        foreach (var child in panelHost.Children)
        {
            if (child is Border b && OverlayTag.Equals(b.Tag))
                return b;
        }

        var badge = new Border
        {
            Style = UiResources.StyleOf(host, "KpiMonogramBadge"),
            Background = UiResources.BrushOf(host, "PrimarySoftBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = spec.Monogram,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = UiResources.BrushOf(host, "PrimaryDarkBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var overlay = new Border
        {
            Tag = OverlayTag,
            Style = UiResources.StyleOf(host, "EmptyStatePanel"),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    badge,
                    new TextBlock
                    {
                        Text = spec.Title,
                        Style = UiResources.StyleOf(host, "EmptyStateTitle"),
                        Margin = new Thickness(0, 16, 0, 0),
                    },
                    new TextBlock
                    {
                        Text = spec.Subtitle,
                        Style = UiResources.StyleOf(host, "EmptyStateSubtitle"),
                        Margin = new Thickness(0, 8, 0, 0),
                        MaxWidth = 420,
                    },
                },
            },
        };

        panelHost.Children.Add(overlay);
        return overlay;
    }
}
