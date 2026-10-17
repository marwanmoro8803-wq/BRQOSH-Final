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
        IssueBtn.Content = LocaleManager.T("صرف مواد للمحدد", "Issue materials");
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPostAccounting),
                (ReleaseBtn, PermissionService.CanPostAccounting),
                (IssueBtn, PermissionService.CanPostAccounting),
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
            DomainLabels.Show(WorkOrder.StatusLabel(o.Status)), o.Status, o.CompletionJournalEntryId)).ToList();
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

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٤٧ · <c>manufacturing#4</c>) <b>صرفُ ما خرج فعلاً</b> — كميّةٌ لكلّ خامةٍ في وصفة الأمر،
    /// والمقترحُ كميّةُ الوصفة للمخطَّط؛ والصفرُ يُتخطّى. والأمرُ الذي صُرف له يُكمَل بما صُرف.
    /// </summary>
    private async void OnIssue(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not OrderRow row || row.Status != WorkOrderStatus.Released)
        {
            UiDialog.Show("اختر أمر إنتاج صادراً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var order = await db.WorkOrders.SingleAsync(w => w.Id == row.Id);
            var bom = await db.BillsOfMaterials.SingleAsync(b => b.Id == order.BomId);
            var ids = order.Materials.Select(m => m.ComponentProductId).ToList();
            var names = await db.Products.IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.NameAr);

            var issues = new List<(long ProductId, decimal Quantity)>();
            foreach (var material in order.Materials)
            {
                var suggested = Math.Round(material.Quantity * order.PlannedQuantity / bom.OutputQuantity, 4);
                var prompt = new TextInputDialog(LocaleManager.T("صرف مواد", "Issue materials"),
                    names.GetValueOrDefault(material.ComponentProductId, "—") + LocaleManager.T(" — الكمية المصروفة:", " — quantity issued:"),
                    suggested.ToString("0.####", CultureInfo.InvariantCulture)) { Owner = Window.GetWindow(this) };
                if (prompt.ShowDialog() != true)
                    return;
                if (!NumericInput.TryParse(prompt.Value, out var qty) || qty < 0)
                    throw new Brqosh.Domain.Accounting.AccountingException(LocaleManager.T("الكمية غير صالحة.", "Invalid quantity."));
                if (qty > 0)
                    issues.Add((material.ComponentProductId, qty));
            }

            if (issues.Count == 0)
                return;

            using var facade = new DesktopAccountingFacade();
            var cost = await facade.GetService<WorkOrderService>().IssueMaterialsAsync(
                AppServices.CompanyId, row.Id, issues, SecurityContext.ResolveUsername());
            UiFeedback.ShowInfo(LocaleManager.T("صُرفت الموادّ للأمر بتكلفة ", "Materials issued to the order at a cost of ")
                                + cost.ToString("N2"));
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            // ‏تحذيرٌ لا حوار — فرسالةُ الخدمة نصٌّ مركَّبٌ لا يُبحث في سجلّ الترجمة (حارسُ الحوارات).
            UiFeedback.ShowWarning(ex.Message);
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

        // ‏(ح-١٤٧) ‏التالفُ يُسجَّل فيُقرأ العائد — والصفرُ افتراضاً.
        var scrapDialog = new TextInputDialog(LocaleManager.T("إكمال أمر الإنتاج", "Complete work order"),
            LocaleManager.T("الكمية التالفة (اختياري):", "Scrapped quantity (optional):"), "0") { Owner = Window.GetWindow(this) };
        if (scrapDialog.ShowDialog() != true)
            return;
        if (!NumericInput.TryParse(scrapDialog.Value, out var scrap) || scrap < 0)
        {
            UiDialog.Show("الكمية غير صالحة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // ‏(ح-١٤٨) ‏**دفعةٌ أم إغلاق** — الدفعةُ تُدخل التامَّ المخزونَ ويبقى الأمرُ صادراً يقبل صرفاً ودفعاتٍ أخرى؛
        // ‏والإغلاقُ يُكمل الأمرَ وينقل باقيَ ما صُرف له.
        var close = UiFeedback.Confirm(LocaleManager.T(
            "هل هذه آخرُ دفعة؟ «نعم» يُغلق الأمر، و«لا» يُكمل دفعةً ويُبقيه صادراً.",
            "Is this the last batch? \"Yes\" closes the order; \"No\" completes a batch and keeps it released."));

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            var service = facade.GetService<WorkOrderService>();

            // ‏(جاهزيّةُ الإطلاق · ح-١٠١) **المنتجُ التامُّ المتتبَّعُ يدخل بدفعته** — فتُسأل دفعةُ الإنتاج وصلاحيتُها قبل الإكمال؛
            // ‏والخاماتُ المتتبَّعةُ تُصرف بالأقدم انتهاءً في الخدمة بلا سؤال.
            string? batch = null;
            DateOnly? expiry = null;
            if (await service.OutputIsTrackedAsync(AppServices.CompanyId, row.Id))
            {
                var batchDialog = new TextInputDialog("دفعة الإنتاج", "رقم دفعة الإنتاج:") { Owner = Window.GetWindow(this) };
                if (batchDialog.ShowDialog() != true)
                    return;
                // ‏والفارغُ يمضي إلى الخدمة فترفضه باسم المنتج — لا يُبتلع هنا صامتاً.
                batch = batchDialog.Value;

                var expiryDialog = new TextInputDialog("دفعة الإنتاج", "تاريخ الصلاحية (اختياري، yyyy-MM-dd):")
                    { Owner = Window.GetWindow(this) };
                if (expiryDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(expiryDialog.Value))
                {
                    if (!DateOnly.TryParseExact(expiryDialog.Value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var parsed))
                        throw new Brqosh.Domain.Accounting.AccountingException("تاريخ الصلاحية غير صالح — الصيغة yyyy-MM-dd.");
                    expiry = parsed;
                }
            }

            await service.CompleteAsync(AppServices.CompanyId, row.Id, qty, username,
                outputBatchNumber: batch, outputExpiry: expiry, scrapQuantity: scrap, closeOrder: close);
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
