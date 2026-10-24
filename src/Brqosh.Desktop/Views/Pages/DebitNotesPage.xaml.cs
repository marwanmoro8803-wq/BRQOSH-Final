using Brqosh.Desktop.Ui;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;

namespace Brqosh.Desktop.Views.Pages;

public partial class DebitNotesPage : UserControl
{
    public record Row(long Id, string? NoteNumber, DateOnly NoteDate, string SupplierName,
        string Reason, decimal GrandTotal, string StatusAr, DebitNoteStatus Status);

    public DebitNotesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPostAccounting),
                (CancelBtn, PermissionService.CanCancelOrReverse),
                (PrintBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var notes = await db.DebitNotes
            .Where(n => n.CompanyId == AppServices.CompanyId)
            .OrderByDescending(n => n.NoteDate).ThenByDescending(n => n.Id)
            .Take(200)
            .ToListAsync();
        var supplierIds = notes.Select(n => n.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        var rows = notes.Select(n => new Row(
            n.Id, n.NoteNumber, n.NoteDate,
            suppliers.GetValueOrDefault(n.SupplierId, "—"),
            n.Reason, n.GrandTotal(),
            n.Status switch
            {
                DebitNoteStatus.Posted => LocaleManager.T("مرحّل", "Posted"),
                DebitNoteStatus.Cancelled => LocaleManager.T("ملغى", "Cancelled"),
                _ => LocaleManager.T("مسودة", "Draft"),
            },
            n.Status)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var posted = notes.Count(n => n.Status == DebitNoteStatus.Posted);
        NoteSummaryText.Text =
            $"إشعارات: {notes.Count} · مرحّل: {posted} · إجمالي: {notes.Where(n => n.Status == DebitNoteStatus.Posted).Sum(n => n.GrandTotal()):N2} {CurrencyLabel.Short}";
    }

    private async void OnNew(object sender, RoutedEventArgs e)
    {
        if (new DebitNoteDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnPrintPdf(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowInfo("اختر إشعاراً للطباعة.");
            return;
        }

        if (row.Status != DebitNoteStatus.Posted)
        {
            UiFeedback.ShowInfo("الطباعة متاحة للإشعارات المرحّلة فقط.");
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportDebitNoteAsync(AppServices.CompanyId, row.Id, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row)
        {
            UiFeedback.ShowInfo("اختر إشعاراً للإلغاء.");
            return;
        }

        if (row.Status != DebitNoteStatus.Posted)
        {
            UiFeedback.ShowWarning("لا يُلغى إلا إشعار مدين مرحّل.");
            return;
        }

        if (!UiFeedback.Confirm($"إلغاء الإشعار {row.NoteNumber}؟ سيتم عكس القيد وإعادة المخزون."))
            return;

        var cancelDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ الإلغاء", "تاريخ إلغاء الإشعار:");
        if (cancelDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<DebitNoteCancellationService>().CancelAsync(
                AppServices.CompanyId, row.Id, cancelDate.Value, SecurityContext.ResolveUsername());
            await ReloadAsync();
            UiFeedback.ShowSuccess("تم إلغاء الإشعار المدين.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }
}
