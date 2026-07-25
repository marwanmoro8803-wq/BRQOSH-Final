using System.Windows;
using System.Windows.Controls;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// يصلّح توزيع أعمدة DataGrid في الواجهة العربية: محرك العرض LTR + ترتيب أعمدة معكوس للقراءة RTL.
/// بدون ذلك، عمود Width="*" في نهاية التعريف يمتص العرض ويُحدث فراغاً بين مجموعات الأعمدة.
/// </summary>
public static class DataGridRtlHelper
{
    public static readonly DependencyProperty EnableLayoutProperty =
        DependencyProperty.RegisterAttached(
            "EnableLayout",
            typeof(bool),
            typeof(DataGridRtlHelper),
            new PropertyMetadata(false, OnEnableLayoutChanged));

    private static readonly DependencyProperty AppliedProperty =
        DependencyProperty.RegisterAttached(
            "Applied",
            typeof(bool),
            typeof(DataGridRtlHelper),
            new PropertyMetadata(false));

    public static bool GetEnableLayout(DependencyObject obj) => (bool)obj.GetValue(EnableLayoutProperty);

    public static void SetEnableLayout(DependencyObject obj, bool value) => obj.SetValue(EnableLayoutProperty, value);

    /// <summary>يعكس ترتيب الأعمدة لعرض LTR مع قراءة RTL (للاختبار).</summary>
    public static IReadOnlyList<T> ReverseColumnsForRtl<T>(IReadOnlyList<T> columns) =>
        columns.Count <= 1 ? columns : columns.Reverse().ToList();

    public static void ApplyLayout(DataGrid grid)
    {
        if ((bool)grid.GetValue(AppliedProperty))
            return;

        grid.SetValue(AppliedProperty, true);
        grid.FlowDirection = FlowDirection.LeftToRight;
        grid.HorizontalAlignment = HorizontalAlignment.Stretch;

        if (grid.Columns.Count <= 1)
            return;

        var reversed = ReverseColumnsForRtl(grid.Columns.ToList());
        grid.Columns.Clear();
        foreach (var column in reversed)
            grid.Columns.Add(column);
    }

    private static void OnEnableLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
            return;

        if ((bool)e.NewValue)
            grid.Loaded += OnGridLoaded;
        else
            grid.Loaded -= OnGridLoaded;
    }

    private static void OnGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            ApplyLayout(grid);
    }
}
