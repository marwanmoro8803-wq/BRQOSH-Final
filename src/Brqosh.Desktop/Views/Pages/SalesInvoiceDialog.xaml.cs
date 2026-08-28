using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class SalesInvoiceDialog : Window
{
    public record CustomerOption(long Id, string Display);

    /// <summary>
    /// (CEEF-229 · م0-١٧) صفٌّ مُشعِر — يُستعمل في فواتير البيع والشراء وإشعارَي الدائن والمدين.
    /// بدون الإشعار كان عمود «الإجمالي» يبقى 0.00 بينما ملخّص الفاتورة صحيح، فيظنّ المستخدم
    /// أن إدخاله لم يُحفَظ.
    /// </summary>
    public class LineRow : Brqosh.Desktop.Ui.NotifyingRow
    {
        private decimal _quantity;
        private decimal _unitPrice;

        public long? ProductId { get; set; }
        public string? Description { get; set; }

        public decimal Quantity
        {
            get => _quantity;
            set => Set(ref _quantity, value, [nameof(LineTotal)]);
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set => Set(ref _unitPrice, value, [nameof(LineTotal)]);
        }

        /// <summary>الفئة الضريبية للسطر — الافتراضي أساسية 15% (CEEF-208).</summary>
        public Brqosh.Domain.Tax.VatCategory VatCategory { get; set; }
            = Brqosh.Domain.Tax.VatCategory.Standard;
        public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    }

    private readonly ObservableCollection<LineRow> _rows = [];
    private List<ProductOption> _products = [];

    public record ProductOption(long Id, string Display, decimal SalePrice, string NameAr);

    public SalesInvoiceDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) =>
        {
            HeroSubtitleText.Text = LocaleManager.T(
                "مسار ذهبي: بيع → ZATCA → تحصيل — Financial Truth First.",
                "Golden path: Sell → ZATCA → Collect — Financial Truth First.");
            await LoadCustomersAsync();
            await LoadProductsAsync();
            await LoadZatcaPanelAsync();
        };
    }

    private async Task LoadZatcaPanelAsync()
    {
        await using var db = AppServices.CreateDb();
        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == AppServices.CompanyId);
        if (settings is { IsEnabled: true })
        {
            ZatcaStatusText.Text = LocaleManager.T(
                $"مفعّل · البيئة: {settings.Environment}",
                $"Enabled · Environment: {settings.Environment}");
            ZatcaHintText.Text = LocaleManager.T(
                "UUID · QR · UBL XML — يُولَّد تلقائياً عند الترحيل.",
                "UUID · QR · UBL XML — generated automatically on post.");
        }
        else
        {
            ZatcaStatusText.Text = LocaleManager.T("ZATCA غير مفعّل", "ZATCA not enabled");
            ZatcaHintText.Text = LocaleManager.T(
                "فعّل من شاشة إعدادات ZATCA للفوترة الإلكترونية.",
                "Enable from ZATCA settings for e-invoicing.");
        }
    }

    private async Task LoadProductsAsync()
    {
        await using var db = AppServices.CreateDb();
        _products = await db.Products.Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new ProductOption(p.Id, $"{p.Code} — {p.NameAr}", p.SalePrice, p.NameAr))
            .ToListAsync();
    }

    private async Task LoadCustomersAsync()
    {
        await using var db = AppServices.CreateDb();
        var customers = await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.NameAr)
            .Select(c => new CustomerOption(c.Id, c.NameAr))
            .ToListAsync();
        CustomerCombo.ItemsSource = customers;
        if (customers.Count > 0)
            CustomerCombo.SelectedIndex = 0;
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(UpdateTotals);

    private void UpdateTotals()
    {
        var subtotal = _rows.Sum(r => r.LineTotal);
        var vat = Math.Round(subtotal * SalesInvoice.SaudiVatRate, 2, MidpointRounding.AwayFromZero);
        SubtotalText.Text = subtotal.ToString("N2");
        VatText.Text = vat.ToString("N2");
        TotalText.Text = (subtotal + vat).ToString("N2");
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        // (CEEF-229 · م0-١٦) ضغطة مزدوجة كانت تُنشئ مستندين — الزرّ لا يُعطَّل أثناء الانتظار.
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (DatePickerBox.SelectedDate is null)
                throw new AccountingException("تاريخ الفاتورة مطلوب.");
            if (CustomerCombo.SelectedValue is not long customerId)
                throw new AccountingException("اختر العميل.");

            var priceWarnings = _rows
                .Where(r => r.ProductId is long pid && _products.FirstOrDefault(p => p.Id == pid) is { SalePrice: > 0 } product
                    && r.UnitPrice != product.SalePrice
                    && Math.Abs(r.UnitPrice - product.SalePrice) / product.SalePrice > 0.01m)
                .Select(r =>
                {
                    var p = _products.First(x => x.Id == r.ProductId);
                    return $"{p.NameAr}: {r.UnitPrice:N2} (الافتراضي {p.SalePrice:N2})";
                })
                .ToList();

            if (priceWarnings.Count > 0 &&
                MessageBox.Show(
                    "أسعار تختلف عن سعر البيع الافتراضي:\n" + string.Join("\n", priceWarnings) + "\n\nمتابعة؟",
                    "BRQOSH", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var invoice = SalesInvoice.CreateDraft(
                AppServices.CompanyId, customerId,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value));

            foreach (var row in _rows.Where(r => !string.IsNullOrWhiteSpace(r.Description) || r.Quantity != 0 || r.UnitPrice != 0))
                invoice.AddLine(row.Description ?? "", row.Quantity, row.UnitPrice, row.ProductId, row.VatCategory);

            using var facade = new DesktopAccountingFacade();
            await facade.PostSalesInvoiceAsync(invoice);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
