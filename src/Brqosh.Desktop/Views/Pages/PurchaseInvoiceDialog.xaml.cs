using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class PurchaseInvoiceDialog : Window
{
    public record SupplierOption(long Id, bool IsVatRegistered, string Display);

    private const string DefaultWarehouseChoice = "(المستودع الافتراضي)";

    private readonly ObservableCollection<SalesInvoiceDialog.LineRow> _rows = [];
    private List<SalesInvoiceDialog.ProductOption> _products = [];

    public PurchaseInvoiceDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) =>
        {
            await LoadSuppliersAsync(); await LoadWarehousesAsync(); await LoadProjectsAsync(); await LoadProductsAsync();
        };
    }

    private async Task LoadProjectsAsync()
    {
        // ‏(جاهزيّةُ الإطلاق · ح-١٤٤ · ‏`sector-modules#9`) ‏المشاريعُ التي تقبل تكلفة — والأوّلُ «بلا مشروع».
        await using var db = AppServices.CreateDb();
        var choices = new List<SalesInvoiceDialog.WarehouseOption> { new(null, LocaleManager.T("(بلا مشروع)", "(No project)")) };
        choices.AddRange(await db.ConstructionProjects
            .Where(p => p.CompanyId == AppServices.CompanyId
                        && p.Status != Domain.Construction.ConstructionProjectStatus.Completed
                        && p.Status != Domain.Construction.ConstructionProjectStatus.Cancelled)
            .OrderBy(p => p.Code)
            .Select(p => new SalesInvoiceDialog.WarehouseOption(p.Id, p.Code + " — " + p.NameAr))
            .ToListAsync());
        ProjectCombo.ItemsSource = choices;
        ProjectCombo.SelectedIndex = 0;
    }

    private async Task LoadProductsAsync()
    {
        await using var db = AppServices.CreateDb();
        _products = await db.Products.Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new SalesInvoiceDialog.ProductOption(p.Id, $"{p.Code} — {p.NameAr}", p.SalePrice, p.NameAr))
            .ToListAsync();
        ProductColumn.ItemsSource = _products;   // (م2-٢٠) بلا هذا يبقى العمود فارغاً
    }

    private async Task LoadWarehousesAsync()
    {
        // ‏(جاهزيّةُ الإطلاق · ح-١١١ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏المستودعاتُ القائمة — والأوّلُ «الافتراضيّ».
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

    private async Task LoadSuppliersAsync()
    {
        await using var db = AppServices.CreateDb();
        var suppliers = await db.Suppliers
            .Where(s => s.CompanyId == AppServices.CompanyId && s.IsActive)
            .OrderBy(s => s.NameAr)
            .ToListAsync();
        SupplierCombo.ItemsSource = suppliers
            .Select(s => new SupplierOption(s.Id, s.IsVatRegistered, s.NameAr))
            .ToList();
        // (ح-٦١) لا طرفَ يُختار سلفاً — «Enter» وأوّلُ اسمٍ أبجديّاً كانا يُرحّلان لمن لم يُقصد.
    }

    private void OnSupplierSelected(object sender, SelectionChangedEventArgs e)
    {
        // مورد غير مسجل ⇒ لا خصم لضريبة المدخلات
        if (SupplierCombo.SelectedItem is SupplierOption option)
        {
            DeductibleCheck.IsChecked = option.IsVatRegistered;
            DeductibleCheck.IsEnabled = option.IsVatRegistered;
        }
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(UpdateTotals);

    private void UpdateTotals()
    {
        // ‏(ح-٦٠) ‏**والمعروضُ هو المُقيَّد ذمّةً للمورّد** — وكان يضرب الوعاءَ كلَّه في النسبة
        // ‏الأساسية فيُحمِّل السطرَ المعفى ضريبةً لا يُطالِب بها المورّد.
        var (subtotal, vat) = SalesInvoiceDialog.TotalsOf(_rows, documentDiscount: 0m);
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
            if (SupplierCombo.SelectedValue is not long supplierId)
                throw new AccountingException("اختر المورد.");

            var invoice = BuildInvoiceFromRows(
                AppServices.CompanyId, supplierId,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value),
                ReferenceBox.Text,
                DeductibleCheck.IsChecked == true);

            using var facade = new DesktopAccountingFacade();
            await facade.PostPurchaseInvoiceAsync(invoice);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }


    /// <summary>
    /// (CEEF-229 · م2-٢٦) الوصلة بين شبكة الشراء والنطاق — مُستخرَجةٌ كي تُقاس، كما في
    /// فاتورة المبيعات. وهذا الجانب أثقل: الشراء هو ما <b>يُدخل</b> البضاعة المستودعَ، فبضياع
    /// <c>ProductId</c> هنا لا يدخل شيء، ويصطدم أوّلُ بيعٍ بحارس «الرصيد غير كافٍ».
    /// </summary>
    internal PurchaseInvoice BuildInvoiceFromRows(
        long companyId, long supplierId, DateOnly invoiceDate, string reference, bool vatDeductible)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            companyId, supplierId, invoiceDate, reference, vatDeductible);

        // (CEEF-230 · ط4-٦) ‏**والعملةُ تُضبط قبل السطور** — `SetCurrency` يُعيد حسابَ
        // ‏المجاميع. وكان المستوردُ يُدخل فاتورةَ الدولار محوَّلةً بيده، **فتضيع القيمةُ
        // ‏الأصلية ولا يُحسب فرقُ عملةٍ محقَّقٌ عند السداد**.
        ApplyCurrency(invoice);

        // ‏(ح-١١١) ‏مستودعُ الاستلام — الفارغُ هو الافتراضيّ؛ والمستودعُ يُسأل عند الترحيل.
        invoice.SetWarehouse(WarehouseCombo.SelectedValue as long?);

        // ‏(ح-١٤٤) ‏المشروعُ — الفارغُ «بلا مشروع»؛ وحالتُه تُسأل عند الترحيل.
        invoice.AssignProject(ProjectCombo.SelectedValue as long?);

        foreach (var row in _rows.Where(r =>
                     !string.IsNullOrWhiteSpace(r.Description) || r.Quantity != 0 || r.UnitPrice != 0))
        {
            // ‏(جاهزيّةُ الإطلاق · ح-١٠٨ · ‏`order-to-cash-procure-to-pay-inventory#7`) ‏**الدفعةُ وانتهاؤها** تسافران مع السطر — وبغيرهما يرفض المَخنَقُ الصنفَ المتتبَّع.
            invoice.AddLine(row.Description ?? "", row.Quantity, row.UnitPrice, row.ProductId, row.VatCategory,
                batchNumber: row.BatchNumber,
                expiryDate: row.ExpiryDate is DateTime d ? DateOnly.FromDateTime(d) : null);
        }

        return invoice;
    }

    /// <summary>صفوفُ الشبكة — للاختبار وحده.</summary>
    internal System.Collections.ObjectModel.ObservableCollection<SalesInvoiceDialog.LineRow> RowsForTests => _rows;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    /// <summary>يضبط عملةَ الفاتورة — <b>وريالٌ بسعرِ واحدٍ يسلك المسارَ القديم حرفاً</b>.</summary>
    private void ApplyCurrency(Brqosh.Domain.Purchasing.PurchaseInvoice invoice)
    {
        var code = (CurrencyBox.Text ?? "SAR").Trim().ToUpperInvariant();
        if (code.Length == 0) code = "SAR";
        if (code == "SAR") return;

        if (!NumericInput.TryParse(RateBox.Text, out var rate))
            throw new Brqosh.Domain.Accounting.AccountingException(
                $"سعر الصرف غير صالح — ووردَ «{RateBox.Text}».");

        invoice.SetCurrency(code, rate);
    }
}
