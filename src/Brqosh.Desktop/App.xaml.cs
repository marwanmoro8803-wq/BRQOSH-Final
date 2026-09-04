using System.IO;
using System.Windows;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Views;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop;

public partial class App : System.Windows.Application
{
    private readonly ISetupStateStore _setupStore = new JsonSetupStateStore();

    /// <summary>
    /// (CEEF-231 · ط5-٦) سطرُ مسار السجلّ — <b>ويُطوى إن تعذّرت كتابتُه</b>.
    /// </summary>
    /// <remarks>
    /// <b>ووعدٌ بسجلٍّ لم يُكتب أسوأُ من الصمت</b>: يُرسل الدعمُ المستخدمَ إلى ملفٍّ
    /// لا وجودَ له، فيضيع وقتُ الطرفين.
    /// </remarks>
    private static string CrashHint(string? path) =>
        path is null ? string.Empty : $"\n\nسُجّل التفصيل في:\n{path}";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Ui.UiLocalizationHost.Register();

        // (CEEF-231 · ط5-٦) ‏**والعطلُ يُكتب على القرص قبل أن يُعرض.**
        //
        // ‏كان المعالجان يعرضان `ex.Message` **ولا يكتبان حرفاً**. فمنشأةٌ يقع عندها
        // ‏عطلٌ تقول للدعم «ظهرت رسالة» — **ولا أثرَ لِما وقع**: لا مكدّس، ولا إصدار،
        // ‏ولا محرّك قاعدة. **فيُطلب منها إعادةُ العطل** وهي تمسك دفاترَها الحقيقية.
        DispatcherUnhandledException += (_, args) =>
        {
            var path = Brqosh.Infrastructure.Operations.CrashLog.Write(
                args.Exception, "DispatcherUnhandledException");

            var detail = args.Exception.InnerException is null
                ? args.Exception.Message
                : $"{args.Exception.Message}\n\n{args.Exception.InnerException.Message}";

            UiDialog.Show(
                $"خطأ غير متوقع:\n{detail}" + CrashHint(path),
                "BRQOSH — خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                var path = Brqosh.Infrastructure.Operations.CrashLog.Write(
                    ex, "AppDomain.UnhandledException");

                UiDialog.Show(
                    $"خطأ حرج:\n{ex.Message}" + CrashHint(path),
                    "BRQOSH — خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        // (CEEF-231 · ط5-٦) ‏**واستثناءُ مهمّةٍ لم يُنتظَر كان يمرّ صامتاً تماماً.**
        //
        // ‏فكلُّ `async void` في الشاشات — وهي كلُّ معالجات الأزرار — **يُنهي المهمّةَ
        // ‏بلا مراقب** إن لم يُلتقط. **ولا يظهر شيء**: لا رسالةَ ولا سجلّ، والزرُّ لا
        // ‏يفعل شيئاً ولا يُقال لماذا.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Brqosh.Infrastructure.Operations.CrashLog.Write(
                args.Exception, "TaskScheduler.UnobservedTaskException");

            // ‏**ويُعلَّم مُشاهَداً فلا يُسقط العملية** — وقد سُجّل.
            args.SetObserved();
        };


        var setup = _setupStore.Load();
        string? adminUsername = null;
        string? adminPassword = null;

        if (setup is null || !setup.IsCompleted)
        {
            var wizard = new SetupWizardWindow(_setupStore);
            var completed = wizard.ShowDialog() == true;
            if (!completed)
            {
                Shutdown();
                return;
            }
            setup = _setupStore.Load();
            adminUsername = wizard.AdminUsername;
            adminPassword = wizard.AdminPassword;
        }

