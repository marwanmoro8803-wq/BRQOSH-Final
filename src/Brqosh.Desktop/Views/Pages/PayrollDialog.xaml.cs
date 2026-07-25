using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;

namespace Brqosh.Desktop.Views.Pages;

public partial class PayrollDialog : Window
{
    public class LineRow
    {
        public string? EmployeeName { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal HousingAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal GosiEmployee { get; set; }
        public decimal GosiEmployer { get; set; }
    }

    private readonly ObservableCollection<LineRow> _rows = [];

    public PayrollDialog()
    {
        InitializeComponent();
        PayDateBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (!int.TryParse(YearBox.Text, out var year) || year < 2000)
                throw new AccountingException("السنة غير صالحة.");
            if (!int.TryParse(MonthBox.Text, out var month) || month is < 1 or > 12)
                throw new AccountingException("الشهر غير صالح.");
            if (PayDateBox.SelectedDate is null)
                throw new AccountingException("تاريخ الصرف مطلوب.");

            var run = PayrollRun.CreateDraft(AppServices.CompanyId, year, month,
                DateOnly.FromDateTime(PayDateBox.SelectedDate.Value));

            foreach (var row in _rows.Where(r => !string.IsNullOrWhiteSpace(r.EmployeeName)))
            {
                run.AddLine(row.EmployeeName!, row.BasicSalary, row.HousingAllowance,
                    row.TransportAllowance, row.GosiEmployee, row.GosiEmployer);
            }

            if (run.Lines.Count == 0)
                throw new AccountingException("أضف موظفاً واحداً على الأقل.");

            await using var db = AppServices.CreateDb();
            db.PayrollRuns.Add(run);
            await db.SaveChangesAsync();

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
