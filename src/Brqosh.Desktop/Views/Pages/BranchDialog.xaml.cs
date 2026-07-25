using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class BranchDialog : Window
{
    public BranchDialog() => InitializeComponent();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var companyId = AppServices.CompanyId;
            var isHq = HeadquartersBox.IsChecked == true;

            var branch = Branch.Create(companyId, CodeBox.Text, NameBox.Text, isHq);
            if (await db.Branches.AnyAsync(b => b.CompanyId == companyId && b.Code == branch.Code))
                throw new AccountingException($"كود الفرع '{branch.Code}' مستخدم مسبقاً.");

            if (isHq)
            {
                var others = await db.Branches
                    .Where(b => b.CompanyId == companyId && b.IsHeadquarters)
                    .ToListAsync();
                foreach (var other in others)
                    other.ClearHeadquarters();
            }

            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex is AccountingException ae ? ae.Message : ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