        // (CEEF-236 · م-١٣) ‏**ملفُّ اتصالٍ لا يُقرأ لم يعد يعني تركيباً ميّتاً.**
        //
        // ‏كان الإخفاقُ هنا يعرض رسالةً ثمّ `Shutdown()` — **والرسالةُ تصف علاجاً لا سبيلَ إليه**:
        // ‏معالجُ التهيئة يُبنى تحت `!setup.IsCompleted` وحدَه ولا شيءَ يمسح العلَم. فمتى تلف
        // ‏`database.json` أو حُذف أو **خُتم بحساب مستخدمٍ آخر** صار كلُّ إقلاعٍ يعرض الرسالةَ
        // ‏نفسَها ويُغلق — **والدفاترُ سليمةٌ في PostgreSQL طوال ذلك**. وصار الطريقُ مسلوكاً
        // ‏بإصلاح ت-٣ نفسِه، إذ لم يكن شيءٌ قبله يُنتج سلسلةً محميّة.
        //
        // ‏فتُعرض الآن `DatabaseConnectionWindow` — اللوحةُ نفسُها التي في المعالج — ويُعاد التحميل.
        while (true)
        {
            try
            {
                AppServices.LoadDatabaseConfig();
                await AppServices.EnsureDatabaseReachableAsync();
                break;
            }
            catch (Exception ex)
            {
                var recovery = new DatabaseConnectionWindow(
                    $"تعذّر الاتصال بقاعدة البيانات:\n{ex.Message}\n\n" +
                    "صحّح بيانات الاتصال أدناه — الدفاتر في الخادم لا تُمسّ.");
                if (recovery.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }
        }

        // (ع-D3، CEEF-202): أعلِم المستخدم إن عمل على قاعدة غير التي هيّأها — قبل أن يُدخل أي بيانات.
        if (AppServices.DatabaseFallbackWarning is { } fallbackWarning)
        {
            UiDialog.Show(
                fallbackWarning,
                "BRQOSH — تنبيه: تغيّرت قاعدة البيانات",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        try
        {
            // CEEF-200: التمهيد عبر DI scope (المصنع كان ينشئ الخدمة يدوياً — يكسر حارس DI،
            // ويشغّل على سياق بلا حارس تدقيق). المزوّد مُهيّأ في LoadDatabaseConfig أعلاه.
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<DatabaseBootstrapService>()
                .InitializeAsync(setup!.CompanyName, setup.VatRegistrationNumber, setup.Profile,
                    adminUsername, adminPassword, setup.CountryTemplateId);
            AppServices.CompanyId = result.CompanyId;
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException is null
                ? ex.Message
                : $"{ex.Message}\n—\n{ex.InnerException.Message}";
            UiDialog.Show(
                $"تعذر تهيئة قاعدة البيانات ({AppServices.ActiveProviderLabel}):\n{detail}",
                "BRQOSH — خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (!LicenseStore.ValidateForCompany(setup!.CompanyName, setup.CompletedAtUtc))
        {
            var license = new LicenseActivationWindow(setup.CompanyName);
            if (license.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        if (await TryEnterWithUiTestAuthAsync(setup))
            return;

        var login = new LoginWindow();
        if (login.ShowDialog() != true || login.AuthenticatedUser is null)
        {
            Shutdown();
            return;
        }

        if (login.AuthenticatedUser.MustChangePasswordOnNextLogin)
        {
            var change = new PasswordChangeWindow(login.AuthenticatedUser);
            if (change.ShowDialog() != true || change.UpdatedUser is null)
            {
                Shutdown();
                return;
            }
            login.AuthenticatedUser = change.UpdatedUser;
        }

        AppServices.CurrentUser = login.AuthenticatedUser;

        var main = new MainWindow(setup!);
        MainWindow = main;
        main.Closed += (_, _) => Shutdown();
        main.Show();
        main.Activate();
        main.Focus();
    }

    /// <summary>دخول آلي للتدقيق FlaUI فقط — BRQOSH_UITEST=1 (لا يُفعَّل في التشغيل العادي).</summary>
    private async Task<bool> TryEnterWithUiTestAuthAsync(SetupState setup)
    {
        // (CEEF-228) معطّل قسرياً في الإنتاج - نفس نمط SecurityContext.AutomatedTestBypass.
        // بدون هذه البوابة كان متغيّر بيئة على مستوى المستخدم (لا يحتاج صلاحية مسؤول)
        // يفتح البرنامج بحساب Administrator بلا كلمة مرور ولا ترخيص.
        if (AppEnvironmentConfig.IsProduction)
            return false;

        if (!string.Equals(Environment.GetEnvironmentVariable("BRQOSH_UITEST"), "1", StringComparison.Ordinal))
            return false;

        WriteUiTestDiag("START · UITEST bypass engaged");

        try
        {
            var companyId = AppServices.CompanyId;
            await using var db = AppServices.CreateDb();

            var testUsername = Environment.GetEnvironmentVariable("BRQOSH_TEST_USERNAME")?.Trim();
            AppUser? user = null;
            if (!string.IsNullOrEmpty(testUsername))
            {
                user = await db.AppUsers.FirstOrDefaultAsync(u =>
                    u.CompanyId == companyId && u.Username == testUsername && u.IsActive);
                user ??= await db.AppUsers.FirstOrDefaultAsync(u =>
                    u.Username == testUsername && u.IsActive);
            }

            user ??= await db.AppUsers
                .Where(u => u.CompanyId == companyId && u.IsActive)
                .OrderByDescending(u => u.Role == UserRole.Administrator)
                .ThenBy(u => u.Id)
                .FirstOrDefaultAsync();

            user ??= await db.AppUsers
                .Where(u => u.IsActive && u.Role == UserRole.Administrator)
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync();

            if (user is null)
            {
                WriteUiTestDiag(
                    $"no active users · provider={AppServices.ActiveProviderLabel} · companyId={companyId} · testUser={testUsername ?? "(none)"}");
                return false;
            }

            if (user.MustChangePasswordOnNextLogin)
            {
                user.ClearMustChangePassword();
                await db.SaveChangesAsync();
            }

            AppServices.CompanyId = user.CompanyId;
            AppServices.CurrentUser = user;

            var main = new MainWindow(setup);
            MainWindow = main;
            main.Closed += (_, _) => Shutdown();
            main.Show();
            main.Activate();
            main.Focus();
            WriteUiTestDiag($"OK · user={user.Username} · companyId={user.CompanyId} · provider={AppServices.ActiveProviderLabel}");
            return true;
        }
        catch (Exception ex)
        {
            WriteUiTestDiag(ex.ToString());
            return false;
        }
    }

    private static void WriteUiTestDiag(string message)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "brqosh-uitest-last.txt");
            File.WriteAllText(path, $"{DateTime.UtcNow:u}\n{message}");
        }
        catch (Exception ex)
        {
            // (CEEF-231 · O-36) ‏**هذا الابتلاعُ مقصودٌ ويبقى**: الكاتبُ نفسُه أداةُ
            // ‏تشخيص، **وسقوطُ التطبيق لأنّ ملفَّ تشخيصٍ تعذّرت كتابتُه عبثٌ**.
            // ‏والسببُ يُكتب ولا يُترك «ignore» — فبيانُ النيّة هو الفرقُ بين تسامحٍ
            // ‏مقصودٍ وسهوٍ نُسي.
            System.Diagnostics.Debug.WriteLine($"UI diag write failed: {ex.Message}");
        }
    }
}
