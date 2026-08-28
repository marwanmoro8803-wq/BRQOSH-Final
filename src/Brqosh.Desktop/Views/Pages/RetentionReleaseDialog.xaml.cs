using System.Globalization;
using System.Windows;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class RetentionReleaseDialog : Window
{
    public decimal Amount { get; private set; }

    public RetentionReleaseDialog(string projectName, decimal maxAmount)
    {
        InitializeComponent();
        HintText.Text = $"مشروع: {projectName}\nالحد الأقصى للإفراج: {maxAmount:N2} ر.س";
        AmountBox.Text = maxAmount.ToString("F2", CultureInfo.InvariantCulture);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!NumericInput.TryParse(AmountBox.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("أدخل مبلغاً صحيحاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Amount = amount;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
