using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Views;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class PosSessionPage : UserControl
{
    public record ProductRow(long Id, string Code, string NameAr, string Barcode, decimal SalePrice);

    public class CartRow : Brqosh.Desktop.Ui.NotifyingRow
    {
        public long ProductId { get; init; }
        public string NameAr { get; init; } = "";
        private decimal _quantity;

        public decimal Quantity
        {
            get => _quantity;
            set => Set(ref _quantity, value, [nameof(LineTotal)]);
        }

        public decimal UnitPrice { get; init; }
        public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    }

    private readonly ObservableCollection<CartRow> _cart = [];
    private List<ProductRow> _products = [];

    public PosSessionPage()
    {
        InitializeComponent();
        CartGrid.ItemsSource = _cart;
        UiTestAutomation.PublishGridCount(CartGrid, _cart.Count);
        PaymentCombo.SelectedIndex = 0;
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (PayBtn, PermissionService.CanPost),
                (ClearCartBtn, PermissionService.CanPost));
            
            await UiFeedback.RunSafeAsync(EnsureShiftAsync);
            await UiFeedback.RunSafeAsync(LoadProductsAsync);
            UpdateTotal();
        };
    }

    private async Task EnsureShiftAsync()
    {
        if (AppServices.CurrentShiftId is long sid)
        {
            await using var db = AppServices.CreateDb();
            var existing = await db.PosShifts.FirstOrDefaultAsync(s =>
                s.Id == sid && s.CompanyId == AppServices.CompanyId && s.Status == PosShiftStatus.Open);
            if (existing is not null)
            {
                ShiftText.Text = $"وردية مفتوحة — {existing.CashierName}";
                return;
            }
        }

        var cashier = AppServices.CurrentUser?.DisplayNameAr ?? AppServices.CurrentUser?.Username ?? "كاشير";
        var shift = PosShift.Open(AppServices.CompanyId, cashier, 0m);
        if (AppServices.CurrentBranchId is long branchId)
            shift.AssignBranch(branchId);
        await using var db2 = AppServices.CreateDb();
        db2.PosShifts.Add(shift);
        await db2.SaveChangesAsync();
        AppServices.CurrentShiftId = shift.Id;
        ShiftText.Text = $"وردية جديدة — {shift.CashierName}";
    }

    private async Task LoadProductsAsync()
    {
        await using var db = AppServices.CreateDb();
        _products = await db.Products
            .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new ProductRow(p.Id, p.Code, p.NameAr, p.Barcode, p.SalePrice))
            .ToListAsync();
        RenderProducts(SearchBox.Text.Trim());
        UpdateTotal();
    }

    private void RenderProducts(string filter)
    {
        var visible = string.IsNullOrEmpty(filter)
            ? _products
            : _products.Where(p =>
                p.NameAr.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Code.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Barcode.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        ProductGrid.ItemsSource = visible.Take(50).ToList();
        UiTestAutomation.PublishGridCount(ProductGrid, visible.Take(50).ToList().Count());
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RenderProducts(SearchBox.Text.Trim());

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        var barcode = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(barcode))
            return;

        var product = _products.FirstOrDefault(p =>
            string.Equals(p.Barcode, barcode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Code, barcode, StringComparison.OrdinalIgnoreCase));

        if (product is null)
        {
            UiFeedback.ShowWarning("لم يُعثر على صنف بهذا الباركود أو الكود.");
            return;
        }

        AddToCart(product);
        SearchBox.Clear();
        RenderProducts("");
    }

    private void OnProductDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProductGrid.SelectedItem is ProductRow product)
            AddToCart(product);
    }

    private void AddToCart(ProductRow product)
    {
        var existing = _cart.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing is not null)
            existing.Quantity += 1m;
        else
            _cart.Add(new CartRow { ProductId = product.Id, NameAr = product.NameAr, Quantity = 1m, UnitPrice = product.SalePrice });

        CartGrid.Items.Refresh();
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        var total = _cart.Sum(c => c.LineTotal);
        TotalText.Text = $"{LocaleManager.T("الإجمالي", "Total")}: {total:N2} {CurrencyLabel.Short}";
        PosSessionSummaryText.Text =
            $"أصناف متاحة: {_products.Count} · بنود السلة: {_cart.Count} · الإجمالي: {total:N2} {CurrencyLabel.Short}";
        UiTestAutomation.PublishGridCount(CartGrid, _cart.Count);
    }

    private void OnClearCart(object sender, RoutedEventArgs e)
    {
        _cart.Clear();
        UpdateTotal();
    }

    private PosPaymentMethod SelectedPayment() => PaymentCombo.SelectedIndex switch
    {
        1 => PosPaymentMethod.Card,
        2 => PosPaymentMethod.Mixed,
        _ => PosPaymentMethod.Cash,
    };

    private async void OnPay(object sender, RoutedEventArgs e)
    {
        // (CEEF-229 · م0-١٦) ضغطة مزدوجة كانت تُنشئ مستندين — الزرّ لا يُعطَّل أثناء الانتظار.
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        if (_cart.Count == 0)
        {
            UiDialog.Show("السلة فارغة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (AppServices.CurrentShiftId is not long shiftId)
        {
            await EnsureShiftAsync();
            shiftId = AppServices.CurrentShiftId!.Value;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var company = await db.Companies.SingleAsync(c => c.Id == AppServices.CompanyId);
            var username = SecurityContext.ResolveUsername();
            var payment = SelectedPayment();

            var lines = _cart.Select(c => (c.ProductId, c.Quantity)).ToList();
            var result = await facade.GetService<PosSaleService>().SellAsync(
                AppServices.CompanyId, shiftId, null, lines,
                company.VatRegistrationNumber.Length == 15, payment, username);

            // ‏(ح-١٢٣) ‏الإيصالُ من النموذج المقيس — بأصنافه ورقمِ تسجيل البائع ووقته — ويُطبع من نافذته.
            var receipt = new PosReceiptWindow(
                PosReceiptBuilder.Build(result.Invoice, company, result.ZatcaDocument),
                StatusOf(result.ZatcaDocument, result.ZatcaMessage))
            { Owner = Window.GetWindow(this) };
            receipt.ShowDialog();

            _cart.Clear();
            UpdateTotal();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٢٣) <b>آخرُ إيصالٍ في ورديتي — إيصالاً يُطبع لا PDF بمقاس A4.</b>
    /// ‏وكان يأخذ آخرَ فاتورة نقطة بيعٍ في المنشأة كلِّها، فيطبع كاشيرٌ إيصالَ زميله.
    /// </summary>
    private async void OnPrintLast(object sender, RoutedEventArgs e)
    {
        try
        {
            if (AppServices.CurrentShiftId is not long shiftId)
            {
                UiDialog.Show("لا توجد وردية مفتوحة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await using var db = AppServices.CreateDb();
            var receipt = await PosReceiptBuilder.LastOfShiftAsync(db, AppServices.CompanyId, shiftId);
            if (receipt is null)
            {
                UiDialog.Show("لا توجد مبيعات في هذه الوردية.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            new PosReceiptWindow(receipt, null) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>سطرُ حالة الهيئة تحت الإيصال — كما كان يُعرض (ح-٥٤): رسالةُ الإرسال، أو الحالةُ بالعربيّة، أو أنّ التكامل غير مفعّل.</summary>
    private static string StatusOf(ZatcaEInvoiceDocument? zatca, string? message) =>
        zatca is null
            ? LocaleManager.T("ZATCA غير مفعّلة — فاتورة محلية فقط", "ZATCA is not enabled — local invoice only")
            : message ?? zatca.ComplianceStatus.NameAr();
}
