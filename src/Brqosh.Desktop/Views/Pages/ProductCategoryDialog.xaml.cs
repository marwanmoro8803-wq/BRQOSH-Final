using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProductCategoryDialog : Window
{
    public record AccountChoice(long? Id, string Label);

    private readonly long? _categoryId;
    private readonly ProductCategory? _source;

    public ProductCategoryDialog()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAccountsAsync();
    }

    public ProductCategoryDialog(ProductCategory category) : this()
    {
        _categoryId = category.Id;
        _source = category;
        CodeBox.Text = category.Code;
        CodeBox.IsEnabled = false;
        NameBox.Text = category.NameAr;
        Title = "تعديل فئة";
    }

    private async Task LoadAccountsAsync()
    {
        await using var db = AppServices.CreateDb();
        var accounts = await db.Accounts
            .Where(a => a.CompanyId == AppServices.CompanyId && a.IsActive && !a.IsGroup)
            .OrderBy(a => a.Code)
            .Select(a => new AccountChoice(a.Id, a.Code + " " + a.NameAr))
            .ToListAsync();
        var choices = new List<AccountChoice> { new(null, "—") };
        choices.AddRange(accounts);
        foreach (var (box, id) in new[] { (RevenueBox, _source?.RevenueAccountId), (InventoryBox, _source?.InventoryAccountId), (CogsBox, _source?.CogsAccountId) })
        {
            box.ItemsSource = choices;
            box.SelectedItem = choices.FirstOrDefault(c => c.Id == id) ?? choices[0];
        }
    }

    private static long? Picked(System.Windows.Controls.ComboBox box) => (box.SelectedItem as AccountChoice)?.Id;

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            await using var db = AppServices.CreateDb();
            if (_categoryId is long id)
            {
                var category = await db.ProductCategories.SingleAsync(c => c.Id == id);
                category.Rename(NameBox.Text);
                category.SetAccounts(Picked(RevenueBox), Picked(InventoryBox), Picked(CogsBox));
            }
            else
            {
                var code = CodeBox.Text.Trim();
                if (await db.ProductCategories.AnyAsync(c => c.CompanyId == AppServices.CompanyId && c.Code == code))
                    throw new AccountingException(LocaleManager.T("كود الفئة مستخدم من قبل.", "This category code is already in use."));
                db.ProductCategories.Add(ProductCategory.Create(AppServices.CompanyId, code, NameBox.Text,
                    Picked(RevenueBox), Picked(InventoryBox), Picked(CogsBox)));
            }

            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
