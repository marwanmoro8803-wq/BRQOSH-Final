using System.Globalization;
using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.RealEstate;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) إنشاءُ وحدةٍ في عقارٍ محدَّد.
///
/// <para><b>ولا حالةَ إشغالٍ تُدخَل هنا</b> — الوحدةُ تولد شاغرةً، والعقدُ وحدَه يُشغلها.
/// <b>وحقلُ حالةٍ في هذه الشاشة يفتح بابَ إشغالٍ بلا عقد</b>، وهو ما أُغلق في النطاق.</para>
/// </summary>
public partial class PropertyUnitDialog : Window
{
    private readonly long _propertyId;

    public PropertyUnitDialog() : this(0) { }

    public PropertyUnitDialog(long propertyId)
    {
        InitializeComponent();
        _propertyId = propertyId;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            var unit = PropertyUnit.Create(AppServices.CompanyId, _propertyId, NumberBox.Text,
                Number(AreaBox.Text, "المساحة"),
                (int)Number(BedroomsBox.Text, "عدد الغرف"),
                Number(RentBox.Text, "الإيجار السنوي المعلن"));

            await using var db = AppServices.CreateDb();
            db.PropertyUnits.Add(unit);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal Number(string text, string field) =>
        decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new AccountingException($"قيمة «{field}» غير صالحة — ووردت «{text}».");

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
