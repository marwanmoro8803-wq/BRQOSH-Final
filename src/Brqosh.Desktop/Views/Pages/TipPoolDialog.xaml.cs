using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط3-٣د) إنشاءُ وعاءِ إكرامياتٍ وقسمتُه.
///
/// <para><b>ورسمُ الخدمة لا يدخل هنا</b>: هو إيرادٌ خاضعٌ للضريبة وسطرٌ في الفاتورة،
/// <b>وإدخالُه الوعاءَ يكتم ضريبةً تجب</b> — وهي مخالفة.</para>
///
/// <para><b>والقسمةُ بالتساوي تُقفل الوعاءَ بالضبط</b>: آخرُ حصّةٍ تأخذ الباقي، فلا يبقى
/// فلسٌ يمنع الإقفالَ إلى الأبد.</para>
/// </summary>
public partial class TipPoolDialog : Window
{
    public record EmployeeOption(long Id, string Label);

    public class StaffRow
    {
        public long EmployeeId { get; set; }
    }

    private readonly ObservableCollection<StaffRow> _rows = [];

    public TipPoolDialog()
    {
        InitializeComponent();
        DateBox.SelectedDate = DateTime.Today;
        StaffGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadEmployeesAsync();
    }

    private async Task LoadEmployeesAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var employees = await db.Employees.AsNoTracking()
                .Where(e => e.CompanyId == AppServices.CompanyId && e.IsActive)
                .OrderBy(e => e.NameAr)
                .Select(e => new { e.Id, e.Code, e.NameAr })
                .ToListAsync();

            EmployeeColumn.ItemsSource = employees
                .Select(e => new EmployeeOption(e.Id, $"{e.Code} — {e.NameAr}"))
                .ToList();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        StaffGrid.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            if (DateBox.SelectedDate is null)
                throw new AccountingException("تاريخ الوعاء مطلوب.");

            var cash = ParseAmount(CashBox.Text, "الإكراميات النقدية");
            var card = ParseAmount(CardBox.Text, "إكراميات البطاقة");

            var pool = TipPool.Open(AppServices.CompanyId,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value));

            if (cash > 0m) pool.Collect(TipSource.Cash, cash);
            if (card > 0m) pool.Collect(TipSource.Card, card);

            var staff = _rows.Where(r => r.EmployeeId > 0)
                .Select(r => r.EmployeeId).Distinct().ToList();

            if (staff.Count > 0) pool.DistributeEvenly(staff);

            await using var db = AppServices.CreateDb();
            db.TipPools.Add(pool);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal ParseAmount(string? text, string field)
    {
        if (NumericInput.TryParse(text, out var value))
            return value;

        throw new AccountingException($"مبلغ «{field}» غير صالح.");
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
