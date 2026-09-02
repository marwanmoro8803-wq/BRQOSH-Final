using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.RealEstate;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>(CEEF-230 · ط6-٤) إنشاءُ عقار — والتحقّقُ كلُّه في النطاق.</summary>
public partial class PropertyDialog : Window
{
    public PropertyDialog() => InitializeComponent();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var property = Property.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                (PropertyKind)Math.Max(0, KindBox.SelectedIndex), CityBox.Text, AddressBox.Text);
            property.SetDeed(DeedBox.Text);

            await using var db = AppServices.CreateDb();
            db.Properties.Add(property);
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
