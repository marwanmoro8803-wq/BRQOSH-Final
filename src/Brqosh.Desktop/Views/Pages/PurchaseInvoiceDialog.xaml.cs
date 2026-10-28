using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.ViewModels;

namespace Brqosh.Desktop.Views.Pages;

public partial class PurchaseInvoiceDialog : Window
{
    public record SupplierOption(long Id, bool IsVatRegistered, string Display);

    private const string DefaultWarehouseChoice = "(المستودع الافتراضي)";

    private readonly ObservableCollection<SalesInvoiceDialog.LineRow> _rows = [];
    private List<SalesInvoiceDialog.ProductOption> _products = [];
    private readonly PurchaseInvoiceDialogViewModel _vm = new();

    public PurchaseInvoiceDialog()
    {
        InitializeComponent();
        Brqosh.Desktop.Ui.CurrencyChoices.Fill(CurrencyBox, Brqosh.Desktop.Ui.CurrencyLabel.Code);
        Loaded += async (_, _) => Brqosh.Desktop.Ui.CurrencyChoices.Fill(
            CurrencyBox, Brqosh.Desktop.Ui.CurrencyLabel.Code, await _vm.LoadCurrenciesAsync());
        DatePickerBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        TreatmentCombo.ItemsSource = TreatmentOptions();
        TreatmentCombo.SelectedIndex = 0;
        Loaded += async (_, _) =>
        {
            await LoadSuppliersAsync(); await LoadWarehousesAsync(); await LoadProjectsAsync(); await LoadAssetsAsync(); await LoadProductsAsync();
        };
    }

    private async Task LoadProjectsAsync()
    {
        // ‏(جاهزيّةُ الإطلاق · ح-١٤٤ · ‏`sector-modules#9`) ‏المشاريعُ التي تقبل تكلفة — والأوّلُ «بلا مشروع».
        var choices = new List<SalesInvoiceDialog.WarehouseOption> { new(null, LocaleManager.T("(بلا مشروع)", "(No project)")) };
        choices.AddRange(await _vm.LoadProjectsAsync());
        ProjectCombo.ItemsSource = choices;
        ProjectCombo.SelectedIndex = 0;
    }

    private async Task LoadAssetsAsync()
    {
        // ‏(جاهزيّةُ الإطلاق · ح-١٥٨ · ‏`treasury-payroll-assets#12`) ‏الأصولُ التي تنتظر فاتورةَ مورّد — والأوّلُ «بلا أصل».
        AssetLabel.Text = LocaleManager.T("الأصل الثابت المشترى", "Fixed asset purchased");
        var choices = new List<SalesInvoiceDialog.WarehouseOption> { new(null, LocaleManager.T("(بلا أصل)", "(No asset)")) };
        choices.AddRange(await _vm.LoadAssetsAsync());
        AssetCombo.ItemsSource = choices;
        AssetCombo.SelectedIndex = 0;
    }

    private async Task LoadProductsAsync()
    {
        _products = await _vm.LoadProductsAsync();
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
        SupplierCombo.ItemsSource = await _vm.LoadSuppliersAsync();
        // (ح-٦١) لا طرفَ يُختار سلفاً — «Enter» وأوّلُ اسمٍ أبجديّاً كانا يُرحّلان لمن لم يُقصد.
    }

    private void OnSupplierSelected(object sender, SelectionChangedEventArgs e) => ApplyDeductibility();

    /// <summary>(جاهزيّةُ الإطلاق · <c>tax-zatca#12</c>) خيارُ معاملة الضريبة في القائمة.</summary>
    internal sealed record TreatmentOption(PurchaseVatTreatment Value, string Label);

