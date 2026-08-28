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
                postJournal: PostJournalBox.IsChecked == true);

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
