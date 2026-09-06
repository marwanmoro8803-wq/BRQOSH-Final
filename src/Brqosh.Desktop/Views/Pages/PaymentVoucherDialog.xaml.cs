using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class PaymentVoucherDialog : Window
{
    public record SupplierOption(long Id, string Display);
    public record BankOption(long Id, string Display);

    public sealed class AllocRow : INotifyPropertyChanged
    {
        private decimal _allocateAmount;
        public PaymentAllocationDocumentType DocumentType { get; init; }
        public long DocumentId { get; init; }
        public string DocumentNumber { get; init; } = "";
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

    public PaymentVoucherDialog()
    {
        InitializeComponent();
        DatePickerBox.SelectedDate = DateTime.Today;
        AllocGrid.ItemsSource = _allocations;
        Loaded += async (_, _) =>
        {
            await LoadSuppliersAsync();
            await LoadBanksAsync();
        };
    }

    private async Task LoadSuppliersAsync()
    {
        await using var db = AppServices.CreateDb();
        var suppliers = await db.Suppliers
            .Where(s => s.CompanyId == AppServices.CompanyId && s.IsActive)
            .OrderBy(s => s.NameAr)
            .Select(s => new SupplierOption(s.Id, s.NameAr))
            .ToListAsync();
        SupplierCombo.ItemsSource = suppliers;
        if (suppliers.Count > 0)
            SupplierCombo.SelectedIndex = 0;
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
    /// (CEEF-229 · م2-٥) الحارسُ الأوّل شرطُ فتح الحوار لا احتياط — انظر الشرح المفصّل في
    /// <see cref="ReceiptVoucherDialog"/>: <c>SelectedIndex="0"</c> في XAML يُطلق هذا المعالج
    /// أثناء <c>InitializeComponent</c> بينما <c>BankPanel</c> بعده في الشجرة فيكون <c>null</c>،
    /// فيرمي الباني ولا يُفتح «سند الصرف» أبداً.
    /// </summary>
    private void OnMethodChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BankPanel is null) return;
        BankPanel.Visibility = MethodCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnSupplierSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SupplierCombo.SelectedValue is not long supplierId)
            return;

        using var facade = new DesktopAccountingFacade();
        var db = facade.Db;
        var balance = await new SupplierBalanceQuery(db).ForSupplierAsync(AppServices.CompanyId, supplierId);
        BalanceText.Text = $"الرصيد المستحق للمورد: {balance.Balance:N2}";

        _allocations.Clear();
        var open = await facade.GetService<PaymentAllocationService>().OpenPurchaseDocumentsAsync(AppServices.CompanyId, supplierId);
        foreach (var d in open)
        {
            _allocations.Add(new AllocRow
            {
                DocumentType = d.DocumentType,
                DocumentId = d.DocumentId,
                DocumentNumber = d.DocumentNumber,
                RemainingAmount = d.RemainingAmount,
                AllocateAmount = 0m,
            });
        }
        AllocSumText.Text = "مجموع التخصيص: 0.00";
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
        AllocSumText.Text = $"مجموع التخصيص: {_allocations.Sum(a => a.AllocateAmount):N2}";
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
            if (SupplierCombo.SelectedValue is not long supplierId)
                throw new AccountingException("اختر المورد.");
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

            var voucher = PaymentVoucher.CreateDraft(
                AppServices.CompanyId, supplierId,
                DateOnly.FromDateTime(DatePickerBox.SelectedDate.Value),
                amount, method, DescriptionBox.Text, ReferenceBox.Text, bankId);

            if (ReadCurrency() is (string code, decimal rate))
                voucher.SetCurrency(code, rate);

            // (CEEF-231 · ط1-٢ج) ‏**والخصمُ يُطفئ من الذمّة ما لم يمرّ بالصندوق.**
            var discount = SettlementDiscount();
            if (discount > 0m)
                voucher.GrantSettlementDiscount(discount);

            using var facade = new DesktopAccountingFacade();
            await facade.PostPaymentVoucherAsync(voucher, allocLines);

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
