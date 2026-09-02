using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Payroll;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط2-٧) <b>دورةُ الرواتب تُبنى من سجلّ الموظفين.</b>
///
/// <para><b>ما كانت:</b> جدولاً فارغاً <b>يُكتب فيه اسمُ الموظّف نصّاً حرّاً</b> وتُكتب
/// حصّتا التأمينات بالأصابع. فسطرٌ بلا <c>EmployeeId</c> <b>يتخطّاه بانيَ ملفّ حماية
/// الأجور</b> — والملفُّ يخرج فارغاً لواجبٍ إلزاميٍّ نظاماً؛ والقسيمةُ بلا هويّةٍ ولا
/// آيبان؛ <b>والسُّلفةُ لا تُستقطَع أبداً</b>.</para>
///
/// <para><b>والحسابُ ليس هنا</b>: <see cref="PayrollRunBuilder"/> يبنيه ويُقاس في
/// <c>PayrollRealPathTests</c>. <b>وهذه الشاشةُ تعرض ما حُسب وتختار مَن يُدرَج</b> —
/// ومنطقٌ في <c>xaml.cs</c> لا يُقاس.</para>
/// </summary>
public partial class PayrollDialog : Window
{
    /// <summary>صفُّ موظّفٍ مرشَّح — <b>كلُّ أرقامه محسوبةٌ ولا تُحرَّر</b>.</summary>
    public class CandidateRow : NotifyingRow
    {
        private bool _included = true;

        public bool Included
        {
            get => _included;
            set => Set(ref _included, value);
        }

        public long EmployeeId { get; init; }
        public string Code { get; init; } = "";
        public string NameAr { get; init; } = "";
        public decimal BasicSalary { get; init; }
        public decimal HousingAllowance { get; init; }
        public decimal TransportAllowance { get; init; }
        public decimal GosiEmployee { get; init; }
        public decimal GosiEmployer { get; init; }
        public decimal AdvanceDeduction { get; init; }
        public decimal NetPay { get; init; }
    }

    private readonly ObservableCollection<CandidateRow> _rows = [];

    public PayrollDialog()
    {
        InitializeComponent();
        PayDateBox.SelectedDate = DateTime.Today;
        LinesGrid.ItemsSource = _rows;
        Loaded += async (_, _) => await LoadCandidatesAsync();
    }

    private async void OnPreview(object sender, RoutedEventArgs e) => await LoadCandidatesAsync();

    /// <summary>
    /// يُعيد ترشيحَ الموظفين لتاريخ الصرف المُدخَل.
    /// </summary>
    /// <remarks>
    /// <b>والقائمةُ تتغيّر بتغيّر التاريخ</b>: مَن انتهت خدمتُه قبله لا يُرشَّح،
    /// <b>وقسطُ سُلفته يُحدُّ بما يسع صافيه</b>.
    /// </remarks>
    private async Task LoadCandidatesAsync()
    {
        try
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (PayDateBox.SelectedDate is null)
                throw new AccountingException("تاريخ الصرف مطلوب.");

            var payDate = DateOnly.FromDateTime(PayDateBox.SelectedDate.Value);

            await using var db = AppServices.CreateDb();
            var candidates = await new PayrollRunBuilder(db)
                .CandidatesAsync(AppServices.CompanyId, payDate);

            var previouslyExcluded = _rows.Where(r => !r.Included)
                .Select(r => r.EmployeeId).ToHashSet();

            _rows.Clear();
            foreach (var candidate in candidates)
            {
                _rows.Add(new CandidateRow
                {
                    Included = !previouslyExcluded.Contains(candidate.EmployeeId),
                    EmployeeId = candidate.EmployeeId,
                    Code = candidate.Code,
                    NameAr = candidate.NameAr,
                    BasicSalary = candidate.BasicSalary,
                    HousingAllowance = candidate.HousingAllowance,
                    TransportAllowance = candidate.TransportAllowance,
                    GosiEmployee = candidate.GosiEmployee,
                    GosiEmployer = candidate.GosiEmployer,
                    AdvanceDeduction = candidate.AdvanceDeduction,
                    NetPay = candidate.NetPay,
                });
            }

            if (_rows.Count == 0)
                ShowError($"لا يوجد موظف على رأس العمل في {payDate:yyyy-MM-dd} — أضف موظفين من شاشة الموظفين أولاً.");
        }
        catch (Exception ex)
        {
            ShowError(ex is AccountingException ae ? ae.Message : ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            ErrorText.Visibility = Visibility.Collapsed;

            if (!int.TryParse(YearBox.Text, out var year) || year < 2000)
                throw new AccountingException("السنة غير صالحة.");
            if (!int.TryParse(MonthBox.Text, out var month) || month is < 1 or > 12)
                throw new AccountingException("الشهر غير صالح.");
            if (PayDateBox.SelectedDate is null)
                throw new AccountingException("تاريخ الصرف مطلوب.");

            var included = _rows.Where(r => r.Included).Select(r => r.EmployeeId).ToList();
            if (included.Count == 0)
                throw new AccountingException("أدرج موظفاً واحداً على الأقل.");

            await using var db = AppServices.CreateDb();
            var run = await new PayrollRunBuilder(db).BuildAsync(
                AppServices.CompanyId, year, month,
                DateOnly.FromDateTime(PayDateBox.SelectedDate.Value), included);

            db.PayrollRuns.Add(run);
            await db.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex is AccountingException ae ? ae.Message : ex.Message);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
