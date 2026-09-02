using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Setup;
using Microsoft.Win32;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٦) <b>ترحيلُ البيانات — وهو أوّلُ ما يحتاجه عميلٌ ينتقل من نظامٍ آخر.</b>
///
/// <para><b>ما كان:</b> <c>CsvDataMigrationService</c> مبنيّةٌ ومسجَّلةٌ في الحقن،
/// <b>ولا شاشةَ لها</b>. فالمنشأةُ التي تنتقل من نظامٍ سابق <b>تُدخل دليلَ حساباتها
/// وعملاءها سطراً سطراً بيدها</b> — أو لا تنتقل.</para>
///
/// <para><b>وما تعذّر يُعرض بسطره وسببه</b>: ملفٌّ من ألف سطرٍ يُرفض جملةً بلا بيانٍ
/// <b>لا يُصلَح أبداً</b> — والمستخدمُ لا يعرف أيَّ سطرٍ يُراجع.</para>
/// </summary>
public partial class DataMigrationPage : UserControl
{
    public record Row(int LineNumber, string State, string Error);

    public DataMigrationPage()
    {
        InitializeComponent();
        Loaded += (_, _) => PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
            (ImportAccountsBtn, PermissionService.CanPostAccounting),
            (ImportCustomersBtn, PermissionService.CanPostAccounting));
    }

    private async void OnImportAccounts(object sender, RoutedEventArgs e) =>
        await ImportAsync(accounts: true);

    private async void OnImportCustomers(object sender, RoutedEventArgs e) =>
        await ImportAsync(accounts: false);

    private async Task ImportAsync(bool accounts)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "ملف نصي مفصول بفواصل (*.csv)|*.csv|كل الملفات (*.*)|*.*",
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var csv = await File.ReadAllTextAsync(dialog.FileName, Encoding.UTF8);

            using var facade = new DesktopAccountingFacade();
            var service = facade.GetService<CsvDataMigrationService>();

            var result = accounts
                ? await service.ImportChartOfAccountsCsvAsync(AppServices.CompanyId, csv)
                : await service.ImportCustomersCsvAsync(AppServices.CompanyId, csv);

            Show(result);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void Show(DataMigrationResult result)
    {
        // ‏**والناجحُ يُعرض مع الفاشل**: قائمةُ أخطاءٍ وحدَها تُخفي أنّ الاستيراد وقع
        // ‏جزئياً — **فيُعاد تشغيلُه فتتكرّر الصفوف**.
        var rows = result.Details
            .Select(d => new Row(d.LineNumber, d.Success ? "استُورد" : "تعذّر", d.Error ?? "—"))
            .OrderBy(r => r.State == "استُورد" ? 1 : 0)
            .ThenBy(r => r.LineNumber)
            .ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        SummaryText.Text = result.Skipped == 0
            ? $"استُورد {result.Imported:N0} من {result.TotalRows:N0} سطرًا — بلا تعذُّر."
            : $"استُورد {result.Imported:N0} من {result.TotalRows:N0} سطرًا · "
              + $"وتعذّر {result.Skipped:N0} — وكلُّ سطرٍ بسببه أدناه.";
    }
}
