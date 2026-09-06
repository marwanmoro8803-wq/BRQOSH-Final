using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>(CEEF-230 · ط6-٣) إنشاءُ وحدة قياس — والتحقّقُ كلُّه في النطاق.</summary>
public partial class UnitOfMeasureDialog : Window
{
    public UnitOfMeasureDialog() => InitializeComponent();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!NumericInput.TryParse(FactorBox.Text, out var factor))
                throw new AccountingException($"معامل التحويل غير صالح — ووردَ «{FactorBox.Text}».");

            var unit = UnitOfMeasure.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                UnCodeBox.Text, BaseCodeBox.Text, factor);

            await using var db = AppServices.CreateDb();
            db.UnitsOfMeasure.Add(unit);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
