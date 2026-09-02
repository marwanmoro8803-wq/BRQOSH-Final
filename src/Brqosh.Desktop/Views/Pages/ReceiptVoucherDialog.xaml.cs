using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

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

    public ReceiptVoucherDialog()
    {
        InitializeComponent();
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
        await using var db = AppServices.CreateDb();
        // (CEEF-229 · م2-٢٢) «من عليه رصيدٌ يُحصَّل منه» لا «النشط يُحصَّل منه».
        // كان الترشيح بـ IsActive وحده، فيصير العميل الموقوف غير قابلٍ للتحصيل بينما ذمّتُه
        // باقيةٌ في الأستاذ — مالٌ مسجَّل بلا طريقٍ لإقفاله. والقاعدة في استعلامٍ مستقلّ
        // كي تُقاس بهوية «مجموع أرصدة القابلين للاختيار == رصيد المدينين».
        var customers = (await new CollectableCustomersQuery(db).ExecuteAsync(AppServices.CompanyId))
            .Select(c => new CustomerOption(c.Id, c.IsActive ? c.NameAr : $"{c.NameAr} (موقوف)"))
            .ToList();
        CustomerCombo.ItemsSource = customers;
        if (customers.Count > 0)
            CustomerCombo.SelectedIndex = 0;
    }

    private async Task LoadBanksAsync()
    {
        await using var db = AppServices.CreateDb();
        var banks = await db.BankAccounts
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.Code)
            .Select(b => new BankOption(b.Id, $"{b.Code} — {b.NameAr} ({b.BankName})"))
            .ToListAsync();
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

        using var facade = new DesktopAccountingFacade();
        var db = facade.Db;
        var balance = await new CustomerBalanceQuery(db).ForCustomerAsync(AppServices.CompanyId, customerId);
        BalanceText.Text = $"الرصيد المستحق على العميل: {balance.Balance:N2}";

        _allocations.Clear();
        var open = await facade.GetService<PaymentAllocationService>().OpenSalesDocumentsAsync(AppServices.CompanyId, customerId);
        foreach (var d in open)
        {
            _allocations.Add(new AllocRow
            {
                DocumentType = d.DocumentType,
                DocumentId = d.DocumentId,
                DocumentNumber = d.DocumentNumber,
                TypeLabel = d.DocumentType switch
                {
                    PaymentAllocationDocumentType.SalesInvoice => "فاتورة",
                    PaymentAllocationDocumentType.ProgressBilling => "مستخلص",
                    _ => d.DocumentType.ToString(),
                },
                RemainingAmount = d.RemainingAmount,
                AllocateAmount = 0m,
            });
        }
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
                voucher.SetCurrency(code, rate);

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<ReceiptVoucherPostingService>().PostAsync(voucher, allocLines);

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
        var code = (CurrencyBox.Text ?? "SAR").Trim().ToUpperInvariant();
        if (code.Length == 0) code = "SAR";
        if (code == "SAR") return null;

        if (!decimal.TryParse((RateBox.Text ?? "").Trim(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var rate))
            throw new AccountingException($"سعر الصرف غير صالح — ووردَ «{RateBox.Text}».");

        return (code, rate);
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
