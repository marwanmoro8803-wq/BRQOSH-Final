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
            (ImportCustomersBtn, PermissionService.CanPostAccounting),
            (ImportSuppliersBtn, PermissionService.CanPostAccounting),
            (ImportReceivablesBtn, PermissionService.CanPostAccounting),
            (ImportPayablesBtn, PermissionService.CanPostAccounting),
            (ImportProductsBtn, PermissionService.CanPostAccounting),
            (ImportStockBtn, PermissionService.CanPostAccounting),
            (ImportOpeningBtn, PermissionService.CanPostAccounting));
    }

    private enum Kind { Accounts, Customers, Suppliers, OpeningTrialBalance, OpeningReceivables, OpeningPayables, Products, OpeningStock }

    private async void OnImportAccounts(object sender, RoutedEventArgs e) => await ImportAsync(Kind.Accounts);

    private async void OnImportCustomers(object sender, RoutedEventArgs e) => await ImportAsync(Kind.Customers);

    private async void OnImportSuppliers(object sender, RoutedEventArgs e) => await ImportAsync(Kind.Suppliers);

    private async void OnImportReceivables(object sender, RoutedEventArgs e) => await ImportAsync(Kind.OpeningReceivables);

    private async void OnImportPayables(object sender, RoutedEventArgs e) => await ImportAsync(Kind.OpeningPayables);

    private async void OnImportProducts(object sender, RoutedEventArgs e) => await ImportAsync(Kind.Products);

    private async void OnImportStock(object sender, RoutedEventArgs e) => await ImportAsync(Kind.OpeningStock);

    /// <summary>‏(ح-١٣٣) الميزانُ الافتتاحيُّ يلزمه تاريخُ بدء التشغيل — بلا تاريخٍ لا يُفتح الملفّ أصلاً.</summary>
    private async void OnImportOpening(object sender, RoutedEventArgs e)
    {
        if (OpeningDatePicker.SelectedDate is null)
        {
            UiFeedback.ShowWarning("اختر تاريخ بدء التشغيل قبل استيراد الميزان الافتتاحي.");
            return;
        }

        await ImportAsync(Kind.OpeningTrialBalance);
    }

    private async Task ImportAsync(Kind kind)
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

            var result = kind switch
            {
                Kind.Accounts => await service.ImportChartOfAccountsCsvAsync(AppServices.CompanyId, csv),
                Kind.Customers => await service.ImportCustomersCsvAsync(AppServices.CompanyId, csv),
                Kind.Suppliers => await service.ImportSuppliersCsvAsync(AppServices.CompanyId, csv),
                Kind.OpeningReceivables => await service.ImportOpeningReceivablesCsvAsync(AppServices.CompanyId, csv),
                Kind.OpeningPayables => await service.ImportOpeningPayablesCsvAsync(AppServices.CompanyId, csv),
                Kind.Products => await service.ImportProductsCsvAsync(AppServices.CompanyId, csv),
                Kind.OpeningStock => await service.ImportOpeningStockCsvAsync(AppServices.CompanyId, csv),
                _ => await service.ImportOpeningTrialBalanceCsvAsync(AppServices.CompanyId,
                    DateOnly.FromDateTime(OpeningDatePicker.SelectedDate!.Value), csv),
            };

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
