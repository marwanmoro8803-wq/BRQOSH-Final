using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.ViewModels;

namespace Brqosh.Desktop.Views.Pages;

public partial class ReceiptVoucherDialog : Window
{
    public record CustomerOption(long Id, string Display);
    public record BankOption(long Id, string Display);

    public sealed class AllocRow : INotifyPropertyChanged
    {
        private decimal _allocateAmount;
        public PaymentAllocationDocumentType DocumentType { get; init; }
        public long DocumentId { get; init; }
        public string DocumentNumber { get; init; } = "";
        public string TypeLabel { get; init; } = "";
        public decimal RemainingAmount { get; init; }
        public decimal AllocateAmount
        {
            get => _allocateAmount;
            set { _allocateAmount = value; OnPropertyChanged(); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private readonly ObservableCollection<AllocRow> _allocations = [];
    private readonly ReceiptVoucherDialogViewModel _vm = new();

    public ReceiptVoucherDialog()
    {
        InitializeComponent();
        CurrencyBox.Text = Brqosh.Desktop.Ui.CurrencyLabel.Code;
        DatePickerBox.SelectedDate = DateTime.Today;
        AllocGrid.ItemsSource = _allocations;
        Loaded += async (_, _) =>
        {
            await LoadCustomersAsync();
            await LoadBanksAsync();
        };
    }

    private async Task LoadCustomersAsync()
    {
        // (CEEF-229 · م2-٢٢) «من عليه رصيدٌ يُحصَّل منه» لا «النشط يُحصَّل منه».
        // كان الترشيح بـ IsActive وحده، فيصير العميل الموقوف غير قابلٍ للتحصيل بينما ذمّتُه
        // باقيةٌ في الأستاذ — مالٌ مسجَّل بلا طريقٍ لإقفاله. والقاعدة في استعلامٍ مستقلّ
        // كي تُقاس بهوية «مجموع أرصدة القابلين للاختيار == رصيد المدينين».
        var customers = await _vm.LoadCustomersAsync();
        CustomerCombo.ItemsSource = customers;
        // (ح-٦١) لا طرفَ يُختار سلفاً — «Enter» وأوّلُ اسمٍ أبجديّاً كانا يُرحّلان لمن لم يُقصد.
    }

    private async Task LoadBanksAsync()
    {
        var banks = await _vm.LoadBanksAsync();
        BankCombo.ItemsSource = banks;
        if (banks.Count > 0)
            BankCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// (CEEF-229 · م2-٥) <b>الحارسُ الأوّل ليس احتياطاً — بدونه لا يُفتح هذا الحوار إطلاقاً.</b>
    ///
    /// <para><c>MethodCombo</c> يُعلَن في XAML بـ <c>SelectedIndex="0"</c>، وضبطُ القيمة أثناء
    /// <c>InitializeComponent</c> <b>يُطلق SelectionChanged فوراً</b>. و<c>BankPanel</c> مُعلَنٌ
    /// بعده في الشجرة، فيكون <c>null</c> حين يجري هذا المعالج — فيرمي
    /// <c>NullReferenceException</c> من داخل الباني، ولا يُفتح «سند القبض» أبداً.</para>
    ///
    /// <para>ولم يكشفه شيء: تسعةٌ وستون اختباراً في <c>GoldenPathUiMarkupTests</c> تقرأ XAML
    /// <b>نصّاً</b> وتتحقّق أنّ الاسم مكتوب — وهو مكتوبٌ فعلاً. والذي كشفه هو <b>بناءُ الحوار</b>.</para>
    /// </summary>
    private void OnMethodChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BankPanel is null) return;
        BankPanel.Visibility = MethodCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnCustomerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (CustomerCombo.SelectedValue is not long customerId)
            return;

        var (balance, open) = await _vm.LoadCustomerDocumentsAsync(customerId);
        BalanceText.Text = $"الرصيد المستحق على العميل: {balance:N2}";

        _allocations.Clear();
        foreach (var row in open)
            _allocations.Add(row);
        UpdateAllocSum();
    }

    private void OnAutoAllocate(object sender, RoutedEventArgs e)
    {
        if (!NumericInput.TryParse(AmountBox.Text, out var amount))
            return;

        var remaining = amount;
        foreach (var row in _allocations)
        {
            var take = Math.Min(remaining, row.RemainingAmount);
            row.AllocateAmount = take;
            remaining = Math.Round(remaining - take, 2, MidpointRounding.AwayFromZero);
        }
        UpdateAllocSum();
    }

    private void UpdateAllocSum()
    {
        var sum = _allocations.Sum(a => a.AllocateAmount);
        AllocSumText.Text = $"مجموع التخصيص: {sum:N2}";
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        // (CEEF-229 · م0-١٦) ضغطة مزدوجة كانت تُنشئ مستندين — الزرّ لا يُعطَّل أثناء الانتظار.
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            if (DatePickerBox.SelectedDate is null)
                throw new AccountingException("تاريخ السند مطلوب.");
            if (CustomerCombo.SelectedValue is not long customerId)
                throw new AccountingException("اختر العميل.");
            if (!NumericInput.TryParse(AmountBox.Text, out var amount))
                throw new AccountingException("أدخل مبلغاً صحيحاً.");

            var method = MethodCombo.SelectedIndex == 0 ? ReceiptMethod.Cash : ReceiptMethod.Bank;
            long? bankId = method == ReceiptMethod.Bank && BankCombo.SelectedValue is long id ? id : null;

            var allocLines = _allocations
                .Where(a => a.AllocateAmount > 0)
                .Select(a => (a.DocumentType, a.DocumentId, a.DocumentNumber, a.AllocateAmount))
                .ToList();

            if (allocLines.Count > 0)
            {
                var sum = allocLines.Sum(a => a.AllocateAmount);
                if (Math.Abs(sum - amount) > 0.01m)
                    throw new AccountingException(
                        $"مجموع التخصيصات {sum:N2} يجب أن يساوي مبلغ السند {amount:N2} (أو اترك التخصيص فارغاً).");
            }

            var voucher = ReceiptVoucher.CreateDraft(
                AppServices.CompanyId, customerId,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value),
                amount, method, DescriptionBox.Text, ReferenceBox.Text, bankId);

            if (ReadCurrency() is (string code, decimal rate))
                voucher.SetCurrency(code, rate, Brqosh.Desktop.Ui.CurrencyLabel.Code);

            // (CEEF-231 · ط1-٢ج) ‏**والخصمُ يُطفئ من الذمّة ما لم يمرّ بالصندوق.**
            var discount = SettlementDiscount();
            if (discount > 0m)
                voucher.GrantSettlementDiscount(discount);

            await _vm.PostAsync(voucher, allocLines);

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
    /// (CEEF-231 · ط1-٤ب) يضبط عملةَ السند وسعرَها — <b>ويرفض ما لا يُقرأ بدل أن يبتلعه</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>وريالٌ بسعرِ واحدٍ لا يُستدعى له شيء</b>: فالمسارُ القديم يبقى حرفاً، ولا
    /// يتغيّر رقمٌ في منشأةٍ لا تتعامل بالعملات. <b>وهو الشرطُ الذي يحمي كلَّ قاعدةٍ قائمة.</b></para>
    ///
    /// <para><b>وما كان:</b> النطاقُ يحمل العملةَ منذ <c>ط1-٤</c> ويحسب فرقَ العملة
    /// المحقَّق عند السداد — <b>ولا حقلَ في أيّ شاشةٍ يصل إليه</b>. فيُدخل المحاسبُ
    /// التحصيلَ محوَّلاً بيده، <b>فتضيع القيمةُ الأصلية</b> ولا يُقيَّد فرقٌ محقَّق:
    /// الحسابان <c>4108</c> و<c>5218</c> بلا كاتبٍ من الشاشة، <b>والذمّةُ تبقى مفتوحةً
    /// بفرقٍ لا يُغلق</b>.</para>
    /// </remarks>
    private (string Code, decimal Rate)? ReadCurrency()
    {
        var code = (CurrencyBox.Text ?? Brqosh.Desktop.Ui.CurrencyLabel.Code).Trim().ToUpperInvariant();
        if (code.Length == 0) code = Brqosh.Desktop.Ui.CurrencyLabel.Code;
        if (code == Brqosh.Desktop.Ui.CurrencyLabel.Code) return null;

        if (!NumericInput.TryParse(RateBox.Text, out var rate))
            throw new AccountingException($"سعر الصرف غير صالح — ووردَ «{RateBox.Text}».");

        return (code, rate);
    }

    /// <summary>
    /// (CEEF-231 · ط1-٢ج) خصمُ السداد المُدخَل — <b>وما لا يُقرأ صفرٌ لا رفض</b>.
    /// </summary>
    /// <remarks>
    /// <b>والحراسةُ في النطاق عند الترحيل</b> — وهو الموضعُ الذي لا يُلتفّ عليه.
    /// </remarks>
    private decimal SettlementDiscount() =>
        NumericInput.TryParsePositive(SettlementDiscountBox.Text, out var value) ? value : 0m;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
