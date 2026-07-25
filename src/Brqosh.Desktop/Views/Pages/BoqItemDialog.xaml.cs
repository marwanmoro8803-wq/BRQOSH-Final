using System.Windows;
using Brqosh.Domain.Construction;

namespace Brqosh.Desktop.Views.Pages;

public partial class BoqItemDialog : Window
{
    public BoqItem? Result { get; private set; }
    private readonly string? _originalItemNumber;

    public BoqItemDialog()
    {
        InitializeComponent();
        TitleText.Text = "إضافة بند لجدول الكميات";
        SaveButton.Content = "إضافة";
    }

    public BoqItemDialog(BoqItem existing) : this()
    {
        TitleText.Text = "تعديل بند BOQ";
        SaveButton.Content = "حفظ";
        _originalItemNumber = existing.ItemNumber;
        NumberBox.Text = existing.ItemNumber;
        DescBox.Text = existing.Description;
        UnitBox.Text = existing.Unit;
        QtyBox.Text = existing.Quantity.ToString("G");
        PriceBox.Text = existing.UnitPrice.ToString("G");
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!decimal.TryParse(QtyBox.Text, out var qty) || qty <= 0)
                throw new InvalidOperationException("الكمية غير صالحة.");
            if (!decimal.TryParse(PriceBox.Text, out var price) || price < 0)
                throw new InvalidOperationException("سعر الوحدة غير صالح.");

            var project = ConstructionProject.Create(AppServices.CompanyId, "TMP", "TMP");
            project.AddBoqItem(NumberBox.Text, DescBox.Text, UnitBox.Text, qty, price);
            Result = project.BoqItems[0];

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

    public string? OriginalItemNumber => _originalItemNumber;
}
