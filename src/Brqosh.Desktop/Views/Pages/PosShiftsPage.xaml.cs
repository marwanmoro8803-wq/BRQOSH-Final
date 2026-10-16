using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Retail;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class PosShiftsPage : UserControl
{
    /// <summary>
    /// (CEEF-231 · ط5-٩) صفُّ ورديةٍ — <b>وفيه فرقُ الدرج</b>.
    /// </summary>
    /// <remarks>
    /// <b>وكان <c>ExpectedCash</c> و<c>CashDifference</c> يُحسبان في النطاق ولا يقرؤهما
    /// أحد</b> — الاختباراتُ وحدَها. <b>فوردةٌ تُغلق بعجزٍ مئتين لا يظهر شيء</b>، ويتكرّر
    /// شهراً ولا يُكتشف. <b>وحسابٌ يُجرى ولا يُعرض ليس رقابةً بل وهمُها.</b>
    /// </remarks>
    public record ShiftRow(long Id, string CashierName, string OpenedAt, string ClosedAt,
        decimal OpeningCash, decimal TotalSales, int TransactionCount, string StatusAr,
        PosShiftStatus Status, decimal ExpectedCash, decimal? ClosingCash, decimal? CashDifference)
    {
        /// <summary>
        /// نصُّ الفرق — <b>و«—» للمفتوحة</b>.
        /// </summary>
        /// <remarks>
        /// <b>وصفرٌ لوردةٍ لم تُغلق كذبٌ صغير</b>: يُقرأ «مطابقة» وهي لم تُعدّ بعد.
        /// <b>والإشارةُ تُكتب صراحةً</b> — فـ«200-» يُقرأ في واجهةٍ عربيةٍ بالتباساتٍ
        /// معروفة، و«عجز 200» لا يلتبس.
        /// </remarks>
        public string CashDifferenceText => CashDifference switch
        {
            null => "—",
            0m => LocaleManager.T("مطابقة", "Balanced"),
            < 0m => LocaleManager.T("عجز ", "Short ") + Math.Abs(CashDifference.Value).ToString("N2"),
            _ => LocaleManager.T("زيادة ", "Over ") + CashDifference.Value.ToString("N2"),
        };
    }

    public PosShiftsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (OpenBtn, PermissionService.CanPost),
                (CloseBtn, PermissionService.CanPost));
            await LoadWarehousesAsync();
            await ReloadAsync();
        };
    }

    private async Task LoadWarehousesAsync()
    {
        // ‏(جاهزيّةُ الإطلاق · ح-١١٤ · ‏`order-to-cash-procure-to-pay-inventory#12`) ‏المستودعاتُ الفعّالة — والأوّلُ «الافتراضيّ».
        await using var db = AppServices.CreateDb();
        var choices = new List<SalesInvoiceDialog.WarehouseOption> { new(null, "(المستودع الافتراضي)") };
        choices.AddRange(await db.Warehouses
            .Where(w => w.CompanyId == AppServices.CompanyId && w.IsActive)
            .OrderBy(w => w.Code)
            .Select(w => new SalesInvoiceDialog.WarehouseOption(w.Id, w.Code + " — " + w.NameAr))
            .ToListAsync());
        ShiftWarehouseCombo.ItemsSource = choices;
        ShiftWarehouseCombo.SelectedIndex = 0;
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var shifts = await db.PosShifts
            .Where(s => s.CompanyId == AppServices.CompanyId)
            .OrderByDescending(s => s.OpenedAtUtc)
            .Take(100)
            .ToListAsync();

        var rows = shifts.Select(s => new ShiftRow(
            s.Id, s.CashierName,
            s.OpenedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            s.ClosedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—",
            s.OpeningCash, s.TotalSales, s.TransactionCount,
            s.Status == PosShiftStatus.Open ? "مفتوحة" : "مغلقة", s.Status,
            s.ExpectedCash,
            s.Status == PosShiftStatus.Closed ? s.ClosingCash : null,
            s.Status == PosShiftStatus.Closed ? s.CashDifference : null)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        var open = shifts.Count(s => s.Status == PosShiftStatus.Open);
        var sales = shifts.Sum(s => s.TotalSales);
        // ‏**والعجزُ هو الخبر** — لا المجموع. فوردياتٌ كثيرةٌ بعجزٍ صغيرٍ متكرّر
        // ‏**أخطرُ من واحدةٍ بعجزٍ كبير**: الأولى نمطٌ والثانية حادثة.
        var closed = shifts.Where(s => s.Status == PosShiftStatus.Closed).ToList();
        var short_ = closed.Where(s => s.CashDifference < 0m).ToList();

        ShiftSummaryText.Text =
            $"ورديات: {shifts.Count} · مفتوحة: {open} · إجمالي مبيعات: {sales:N2} ر.س"
            + (short_.Count > 0
                ? $" · ورديات بعجز: {short_.Count} بمجموع {Math.Abs(short_.Sum(s => s.CashDifference)):N2}"
                : closed.Count > 0 ? " · ولا عجزَ في أي وردية مغلقة." : string.Empty);
    }

    private async void OnOpenShift(object sender, RoutedEventArgs e)
    {
        try
        {
            SecurityContext.EnsureCanPost();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
            return;
        }

        var nameDialog = new TextInputDialog("فتح وردية", "اسم الكاشير:",
            AppServices.CurrentUser?.DisplayNameAr ?? "")
            { Owner = Window.GetWindow(this) };
        if (nameDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(nameDialog.Value))
            return;

        var cashDialog = new TextInputDialog("فتح وردية", "النقد الافتتاحي:", "0")
            { Owner = Window.GetWindow(this) };
        if (cashDialog.ShowDialog() != true)
            return;

        if (!NumericInput.TryParse(cashDialog.Value, out var cash) || cash < 0)
        {
            UiFeedback.ShowWarning("مبلغ النقد غير صالح.");
            return;
        }

        try
        {
            var shift = PosShift.Open(AppServices.CompanyId, nameDialog.Value.Trim(), cash,
                ShiftWarehouseCombo.SelectedValue as long?);   // ‏(ح-١١٤) مستودعُ بيع الوردية
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            db.PosShifts.Add(shift);
            await db.SaveChangesAsync();
            await facade.GetService<AuditService>().LogAsync(AppServices.CompanyId, SecurityContext.ResolveUsername(),
                "فتح وردية POS", "PosShift", shift.Id.ToString(), shift.CashierName);
            AppServices.CurrentShiftId = shift.Id;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }

    private async void OnCloseShift(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ShiftRow row || row.Status != PosShiftStatus.Open)
        {
            UiFeedback.ShowInfo("اختر وردية مفتوحة.");
            return;
        }

        try
        {
            SecurityContext.EnsureCanPost();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
            return;
        }

        var dialog = new TextInputDialog("إغلاق الوردية", "النقد عند الإغلاق:", row.OpeningCash.ToString("N2"))
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;

        if (!NumericInput.TryParse(dialog.Value, out var closing))
        {
            UiFeedback.ShowWarning("المبلغ غير صالح.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var shift = await db.PosShifts.SingleAsync(s => s.Id == row.Id);
            shift.Close(closing);
            await db.SaveChangesAsync();
            await facade.GetService<AuditService>().LogAsync(AppServices.CompanyId, SecurityContext.ResolveUsername(),
                "إغلاق وردية POS", "PosShift", shift.Id.ToString(), $"نقد إغلاق {closing:N2}");

            // (CEEF-232 · ن-١) ‏**والفرقُ يُقيَّد مع الإغلاق لا بزرٍّ ثانٍ.**
            //
            // ‏فالمبيعاتُ النقدية تُقيَّد على `1101` بالكامل، **ورصيدُ الصندوق في الأستاذ
            // ‏هو المتوقَّع لا المعدود**. وكان الإغلاقُ يحسب الفرقَ ويعرضه **ويكتب سجلَّ
            // ‏تدقيقٍ فقط** — فيُباعد بين الدفتر والدرج إلى الأبد، **ولا يُجدي جردٌ بعده**
            // ‏إذ لا يُعرف أيُّ ورديّةٍ أحدثت الفارق.
            //
            // ‏**وزرٌّ منفصلٌ يُنسى** — وأوّلُ ما يُنسى هو ما لا يمنع المستخدمَ من المضيّ.
            var variance = await facade.GetService<PosShiftVariancePostingService>()
                .PostForShiftAsync(AppServices.CompanyId, shift.Id, SecurityContext.ResolveUsername());

            if (AppServices.CurrentShiftId == row.Id)
                AppServices.CurrentShiftId = null;

            await ReloadAsync();

            // ‏**والفرقُ يُقال للكاشير عند وقوعه** — والأرقامُ في العنوان، فالنصُّ حرفيّ.
            if (variance.Difference != 0m)
                UiDialog.Show("قُيّد فرق نقد الوردية.",
                    $"{variance.Counted:N2} / {variance.Expected:N2}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }
}
