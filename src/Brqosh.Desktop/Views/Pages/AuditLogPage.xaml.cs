using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class AuditLogPage : UserControl
{
    public record LogRow(string OccurredAt, string Username, string Action,
        string EntityType, string EntityKey, string Details);

    private readonly PagingState _paging = new(100);

    public AuditLogPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var query = db.AuditLogEntries.Where(a => a.CompanyId == AppServices.CompanyId);
        _paging.SetTotal(await query.CountAsync());

        var entries = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip(_paging.Skip)
            .Take(_paging.PageSize)
            .ToListAsync();

        var rows = entries.Select(a => new LogRow(
            a.OccurredAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            a.Username, a.Action, a.EntityType, a.EntityKey, a.Details)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        CountText.Text = _paging.StatusText("التسجيلات");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;
        AuditSummaryText.Text =
            $"إجمالي التسجيلات: {_paging.TotalCount:N0} · معروض: {rows.Count:N0}";
    }

    private async void OnPrev(object sender, RoutedEventArgs e)
    {
        _paging.Prev();
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        _paging.Next();
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }
}
