using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط3-٢د) <b>شركاتُ التأمين — كِيانٌ يُنشأ ضمناً ولا بابَ له.</b>
///
/// <para><b>ما كان:</b> <c>InsurancePayer</c> لا تُنشأ إلا <b>أثراً جانبياً لحوار
/// الوثيقة</b>: يكتب المستخدمُ اسمَ الشركة في حقلٍ حرّ، فتُنشأ شركةٌ وعميلٌ لها برمزٍ
/// مقتطَعٍ من أوّل اثنين وثلاثين حرفاً من الاسم. <b>فخطأُ حرفٍ واحدٍ يُنشئ شركةً ثانية</b>
/// بذمّةٍ ثانية، ولا شيءَ في النظام يُظهر ذلك ولا يُصلحه.</para>
///
/// <para><b>وأثرُه في المطالبات</b>: دورةُ <c>ط3-٢ب</c> تُجمِّع مطالبةً <b>لشركةٍ
/// بعينها</b> — وشركتان بالاسم نفسِه تُنتجان مطالبتين نصفَين، <b>ويُغلق كلٌّ منهما على
/// حدة</b>، فلا يُطابق كشفُ الشركة مطالبتَها أبداً.</para>
///
/// <para><b>ورصيدُ الشركة يُقرأ من دفتر العملاء نفسِه</b> — لأنّها عميلٌ له ذمّة، وهو
/// القرارُ المعماريّ الذي بُنيت عليه <c>ط3-٢</c>.</para>
/// </summary>
public partial class InsurancePayersPage : UserControl
{
    public record PayerRow(long Id, string Code, string NameAr, int PaymentTermDays,
        decimal Balance, string StatusAr, bool IsActive);

    public InsurancePayersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var payers = await db.InsurancePayers.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId)
                .OrderBy(p => p.Code)
                .ToListAsync();

            var balances = await new CustomerBalanceQuery(db).AllBalancesAsync(AppServices.CompanyId);

            var rows = payers.Select(p => new PayerRow(p.Id, p.Code, p.NameAr, p.PaymentTermDays,
                balances.TryGetValue(p.CustomerId, out var b) ? b : 0m,
                p.IsActive ? "نشطة" : "موقوفة", p.IsActive)).ToList();

            Grid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(Grid, rows.Count);

            var owed = rows.Sum(r => r.Balance);
            SummaryText.Text = rows.Count == 0
                ? "لا شركاتِ تأمينٍ مسجّلة — وأضِفْها قبل إصدار الوثائق."
                : $"شركات: {rows.Count:N0} · نشطة: {rows.Count(r => r.IsActive):N0} · "
                  + $"وذممٌ عليها: {owed:N2}";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnNew(object sender, RoutedEventArgs e)
    {
        if (new InsurancePayerDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    /// <summary>
    /// يُوقف شركةً — <b>إيقافٌ لا حذف</b>.
    /// </summary>
    /// <remarks>
    /// <b>وشركةٌ لها مطالباتٌ ووثائقُ لا تُحذف</b>: الحذفُ يقطع الصلةَ بين الفاتورة
    /// وصاحبها، <b>ويترك ذمّةً في دفتر العملاء بلا اسمٍ يُفسّرها</b>. والإيقافُ يمنع
    /// وثائقَ جديدةً ويُبقي ما مضى مقروءاً.
    /// </remarks>
    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not PayerRow row)
        {
            UiFeedback.ShowWarning("اختر شركة تأمين من الجدول.");
            return;
        }

        if (!row.IsActive)
        {
            UiFeedback.ShowWarning("الشركة موقوفة بالفعل.");
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var payer = await db.InsurancePayers.SingleAsync(p => p.Id == row.Id);
            payer.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
