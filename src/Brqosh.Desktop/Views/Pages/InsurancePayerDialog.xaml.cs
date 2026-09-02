using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط3-٢د) إنشاءُ شركة تأمين — <b>برمزٍ يختاره المستخدم لا مقتطَعٍ من اسمها</b>.
///
/// <para><b>وما كان:</b> الرمزُ أوّلُ اثنين وثلاثين حرفاً من الاسم، والشركةُ تُنشأ أثراً
/// جانبياً لحوار الوثيقة. <b>فخطأُ حرفٍ يُنشئ شركةً ثانيةً بذمّةٍ ثانية</b>.</para>
/// </summary>
public partial class InsurancePayerDialog : Window
{
    public InsurancePayerDialog() => InitializeComponent();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (!int.TryParse(TermBox.Text, out var term) || term < 0)
                throw new AccountingException("مهلة السداد يجب أن تكون عدداً غير سالب.");

            await using var db = AppServices.CreateDb();

            var code = CodeBox.Text?.Trim() ?? "";
            if (await db.InsurancePayers.AnyAsync(p =>
                    p.CompanyId == AppServices.CompanyId && p.Code == code))
                throw new AccountingException($"الرمز «{code}» مستعمَلٌ لشركةٍ أخرى — والرمزُ لا يتكرّر.");

            // ‏**والذمّةُ أوّلاً**: `InsurancePayer.Create` يرفض شركةً بلا عميل — فالشركةُ
            // ‏في هذا النظام **عميلٌ له ذمّةٌ في الدفتر المساعد**، وهو القرارُ الذي بُنيت
            // ‏عليه فوترةُ الزيارة.
            var customer = Customer.Create(AppServices.CompanyId, NameBox.Text?.Trim() ?? "",
                VatNumberBox.Text?.Trim() ?? "");
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            var payer = InsurancePayer.Create(AppServices.CompanyId, code,
                NameBox.Text?.Trim() ?? "", customer.Id, term);
            db.InsurancePayers.Add(payer);
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
