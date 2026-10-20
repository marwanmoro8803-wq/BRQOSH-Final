using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Workflow;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Microsoft.Extensions.DependencyInjection;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class PurchaseOrdersPage : UserControl
{
    private readonly PagingState _paging = new(100);

    private static readonly WorkflowApproverSlot[] PurchaseOrderChain =
        [WorkflowApproverSlot.Manager, WorkflowApproverSlot.Finance];

    public record OrderRow(long Id, string? OrderNumber, string OrderDate, string SupplierName,
        decimal GrandTotal, string StatusAr, PurchaseOrderStatus Status, string PipelineStep, long? WorkflowInstanceId);

    public PurchaseOrdersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPostAccounting),
                (SubmitBtn, PermissionService.CanPostAccounting),
                (ApproveBtn, PermissionService.CanPostAccounting),
                (RejectBtn, PermissionService.CanPostAccounting),
                (CreateInvoiceBtn, PermissionService.CanPostAccounting),
                (WarehousePanel, PermissionService.CanPostAccounting),
                (CancelOrderBtn, PermissionService.CanCancelOrReverse));
            await UiFeedback.RunSafeAsync(LoadWarehousesAsync);
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private const string DefaultWarehouseChoice = "(المستودع الافتراضي)";

    private async Task LoadWarehousesAsync()
    {
        // ‏(جاهزيّةُ الإطلاق · ح-١١٣ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏**المستودعاتُ الفعّالة — والأوّلُ «الافتراضيّ» (null) فلا يتغيّر شيءٌ لمن لا يختار.**
        await using var db = AppServices.CreateDb();
        var choices = new List<SalesInvoiceDialog.WarehouseOption> { new(null, DefaultWarehouseChoice) };
        choices.AddRange(await db.Warehouses
            .Where(w => w.CompanyId == AppServices.CompanyId && w.IsActive)
            .OrderBy(w => w.Code)
            .Select(w => new SalesInvoiceDialog.WarehouseOption(w.Id, w.Code + " — " + w.NameAr))
            .ToListAsync());
        WarehouseCombo.ItemsSource = choices;
        WarehouseCombo.SelectedIndex = 0;
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var baseQuery = db.PurchaseOrders.Where(o => o.CompanyId == AppServices.CompanyId);
        _paging.SetTotal(await baseQuery.CountAsync());

        var orders = await baseQuery
            .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id)
            .Skip(_paging.Skip)
            .Take(_paging.PageSize)
            .ToListAsync();

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        var wfIds = orders.Where(o => o.WorkflowInstanceId.HasValue)
            .Select(o => o.WorkflowInstanceId!.Value).ToList();
        var workflows = wfIds.Count == 0
            ? new Dictionary<long, WorkflowInstance>()
            : await db.WorkflowInstances
                .Include(w => w.Approvals)
                .Where(w => wfIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id);

        var rows = orders.Select(o =>
        {
            workflows.TryGetValue(o.WorkflowInstanceId ?? 0, out var wf);
            return new OrderRow(
                o.Id, o.OrderNumber, o.OrderDate.ToString("yyyy-MM-dd"),
                suppliers.GetValueOrDefault(o.SupplierId, "—"),
                o.GrandTotal(), StatusLabel(o.Status), o.Status,
                PipelineStepLabel(o.Status, wf), o.WorkflowInstanceId);
        }).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        CountText.Text = _paging.StatusText("الأوامر");
        PrevBtn.IsEnabled = _paging.CanPrev;
        NextBtn.IsEnabled = _paging.CanNext;

        var open = await baseQuery.CountAsync(o =>
            o.Status == PurchaseOrderStatus.Draft || o.Status == PurchaseOrderStatus.Approved);
        var totalValue = await baseQuery.SumGrandTotalAsync();
        OrderSummaryText.Text =
            $"أوامر: {_paging.TotalCount:N0} · مفتوحة: {open:N0} · إجمالي القيمة: {totalValue:N2} ر.س";
    }

    private static string StatusLabel(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => LocaleManager.T("مسودة", "Draft"),
        PurchaseOrderStatus.Submitted => LocaleManager.T("مُرسَل", "Submitted"),
        PurchaseOrderStatus.Approved => LocaleManager.T("معتمد", "Approved"),
        PurchaseOrderStatus.Rejected => LocaleManager.T("مرفوض", "Rejected"),
        PurchaseOrderStatus.Cancelled => LocaleManager.T("ملغاة", "Cancelled"),
        PurchaseOrderStatus.Fulfilled => LocaleManager.T("مُستكمل", "Fulfilled"),
        _ => status.ToString(),
    };

    private static string PipelineStepLabel(PurchaseOrderStatus status, WorkflowInstance? wf) => status switch
    {
        PurchaseOrderStatus.Draft => "—",
        PurchaseOrderStatus.Submitted => wf?.CurrentRequiredSlot(PurchaseOrderChain) switch
        {
            WorkflowApproverSlot.Manager => LocaleManager.T("بانتظار المدير", "Awaiting manager"),
            WorkflowApproverSlot.Finance => LocaleManager.T("بانتظار المالية", "Awaiting finance"),
            _ => wf?.Status == WorkflowInstanceStatus.Approved ? "معتمد" : "مُرسَل",
        },
        PurchaseOrderStatus.Approved => LocaleManager.T("جاهز للتنفيذ", "Ready to fulfil"),
        PurchaseOrderStatus.Rejected => LocaleManager.T("مرفوض", "Rejected"),
        _ => "—",
    };

    private OrderRow? Selected => Grid.SelectedItem as OrderRow;

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

    private async void OnNew(object sender, RoutedEventArgs e)
    {
        if (new PurchaseOrderDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
        {
            _paging.Reset();
            await UiFeedback.RunSafeAsync(ReloadAsync);
        }
    }

    private async void OnView(object sender, RoutedEventArgs e) => await UiFeedback.RunViewSafeAsync(ViewSelectedAsync, "PurchaseOrdersPage.View");

    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e) => await UiFeedback.RunViewSafeAsync(ViewSelectedAsync, "PurchaseOrdersPage.View");

    private async Task ViewSelectedAsync()
    {
        if (Grid.SelectedItem is not OrderRow row)
        {
            UiDialog.Show("اختر أمر شراء من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await using var db = AppServices.CreateDb();
        var order = await db.PurchaseOrders.SingleOrDefaultAsync(o => o.Id == row.Id);
        if (order is null)
        {
            UiFeedback.ShowRecordMissing(LocaleManager.T("لم يعد أمر الشراء هذا موجوداً — حدّث الجدول وحاول مجدداً.", "This purchase order no longer exists — refresh the list and try again."));
            return;
        }
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == order.SupplierId);
        var supplierName = supplier?.NameAr ?? LocaleManager.T("(مورّد غير موجود)", "(supplier not found)");
        var header = $"رقم: {order.OrderNumber ?? "—"}  |  التاريخ: {order.OrderDate:yyyy-MM-dd}  |  المورد: {supplierName}  |  الحالة: {row.StatusAr}";
        new DocumentLinesViewDialog("أمر شراء", header, order.Lines.ToList(),
            order.Subtotal, order.VatAmount, order.GrandTotal()) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private async void OnCancelOrder(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not OrderRow row)
        {
            UiDialog.Show("اختر أمر شراء للإلغاء.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.Status is PurchaseOrderStatus.Fulfilled or PurchaseOrderStatus.Cancelled)
        {
            UiDialog.Show("لا يمكن إلغاء أمر شراء في حالته الحالية.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (UiDialog.Show($"إلغاء أمر الشراء {row.OrderNumber ?? row.Id.ToString()}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<PurchaseOrderService>().CancelAsync(AppServices.CompanyId, row.Id);
            await UiFeedback.RunSafeAsync(ReloadAsync);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnCreateInvoice(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not OrderRow row || row.Status != PurchaseOrderStatus.Approved)
        {
            UiDialog.Show("اختر أمر شراء معتمداً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new TextInputDialog("إنشاء فاتورة مشتريات", "رقم فاتورة المورد:") { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value))
            return;
        var reference = dialog.Value.Trim();

        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var supplier = await db.PurchaseOrders.Where(o => o.Id == row.Id)
                .Join(db.Suppliers, o => o.SupplierId, s => s.Id, (o, s) => s)
                .SingleAsync();

            // ‏(جاهزيّةُ الإطلاق · ح-١١٣ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏**المستودعُ المختار يصل الخدمة (null = الافتراضيّ).**
            var warehouseId = WarehouseCombo.SelectedValue as long?;
            await facade.GetService<PurchaseOrderService>().CreateInvoiceFromOrderAsync(
                AppServices.CompanyId, row.Id, reference,
                DateOnly.FromDateTime(DateTime.Today), supplier.IsVatRegistered, warehouseId: warehouseId);

            UiDialog.Show("تم إنشاء فاتورة المشتريات وترحيلها.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await UiFeedback.RunSafeAsync(ReloadAsync);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnSubmit(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(SubmitAsync);

    private async Task SubmitAsync()
    {
        if (Selected is not { Status: PurchaseOrderStatus.Draft } row)
        {
            UiDialog.Show("اختر أمر شراء في حالة مسودة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await using var db = AppServices.CreateDb();
        var order = await db.PurchaseOrders.SingleAsync(o => o.Id == row.Id);
        using var scope = AppServices.CreateInfrastructureScope();
        await scope.ServiceProvider.GetRequiredService<PurchaseOrderWorkflowService>().SubmitAsync(order);
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnApprove(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ApproveAsync);

    private async Task ApproveAsync()
    {
        if (Selected is not { Status: PurchaseOrderStatus.Submitted } row)
        {
            UiDialog.Show("اختر أمر شراء مُرسَلاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // (CEEF-229 · م1-١٤) الرقمُ لم يعد يُحسب هنا: كان يُخصَّص على سياق الشاشة **بلا
        // معاملة** ثمّ يُمرَّر إلى خدمةٍ تكتب في معاملةٍ أخرى — فيقرأ مستخدمان الرقمَ نفسَه.
        // والخدمةُ تُخصّصه الآن داخل معاملتها حيث يحيا القفل.
        using var scope = AppServices.CreateInfrastructureScope();
        await scope.ServiceProvider.GetRequiredService<PurchaseOrderWorkflowService>()
            .ApproveAsync(row.Id);
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }

    private async void OnReject(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RejectAsync);

    private async Task RejectAsync()
    {
        if (Selected is not { Status: PurchaseOrderStatus.Submitted } row)
        {
            UiDialog.Show("اختر أمر شراء مُرسَلاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var reason = UiDialogs.PromptText("رفض أمر الشراء", "سبب الرفض:");
        if (string.IsNullOrWhiteSpace(reason))
            return;

        using var scope = AppServices.CreateInfrastructureScope();
        await scope.ServiceProvider.GetRequiredService<PurchaseOrderWorkflowService>()
            .RejectAsync(row.Id, reason.Trim());
        await UiFeedback.RunSafeAsync(ReloadAsync);
    }
}