    /// <summary>(<c>tax-zatca#12</c>) الخياراتُ الثلاثة — والمحلّيُّ أوّلاً.</summary>
    internal static IReadOnlyList<TreatmentOption> TreatmentOptions() =>
    [
        new(PurchaseVatTreatment.Domestic, LocaleManager.T("شراء محلي — ضريبة المورد على فاتورته", "Domestic purchase — VAT on the supplier's invoice")),
        new(PurchaseVatTreatment.ReverseCharge, LocaleManager.T("احتساب عكسي — مورد غير مقيم (الخانة 9)", "Reverse charge — non-resident supplier (box 9)")),
        new(PurchaseVatTreatment.ImportPaidAtCustoms, LocaleManager.T("ضريبة استيراد مدفوعة في الجمارك — مستند التخليص (الخانة 8)", "Import VAT paid at customs — clearance document (box 8)")),
    ];

    private PurchaseVatTreatment SelectedTreatment =>
        (TreatmentCombo.SelectedItem as TreatmentOption)?.Value ?? PurchaseVatTreatment.Domestic;

    private void OnTreatmentChanged(object sender, SelectionChangedEventArgs e) => ApplyDeductibility();

    /// <summary>
    /// مورد غير مسجل ⇒ لا خصم لضريبة المدخلات — <b>إلّا في الاحتساب العكسيّ والاستيراد</b> (<c>tax-zatca#12</c>): فالمورّدُ غيرُ المقيم
    /// لا يُسجَّل أصلاً، والإقرارُ يعدّ الضريبتين قابلتين للخصم (دليلُ الاستيراد §6.3 و§10.3).
    /// </summary>
    private void ApplyDeductibility()
    {
        if (SelectedTreatment != PurchaseVatTreatment.Domestic)
        {
            DeductibleCheck.IsChecked = true;
            DeductibleCheck.IsEnabled = false;
        }
        else if (SupplierCombo.SelectedItem is SupplierOption option)
        {
            DeductibleCheck.IsChecked = option.IsVatRegistered;
            DeductibleCheck.IsEnabled = option.IsVatRegistered;
        }
        else
        {
            DeductibleCheck.IsEnabled = true;
        }
        UpdateTotals();
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
        // ‏(tax-zatca#12) ‏المستحقُّ للمورّد: الصافي في الاحتساب العكسيّ، والضريبةُ وحدَها لمستند التخليص.
        TotalText.Text = (SelectedTreatment switch
        {
            PurchaseVatTreatment.ReverseCharge => subtotal,
            PurchaseVatTreatment.ImportPaidAtCustoms => vat,
            _ => subtotal + vat,
        }).ToString("N2");
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

            await _vm.PostAsync(invoice);

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

        // ‏(جاهزيّةُ الإطلاق · tax-zatca#12) ‏معاملةُ الضريبة — المحلّيُّ لمن لم يختر، فيبقى المسارُ القديمُ حرفاً.
        if (SelectedTreatment != PurchaseVatTreatment.Domestic)
            invoice.SetVatTreatment(SelectedTreatment);

        // ‏(ح-١١١) ‏مستودعُ الاستلام — الفارغُ هو الافتراضيّ؛ والمستودعُ يُسأل عند الترحيل.
        invoice.SetWarehouse(WarehouseCombo.SelectedValue as long?);

        // ‏(ح-١٤٤) ‏المشروعُ — الفارغُ «بلا مشروع»؛ وحالتُه تُسأل عند الترحيل.
        invoice.AssignProject(ProjectCombo.SelectedValue as long?);
        // ‏(ح-١٥٨) ‏والأصلُ — يُرفض مع مشروع، وتكلفتُه وتاريخُه يُطابَقان عند الترحيل.
        invoice.AssignFixedAsset(AssetCombo.SelectedValue as long?);

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
        var code = (CurrencyBox.SelectedItem as string ?? Brqosh.Desktop.Ui.CurrencyLabel.Code).Trim().ToUpperInvariant();
        if (code.Length == 0) code = Brqosh.Desktop.Ui.CurrencyLabel.Code;
        if (code == Brqosh.Desktop.Ui.CurrencyLabel.Code) return;

        if (!NumericInput.TryParse(RateBox.Text, out var rate))
            throw new Brqosh.Domain.Accounting.AccountingException(
                $"سعر الصرف غير صالح — ووردَ «{RateBox.Text}».");

        invoice.SetCurrency(code, rate, Brqosh.Desktop.Ui.CurrencyLabel.Code);
    }
}
