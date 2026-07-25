using System.Windows;
using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class AccountDialog : Window
{
    private record ParentOption(Account? Account, string Label)
    {
        public override string ToString() => Label;
    }

    private long? _accountId;

    public AccountDialog(IReadOnlyList<Account> accounts, Account? preselectedParent)
    {
        InitializeComponent();
        PopulateParents(accounts, preselectedParent);
    }

    public static AccountDialog ForEdit(IReadOnlyList<Account> accounts, Account editAccount)
    {
        var dialog = new AccountDialog(accounts, accounts.FirstOrDefault(a => a.Id == editAccount.ParentId));
        dialog._accountId = editAccount.Id;
        dialog.Title = editAccount.IsSystem ? "حساب نظامي — تعديل الاسم فقط" : "تعديل حساب";
        dialog.CodeBox.Text = editAccount.Code;
        dialog.CodeBox.IsReadOnly = true;
        dialog.NameArBox.Text = editAccount.NameAr;
        dialog.NameEnBox.Text = editAccount.NameEn;
        dialog.IsGroupCheck.IsChecked = editAccount.IsGroup;
        dialog.IsGroupCheck.IsEnabled = false;
        dialog.ParentCombo.IsEnabled = false;
        return dialog;
    }

    private void PopulateParents(IReadOnlyList<Account> accounts, Account? preselectedParent)
    {
        ParentCombo.Items.Add(new ParentOption(null, "— بدون (حساب جذري) —"));
        foreach (var group in accounts.Where(a => a.IsGroup).OrderBy(a => a.Code))
        {
            var option = new ParentOption(group, $"{group.Code} — {group.NameAr}");
            ParentCombo.Items.Add(option);
            if (preselectedParent?.Id == group.Id)
                ParentCombo.SelectedItem = option;
        }
        if (ParentCombo.SelectedItem is null)
            ParentCombo.SelectedIndex = 0;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            await using var db = AppServices.CreateDb();

            if (_accountId is long id)
            {
                var account = await db.Accounts.SingleAsync(a => a.Id == id);
                account.Rename(NameArBox.Text, NameEnBox.Text);
                await db.SaveChangesAsync();
                DialogResult = true;
                Close();
                return;
            }

            var parent = (ParentCombo.SelectedItem as ParentOption)?.Account;
            var type = parent?.Type ?? InferTypeFromCode(CodeBox.Text.Trim());

            var newAccount = Account.Create(
                CodeBox.Text.Trim(),
                NameArBox.Text.Trim(),
                NameEnBox.Text.Trim(),
                type,
                parent?.Id,
                IsGroupCheck.IsChecked == true,
                isSystem: false,
                AppServices.CompanyId);

            if (parent is not null && !newAccount.Code.StartsWith(parent.Code, StringComparison.Ordinal))
                throw new AccountingException($"رقم الحساب يجب أن يبدأ برقم الحساب الأب ({parent.Code}).");

            if (db.Accounts.Any(a => a.CompanyId == AppServices.CompanyId && a.Code == newAccount.Code))
                throw new AccountingException($"رقم الحساب {newAccount.Code} مستخدم مسبقاً.");

            db.Accounts.Add(newAccount);
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

    private static AccountType InferTypeFromCode(string code) => code.FirstOrDefault() switch
    {
        '1' => AccountType.Asset,
        '2' => AccountType.Liability,
        '3' => AccountType.Equity,
        '4' => AccountType.Revenue,
        '5' => AccountType.Expense,
        _ => throw new AccountingException("رقم الحساب الجذري يجب أن يبدأ بـ 1–5 حسب التصنيف المحاسبي."),
    };

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
