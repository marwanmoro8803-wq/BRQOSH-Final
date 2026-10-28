using System.Globalization;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Assets;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class FixedAssetDialog : Window
{
    public record AccountOption(long Id, string Display);

    /// <summary>(جاهزيّةُ الإطلاق · ح-٩٩) من أين دُفع الأصل: الصندوقُ نقداً، أو بنكٌ قائم.</summary>
    public record FundingOption(ReceiptMethod Method, long? BankAccountId, string Display, bool ByInvoice = false);

    private readonly long? _assetId;

    public FixedAssetDialog()
    {
        InitializeComponent();
        _assetId = null;
        DateBox.SelectedDate = DateTime.Today;
        MethodCombo.ItemsSource = MethodOptions();
        MethodCombo.SelectedIndex = 0;
        Loaded += async (_, _) => await LoadAccountsAsync();
    }

    public FixedAssetDialog(FixedAsset asset) : this()
    {
        _assetId = asset.Id;
        Title = "تعديل أصل ثابت";
        CodeBox.Text = asset.Code;
        CodeBox.IsReadOnly = true;
        NameBox.Text = asset.NameAr;
        DateBox.SelectedDate = asset.AcquisitionDate.ToDateTime(TimeOnly.MinValue);
        DateBox.IsEnabled = false;
        CostBox.Text = asset.AcquisitionCost.ToString(CultureInfo.InvariantCulture);
        CostBox.IsReadOnly = true;
        SalvageBox.Text = asset.SalvageValue.ToString(CultureInfo.InvariantCulture);
        SalvageBox.IsReadOnly = true;
        LifeBox.Text = asset.UsefulLifeMonths.ToString();
        LifeBox.IsReadOnly = true;
        // ‏(treasury-payroll-assets#11) ‏الطريقةُ تُحدَّد قبل أوّل إهلاكٍ وحدَه — فتُعرض ولا تُغيَّر هنا.
        MethodCombo.SelectedItem = ((IEnumerable<MethodOption>)MethodCombo.ItemsSource).First(o => o.Value == asset.Method);
        MethodCombo.IsEnabled = false;
        AssetAccountCombo.IsEnabled = false;
        DepAccountCombo.IsEnabled = false;
        FundingCombo.IsEnabled = false;
        OpeningCheck.IsEnabled = false;
    }

    /// <summary>(جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c>) خيارُ طريقة الإهلاك في القائمة.</summary>
    internal sealed record MethodOption(DepreciationMethod Value, string Label);

    /// <summary>(<c>treasury-payroll-assets#11</c>) الطريقتان — والخطُّ المستقيمُ أوّلاً.</summary>
    internal static IReadOnlyList<MethodOption> MethodOptions() =>
    [
        new(DepreciationMethod.StraightLine, LocaleManager.T("القسط الثابت (خط مستقيم)", "Straight-line")),
        new(DepreciationMethod.DecliningBalance, LocaleManager.T("القسط المتناقص المضاعف", "Double-declining balance")),
    ];

    private DepreciationMethod SelectedMethod =>
        (MethodCombo.SelectedItem as MethodOption)?.Value ?? DepreciationMethod.StraightLine;

    /// <summary>(جاهزيّةُ الإطلاق · ح-١٠٠) الرصيدُ الافتتاحيُّ لا يُدفع من شيء — فطريقةُ الدفع تُعطَّل ويظهر تاريخُه ومجمّعُه.</summary>
    private void OnOpeningToggled(object sender, RoutedEventArgs e)
    {
        var opening = OpeningCheck.IsChecked == true;
        OpeningPanel.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        FundingCombo.IsEnabled = !opening;
    }

    /// <summary>‏لكلّ فئة أصولٍ مجمّعُ إهلاكها (12071-12076) — يُقترح تلقائياً عند اختيار حساب الأصل ويبقى قابلاً للتغيير.</summary>
    private void SuggestDepreciationAccount()
    {
        if (AssetAccountCombo.SelectedItem is not AccountOption asset) return;
        var code = asset.Display.Split(' ')[0];
        var target = Brqosh.Domain.Accounting.SaudiChartOfAccounts.AccumulatedDepreciationFor(code.Length > 4 ? code[..4] : code);
        var match = DepAccountCombo.Items.OfType<AccountOption>().FirstOrDefault(a => a.Display.Split(' ')[0] == target);
        if (match is not null)
            DepAccountCombo.SelectedItem = match;
    }

    private async Task LoadAccountsAsync()
    {
        await using var db = AppServices.CreateDb();
        var accounts = await db.Accounts
            .Where(a => a.CompanyId == AppServices.CompanyId && !a.IsGroup)
            .OrderBy(a => a.Code)
            .Select(a => new AccountOption(a.Id, $"{a.Code} — {a.NameAr}"))
            .ToListAsync();

        AssetAccountCombo.ItemsSource = accounts.Where(a => a.Display.StartsWith("12") && !a.Display.StartsWith("1207")).ToList();
        DepAccountCombo.ItemsSource = accounts.Where(a => a.Display.StartsWith("1207")).ToList();
        AssetAccountCombo.SelectionChanged += (_, _) => SuggestDepreciationAccount();

        var banks = await db.BankAccounts
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderByDescending(b => b.IsDefault).ThenBy(b => b.Code)
            .Select(b => new { b.Id, b.Code, b.NameAr })
            .ToListAsync();
        FundingCombo.ItemsSource = new[] { new FundingOption(ReceiptMethod.Cash, null, "نقداً (الصندوق)") }
            .Concat(banks.Select(b => new FundingOption(ReceiptMethod.Bank, b.Id, $"{b.Code} — {b.NameAr}")))
            // ‏(ح-١٥٨) ‏شراءٌ من مورّدٍ بفاتورته — يُسجَّل الأصلُ ويقتنيه ترحيلُ فاتورة الشراء المربوطة به.
            .Append(new FundingOption(ReceiptMethod.Cash, null,
                LocaleManager.T("بفاتورة مورّد (يُقتنى عند ترحيلها)", "By supplier invoice (acquired when it is posted)"), ByInvoice: true))
            .ToList();
        FundingCombo.SelectedIndex = 0;

        if (_assetId is long id)
        {
            var asset = await db.FixedAssets.SingleAsync(a => a.Id == id);
            AssetAccountCombo.SelectedValue = asset.GlAssetAccountId;
            DepAccountCombo.SelectedValue = asset.GlDepreciationAccountId;
            return;
        }

        if (AssetAccountCombo.Items.Count > 0)
            AssetAccountCombo.SelectedIndex = 0;
        if (DepAccountCombo.SelectedIndex < 0 && DepAccountCombo.Items.Count > 0)
            DepAccountCombo.SelectedIndex = 0;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;

            if (_assetId is long id)
            {
                var asset = await db.FixedAssets.SingleAsync(a => a.Id == id);
                asset.Rename(NameBox.Text);
                await db.SaveChangesAsync();
                DialogResult = true;
                Close();
                return;
            }

            if (DateBox.SelectedDate is null)
                throw new AccountingException("تاريخ الاقتناء مطلوب.");
            if (AssetAccountCombo.SelectedValue is not long assetAcctId)
                throw new AccountingException("اختر حساب الأصل.");
            if (DepAccountCombo.SelectedValue is not long depAcctId)
                throw new AccountingException("اختر حساب مجمع الإهلاك.");
            if (!NumericInput.TryParse(CostBox.Text, out var cost) || cost <= 0)
                throw new AccountingException("تكلفة الاقتناء غير صالحة.");
            if (!NumericInput.TryParse(SalvageBox.Text, out var salvage) || salvage < 0)
                throw new AccountingException("القيمة المتبقية غير صالحة.");
            if (!int.TryParse(LifeBox.Text, out var life) || life <= 0)
                throw new AccountingException("العمر الإنتاجي غير صالح.");

            if (OpeningCheck.IsChecked == true)
            {
                if (OpeningDateBox.SelectedDate is null)
                    throw new AccountingException("تاريخ الرصيد الافتتاحي مطلوب.");
                if (!NumericInput.TryParse(AccumulatedBox.Text, out var accumulated) || accumulated < 0)
                    throw new AccountingException("مجمع الإهلاك الافتتاحي غير صالح.");

                // ‏(جاهزيّةُ الإطلاق · ح-١٠٠) **أصلٌ قائمٌ قبل بدء التشغيل برصيده الافتتاحيّ** — لا اقتناءٌ يُدان به الصندوق،
                // ‏ولا يُهلَك ما أُهلك قبل الدفاتر ثانيةً.
                var openingAsset = FixedAsset.CreateOpening(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                    DateOnly.FromDateTime(DateBox.SelectedDate.Value), cost, salvage, life, assetAcctId, depAcctId,
                    accumulated, DateOnly.FromDateTime(OpeningDateBox.SelectedDate.Value), SelectedMethod);
                await facade.GetService<FixedAssetAcquisitionPostingService>().PostOpeningAsync(openingAsset);
                DialogResult = true;
                Close();
                return;
            }

            if (FundingCombo.SelectedItem is not FundingOption funding)
                throw new AccountingException("اختر طريقة الدفع.");

            if (funding.ByInvoice)
            {
                var awaiting = FixedAsset.CreateAwaitingInvoice(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                    DateOnly.FromDateTime(DateBox.SelectedDate.Value), cost, salvage, life, assetAcctId, depAcctId, SelectedMethod);
                await facade.GetService<FixedAssetAcquisitionPostingService>().RegisterAwaitingInvoiceAsync(awaiting);
                DialogResult = true;
                Close();
                return;
            }

            var assetNew = FixedAsset.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value), cost, salvage, life, assetAcctId, depAcctId, SelectedMethod);

            // ‏(جاهزيّةُ الإطلاق · ح-٩٩) **الأصلُ يُحفظ مع قيد اقتنائه في معاملةٍ واحدة** — وكان يُحفظ هنا أوّلاً،
            // ‏فترحيلٌ يُرفض بعده يُبقي أصلاً بلا قيد.
            await facade.GetService<FixedAssetAcquisitionPostingService>()
                .PostAcquisitionAsync(assetNew, funding.Method, funding.BankAccountId);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex is AccountingException ae ? ae.Message : ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
