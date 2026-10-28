using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Privacy;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// ‏الخصوصية وحماية البيانات: نصُّ السياسة، وتصديرُ بيانات عميلٍ أو موظفٍ لصاحبها، وإخفاؤها — للمدير وحدَه
/// (<see cref="IPersonalDataService"/> يُسجّل العمليتين في سجل التدقيق ولا يمسّ المستندات المرحَّلة).
/// </summary>
public partial class PrivacyDataPage : UserControl
{
    public sealed record SubjectItem(long Id, string Name);

    public PrivacyDataPage()
    {
        InitializeComponent();
        PolicyText.Text = LocaleManager.T(PolicyAr, PolicyEn);
        SubjectTypeCombo.ItemsSource = new[] { LocaleManager.T("عميل", "Customer"), LocaleManager.T("موظف", "Employee") };
        SubjectTypeCombo.SelectedIndex = 0;
        SubjectTypeCombo.SelectionChanged += async (_, _) => await UiFeedback.RunSafeAsync(LoadSubjectsAsync);
        ConfirmBox.Checked += (_, _) => AnonymizeButton.IsEnabled = true;
        ConfirmBox.Unchecked += (_, _) => AnonymizeButton.IsEnabled = false;
        Loaded += async (_, _) => await UiFeedback.RunSafeAsync(LoadSubjectsAsync);
    }

    private PersonalDataSubjectType SubjectType =>
        SubjectTypeCombo.SelectedIndex == 1 ? PersonalDataSubjectType.Employee : PersonalDataSubjectType.Customer;

    private async Task LoadSubjectsAsync()
    {
        using var facade = new DesktopAccountingFacade();
        var companyId = AppServices.CompanyId;
        List<SubjectItem> items = SubjectType == PersonalDataSubjectType.Employee
            ? await facade.Db.Employees.AsNoTracking().Where(e => e.CompanyId == companyId)
                .OrderBy(e => e.Code).Select(e => new SubjectItem(e.Id, e.Code + " — " + e.NameAr)).ToListAsync()
            : await facade.Db.Customers.AsNoTracking().Where(c => c.CompanyId == companyId)
                .OrderBy(c => c.NameAr).Select(c => new SubjectItem(c.Id, c.NameAr)).ToListAsync();
        SubjectCombo.ItemsSource = items;
        SubjectCombo.SelectedIndex = items.Count > 0 ? 0 : -1;
        UiTestAutomation.PublishSummary(ResultText, ResultText.Text);
    }

    private async void OnExport(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ExportAsync);

    private async void OnAnonymize(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(AnonymizeAsync);

    private SubjectItem? Selected()
    {
        if (SubjectCombo.SelectedItem is SubjectItem item)
            return item;
        ResultText.Text = LocaleManager.T("اختر صاحب البيانات أولاً.", "Choose the data subject first.");
        UiTestAutomation.PublishSummary(ResultText, ResultText.Text);
        return null;
    }

    private async Task ExportAsync()
    {
        if (Selected() is not { } subject)
            return;

        using var facade = new DesktopAccountingFacade();
        var export = await facade.GetService<IPersonalDataService>().ExportAsync(
            AppServices.CompanyId, SubjectType, subject.Id, SecurityContext.ResolveUsername());

        var dialog = new SaveFileDialog
        {
            FileName = $"personal-data-{SubjectType.ToString().ToLowerInvariant()}-{subject.Id}.json",
            Filter = "JSON (*.json)|*.json",
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        await File.WriteAllTextAsync(dialog.FileName, export.ToJson());
        ResultText.Text = LocaleManager.T("صُدِّرت البيانات إلى: ", "Exported to: ") + dialog.FileName;
        UiTestAutomation.PublishSummary(ResultText, ResultText.Text);
    }

    private async Task AnonymizeAsync()
    {
        if (ConfirmBox.IsChecked != true || Selected() is not { } subject)
            return;

        using (var facade = new DesktopAccountingFacade())
        {
            var result = await facade.GetService<IPersonalDataService>().AnonymizeAsync(
                AppServices.CompanyId, SubjectType, subject.Id, SecurityContext.ResolveUsername());
            ResultText.Text = LocaleManager.T("أُخفيت البيانات الشخصية. المستندات المرحَّلة المحتفظ بها: ",
                                  "Personal data anonymized. Posted documents retained: ") + result.RetainedDocuments;
        }

        ConfirmBox.IsChecked = false;
        UiTestAutomation.PublishSummary(ResultText, ResultText.Text);
        await LoadSubjectsAsync();
    }

    private const string PolicyAr =
        "يحفظ برقوش البيانات الشخصية اللازمة لتشغيل دفاتر منشأتك فقط: أسماء العملاء والموظفين وأرقام هوياتهم وبيانات التواصل والحسابات البنكية والرواتب.\n\n"
        + "تُحفظ هذه البيانات في قاعدة بيانات منشأتك على أجهزتها، ولا يرسل البرنامج منها شيئاً إلى المورّد. وما يخرج من المنشأة هو ما تفرضه الفوترة الإلكترونية إلى هيئة الزكاة والضريبة والجمارك، وما تختار المنشأة إرساله بنفسها.\n\n"
        + "لصاحب البيانات حق الاطلاع على بياناته والحصول على نسخة منها وطلب إخفائها. صدّر نسخته من هنا بصيغة مقروءة آلياً، أو أخفِ بياناته الشخصية من بطاقته.\n\n"
        + "المستندات المرحَّلة (الفواتير والقيود) يُحتفظ بها كما هي للمدة النظامية ولا تُعدَّل عند الإخفاء.\n\n"
        + "التصدير والإخفاء للمدير فقط، ويُسجَّل كل منهما في سجل التدقيق.";

    private const string PolicyEn =
        "BRQOSH keeps only the personal data your books need: names of customers and employees, their ID numbers, contact details, bank accounts and salaries.\n\n"
        + "This data is stored in your company's database on its own computers, and the program sends none of it to the vendor. What leaves the company is what e-invoicing requires to be sent to ZATCA, and whatever the company chooses to send itself.\n\n"
        + "Data subjects may see their data, receive a copy of it and ask for it to be anonymized. Export their copy here in a machine-readable format, or anonymize the personal data on their record.\n\n"
        + "Posted documents (invoices and journal entries) are retained unchanged for the statutory period and are not altered by anonymization.\n\n"
        + "Export and anonymization are for the administrator only, and each is recorded in the audit log.";
}
