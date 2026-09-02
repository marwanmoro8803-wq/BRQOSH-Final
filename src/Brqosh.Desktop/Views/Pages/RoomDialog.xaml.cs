using System.Globalization;
using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>(CEEF-230 · ط6-٤) إنشاءُ غرفة — وتولد شاغرةً، والحجزُ وحدَه يشغلها.</summary>
public partial class RoomDialog : Window
{
    public RoomDialog() => InitializeComponent();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var room = Room.Create(AppServices.CompanyId, NumberBox.Text,
                (RoomKind)Math.Max(0, KindBox.SelectedIndex),
                Number(RateBox.Text, "سعر الليلة"),
                (int)Number(FloorBox.Text, "الدور"));

            await using var db = AppServices.CreateDb();
            db.Rooms.Add(room);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal Number(string text, string field) =>
        decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new AccountingException($"قيمة «{field}» غير صالحة — ووردت «{text}».");

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
