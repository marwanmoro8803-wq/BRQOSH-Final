using Brqosh.Infrastructure.Platform;
using System.Windows;
using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class CostCenterDialog : Window
{
    private readonly long? _centerId;

    public CostCenterDialog() => InitializeComponent();

    public CostCenterDialog(CostCenter center) : this()
    {
        _centerId = center.Id;
        CodeBox.Text = center.Code;
        CodeBox.IsEnabled = false;
        NameBox.Text = center.NameAr;
        Title = "تعديل مركز تكلفة";
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            await using var db = AppServices.CreateDb();
            if (_centerId is long id)
            {
                var center = await db.CostCenters.SingleAsync(c => c.Id == id);
                center.Update(NameBox.Text);
            }
            else
            {
                // (CEEF-229 · م2-١٠ / D-6) ‏فحصُ الكود قبل الحفظ — وإلا تحدّثت قاعدةُ
                // ‏البيانات بلغتها: انتهاكُ تفرّدٍ يُعرض نصُّه الإنجليزيّ في حوارٍ عربيّ،
                // ‏والمالكُ لا يرى الصفَّ الحاجز في أيّ قائمة (قد يكون محذوفاً).
                var availability = await new CodeAvailabilityQuery(db)
                    .CheckAsync(CodeAvailabilityScope.CostCenter, AppServices.CompanyId, CodeBox.Text);
                if (CodeAvailabilityQuery.Explain(availability, CodeAvailabilityScope.CostCenter, CodeBox.Text.Trim())
                    is string reason)
                    throw new AccountingException(reason);

                var center = CostCenter.Create(AppServices.CompanyId, CodeBox.Text, NameBox.Text);
                if (await db.CostCenters.AnyAsync(c => c.CompanyId == AppServices.CompanyId && c.Code == center.Code))
                    throw new AccountingException($"كود مركز التكلفة '{center.Code}' مستخدم مسبقاً.");
                db.CostCenters.Add(center);
            }

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
