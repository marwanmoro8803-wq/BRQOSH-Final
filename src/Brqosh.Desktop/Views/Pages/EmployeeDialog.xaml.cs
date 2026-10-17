using System.Globalization;
using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٢) إنشاءُ موظفٍ وتعديلُه.
///
/// <para><b>ولا يُتحقّق من شيءٍ هنا</b>: الهويّةُ عشرةُ أرقام، والآيبانُ سعوديٌّ صحيح،
/// والأجرُ غيرُ سالب — <b>كلُّها في النطاق</b>. والشاشةُ تُمرّر وتعرض ما رُفض. <b>وتكرارُ
/// الفحص هنا يُنشئ حقيقتين تختلفان يوماً</b>، ويُغري بإضعاف إحداهما «لأنّ الأخرى تحرس».</para>
/// </summary>
public partial class EmployeeDialog : Window
{
    private readonly long? _employeeId;

    public EmployeeDialog()
    {
        InitializeComponent();
        HireDateBox.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => { await LoadCostCentersAsync(); await LoadProjectsAsync(); };
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٤٦ · <c>sector-modules#9</c>) المشاريعُ التي تقبل تكلفة — والأوّلُ «بلا مشروع»؛
    /// ومشروعُ الموظّف الحاليُّ يبقى معروضاً ولو انتهى، كي يُرى ويُعاد إسنادُه.
    /// </summary>
    private async Task LoadProjectsAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var projects = await db.ConstructionProjects
                .Where(p => p.CompanyId == AppServices.CompanyId
                            && ((p.Status != Domain.Construction.ConstructionProjectStatus.Completed
                                 && p.Status != Domain.Construction.ConstructionProjectStatus.Cancelled)
                                || p.Id == _selectedProjectId))
                .OrderBy(p => p.Code)
                .Select(p => new CostCenterOption(p.Id, p.Code + " — " + p.NameAr))
                .ToListAsync();

            ProjectBox.ItemsSource = new List<CostCenterOption>
            {
                new(0L, LocaleManager.T("بلا مشروع", "No project")),
            }.Concat(projects).ToList();

            ProjectBox.SelectedValue = _selectedProjectId ?? 0L;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private long? _selectedProjectId;

    /// <summary>
    /// (CEEF-232 · م-١) مراكزُ التكلفة النشطة — <b>و«بلا مركز» خيارٌ أوّل مقصود</b>.
    /// </summary>
    /// <remarks>
    /// <b>وكان <c>Employee.CostCenterId</c> عموداً في المخطَّط بلا حقلٍ في أيّ شاشة</b>،
    /// و<c>AssignCostCenter</c> بلا مستدعٍ واحد. <b>فقيدُ الرواتب يخرج بلا بُعدٍ أبداً</b>
    /// وتُسقط كلُّ قائمةِ مركزِ تكلفةٍ أكبرَ بنودها.
    ///
    /// <para><b>ولا يُفرَض المركزُ إلزاماً</b>: منشأةٌ لا تُمسك مراكزَ تكلفةٍ لا تُمنع من
    /// إضافة موظّف، <b>ومَن بلا مركزٍ يبقى في سطرٍ بلا بُعد</b> لا يُنسب بالتخمين.</para>
    /// </remarks>
    private async Task LoadCostCentersAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var centers = await db.CostCenters
                .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new CostCenterOption(c.Id, c.NameAr))
                .ToListAsync();

            CostCenterBox.ItemsSource = new List<CostCenterOption>
            {
                new(0L, LocaleManager.T("بلا مركز تكلفة", "No cost center")),
            }.Concat(centers).ToList();

            CostCenterBox.SelectedValue = _selectedCostCenterId ?? 0L;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>خيارُ مركزِ تكلفةٍ في القائمة — و<c>Id = 0</c> يعني «بلا مركز».</summary>
    public record CostCenterOption(long Id, string NameAr);

    private long? _selectedCostCenterId;

