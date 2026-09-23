using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Manufacturing;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Manufacturing;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class WorkOrdersPage : UserControl
{
    public record OrderRow(long Id, string? OrderNumber, string OrderDate, string BomName,
        decimal PlannedQuantity, decimal ProducedQuantity, string StatusAr, WorkOrderStatus Status,
        long? CompletionJournalEntryId);

    public WorkOrdersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPostAccounting),
                (ReleaseBtn, PermissionService.CanPostAccounting),
                (CompleteBtn, PermissionService.CanPostAccounting),
                (CancelBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var orders = await db.WorkOrders
            .Where(w => w.CompanyId == AppServices.CompanyId)
            .OrderByDescending(w => w.OrderDate).ThenByDescending(w => w.Id)
            .Take(200)
            .ToListAsync();

        var bomIds = orders.Select(o => o.BomId).Distinct().ToList();
        var boms = await db.BillsOfMaterials.Where(b => bomIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.NameAr);

        var rows = orders.Select(o => new OrderRow(
            o.Id, o.OrderNumber, o.OrderDate.ToString("yyyy-MM-dd"),
            boms.GetValueOrDefault(o.BomId, "—"),
            o.PlannedQuantity, o.ProducedQuantity,
            WorkOrder.StatusLabel(o.Status), o.Status, o.CompletionJournalEntryId)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        var released = orders.Count(o => o.Status == WorkOrderStatus.Released);
        var completed = orders.Count(o => o.Status == WorkOrderStatus.Completed);
        WorkOrderSummaryText.Text =
            $"أوامر: {orders.Count} · صادر: {released} · مكتمل: {completed}";
    }

    private async void OnNew(object sender, RoutedEventArgs e)
    {
        if (new WorkOrderDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnRelease(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not OrderRow row || row.Status != WorkOrderStatus.Draft)
        {
            UiDialog.Show("اختر أمر إنتاج بحالة مسودة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<WorkOrderService>().ReleaseAsync(AppServices.CompanyId, row.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnComplete(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not OrderRow row || row.Status != WorkOrderStatus.Released)
        {
            UiDialog.Show("اختر أمر إنتاج صادراً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new TextInputDialog("إكمال أمر الإنتاج", "الكمية المنتجة:", row.PlannedQuantity.ToString("N2"))
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;

        if (!NumericInput.TryParse(dialog.Value, out var qty) || qty <= 0)
        {
            UiDialog.Show("الكمية غير صالحة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<WorkOrderService>().CompleteAsync(AppServices.CompanyId, row.Id, qty, username);
            UiDialog.Show("تم إكمال أمر الإنتاج وترحيل المخزون.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnViewJournal(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not OrderRow row)
        {
            UiDialog.Show("اختر أمر إنتاج من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.CompletionJournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            UiDialog.Show("لا يوجد قيد إكمال مرتبط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٥٥ · <c>manufacturing#2</c>) <b>الأمرُ المكتملُ يُلغى من الصفحة — وكانت ترفضه.</b>
    ///
    /// <para><c>WorkOrderService.CancelAsync</c> يعكس المكتملَ منذ أمد: قيدا الإكمال، وحركاتُ المخزون
    /// بطبقاتها بأعيانها (ح-٣٠)، <b>ويرفض إن صُرف شيءٌ من إنتاجه</b>. <b>والصفحةُ كانت تمنعه</b> بـ«اختر
    /// أمر إنتاج مسودة أو صادراً» — فكمّيّةُ إنتاجٍ خاطئةٌ دائمة، لا تُصحَّح إلّا بقيدٍ يدويٍّ يُفسد الطبقات.
    /// <b>والتصحيحُ الآن: يُلغى المكتملُ فتُعكس آثارُه، ثمّ يُصدَر أمرٌ بالكمّيّة الصحيحة.</b></para>
    /// </summary>
    internal static bool CanCancel(WorkOrderStatus status) => status is not WorkOrderStatus.Cancelled;

    /// <summary>(ح-٥٥) <b>إلغاءُ المكتمل عكسٌ لمرحَّل</b> — فيُسأل عنه بتأكيدٍ يقول ذلك.</summary>
    internal static bool ReversesPostings(WorkOrderStatus status) => status is WorkOrderStatus.Completed;

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not OrderRow row || !CanCancel(row.Status))
        {
            UiDialog.Show("اختر أمر إنتاج لم يُلغَ.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ReversesPostings(row.Status))
        {
            if (UiDialog.Show("أمر الإنتاج مكتمل — سيُعكس قيدا إكماله وحركات مخزونه بتاريخ اليوم، ويُرفض العكس إن صُرف شيء من إنتاجه. ثم أصدِر أمراً بالكمية الصحيحة. هل تريد المتابعة؟",
                    "BRQOSH", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
        }
        else if (UiDialog.Show($"إلغاء أمر الإنتاج {row.OrderNumber ?? row.Id.ToString()}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<WorkOrderService>().CancelAsync(AppServices.CompanyId, row.Id, username);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
