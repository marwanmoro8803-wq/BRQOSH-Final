using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Hospitality;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) <b>الغرف — والصيانةُ وحدَها تُضبط يدوياً.</b>
///
/// <para><b>والإشغالُ يقوده الحجز</b>: النزولُ يشغل الغرفةَ والمغادرةُ تُخليها. <b>وزرُّ
/// «اشغل الغرفة» يفتح بابَ إشغالٍ بلا حجز</b> — فتُعطَّل غرفةٌ بلا نزيلٍ ولا إيراد،
/// ولا شيءَ يُخليها. فليس هنا إلا تبديلُ الصيانة، <b>والنطاقُ يرفضه على غرفةٍ مشغولة</b>.</para>
/// </summary>
public partial class RoomsPage : UserControl
{
    public record Row(long Id, string RoomNumber, string Kind, int Floor, decimal NightlyRate,
        string Status, bool IsOccupied, bool IsMaintenance);

    public RoomsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (MaintenanceBtn, PermissionService.CanPost));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();

        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => r.CompanyId == AppServices.CompanyId)
            .OrderBy(r => r.RoomNumber)
            .ToListAsync();

        var rows = rooms.Select(r => new Row(r.Id, r.RoomNumber, KindText(r.Kind), r.Floor,
            r.NightlyRate, StatusText(r.Status),
            r.Status == RoomStatus.Occupied, r.Status == RoomStatus.Maintenance)).ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var occupied = rooms.Count(r => r.Status == RoomStatus.Occupied);
        var maintenance = rooms.Count(r => r.Status == RoomStatus.Maintenance);

        SummaryText.Text = rooms.Count == 0
            ? "لا غرفَ بعد."
            : $"غرف: {rooms.Count:N0} · مشغولة: {occupied:N0} · صيانة: {maintenance:N0} · "
              + $"الإشغال: {(decimal)occupied * 100m / rooms.Count:N1}٪";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new RoomDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnToggleMaintenance(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;

        try
        {
            await using var db = AppServices.CreateDb();
            var room = await db.Rooms.SingleAsync(r => r.Id == row.Id);

            if (room.Status == RoomStatus.Maintenance) room.LeaveMaintenance();
            else room.EnterMaintenance();

            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string KindText(RoomKind kind) => kind switch
    {
        RoomKind.Single => "مفردة",
        RoomKind.Double => "مزدوجة",
        RoomKind.Twin => "بسريرين",
        RoomKind.Suite => "جناح",
        RoomKind.Hall => "قاعة",
        _ => kind.ToString(),
    };

    private static string StatusText(RoomStatus status) => status switch
    {
        RoomStatus.Vacant => "شاغرة",
        RoomStatus.Occupied => "مشغولة",
        RoomStatus.Maintenance => "صيانة",
        _ => status.ToString(),
    };
}
