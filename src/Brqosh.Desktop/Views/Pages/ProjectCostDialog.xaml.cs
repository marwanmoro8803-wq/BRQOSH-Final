using System.Globalization;
using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Construction;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProjectCostDialog : Window
{
    private readonly long _projectId;
    private readonly string _projectName;

    public ProjectCostDialog(long projectId, string projectName)
    {
        _projectId = projectId;
        _projectName = projectName;
        InitializeComponent();
        TitleText.Text = $"تكلفة مشروع — {projectName}";
        DateBox.SelectedDate = DateTime.Today;
        CreditLabel.Text = LocaleManager.T("الحساب الدائن *", "Credit account *");
        PostJournalBox.Content = LocaleManager.T(
            "ترحيل قيد محاسبي (مدين 5105 / دائن الحساب المختار)", "Post a journal entry (Dr 5105 / Cr the chosen account)");
        Loaded += async (_, _) => await LoadCreditAccountsAsync();
    }

    public sealed record CreditOption(string Code, string Display);

    /// <summary>
    /// (ح-١٤٥ · ‏<c>sector-modules#9</c>) ما يقبله <c>ProjectCostService.CreditAccountAsync</c> وحدَه —
    /// والصندوقُ أوّلاً كما كان الافتراض.
    /// </summary>
    private async Task LoadCreditAccountsAsync()
    {
        using var facade = new DesktopAccountingFacade();
        var accounts = await facade.GetService<ProjectCostService>().CreditableAccountsAsync(AppServices.CompanyId);
        var options = accounts
            .OrderBy(a => a.Code == Brqosh.Domain.Accounting.SaudiChartOfAccounts.SystemCodes.CashOnHand ? 0 : 1)
            .ThenBy(a => a.Code, StringComparer.Ordinal)
            .Select(a => new CreditOption(a.Code, a.Code + " — " + a.NameAr))
            .ToList();
        CreditAccountCombo.ItemsSource = options;
        CreditAccountCombo.SelectedIndex = options.Count > 0 ? 0 : -1;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DateBox.SelectedDate is null)
                throw new AccountingException("التاريخ مطلوب.");
            if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
                throw new AccountingException("الوصف مطلوب.");
            if (!NumericInput.TryParse(AmountBox.Text, out var amount))
                throw new AccountingException("أدخل مبلغاً صحيحاً.");

            using var facade = new DesktopAccountingFacade();
            await facade.GetService<ProjectCostService>().RecordAsync(
                AppServices.CompanyId, _projectId,
                DateOnly.FromDateTime(DateBox.SelectedDate.Value),
                DescriptionBox.Text.Trim(), amount,
                AppServices.CurrentUser?.Username ?? "",
                postJournal: PostJournalBox.IsChecked == true,
                creditAccountCode: CreditAccountCombo.SelectedValue as string);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
