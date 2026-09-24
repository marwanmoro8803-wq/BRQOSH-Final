using System.Globalization;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ChequeDialog : Window
{
    public record PartyOption(long Id, string Display);
    public record BankOption(long Id, string Display);

    private readonly ChequeType _type;

    public ChequeDialog(ChequeType type)
    {
        _type = type;
        InitializeComponent();
        TitleText.Text = type == ChequeType.Received ? "شيك وارد جديد" : "شيك صادر جديد";
        PartyLabel.Text = type == ChequeType.Received ? "العميل *" : "المورد *";
        DateBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => { await LoadPartiesAsync(); await LoadBanksAsync(); };
    }

    private async Task LoadPartiesAsync()
    {
        await using var db = AppServices.CreateDb();
        if (_type == ChequeType.Received)
        {
            PartyCombo.ItemsSource = await db.Customers
                .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new PartyOption(c.Id, c.NameAr))
                .ToListAsync();
        }
        else
        {
            PartyCombo.ItemsSource = await db.Suppliers
                .Where(s => s.CompanyId == AppServices.CompanyId && s.IsActive)
                .OrderBy(s => s.NameAr)
                .Select(s => new PartyOption(s.Id, s.NameAr))
                .ToListAsync();
        }

        // (ح-٦١) لا طرفَ يُختار سلفاً — «Enter» وأوّلُ اسمٍ أبجديّاً كانا يُرحّلان لمن لم يُقصد.
    }

    private async Task LoadBanksAsync()
    {
        await using var db = AppServices.CreateDb();
        BankCombo.ItemsSource = await db.BankAccounts
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderByDescending(b => b.IsDefault).ThenBy(b => b.Code)
            .Select(b => new BankOption(b.Id, $"{b.Code} — {b.NameAr}"))
            .ToListAsync();
        if (BankCombo.Items.Count > 0)
            BankCombo.SelectedIndex = 0;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        // (CEEF-229 · م0-١٦) ضغطة مزدوجة كانت تُنشئ مستندين — الزرّ لا يُعطَّل أثناء الانتظار.
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            if (DateBox.SelectedDate is null)
                throw new AccountingException("تاريخ الشيك مطلوب.");
            if (PartyCombo.SelectedValue is not long partyId)
                throw new AccountingException("اختر الطرف.");
            if (BankCombo.SelectedValue is not long bankId)
                throw new AccountingException("اختر الحساب البنكي.");
            if (!NumericInput.TryParse(AmountBox.Text, out var amount))
                throw new AccountingException("أدخل مبلغاً صحيحاً.");

            var date = DateOnly.FromDateTime(DateBox.SelectedDate.Value);
            DateOnly? due = DueBox.SelectedDate is null ? null : DateOnly.FromDateTime(DueBox.SelectedDate.Value);

            Cheque cheque = _type == ChequeType.Received
                ? Cheque.CreateReceived(AppServices.CompanyId, NumberBox.Text, date, amount,
                    DescriptionBox.Text, bankId, partyId, due)
                : Cheque.CreateIssued(AppServices.CompanyId, NumberBox.Text, date, amount,
                    DescriptionBox.Text, bankId, partyId, due);

            using var facade = new DesktopAccountingFacade();
            var service = facade.GetService<ChequePostingService>();
            if (_type == ChequeType.Received)
                await service.RegisterReceivedAsync(cheque);
            else
                await service.RegisterIssuedAsync(cheque);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
