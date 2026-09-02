using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٢) <b>سجلُّ الموظفين — الشاشةُ التي بلا وجودها بقي `ط2-١` وعداً.</b>
///
/// <para><b>ما كان:</b> سجلُّ الموظفين بُني في <c>ط2-١</c> بهويّةٍ لا تتكرّر وآيبانٍ
/// مُتحقَّقٍ وجنسيةٍ تحكم الاشتراك — <b>ولا شاشةَ تصل إليه</b>. فالمالكُ يكتب اسمَ الموظّف
/// نصّاً حرّاً في دورة الرواتب كلَّ شهر، <b>وسجلُّه يبقى فارغاً</b>: لا قسيمةَ رواتب،
/// ولا ملفَّ أجورٍ، ولا مكافأةَ نهاية خدمةٍ تُخصَّص.</para>
/// </summary>
public partial class EmployeesPage : UserControl
{
    public record Row(long Id, string Code, string NameAr, string NationalId, string Nationality,
        string JobTitle, string HireDate, decimal BasicSalary, decimal GrossPay, string? Iban,
        string Status);

    private List<Employee> _items = [];

    public EmployeesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPostAccounting),
                (TerminateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var showTerminated = ShowTerminatedBox.IsChecked == true;

        _items = await db.Employees
            .Where(e => e.CompanyId == AppServices.CompanyId)
            .OrderBy(e => e.Code)
            .ToListAsync();

        // ‏**ومَن انتهت خدمتُه لا يُحذف بل يُخفى**: مستحقّاتُه قد تبقى، وقسائمُ ما مضى
        // ‏تُبنى من دوراتٍ تحمل اسمَه. **وحذفُه يقطع تاريخاً يُطالَب به.**
        var visible = showTerminated ? _items : [.. _items.Where(e => e.IsActive)];

        var rows = visible.Select(e => new Row(
            e.Id, e.Code, e.NameAr, e.NationalId,
            e.Nationality == EmployeeNationality.Saudi ? "سعودي" : "وافد",
            e.JobTitle, e.HireDate.ToString("yyyy-MM-dd"), e.BasicSalary, e.GrossPay, e.Iban,
            e.TerminationDate is DateOnly t ? $"انتهت في {t:yyyy-MM-dd}" : "على رأس العمل")).ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var active = _items.Count(e => e.IsActive);
        var withoutIban = _items.Count(e => e.IsActive && string.IsNullOrWhiteSpace(e.Iban));

        // ‏**ونقصُ الآيبان يُعرض هنا لا عند التصدير**: ملفُّ حماية الأجور يرفض مَن لا آيبانَ
        // ‏له، **واكتشافُه ليلةَ الإيداع يُؤخّر رواتبَ الجميع**.
        SummaryText.Text = withoutIban == 0
            ? $"موظفون على رأس العمل: {active:N0} · إجمالي الأجور: {_items.Where(e => e.IsActive).Sum(e => e.GrossPay):N2}"
            : $"موظفون على رأس العمل: {active:N0} · ⚠️ {withoutIban:N0} بلا آيبان — ولن يقبلهم ملفُّ حماية الأجور.";
    }

    private async void OnFilterChanged(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new EmployeeDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        var employee = _items.Single(x => x.Id == row.Id);
        if (new EmployeeDialog(employee) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnTerminate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        // ‏**والنصُّ حرفيٌّ والاسمُ في العنوان**: نصٌّ مركَّبٌ لا يبلغه أيُّ مترجِم فيبقى
        // ‏عربياً في واجهةٍ إنجليزية — وحارسُ `DialogLocalizationTests` يعدّه. **والاسمُ
        // ‏لا يسقط**: يُحمل في عنوان الحوار، فيرى المستخدمُ مَن يُنهي خدمتَه.
        if (UiDialog.Show("إنهاء خدمة الموظف المحدد؟ — ويبقى في السجل ولا يُحذف.", row.NameAr,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var employee = await db.Employees.SingleAsync(x => x.Id == row.Id);
            employee.Terminate(DateOnly.FromDateTime(DateTime.Today));
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
