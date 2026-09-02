using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Infrastructure.Healthcare;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط3-٢ب) بناءُ دفعة مطالبة.
///
/// <para><b>والمعاينةُ قبل الحفظ</b>: الدفعةُ تُبنى ممّا فُوتر فعلاً، <b>ومَن لا يرى ما
/// سيُضمّ يحفظ دفعةً فارغةً أو أكبرَ ممّا قصد</b> — ثمّ لا تُعدَّل بعد التقديم.</para>
/// </summary>
public partial class ClaimBuildDialog : Window
{
    public record PayerOption(long Id, string Label);

    private InsuranceClaim? _preview;

    public ClaimBuildDialog()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromBox.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ToBox.SelectedDate = today;
        Loaded += async (_, _) => await LoadPayersAsync();
    }

    private async Task LoadPayersAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var payers = await db.InsurancePayers.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
                .OrderBy(p => p.NameAr)
                .Select(p => new { p.Id, p.Code, p.NameAr })
                .ToListAsync();

            PayerBox.ItemsSource = payers
                .Select(p => new PayerOption(p.Id, $"{p.Code} — {p.NameAr}"))
                .ToList();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnPreview(object sender, RoutedEventArgs e) => await BuildAsync(save: false);

    private async void OnSave(object sender, RoutedEventArgs e) => await BuildAsync(save: true);

    private async Task BuildAsync(bool save)
    {
        try
        {
            if (PayerBox.SelectedItem is not PayerOption payer)
                throw new AccountingException("اختر شركة التأمين.");
            if (FromBox.SelectedDate is null || ToBox.SelectedDate is null)
                throw new AccountingException("حدّد المدة.");

            using var facade = new DesktopAccountingFacade();
            var service = facade.GetService<ClaimSettlementService>();

            _preview = await service.BuildAsync(AppServices.CompanyId, payer.Id,
                (NumberBox.Text ?? "").Trim(),
                DateOnly.FromDateTime(FromBox.SelectedDate.Value),
                DateOnly.FromDateTime(ToBox.SelectedDate.Value));

            if (_preview.Lines.Count == 0)
            {
                PreviewText.Text = "لا فواتيرَ على هذه الشركة في هذه المدة لم تُطالَب بعد — "
                    + "وربّما ضُمّت إلى دفعةٍ سابقة.";
                return;
            }

            PreviewText.Text = $"سيُضمّ {_preview.Lines.Count:N0} زيارةً بمبلغ "
                + $"{_preview.ClaimedTotal:N2} — والمبلغُ مأخوذٌ من الفواتير نفسِها.";

            if (!save) return;

            facade.Db.InsuranceClaims.Add(_preview);
            await facade.Db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