    public EmployeeDialog(Employee employee) : this()
    {
        ArgumentNullException.ThrowIfNull(employee);

        _employeeId = employee.Id;
        HeaderText.Text = $"تعديل الموظف {employee.Code}";

        CodeBox.Text = employee.Code;
        CodeBox.IsEnabled = false;
        NameBox.Text = employee.NameAr;
        NationalIdBox.Text = employee.NationalId;
        NationalIdBox.IsEnabled = false;
        NationalityBox.SelectedIndex = employee.Nationality == EmployeeNationality.Saudi ? 0 : 1;
        GosiRegimeBox.SelectedIndex = employee.GosiRegime == GosiRegime.NewLaw2024 ? 1 : 0;
        JobTitleBox.Text = employee.JobTitle;
        HireDateBox.SelectedDate = employee.HireDate.ToDateTime(TimeOnly.MinValue);
        HireDateBox.IsEnabled = false;
        BasicBox.Text = employee.BasicSalary.ToString(CultureInfo.InvariantCulture);
        HousingBox.Text = employee.HousingAllowance.ToString(CultureInfo.InvariantCulture);
        TransportBox.Text = employee.TransportAllowance.ToString(CultureInfo.InvariantCulture);
        OtherBox.Text = employee.OtherAllowance.ToString(CultureInfo.InvariantCulture);
        IbanBox.Text = employee.Iban ?? string.Empty;
        _selectedCostCenterId = employee.CostCenterId;
        _selectedProjectId = employee.ProjectId;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var basic = Money(BasicBox.Text, "الأساسي");
            var housing = Money(HousingBox.Text, "بدل السكن");
            var transport = Money(TransportBox.Text, "بدل النقل");
            var other = Money(OtherBox.Text, "البدلات الأخرى");
            var iban = string.IsNullOrWhiteSpace(IbanBox.Text) ? null : IbanBox.Text.Trim();

            // ‏(CEEF-232 · م-١) و«بلا مركز» يُخزَّن `null` لا صفراً — **فصفرٌ مفتاحٌ لا وجودَ له**.
            var costCenterId = CostCenterBox.SelectedValue is long cc && cc > 0 ? cc : (long?)null;
            var projectId = ProjectBox.SelectedValue is long pj && pj > 0 ? pj : (long?)null;

            if (_employeeId is long id)
            {
                var existing = await db.Employees.SingleAsync(x => x.Id == id);
                existing.Rename(NameBox.Text, JobTitleBox.Text);
                existing.SetPay(basic, housing, transport, other);
                existing.SetIban(iban);
                existing.AssignCostCenter(costCenterId);
                existing.AssignProject(projectId);
                existing.SetGosiRegime(SelectedGosiRegime());
            }
            else
            {
                if (HireDateBox.SelectedDate is null)
                    throw new AccountingException("تاريخ التعيين مطلوب.");

                var employee = Employee.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                    NationalIdBox.Text,
                    NationalityBox.SelectedIndex == 0 ? EmployeeNationality.Saudi : EmployeeNationality.NonSaudi,
                    JobTitleBox.Text, DateOnly.FromDateTime(HireDateBox.SelectedDate.Value));

                employee.SetPay(basic, housing, transport, other);
                employee.SetIban(iban);
                employee.AssignCostCenter(costCenterId);
                employee.AssignProject(projectId);
                employee.SetGosiRegime(SelectedGosiRegime());
                db.Employees.Add(employee);
            }

            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary><b>ورقمٌ لا يُقرأ يُسمّى حقلَه</b> — فلا يبحث المستخدم عن الخطأ في أربعة حقول.</summary>
    private static decimal Money(string text, string field) =>
        NumericInput.TryParse(text, out var value)
            ? value
            : throw new AccountingException($"قيمة «{field}» غير صالحة — ووردت «{text}».");

    /// <summary>‏(ح-١٣٨) فئةُ الاشتراك كما اختيرت — والنطاقُ يرفض «الجديد» لغير السعوديّ.</summary>
    private GosiRegime SelectedGosiRegime() =>
        GosiRegimeBox.SelectedIndex == 1 ? GosiRegime.NewLaw2024 : GosiRegime.Legacy;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
