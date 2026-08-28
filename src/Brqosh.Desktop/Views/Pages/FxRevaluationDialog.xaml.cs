using System.Globalization;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class FxRevaluationDialog : Window
{
    private readonly long _bankAccountId;

    public FxRevaluationDialog(long bankAccountId, string bankName, string currencyCode, string functionalCurrencyCode)
    {
        InitializeComponent();
        _bankAccountId = bankAccountId;
        TitleText.Text = $"إعادة تقييم — {bankName} ({currencyCode})";
        RateLabel.Text = $"سعر الصرف الجديد ({functionalCurrencyCode} لكل 1 {currencyCode}) *";
        BalanceLabel.Text = $"الرصيد بالعملة الأجنبية ({currencyCode}) *";
        DatePicker.SelectedDate = DateTime.Today;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DatePicker.SelectedDate is not DateTime dt)
                throw new AccountingException("اختر تاريخاً صالحاً.");

            if (!NumericInput.TryParse(RateBox.Text, out var rate))
                throw new AccountingException("سعر الصرف غير صالح.");

            if (!NumericInput.TryParse(ForeignBalanceBox.Text, out var foreignBalance))
                throw new AccountingException("رصيد العملة الأجنبية غير صالح.");

            var asOfDate = DateOnly.FromDateTime(dt);
            var username = AppServices.CurrentUser?.Username ?? "system";

            using var facade = new DesktopAccountingFacade();
            var entry = await facade.GetService<FxRevaluationService>().RevalueAsync(
                AppServices.CompanyId, _bankAccountId, asOfDate, rate, username, foreignBalance);

            if (entry is null)
            {
                MessageBox.Show("لا يوجد فرق تقييم — لم يُنشأ قيد.", "BRQOSH",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"تم ترحيل قيد التقييم رقم {entry.EntryNumber ?? entry.Id.ToString()}.", "BRQOSH",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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
