using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Payroll;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class PayrollPage : UserControl
{
    public record RunRow(long Id, string PeriodCode, string PayDate, int EmployeeCount,
        decimal TotalGross, decimal TotalNet, string StatusAr, PayrollRunStatus Status, long? JournalEntryId);

    public PayrollPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPostAccounting),
                (PostBtn, PermissionService.CanPostAccounting),
                (ReverseBtn, PermissionService.CanCancelOrReverse),
                (AccrueEosBtn, PermissionService.CanPostAccounting),
                (SettleEosBtn, PermissionService.CanPostAccounting),
                // ‏(ح-٩١) وعكسُ التصفية بحارس العكس.
                (ReverseSettleEosBtn, PermissionService.CanCancelOrReverse),
                // ‏(ح-٨٤) **والصرفُ بحارس خدمته** — `PayrollPaymentService` = `EnsureCanPostAccounting`.
                (PaySalariesBtn, PermissionService.CanPostAccounting),
                (PayGosiBtn, PermissionService.CanPostAccounting),
                (PayEosBtn, PermissionService.CanPostAccounting),
                // ‏(ح-٨٨) وعكسُ الصرف بحارس العكس — كعكس الدورة نفسِها.
                (ReversePaymentBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var runs = await db.PayrollRuns
            .Where(p => p.CompanyId == AppServices.CompanyId)
            .OrderByDescending(p => p.PayDate)
            .Take(100)
            .ToListAsync();

        Grid.ItemsSource = runs.Select(p => new RunRow(
            p.Id, p.PeriodCode, p.PayDate.ToString("yyyy-MM-dd"),
            p.Lines.Count, p.TotalGross, p.TotalNet,
            p.Status switch
            {
                PayrollRunStatus.Posted => LocaleManager.T("مرحّل", "Posted"),
                PayrollRunStatus.Reversed => LocaleManager.T("معكوس", "Reversed"),
                _ => LocaleManager.T("مسودة", "Draft"),
            }, p.Status, p.JournalEntryId)).ToList();
        UiTestAutomation.PublishGridCount(Grid, runs.Count);
        RenderKpis(runs);
    }

    private void RenderKpis(IReadOnlyList<PayrollRun> runs)
    {
        KpiGrid.Children.Clear();
        var posted = runs.Count(r => r.Status == PayrollRunStatus.Posted);
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("دورات", "Payroll runs"), "PR", runs.Count, "#7C3AED", KpiValueKind.Count));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("إجمالي إجمالي", "Total gross"), "GR",
            runs.Sum(r => r.TotalGross), "#2563EB", KpiValueKind.Currency));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("مرحّل", "Posted"), "OK", posted, "#0D9488", KpiValueKind.Count));
    }

    private async void OnNew(object sender, RoutedEventArgs e)
    {
        if (new PayrollDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnPost(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RunRow row || row.Status != PayrollRunStatus.Draft)
        {
            UiDialog.Show("اختر دورة رواتب بحالة مسودة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var run = await db.PayrollRuns.SingleAsync(p => p.Id == row.Id);
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<PayrollPostingService>().PostAsync(run, username);

            UiDialog.Show("تم ترحيل دورة الرواتب.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnViewJournal(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RunRow row)
        {
            UiDialog.Show("اختر دورة من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.JournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            UiDialog.Show("لا يوجد قيد محاسبي مرتبط بهذه الدورة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnReverse(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RunRow row || row.Status != PayrollRunStatus.Posted)
        {
            UiDialog.Show("اختر دورة رواتب مرحّلة للعكس.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (UiDialog.Show($"عكس دورة الرواتب {row.PeriodCode}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var reversalDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ العكس", "تاريخ قيد عكس الرواتب:");
        if (reversalDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<PayrollReversalService>().ReverseAsync(
                AppServices.CompanyId, row.Id, reversalDate.Value, username);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    /// <summary>
    /// (CEEF-230 · ط6-٥) <b>تخصيصُ مكافأة نهاية الخدمة — والخدمةُ لم تكن مسجَّلةً في الحقن أصلاً.</b>
    ///
    /// <para>بُنيت <c>EndOfServiceAccrualService</c> في <c>ط2-٣</c> بالمادتين 84 و85
    /// ومُقيست بالطفرات، <b>ولم تكن مسجَّلةً في الحقن ولا يستدعيها زرّ</b>. فحسابُ المخصّص
    /// <c>2202</c> — الذي كان بلا كاتبٍ منذ اليوم الأوّل — <b>بقي بلا كاتبٍ فعلاً</b>:
    /// المنشأةُ تحمل التزاماً نظامياً يتراكم <b>ولا يظهر في ميزانيّتها بريالٍ واحد</b>.</para>
    ///
    /// <para><b>وهو فرقٌ لا تراكم</b>: يُحسب الالتزامُ اليومَ ويُقارَن بما في الحساب،
    /// <b>ويُقيَّد الفرقُ وحده</b> — فتشغيلٌ ثانٍ بلا تغييرٍ لا يُقيّد شيئاً.</para>
    /// </summary>
    private async void OnAccrueEndOfService(object sender, RoutedEventArgs e)
    {
        if (UiDialog.Show("تخصيص مكافأة نهاية الخدمة حتى اليوم؟ — ويُقيَّد الفرق وحده.", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<EndOfServiceAccrualService>().RunMonthlyAsync(
                AppServices.CompanyId, DateOnly.FromDateTime(DateTime.Today),
                SecurityContext.ResolveUsername());

            await ReloadAsync();

            // ‏**والنصُّ حرفيٌّ والأرقامُ في العنوان**: نصٌّ مركَّبٌ لا يبلغه مترجِم،
            // ‏وحارسُ `DialogLocalizationTests` يعدّه. **والرقمُ لا يسقط** — يُحمل في
            // ‏عنوان الحوار.
            if (result.Movement == 0m)
                UiDialog.Show("لا تغيّرَ في الالتزام — ولم يُقيَّد شيء.", "BRQOSH",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            else
                UiDialog.Show("قُيّدت حركة تخصيص مكافأة نهاية الخدمة.",
                    $"{result.Movement:N2} · {result.Employees:N0}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// (CEEF-232 · ر-١) <b>تصفيةُ نهاية الخدمة — الطرفُ الذي كان مفقوداً.</b>
    ///
    /// <para><b>وكان المستودعُ يبني المخصّصَ ولا يُبرئه</b>: خدمةُ التخصيص وحدَها،
    /// <b>ولا مسارَ دفعٍ ولا شاشة</b>. فحسابُ <c>2202</c> ينمو بلا حدّ ويحمل مستحقّاتِ
    /// مَن غادروا منذ سنوات، <b>ومستحقُّ الموظّف يُدفع خارجَ الدفاتر أو لا يُدفع</b> —
    /// وهو حقٌّ نظاميٌّ لا يسقط.</para>
    ///
    /// <para><b>والسببُ يُسأل هنا ولا يُسأل عند التخصيص</b>: التخصيصُ يجهل كيف ستنتهي
    /// الخدمة <b>فيحتاط بالمستحقّ الكامل</b>، والتصفيةُ تعرف. <b>والفرقُ بين الجوابين
    /// ثلثا المستحقّ</b> لمن أمضى دون خمس سنوات (المادة 85).</para>
    /// </summary>
    private async void OnSettleEndOfService(object sender, RoutedEventArgs e)
    {
        var codeDialog = new TextInputDialog("تصفية نهاية الخدمة",
            "رمز الموظّف المنتهية خدمتُه:", "")
            { Owner = Window.GetWindow(this) };
        if (codeDialog.ShowDialog() != true)
            return;

        var code = (codeDialog.Value ?? "").Trim();
        if (code.Length == 0)
        {
            UiFeedback.ShowWarning("أدخل رمز الموظّف.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var employee = await facade.Db.Employees
                .FirstOrDefaultAsync(x => x.CompanyId == AppServices.CompanyId && x.Code == code);
            if (employee is null)
            {
                UiFeedback.ShowWarning("لا يوجد موظّف بهذا الرمز.");
                return;
            }

            // ‏**والسببُ سؤالٌ صريحٌ لا يُستنتج** — واستنتاجُه يُسقط ثلثي حقٍّ أو يمنحه بغير وجه.
            //
            // ‏**والنصُّ حرفيٌّ واسمُ الموظّف في العنوان** — على اصطلاح الصفحة نفسِها في
            // ‏الأرقام. **ونصٌّ مركَّبٌ لا يبلغه مترجِمٌ ويرفع سقفَ «غير القابل للبحث»**،
            // ‏وحارسُ `DialogLocalizationTests` يعدّه ويرفض رفعَ السقف.
            var resigned = UiDialog.Show(
                "هل انتهت الخدمة بالاستقالة؟ — «نعم» تُطبّق سُلَّم المادة 85.",
                employee.NameAr, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

            var reason = resigned
                ? EndOfServiceReason.Resignation
                : EndOfServiceReason.EmployerTermination;

            var result = await facade.GetService<EndOfServiceSettlementService>().SettleAsync(
                AppServices.CompanyId, employee.Id, reason,
                DateOnly.FromDateTime(DateTime.Today), SecurityContext.ResolveUsername());

            await ReloadAsync();

            // ‏**والأرقامُ في العنوان لا في النصّ** — فالنصُّ حرفيٌّ يعدّه حارسُ الترجمة.
            UiDialog.Show("قُيّدت تصفية مكافأة نهاية الخدمة.",
                $"{result.Benefit:N2} · {result.Adjustment:N2}",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٩١) <b>عكسُ تصفية نهاية الخدمة</b> — فيُصفّى الموظّفُ من جديد بالسبب الصحيح. <b>وإن دُفع المستحقُّ سُئل المستخدمُ</b>
    /// أيعكس الدفعَ ثمّ التصفية — ولا يُعكس الدفعُ صامتاً؛ وكان عكسُ دفع نهاية الخدمة (ح-٨٨) بلا زرٍّ أصلاً.
    /// </summary>
    private async void OnReverseEndOfServiceSettlement(object sender, RoutedEventArgs e)
    {
        var codeDialog = new TextInputDialog("عكس تصفية نهاية الخدمة",
            "رمز الموظّف المراد عكس تصفيته:", "")
            { Owner = Window.GetWindow(this) };
        if (codeDialog.ShowDialog() != true)
            return;

        var code = (codeDialog.Value ?? "").Trim();
        if (code.Length == 0)
        {
            UiFeedback.ShowWarning("أدخل رمز الموظّف.");
            return;
        }

        var reversalDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ العكس", "تاريخ قيد عكس التصفية:");
        if (reversalDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var employee = await facade.Db.Employees
                .FirstOrDefaultAsync(x => x.CompanyId == AppServices.CompanyId && x.Code == code);
            if (employee is null)
            {
                UiFeedback.ShowWarning("لا يوجد موظّف بهذا الرمز.");
                return;
            }

            var username = SecurityContext.ResolveUsername();
            var payments = facade.GetService<PayrollPaymentService>();
            if (await payments.EndOfServicePaidAsync(AppServices.CompanyId, employee.Id) > 0m)
            {
                if (UiDialog.Show("دُفع مستحقّ نهاية الخدمة — أتعكس الدفع ثمّ التصفية؟", employee.NameAr,
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
                await payments.ReverseEndOfServicePaymentAsync(
                    AppServices.CompanyId, employee.Id, reversalDate.Value, username);
            }

            await facade.GetService<EndOfServiceSettlementService>().ReverseAsync(
                AppServices.CompanyId, employee.Id, reversalDate.Value, username);

            await ReloadAsync();
            UiDialog.Show("عُكست تصفية نهاية الخدمة.", employee.NameAr, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٨٤ · <c>treasury-payroll-assets#3</c>) <b>صرفُ رواتب الدورة المحدّدة — وكان <c>2108</c> يُدان ولا يُمَسّ</b>:
    /// الرواتبُ تُدفع خارج الدفاتر، والالتزامُ يتراكم شهراً بعد شهر.
    /// </summary>
    private async void OnPaySalaries(object sender, RoutedEventArgs e) => await PayRunAsync(gosi: false);

    /// <summary>(ح-٨٤) <b>سدادُ تأمينات الدورة</b> — حصّتا الموظّف وصاحب العمل؛ وكان <c>2109</c> لا يُدينه شيء.</summary>
    private async void OnPayGosi(object sender, RoutedEventArgs e) => await PayRunAsync(gosi: true);

    private async Task PayRunAsync(bool gosi)
    {
        if (Grid.SelectedItem is not RunRow row || row.Status != PayrollRunStatus.Posted)
        {
            UiDialog.Show("اختر دورة رواتب مرحّلة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (AskPaymentMethod() is not ReceiptMethod method)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var payments = facade.GetService<PayrollPaymentService>();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var entry = gosi
                ? await payments.PayGosiAsync(AppServices.CompanyId, row.Id, today, method)
                : await payments.PaySalariesAsync(AppServices.CompanyId, row.Id, today, method);

            await ReloadAsync();
            UiDialog.Show("قُيّد الصرف وصُفّي المستحقّ.", entry.EntryNumber ?? "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// (ح-٨٤) <b>دفعُ مستحقّ نهاية الخدمة</b> — ما قيّدته التصفيةُ في <c>2108</c> (المكافأةُ وبدلُ الإجازة)، وكان لا يدفعه شيء.
    /// </summary>
    private async void OnPayEndOfService(object sender, RoutedEventArgs e)
    {
        var codeDialog = new TextInputDialog("دفع مستحق نهاية الخدمة", "رمز الموظّف:", "")
            { Owner = Window.GetWindow(this) };
        if (codeDialog.ShowDialog() != true)
            return;

        var code = (codeDialog.Value ?? "").Trim();
        if (code.Length == 0)
        {
            UiFeedback.ShowWarning("أدخل رمز الموظّف.");
            return;
        }

        if (AskPaymentMethod() is not ReceiptMethod method)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var employee = await facade.Db.Employees
                .FirstOrDefaultAsync(x => x.CompanyId == AppServices.CompanyId && x.Code == code);
            if (employee is null)
            {
                UiFeedback.ShowWarning("لا يوجد موظّف بهذا الرمز.");
                return;
            }

            var entry = await facade.GetService<PayrollPaymentService>().PayEndOfServiceAsync(
                AppServices.CompanyId, employee.Id, DateOnly.FromDateTime(DateTime.Today), method);

            UiDialog.Show("قُيّد الصرف وصُفّي المستحقّ.", entry.EntryNumber ?? "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٨٨) <b>عكسُ صرف الدورة المحدّدة</b> — رواتبُها وتأميناتُها ما كان منهما قائماً؛ وبعده تُعكس الدورةُ نفسُها إن
    /// لزم. <b>وكانت دورةٌ صُرفت خطأً لا تُصحَّح أبداً.</b>
    /// </summary>
    private async void OnReversePayment(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not RunRow row || row.Status != PayrollRunStatus.Posted)
        {
            UiDialog.Show("اختر دورة رواتب مرحّلة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var reversalDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ العكس", "تاريخ قيد عكس الصرف:");
        if (reversalDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var payments = facade.GetService<PayrollPaymentService>();
            var username = SecurityContext.ResolveUsername();
            var reversed = 0;
            if (await payments.SalariesPaidAsync(AppServices.CompanyId, row.Id) > 0m)
            {
                await payments.ReverseSalariesPaymentAsync(AppServices.CompanyId, row.Id, reversalDate.Value, username);
                reversed++;
            }
            if (await payments.GosiPaidAsync(AppServices.CompanyId, row.Id) > 0m)
            {
                await payments.ReverseGosiPaymentAsync(AppServices.CompanyId, row.Id, reversalDate.Value, username);
                reversed++;
            }

            if (reversed == 0)
                UiFeedback.ShowWarning("لا صرفَ قائماً يُعكس.");
            else
                UiDialog.Show("عُكس صرف الدورة وعاد المستحقّ قائماً.", row.PeriodCode,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary><b>ومن أين يخرج المال</b> — سؤالٌ صريحٌ لا يُفترض: البنكُ الافتراضيّ أو الصندوق.</summary>
    private static ReceiptMethod? AskPaymentMethod()
    {
        var answer = UiDialog.Show("طريقة الصرف — نعم: تحويلٌ من الحساب البنكيّ الافتراضيّ · لا: نقداً من الصندوق", "BRQOSH",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
            return ReceiptMethod.Bank;
        if (answer == MessageBoxResult.No)
            return ReceiptMethod.Cash;
        return null;
    }
}
