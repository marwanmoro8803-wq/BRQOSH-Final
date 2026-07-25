using System.Windows;
using Brqosh.Domain.Sales;

namespace Brqosh.Desktop.Views.Pages;

public partial class DocumentLinesViewDialog : Window
{
    public DocumentLinesViewDialog(string title, string header, IReadOnlyList<SalesInvoiceLine> lines,
        decimal subtotal, decimal vat, decimal total)
    {
        InitializeComponent();
        TitleText.Text = title;
        HeaderText.Text = header;
        LinesGrid.ItemsSource = lines;
        TotalsText.Text = $"قبل الضريبة: {subtotal:N2}  |  الضريبة: {vat:N2}  |  الإجمالي: {total:N2}";
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
