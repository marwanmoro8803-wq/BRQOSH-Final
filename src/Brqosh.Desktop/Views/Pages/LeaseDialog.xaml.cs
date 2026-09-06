using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.RealEstate;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) إنشاءُ عقد إيجار — <b>ويُحفظ مسودّةً، ولا يُفعَّل من هنا</b>.
///
/// <para><b>والفصلُ مقصود</b>: التفعيلُ يُنشئ خطةَ فوترةٍ ويشغل الوحدة ويفحص التداخل —
/// <b>وهو حدثٌ ماليّ</b>، لا حفظُ بيانات. فيبقى زرّاً صريحاً في الشاشة.</para>
/// </summary>
public partial class LeaseDialog : Window
{
    private sealed record Option(long Id, string Label)
    {
        public override string ToString() => Label;
    }

    public LeaseDialog()
    {
        InitializeComponent();
        StartBox.SelectedDate = DateTime.Today;
        EndBox.SelectedDate = DateTime.Today.AddYears(1).AddDays(-1);
        Loaded += async (_, _) => await LoadListsAsync();
    }

    private async Task LoadListsAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var properties = await db.Properties.AsNoTracking()
                .Where(p => p.CompanyId == AppServices.CompanyId)
                .ToDictionaryAsync(p => p.Id, p => p.Code);

            // ‏**والوحداتُ الشاغرةُ وحدَها تُعرض**: المؤجَّرةُ لها عقدٌ قائم، والصيانةُ
            // ‏لا تُسلَّم. **وعرضُها يُغري بتوقيعٍ يرفضه النظامُ بعد ملء النموذج كلِّه.**
            var units = await db.PropertyUnits.AsNoTracking()
                .Where(u => u.CompanyId == AppServices.CompanyId && u.Status == UnitStatus.Vacant)
                .OrderBy(u => u.UnitNumber)
                .ToListAsync();

            UnitBox.ItemsSource = units.Select(u => new Option(u.Id,
                $"{(properties.TryGetValue(u.PropertyId, out var code) ? code : "?")} / {u.UnitNumber}")).ToList();

            var tenants = await db.Customers.AsNoTracking()
                .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new { c.Id, c.NameAr })
                .ToListAsync();

            TenantBox.ItemsSource = tenants.Select(t => new Option(t.Id, t.NameAr)).ToList();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (UnitBox.SelectedItem is not Option unit)
                throw new AccountingException("اختر الوحدة — ولا تُعرض إلا الشاغرة.");
            if (TenantBox.SelectedItem is not Option tenant)
                throw new AccountingException("اختر المستأجر.");
            if (StartBox.SelectedDate is null || EndBox.SelectedDate is null)
                throw new AccountingException("حدّد بداية العقد ونهايته.");

            var lease = LeaseContract.Create(AppServices.CompanyId, unit.Id, tenant.Id,
                NumberBox.Text,
                DateOnly.FromDateTime(StartBox.SelectedDate.Value),
                DateOnly.FromDateTime(EndBox.SelectedDate.Value),
                Number(RentBox.Text, "الإيجار"),
                (RecurrenceFrequency)Math.Max(0, FrequencyBox.SelectedIndex),
                Number(DepositBox.Text, "التأمين"));

            await using var db = AppServices.CreateDb();
            db.LeaseContracts.Add(lease);
            await db.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static decimal Number(string text, string field) =>
        NumericInput.TryParse(text, out var value)
            ? value
            : throw new AccountingException($"قيمة «{field}» غير صالحة — ووردت «{text}».");

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
