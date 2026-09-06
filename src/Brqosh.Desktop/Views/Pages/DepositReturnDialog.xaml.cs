using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط4-٦) يسأل عن المخصوم من التأمين قبل ردّه.
///
/// <para><b>والصفرُ هو الافتراض</b>: خصمٌ يقع بلا سؤالٍ يأخذ من أمانةٍ بلا قرار.
/// <b>ولا يُتحقَّق من السقف هنا</b> — النطاقُ يرفض ما جاوز التأمين برسالته، والشاشةُ
/// تعرضها. <b>وتكرارُ الفحص يُنشئ حقيقتين تختلفان يوماً.</b></para>
/// </summary>
public partial class DepositReturnDialog : Window
{
    public decimal Forfeited { get; private set; }

    public DepositReturnDialog() : this("", 0m) { }

    public DepositReturnDialog(string contractNumber, decimal deposit)
    {
        InitializeComponent();
        HeaderText.Text = "رد تأمين العقد";
        DepositText.Text = $"العقد {contractNumber} — التأمين المقبوض {deposit:N2}";
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!NumericInput.TryParse(ForfeitedBox.Text, out var forfeited))
                throw new AccountingException($"المخصوم غير صالح — ووردَ «{ForfeitedBox.Text}».");

            Forfeited = forfeited;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
