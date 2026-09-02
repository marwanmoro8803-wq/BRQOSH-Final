using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٢) <b>ملفُّ حماية الأجور — الشاشةُ التي بلا وجودها بقي `ط2-٥` وعداً.</b>
///
/// <para><b>ما كان:</b> <c>WageProtectionFileBuilder</c> مبنيٌّ ومختبَرٌ في <c>ط2-٥</c>،
/// <b>ولا شاشةَ تُخرجه</b>. والملفُّ <b>إلزاميٌّ نظاماً</b>: بغيره لا يُودِع البنكُ الرواتب،
/// وتُسجَّل المنشأةُ مخالِفةً في «حماية الأجور» فتُحجب عنها خدماتُ وزارة الموارد البشرية.</para>
///
/// <para><b>وما تعذّر يُعرض بأسمائه قبل الحفظ</b>: موظّفٌ بلا آيبانٍ لا يدخل الملفّ،
/// <b>واكتشافُ ذلك في البنك ليلةَ الإيداع يُؤخّر رواتبَ الجميع</b>. فالرفضُ يُرى هنا،
/// ومعه ما يُصلحه.</para>
/// </summary>
public partial class WageProtectionPage : UserControl
{
    private sealed record RunOption(long Id, string Label)
    {
        public override string ToString() => Label;
    }

    private WageProtectionResult? _result;
    private string _periodCode = string.Empty;

    public WageProtectionPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (ExportBtn, PermissionService.CanPostAccounting));
            await LoadRunsAsync();
        };
    }

    private async Task LoadRunsAsync()
    {
        await using var db = AppServices.CreateDb();

        var runs = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.CompanyId == AppServices.CompanyId && r.Status == PayrollRunStatus.Posted)
            .OrderByDescending(r => r.PayDate)
            .Select(r => new { r.Id, r.PeriodCode, r.PayDate })
            .ToListAsync();

        RunBox.ItemsSource = runs
            .Select(r => new RunOption(r.Id, $"{r.PeriodCode} — صُرفت في {r.PayDate:yyyy-MM-dd}"))
            .ToList();

        if (runs.Count == 0)
        {
            SummaryText.Text = "لا دورةَ رواتبٍ مرحّلة بعد — ولا يُصدَّر الملفُّ من مسودّة.";
            ExportBtn.IsEnabled = false;
            return;
        }

        RunBox.SelectedIndex = 0;
    }

    private async void OnRunChanged(object sender, SelectionChangedEventArgs e) => await BuildAsync();

    private async Task BuildAsync()
    {
        if (RunBox.SelectedItem is not RunOption option) return;

        try
        {
            await using var db = AppServices.CreateDb();
            _result = await new WageProtectionFileBuilder(db).BuildAsync(AppServices.CompanyId, option.Id);
            _periodCode = option.Label.Split(' ')[0];

            CsvBox.Text = _result.Csv;

            SummaryText.Text =
                $"سجلات في الملف: {_result.Records:N0} · إجمالي الصافي: {_result.TotalNet:N2}";

            if (_result.Refusals.Count > 0)
            {
                RefusalList.ItemsSource = _result.Refusals;
                RefusalCard.Visibility = Visibility.Visible;
                SummaryText.Text += $" · وتعذّر {_result.Refusals.Count:N0}";
            }
            else
            {
                RefusalCard.Visibility = Visibility.Collapsed;
            }

            ExportBtn.IsEnabled = _result.Records > 0;
        }
        catch (Exception ex)
        {
            _result = null;
            CsvBox.Text = string.Empty;
            RefusalCard.Visibility = Visibility.Collapsed;
            ExportBtn.IsEnabled = false;
            SummaryText.Text = ex.Message;
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (_result is null || _result.Records == 0)
        {
            UiFeedback.ShowWarning("لا سجلاتٍ في الملف — راجعْ أسبابَ التعذّر أعلاه.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = $"wps-{_periodCode}.csv",
            Filter = "ملف نصي مفصول بفواصل (*.csv)|*.csv",
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            // ‏**وترميزُ UTF-8 بعلامةِ ترتيبِ بايت**: أسماءُ الموظفين عربية، وبنوكٌ كثيرةٌ
            // ‏تفتح الملفَّ في Excel — **وبغير العلامة تُقرأ الأسماءُ رموزاً**، فيُرفض الملفّ.
            File.WriteAllText(dialog.FileName, _result.Csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            UiFeedback.ShowInfo($"حُفظ الملف: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
