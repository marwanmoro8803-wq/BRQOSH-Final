using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط2-٨) <b>نِسَبُ التأمينات — والمنشأةُ لم تكن تملكها.</b>
///
/// <para><b>ما كان:</b> <c>GosiPolicy.SetRates</c> و<c>SetContributoryWageBounds</c>
/// مبنيّان ومقيسان، <b>ولا شاشةَ في المستودع كلِّه تستدعيهما</b> — والاختباراتُ وحدَها.
/// <b>فالنِّسَبُ مثبَّتةٌ عند الافتراضيّ إلى الأبد.</b></para>
///
/// <para><b>وهذا ليس تفصيلاً تجميلياً</b>: نِسَبُ التأمينات <b>تتغيّر بقرارٍ من الهيئة</b>،
/// وسقفُ الأجر الخاضع كذلك. <b>فمنشأةٌ تُشغّل نسبةً قديمةً تُبلّغ اشتراكاً خاطئاً كلَّ
/// شهر</b> — تدفع أقلَّ فتُطالَب بفروقٍ وغرامة، أو أكثرَ فتُنقص أجورَ موظفيها بما لا
/// يستحقّ. <b>وقد أُعلن في <c>ط2-٢</c> أنّ «النِّسَبَ مملوكةٌ للمنشأة» — ولم يكن لها باب.</b></para>
///
/// <para><b>ولا تُعاد دورةٌ مرحَّلةٌ بالنِّسَب الجديدة</b>: الاشتراكُ يُحسب عند بناء الدورة
/// ويُخزَّن في سطرها. <b>وتغييرُ النسبة يسري على ما يُبنى بعده</b> — وإعادةُ حساب شهرٍ
/// رُحِّل وسُدِّد تُغيّر إقراراً قُدِّم.</para>
/// </summary>
public partial class GosiPolicyPage : UserControl
{
    public GosiPolicyPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var policy = await PolicyAsync(db);

            SaudiEmployeeBox.Text = policy.SaudiEmployeePercent.ToString("0.##");
            SaudiEmployerBox.Text = policy.SaudiEmployerPercent.ToString("0.##");
            NonSaudiEmployeeBox.Text = policy.NonSaudiEmployeePercent.ToString("0.##");
            NonSaudiEmployerBox.Text = policy.NonSaudiEmployerPercent.ToString("0.##");
            FloorBox.Text = policy.ContributoryWageFloor.ToString("0.##");
            CapBox.Text = policy.ContributoryWageCap.ToString("0.##");
            NewLawEmployeeBox.Text = policy.NewLawEmployeePercent.ToString("0.##");
            NewLawEmployerBox.Text = policy.NewLawEmployerPercent.ToString("0.##");
            NewLawStepBox.Text = policy.NewLawAnnualStepPercent.ToString("0.##");
            NewLawFirstStepPicker.SelectedDate = policy.NewLawFirstStep.ToDateTime(TimeOnly.MinValue);
            NewLawStepsBox.Text = policy.NewLawSteps.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var (newLawEmployee, newLawEmployer) = policy.NewLawPercentOn(DateOnly.FromDateTime(DateTime.Today));

            SummaryText.Text =
                $"السعودي: {policy.SaudiEmployeePercent:0.##}٪ موظف + {policy.SaudiEmployerPercent:0.##}٪ صاحب عمل"
                + $" · الوافد: {policy.NonSaudiEmployeePercent:0.##}٪ + {policy.NonSaudiEmployerPercent:0.##}٪"
                + $" · السقف: {policy.ContributoryWageCap:N0}"
                + $" · النظام الجديد هذا الشهر: {newLawEmployee:0.##}٪ + {newLawEmployer:0.##}٪";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            await using var db = AppServices.CreateDb();
            var policy = await PolicyAsync(db);

            // ‏**والحدودُ قبل النِّسَب**: النطاقُ يرفض حدّاً فوق سقفه، **ورفضٌ بعد كتابة
            // ‏النِّسَب يترك نصفَ التغيير محفوظاً** — فتُحسب دورةٌ بنسبةٍ جديدةٍ وسقفٍ قديم.
            policy.SetContributoryWageBounds(
                Number(FloorBox.Text, "الحد الأدنى"),
                Number(CapBox.Text, "السقف"));

            policy.SetRates(
                Number(SaudiEmployeeBox.Text, "حصة الموظف السعودي"),
                Number(SaudiEmployerBox.Text, "حصة صاحب العمل عن السعودي"),
                Number(NonSaudiEmployeeBox.Text, "حصة الموظف الوافد"),
                Number(NonSaudiEmployerBox.Text, "حصة صاحب العمل عن الوافد"));

            // ‏(ح-١٣٨) جدولُ النظام الجديد — وعددُ الزيادات عددٌ صحيح.
            var steps = Number(NewLawStepsBox.Text, "عدد الزيادات");
            if (steps != decimal.Truncate(steps))
                throw new AccountingException("عددُ الزيادات عددٌ صحيح.");
            if (NewLawFirstStepPicker.SelectedDate is null)
                throw new AccountingException("تاريخ أول زيادة مطلوب.");
            policy.SetNewLawSchedule(
                Number(NewLawEmployeeBox.Text, "حصة الموظف عند البدء"),
                Number(NewLawEmployerBox.Text, "حصة صاحب العمل عند البدء"),
                Number(NewLawStepBox.Text, "الزيادة السنوية"),
                DateOnly.FromDateTime(NewLawFirstStepPicker.SelectedDate.Value),
                (int)steps);

            await db.SaveChangesAsync();
            await LoadAsync();

            UiFeedback.ShowInfo("حُفظت النسب — وتسري على الدورات التي تُبنى بعد الآن.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex is AccountingException ae ? ae.Message : ex.Message);
        }
    }

    /// <summary>
    /// نِسَبُ المنشأة — <b>وتُنشأ بالافتراضيّ إن غابت</b>.
    /// </summary>
    /// <remarks>
    /// <b>وقاعدةٌ رُقّيت من نسخةٍ أقدمَ لا تحمل صفّاً</b> — والبذّارُ يُنشئه للمنشأة
    /// الجديدة وحدَها. <b>وسقوطٌ هنا يمنع الشاشةَ كلَّها</b> عن منشأةٍ قائمة.
    /// </remarks>
    private static async Task<GosiPolicy> PolicyAsync(BrqoshDbContext db)
    {
        var policy = await db.GosiPolicies
            .SingleOrDefaultAsync(p => p.CompanyId == AppServices.CompanyId);

        if (policy is not null) return policy;

        policy = GosiPolicy.CreateDefault(AppServices.CompanyId);
        db.GosiPolicies.Add(policy);
        await db.SaveChangesAsync();
        return policy;
    }

    private static decimal Number(string? text, string field) =>
        NumericInput.TryParse(text, out var value)
            ? value
            : throw new AccountingException($"«{field}» رقمٌ غير صالح — ووردَ «{text}».");
}
