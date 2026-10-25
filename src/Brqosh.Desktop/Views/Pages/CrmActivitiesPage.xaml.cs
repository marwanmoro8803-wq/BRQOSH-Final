using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Crm;
using Brqosh.Desktop.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class CrmActivitiesPage : UserControl
{
    public record ActivityRow(long Id, string TypeLabel, string Subject, string DueAt, string Notes);

    public CrmActivitiesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser, (AddBtn, PermissionService.CanPost));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var rows = await db.CrmActivities
            .Where(a => a.CompanyId == AppServices.CompanyId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(200)
            .ToListAsync();
        var view = rows.Select(a => new ActivityRow(
            a.Id,
            TypeLabel(a.ActivityType),
            a.Subject,
            a.DueAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—",
            a.Notes.Length > 80 ? a.Notes[..80] + "…" : a.Notes)).ToList();
        Grid.ItemsSource = view;
        UiTestAutomation.PublishGridCount(Grid, view.Count, pageKey: "crm_activities");
    }

    private static string TypeLabel(CrmActivityType type) => type switch
    {
        CrmActivityType.Call => LocaleManager.T("مكالمة", "Call"),
        CrmActivityType.Meeting => LocaleManager.T("اجتماع", "Meeting"),
        CrmActivityType.Note => LocaleManager.T("ملاحظة", "Note"),
        CrmActivityType.Email => LocaleManager.T("بريد", "Email"),
        _ => type.ToString()
    };

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new CrmActivityDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }
}
