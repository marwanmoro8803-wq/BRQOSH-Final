using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٦) شاشةُ بيع أصلٍ ثابت — <b>وكانت مربَّعَ نصٍّ واحداً للمبلغ.</b>
///
/// <para><b>وبيعُ أصلٍ توريدٌ خاضعٌ نظاماً</b>، فينقصه أمران لم يكن للشاشة سبيلٌ إليهما:
/// <b>الفئةُ الضريبيّة</b> — تقبلها الخدمةُ منذ <c>CEEF-232 · ض-٢</c> ولا تُمرّرها شاشة —
/// <b>والمشتري</b>، ولم يكن للأصل مشترٍ في النموذج أصلاً.</para>
///
/// <para><b>والحسابُ في <see cref="FixedAssetDisposalPreview"/> لا هنا</b> — فما يُعرض
/// يُقاس باختبار، ولا يُقاس ما يُحسب داخل معالجِ ضغط.</para>
/// </summary>
public partial class FixedAssetDisposalDialog : Window
{
    private sealed record BuyerOption(long Id, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly decimal _acquisitionCost;
    private readonly decimal _accumulatedDepreciation;
    private readonly decimal _bookValue;
    private readonly string _assetName;
    private readonly bool _companyIsVatRegistered;

    /// <summary>العائدُ قبل الضريبة — صالحٌ بعد <c>DialogResult == true</c> وحدَه.</summary>
    public decimal Proceeds { get; private set; }

    /// <summary>المعاملةُ المختارة — الفئةُ ورمزُها معاً.</summary>
    public FixedAssetDisposalPreview.VatTreatment Treatment { get; private set; } =
        FixedAssetDisposalPreview.VatTreatment.Options[0];

    /// <summary><c>null</c> = بيعٌ نقديٌّ لمشترٍ غير مُعرَّف ⟵ فاتورةٌ مبسَّطة.</summary>
    public long? BuyerCustomerId { get; private set; }

    public FixedAssetDisposalDialog(
        string assetName, decimal acquisitionCost, decimal accumulatedDepreciation, decimal bookValue,
        bool companyIsVatRegistered, IReadOnlyList<(long Id, string Name)> customers)
    {
        InitializeComponent();

        _assetName = assetName;
        _acquisitionCost = acquisitionCost;
        _accumulatedDepreciation = accumulatedDepreciation;
        _bookValue = bookValue;
        _companyIsVatRegistered = companyIsVatRegistered;

        TitleText.Text = $"بيع/استبعاد الأصل {assetName}";
        VatCombo.ItemsSource = FixedAssetDisposalPreview.VatTreatment.Options;
        VatCombo.SelectedIndex = 0;

        // ‏**والخيارُ الأوّلُ هو الحالُ القائمة** — بيعٌ نقديٌّ بلا مشترٍ مُعرَّف، فلا
        // ‏تتغيّر عادةُ المستخدم بهذه الترقية ما لم يُرِد.
        var options = new List<BuyerOption> { new(0L, "بيع نقدي — مشترٍ غير مُعرَّف (فاتورة مبسّطة)") };
        options.AddRange(customers.Select(c => new BuyerOption(c.Id, c.Name)));
        BuyerCombo.ItemsSource = options;
        BuyerCombo.SelectedIndex = 0;

        Refresh();
    }

    /// <summary>قائمةُ العملاء كما تقرأها الشاشةُ من القاعدة.</summary>
    public static async Task<IReadOnlyList<(long Id, string Name)>> LoadCustomersAsync()
    {
        await using var db = AppServices.CreateDb();
        var rows = await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId)
            .OrderBy(c => c.NameAr)
            .Select(c => new { c.Id, c.NameAr })
            .ToListAsync();

        return rows.Select(r => (r.Id, r.NameAr)).ToList();
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (VerdictText is null) return;

        if (!NumericInput.TryParse(ProceedsBox.Text, out var proceeds) || proceeds < 0)
        {
            VerdictText.Text = "أدخل مبلغاً صحيحاً (صفر أو أكثر).";
            return;
        }

        var treatment = VatCombo.SelectedItem as FixedAssetDisposalPreview.VatTreatment
                        ?? FixedAssetDisposalPreview.VatTreatment.Options[0];

        VerdictText.Text = FixedAssetDisposalPreview.Describe(
            _assetName, _acquisitionCost, _accumulatedDepreciation, _bookValue,
            FixedAssetDisposalPreview.Compute(proceeds, treatment, _bookValue, _companyIsVatRegistered));
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!NumericInput.TryParse(ProceedsBox.Text, out var proceeds) || proceeds < 0)
        {
            UiDialog.Show("أدخل مبلغاً صحيحاً (صفر أو أكثر).", "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Proceeds = proceeds;
        Treatment = VatCombo.SelectedItem as FixedAssetDisposalPreview.VatTreatment
                    ?? FixedAssetDisposalPreview.VatTreatment.Options[0];
        BuyerCustomerId = BuyerCombo.SelectedValue is long id && id > 0 ? id : null;

        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
