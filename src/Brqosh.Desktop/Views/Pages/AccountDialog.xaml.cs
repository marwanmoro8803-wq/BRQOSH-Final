using System.Windows;
using Brqosh.Domain.Accounting;

namespace Brqosh.Desktop.Views.Pages;

public partial class AccountDialog : Window
{
    private record ParentOption(Account? Account, string Label)
    {
        public override string ToString() => Label;
    }

    public AccountDialog(IReadOnlyList<Account> accounts, Account? preselectedParent)
    {
        InitializeComponent();

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
        var parent = (ParentCombo.SelectedItem as ParentOption)?.Account;
        try
        {
            // نوع الحساب يُشتق من الأب؛ الحساب الجذري يتطلب كوداً يبدأ بتصنيف معروف
            var type = parent?.Type ?? InferTypeFromCode(CodeBox.Text.Trim());

            var account = Account.Create(
                CodeBox.Text.Trim(),
                NameArBox.Text.Trim(),
                NameEnBox.Text.Trim(),
                type,
                parent?.Id,
                IsGroupCheck.IsChecked == true,
                isSystem: false,
                AppServices.CompanyId);

            if (parent is not null && !account.Code.StartsWith(parent.Code, StringComparison.Ordinal))
                throw new AccountingException($"رقم الحساب يجب أن يبدأ برقم الحساب الأب ({parent.Code}).");

            await using var db = AppServices.CreateDb();
            if (db.Accounts.Any(a => a.CompanyId == AppServices.CompanyId && a.Code == account.Code))
                throw new AccountingException($"رقم الحساب {account.Code} مستخدم مسبقاً.");

            db.Accounts.Add(account);
            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (AccountingException ex)
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
