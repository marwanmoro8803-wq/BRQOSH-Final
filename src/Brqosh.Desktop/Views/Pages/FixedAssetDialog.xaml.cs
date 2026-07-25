using System.Globalization;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Infrastructure.Assets;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class FixedAssetDialog : Window
{
    public record AccountOption(long Id, string Display);

    private readonly long? _assetId;

    public FixedAssetDialog()
    {
        InitializeComponent();
        _assetId = null;
        DateBox.SelectedDate = DateTime.Today;
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
        AssetAccountCombo.IsEnabled = false;
        DepAccountCombo.IsEnabled = false;
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

        if (_assetId is long id)
        {
            var asset = await db.FixedAssets.SingleAsync(a => a.Id == id);
            AssetAccountCombo.SelectedValue = asset.GlAssetAccountId;
            DepAccountCombo.SelectedValue = asset.GlDepreciationAccountId;
            return;
        }

        if (AssetAccountCombo.Items.Count > 0)
            AssetAccountCombo.SelectedIndex = 0;
        if (DepAccountCombo.Items.Count > 0)
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
            if (!decimal.TryParse(CostBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) || cost <= 0)
                throw new AccountingException("تكلفة الاقتناء غير صالحة.");
            if (!decimal.TryParse(SalvageBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var salvage) || salvage < 0)
                throw new AccountingException("القيمة المتبقية غير صالحة.");
            if (!int.TryParse(LifeBox.Text, out var life) || life <= 0)
                throw new AccountingException("العمر الإنتاجي غير صالح.");

            var assetNew = FixedAsset.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value), cost, salvage, life, assetAcctId, depAcctId);
            db.FixedAssets.Add(assetNew);
            await db.SaveChangesAsync();

            await facade.GetService<FixedAssetAcquisitionPostingService>().PostAcquisitionAsync(assetNew);

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
