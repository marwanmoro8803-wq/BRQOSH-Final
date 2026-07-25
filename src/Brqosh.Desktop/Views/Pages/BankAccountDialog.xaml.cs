using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class BankAccountDialog : Window
{
    private readonly long? _bankAccountId;

    public BankAccountDialog()
    {
        InitializeComponent();
        _bankAccountId = null;
    }

    public BankAccountDialog(BankAccount bank) : this()
    {
        _bankAccountId = bank.Id;
        Title = "تعديل حساب بنكي";
        CodeBox.Text = bank.Code;
        CodeBox.IsReadOnly = true;
        NameBox.Text = bank.NameAr;
        BankBox.Text = bank.BankName;
        AccountNumberBox.Text = bank.AccountNumber;
        IbanBox.Text = bank.Iban;
        DefaultCheck.IsChecked = bank.IsDefault;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            using var facade = new DesktopAccountingFacade();
            var service = facade.GetService<BankAccountService>();

            if (_bankAccountId is long id)
            {
                await service.UpdateAsync(AppServices.CompanyId, id, NameBox.Text, BankBox.Text,
                    AccountNumberBox.Text, IbanBox.Text, DefaultCheck.IsChecked == true);
            }
            else
            {
                await service.CreateAsync(AppServices.CompanyId, CodeBox.Text, NameBox.Text, BankBox.Text,
                    AccountNumberBox.Text, IbanBox.Text, DefaultCheck.IsChecked == true);
            }

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
