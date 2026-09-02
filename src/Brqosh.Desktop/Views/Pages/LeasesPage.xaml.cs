using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.RealEstate;
using Brqosh.Infrastructure.RealEstate;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٤) <b>عقودُ الإيجار — والتفعيلُ هو ما يُشغّل القطاعَ كلَّه.</b>
///
/// <para><b>ما كان:</b> <c>LeaseService</c> مبنيٌّ ومقيسٌ في <c>ط3-١</c>: يفحص التداخلَ
/// بالتواريخ، ويُنشئ خطةَ فوترةٍ دورية، ويشغل الوحدة — <b>ولا زرَّ يستدعيه</b>. فالعقدُ
/// لا يُوقَّع في النظام، <b>والإيجارُ لا يُفوتَر</b>.</para>
///
/// <para><b>والتفعيلُ هو الحدث</b>: يُنشئ خطةً في «الفوترة الدورية»، ومن هناك تُولَّد
/// فواتيرُ الإيجار شهراً بشهر. <b>فالشاشتان معاً تُغلقان الحلقة</b>.</para>
/// </summary>
public partial class LeasesPage : UserControl
{
    public record Row(long Id, string ContractNumber, string UnitLabel, string TenantName,
        string StartDate, string EndDate, decimal Rent, decimal Deposit, string DepositState,
        string Status, bool IsDraft, bool CanReceiveDeposit, bool CanReturnDeposit);

    public LeasesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (ActivateBtn, PermissionService.CanPost),
                (TerminateBtn, PermissionService.CanPost),
                (ReceiveDepositBtn, PermissionService.CanPostAccounting),
                (ReturnDepositBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();

        var leases = await db.LeaseContracts.AsNoTracking()
            .Where(l => l.CompanyId == AppServices.CompanyId)
            .OrderBy(l => l.ContractNumber)
            .ToListAsync();

        var units = await db.PropertyUnits.AsNoTracking()
            .Where(u => u.CompanyId == AppServices.CompanyId)
            .ToDictionaryAsync(u => u.Id, u => u.UnitNumber);

        var tenants = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.CompanyId == AppServices.CompanyId)
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        var rows = leases.Select(l => new Row(
            l.Id, l.ContractNumber,
            units.TryGetValue(l.PropertyUnitId, out var unit) ? unit : $"#{l.PropertyUnitId}",
            tenants.TryGetValue(l.TenantCustomerId, out var tenant) ? tenant : $"#{l.TenantCustomerId}",
            l.StartDate.ToString("yyyy-MM-dd"), l.EndDate.ToString("yyyy-MM-dd"),
            l.RentPerPeriod, l.SecurityDeposit, DepositStateText(l), StatusText(l.Status),
            l.Status == LeaseStatus.Draft,
            l.SecurityDeposit > 0m && l.DepositReceivedOn is null,
            l.DepositReceivedOn is not null && l.DepositReturnedOn is null)).ToList();

        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);

        var active = leases.Count(l => l.Status == LeaseStatus.Active);
        var drafts = leases.Count(l => l.Status == LeaseStatus.Draft);

        // ‏**والمسودّةُ لا تُفوتِر**: عقدٌ وُقّع ولم يُفعَّل يترك الإيجارَ بلا خطةٍ ولا
        // ‏فاتورة — **وهو أشيعُ ما يُنسى** لأنّ العقدَ يبدو محفوظاً.
        SummaryText.Text = drafts == 0
            ? $"عقود: {leases.Count:N0} · مفعَّلة: {active:N0}"
            : $"عقود: {leases.Count:N0} · مفعَّلة: {active:N0} · "
              + $"⚠️ مسودّات لم تُفعَّل: {drafts:N0} — ولا تُنشئ خطةَ فوترةٍ حتى تُفعَّل.";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new LeaseDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnActivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (!row.IsDraft)
        {
            UiFeedback.ShowWarning("لا يُفعَّل إلا عقدٌ مسودّة.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var lease = await facade.Db.LeaseContracts.SingleAsync(l => l.Id == row.Id);
            await facade.GetService<LeaseService>().ActivateAsync(lease);
            await ReloadAsync();

            UiDialog.Show("فُعِّل العقد وأُنشئت خطة فوترته الدورية.", "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnTerminate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;

        if (UiDialog.Show("إنهاء العقد المحدد؟ — وتُوقَف خطة فوترته وتُخلى الوحدة.",
                row.ContractNumber, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<LeaseService>().TerminateAsync(AppServices.CompanyId, row.Id,
                DateOnly.FromDateTime(DateTime.Today));
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// (CEEF-230 · ط4-٦) <b>قبضُ التأمين — والخدمةُ كانت مبنيّةً ولا زرَّ لها.</b>
    ///
    /// <para>بُنيت <c>SecurityDepositService</c> في <c>ط3-١</c> ومُقيست بالطفرات: تقبض
    /// التأمينَ <b>التزاماً في الأستاذ</b> لا إيراداً — <b>ولم يكن في الشاشة ما يستدعيها</b>.
    /// وهو عينُ العطل الذي بُنيت له مرحلةُ <c>ط6</c> كاملة، <b>ووقعتُ فيه في البند نفسِه
    /// الذي أصلحتُه</b>.</para>
    /// </summary>
    private async void OnReceiveDeposit(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (!row.CanReceiveDeposit)
        {
            UiFeedback.ShowWarning("لا تأمينَ على هذا العقد، أو قُبض سلفاً.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<SecurityDepositService>().ReceiveAsync(
                AppServices.CompanyId, row.Id, DateOnly.FromDateTime(DateTime.Today),
                SecurityContext.ResolveUsername());
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// <b>وردُّ التأمين يُغلق الالتزامَ كاملاً</b> — والمخصومُ وحده يصير إيراداً.
    ///
    /// <para><b>والمخصومُ يُسأل عنه صراحةً</b>: صفرٌ افتراضاً، <b>فخصمٌ يقع بلا سؤالٍ
    /// يأخذ من أمانةٍ بلا قرار</b>.</para>
    /// </summary>
    private async void OnReturnDeposit(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (!row.CanReturnDeposit)
        {
            UiFeedback.ShowWarning("لا يُردّ تأمينٌ لم يُقبض، أو رُدّ سلفاً.");
            return;
        }

        var dialog = new DepositReturnDialog(row.ContractNumber, row.Deposit)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<SecurityDepositService>().ReturnAsync(
                AppServices.CompanyId, row.Id, DateOnly.FromDateTime(DateTime.Today),
                dialog.Forfeited, SecurityContext.ResolveUsername());
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string DepositStateText(LeaseContract lease)
    {
        if (lease.SecurityDeposit <= 0m) return "لا تأمين";
        if (lease.DepositReturnedOn is not null)
            return lease.DepositForfeited > 0m ? "رُدّ بخصم" : "رُدّ كاملاً";
        return lease.DepositReceivedOn is not null ? "مقبوض — التزام" : "لم يُقبض";
    }

    private static string StatusText(LeaseStatus status) => status switch
    {
        LeaseStatus.Draft => "مسودّة",
        LeaseStatus.Active => "مفعَّل",
        LeaseStatus.Terminated => "منتهٍ",
        LeaseStatus.Expired => "منقضٍ",
        _ => status.ToString(),
    };
}
