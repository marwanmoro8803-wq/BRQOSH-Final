using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Controls;

/// <summary>خيار في قائمة الحالة: نص معروض وقيمة (null = الكل).</summary>
public sealed record ListFilterOption(string Label, object? Value);

/// <summary>
/// شريط البحث والفلترة المشترك للقوائم. لا يعرف قاعدة البيانات: يجمع المعايير ويطلق
/// <see cref="FilterChanged"/>، والصفحة تمرّرها لنموذجها وتعيد التحميل.
/// </summary>
public partial class ListFilterBar : UserControl
{
    private readonly SearchDebouncer _debouncer;
    private bool _suppress;

    /// <summary>يُطلق بعد تغيير المستخدم لأي معيار (البحث مؤجَّل 300ms).</summary>
    public event EventHandler? FilterChanged;

    public ListFilterBar()
    {
        InitializeComponent();
        _debouncer = new SearchDebouncer(TimeSpan.FromMilliseconds(300), () =>
        {
            Raise();
            return Task.CompletedTask;
        });
    }

    public string SearchText => SearchBox.Text.Trim();

    public object? SearchToolTip
    {
        get => SearchBox.ToolTip;
        set => SearchBox.ToolTip = value;
    }

    public bool ShowStatus
    {
        get => StatusPanel.Visibility == Visibility.Visible;
        set => StatusPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public bool ShowDateRange
    {
        get => DatePanel.Visibility == Visibility.Visible;
        set => DatePanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>القيمة المختارة في الحالة، أو null للكل أو إن لم تُضبط خيارات.</summary>
    public object? SelectedStatusValue => (StatusCombo.SelectedItem as ListFilterOption)?.Value;

    public DateOnly? FromDate => FromPicker.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null;

    public DateOnly? ToDate => ToPicker.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null;

    public bool HasCriteria =>
        SearchText.Length > 0 || SelectedStatusValue is not null || FromDate is not null || ToDate is not null;

    /// <summary>يضبط خيارات الحالة ويختار الأول بلا إطلاق FilterChanged.</summary>
    public void SetStatusOptions(IEnumerable<ListFilterOption> options)
    {
        _suppress = true;
        try
        {
            StatusCombo.ItemsSource = options.ToList();
            StatusCombo.SelectedIndex = 0;
            ShowStatus = true;
        }
        finally
        {
            _suppress = false;
        }
    }

    /// <summary>يمسح كل المعايير ويطلق FilterChanged مرة واحدة.</summary>
    public void Clear()
    {
        _suppress = true;
        try
        {
            SearchBox.Text = string.Empty;
            if (StatusCombo.Items.Count > 0) StatusCombo.SelectedIndex = 0;
            FromPicker.SelectedDate = null;
            ToPicker.SelectedDate = null;
        }
        finally
        {
            _suppress = false;
        }

        Raise();
    }

    private void Raise()
    {
        if (!_suppress) FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_suppress) _debouncer.Trigger();
    }

    private void OnStatusChanged(object sender, SelectionChangedEventArgs e) => Raise();

    private void OnDateChanged(object? sender, SelectionChangedEventArgs e) => Raise();

    private void OnClear(object sender, RoutedEventArgs e) => Clear();
}
