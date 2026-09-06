using System.IO;
using System.Windows;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Views;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Operations;
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
        // ‏(CEEF-236 · م-٢٠) **واتصالٌ يُصلَح لا يُصدَّق على هويّته.** اختبارُ الاتصال يُثبت
        // ‏البلوغَ لا الهويّة، وما بعده كان يُهيّئ بلا سؤال: قاعدةٌ فارغةٌ ⟵ أربعون هجرةً
        // ‏وشركةٌ جديدة فيها، وقاعدةٌ فيها منشأةٌ أخرى ⟵ **تُتبنّى ويُرحَّل على دفترِ غيرِه**.
        // ‏فيُسأل بعد كلّ إصلاحٍ: أهذه قاعدةُ هذا التركيب؟
        var connectionWasRepaired = false;
        while (true)
        {
            string? problem;
            try
            {
                AppServices.LoadDatabaseConfig();
                await AppServices.EnsureDatabaseReachableAsync();
                // (CEEF-236 · م-٢٩) ‏**والسؤالُ يُطرح على كلّ تركيبٍ قائم، لا على المُصلَح وحدَه.**
                //
                // ‏كان الشرطُ `connectionWasRepaired` — فملفُّ اتصالٍ **يُكتب باليد** (وكتيّبُنا
                // ‏يدلّ عليه، والقارئُ يقبل الصريحَ غيرَ المحميّ) أو جذرُ بياناتٍ منسوخٌ من محطّةٍ
                // ‏أخرى يمرّ من أوّل مرّةٍ بلا سؤال — **فيبقى ضررُ م-٢٠ قائماً من البابِ الآخر**.
                var isFreshInstall = adminUsername is not null;
                problem = isFreshInstall
                    ? null
                    : await DescribeIdentityProblemAsync(setup!, refuseVirgin: connectionWasRepaired);
                if (problem is null)
                    break;
            }
            catch (Exception ex)
            {
                problem = $"تعذّر الاتصال بقاعدة البيانات:\n{ex.Message}";
            }

            // (CEEF-236 · م-٢١) ‏**وبناءُ نافذة الإنقاذ نفسُه محروس.** مُنشئُها يقرأ الملفَّ
            // ‏الذي أعطبَ الإقلاعَ للتوّ — فإن رمى، بقيت العمليةُ حيّةً بلا نافذةٍ ولا سبيلَ
            // ‏إلى إغلاقها (‏ShutdownMode=OnExplicitShutdown‎). فيُقال العطلُ ويُغلَق صراحةً.
            bool? repaired;
            try
            {
                var recovery = new DatabaseConnectionWindow(
                    problem + "\n\nصحّح بيانات الاتصال أدناه — الدفاتر في الخادم لا تُمسّ.");
                repaired = recovery.ShowDialog();
            }
            catch (Exception windowEx)
            {
                // ‏**نصٌّ حرفيٌّ لا مركَّب** — فيبلغه المترجِم. والتفصيلُ (الاستثناءُ ومساره)
                // ‏مكتوبٌ في سجلّ الأعطال أصلاً، فلا يُفقد شيءٌ بجعل الرسالة قابلةً للترجمة.
                // ‏وحارسُ `TheUntranslatableRemainder` عضّ هنا: رفعُ سقفه كان أسهلَ وأكذب.
                Brqosh.Infrastructure.Operations.CrashLog.Write(windowEx, "DatabaseConnectionWindow");
                UiDialog.Show(
                    "تعذّر فتح شاشة إعدادات الاتصال. احذف ملف الاتصال database.json من مجلد BRQOSH في APPDATA ثم أعد التشغيل — التفصيل في سجل الأعطال.",
                    "BRQOSH — خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            if (repaired != true)
            {
                Shutdown();
                return;
            }
            connectionWasRepaired = true;
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

        // ‏(CEEF-237 · ت-١٢) رجوعُ الساعة يُسجَّل قبل حكم الصلاحية — **ولا يُوقف العمل**.
        // ‏فساعةٌ تُصحَّح بعد بطاريّةِ لوحةٍ فارغة تصرف عميلاً صادقاً عن دفاتره، والضررُ من
        // ‏إغلاقٍ خاطئ أكبرُ من الضرر من تمديدٍ يُكتشف. والقرارُ للمالك لا لهذه الطبقة.
        var clock = ClockRollbackDetector.Observe();
        if (clock.RolledBack)
            CrashLog.Write(
                new InvalidOperationException(
                    $"ساعةُ الجهاز رجعت {clock.Backwards!.Value.TotalDays:N0} يوماً "
                    + $"عن أحدثِ وقتٍ رآه التطبيق ({clock.HighWaterUtc:u})."),
                "clock-rollback");

        // ‏(م-٣ · ن-٣٥) ‏**يُقاس الترخيصُ على الاسم المُرخَّص لا على المعروض.**
        //
        // ‏كان يُقاس على `setup.CompanyName` — **وهو الحقلُ الذي يُعيد هذا الملفُّ نفسُه
        // ‏كتابتَه** أدناه من هويّة القاعدة بعد قبول المالك. فتصحيحُ خطأٍ مطبعيٍّ في اسم
        // ‏المنشأة كان **يُقفل الإنتاجَ عند الإقلاع التالي** برسالة «الترخيص لمنشأة أخرى».
        //
        // ‏ولم يُفكَّ الرباط: `CompanyMismatch` ما زال يقع، والمتغيّرُ أنّ المقيسَ عليه ثابت.
        var licensedName = setup!.LicensedCompanyName ?? setup.CompanyName;

        if (!LicenseStore.ValidateForCompany(licensedName, setup.CompletedAtUtc))
        {
            var license = new LicenseActivationWindow(licensedName);
            if (license.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        // ‏ويُثبَّت الاسمُ **بعد قبولٍ ناجحٍ وحدَه** — فلا يُثبَّت اسمٌ لم يُقبل به ترخيصٌ قطّ.
        // ‏ومرّةً واحدة: تنصيبٌ قائمٌ يسبق هذا الحقل يُثبَّت عند أوّل إقلاعٍ بعد الترقية.
        if (setup.LicensedCompanyName is null)
        {
            setup = setup with { LicensedCompanyName = licensedName };
            try
            {
                _setupStore.Save(setup);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // ‏تعذّرُ التثبيت لا يمنع الدخول — الترخيصُ قُبل بالفعل. ويُسجَّل ليُرى.
                CrashLog.Write(ex, "license-name-freeze");
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

    /// <summary>
    /// (CEEF-236 · م-٢٠) يصف سببَ رفضِ قاعدةٍ اختِيرت من نافذة الإنقاذ — أو <c>null</c> إن قُبلت.
    ///
    /// <para><b>عذراءُ ⟵ رفضٌ قاطع.</b> بعد إصلاحِ اتصالٍ لا يجوز أن يُنشأ مخطَّطٌ جديد: التركيبُ
    /// له دفاترُ قائمةٌ بالتعريف، فقاعدةٌ فارغةٌ تعني اسماً خاطئاً — والاسمُ الافتراضيُّ في الشاشة
    /// <c>brqosh</c> بينما دفاترُ المنشأة قد تكون في <c>brqosh_prod</c>. والإنشاءُ الصامتُ يترك
    /// المشغّلَ أمام نظامٍ فارغٍ يظنّ معه أنّ دفاترَه ضاعت، ويُلوّث قاعدةً أجنبيّةً بأربعين هجرة.</para>
    ///
    /// <para><b>ومسكونةٌ بغيرِ منشأتنا ⟵ سؤالٌ صريح</b> يُسمّي الاسمَين، فقد يكون التغييرُ مقصوداً
    /// (منشأةٌ أُعيدت تسميتُها) — لكنّه لا يمرّ صامتاً.</para>
    /// </summary>
    private async Task<string?> DescribeIdentityProblemAsync(SetupState setup, bool refuseVirgin)
    {
        DatabaseIdentity identity;
        try
        {
            using var facade = new DesktopAccountingFacade();
            identity = await facade.GetService<DatabaseBootstrapService>().InspectAsync();
        }
        catch (Exception ex)
        {
            return $"تعذّر فحصُ محتوى القاعدة:\n{ex.Message}";
        }

        // ‏**والعذراءُ تُرفض بعد إصلاحِ اتصالٍ فقط.** فاتصالٌ حُمِّل من ملفٍّ قائمٍ ونجح قد يكون
        // ‏أوّلَ إقلاعٍ بعد معالجٍ اكتمل ثمّ سقط التطبيقُ قبل التهيئة — ورفضُه يُقفل التركيبَ على
        // ‏صاحبه. أمّا سلسلةٌ كُتبت في نافذة الإنقاذ فالفراغُ فيها يعني اسماً خاطئاً.
        if (identity.IsVirgin)
            return refuseVirgin
                ? "هذه القاعدة لا تحمل دفاتر BRQOSH." + "\n\n"
                  + "تصحيحُ الاتصال لا يُنشئ دفاتر جديدة — فالأرجح أنّ اسم القاعدة أو الخادم غير صحيح. "
                  + "راجع اسم القاعدة (قد يكون غير الاسم الافتراضي) ثم أعد المحاولة."
                : null;

        if (!identity.Matches(setup.CompanyName, setup.VatRegistrationNumber))
        {
            var answer = UiDialog.Show(
                "القاعدة المختارة تحمل دفاتر منشأة أخرى:" + "\n\n"
                    + $"في القاعدة: {identity.CompanyName}"
                    + (string.IsNullOrWhiteSpace(identity.VatNumber) ? "" : $" (ضريبي {identity.VatNumber})")
                    + "\n"
                    + $"في هذا التركيب: {setup.CompanyName}"
                    + (string.IsNullOrWhiteSpace(setup.VatRegistrationNumber) ? "" : $" (ضريبي {setup.VatRegistrationNumber})")
                    + "\n\n"
                    + "المتابعة تعني الترحيل على دفاتر المنشأة الأولى. هل تريد المتابعة؟",
                "BRQOSH — تنبيه: دفاتر منشأة أخرى",
                // (CEEF-236 · م-٢٧) ‏**الافتراضُ «لا».** بلا هذا الوسيط يُصيّر WPF الزرَّ الأوّل
                // ‏(نعم) هو المُركَّز، فيكفي ضغطُ Enter وسط سلسلةِ حواراتِ إقلاعٍ لتُنفَّذ DDL على
                // ‏قاعدةِ منشأةٍ أخرى حيّة. والعُرفُ قائمٌ في المستودع: `BackupPage` تُمرّر `No` في
                // ‏تأكيدَيها اللذَين لا رجعةَ فيهما.
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes)
                return "لم تُقبل القاعدة — اختر قاعدة هذه المنشأة.";

            // (CEEF-236 · م-٢٨) ‏**وقبولٌ يُسجَّل، وإلا تكرّر الاتهامُ أبداً.**
            //
            // ‏الهويّةُ المتوقَّعةُ تُقرأ من `setup.json`، ويُكتب في موضعٍ واحدٍ هو معالجُ التهيئة.
            // ‏و«إعدادات المنشأة» تُغيّر `Company.NameAr` و`VatRegistrationNumber` في القاعدة
            // ‏**ولا تمسّ الملفّ** — فمن صحّح خطأً مطبعياً في اسم منشأته يُتَّهم بعد أشهرٍ بأنّ
            // ‏قاعدتَه «تحمل دفاتر منشأة أخرى»، والاسمان اسمُه هو. فبعد قبولٍ صريحٍ تُحدَّث اللقطة.
            try
            {
                _setupStore.Save(setup with
                {
                    CompanyName = identity.CompanyName ?? setup.CompanyName,
                    VatRegistrationNumber = identity.VatNumber ?? setup.VatRegistrationNumber,
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // ‏تعذّر تحديثُ اللقطة لا يمنع المتابعة — يُسجَّل ويُمضى.
                Brqosh.Infrastructure.Operations.CrashLog.Write(ex, "SetupStateSyncAfterIdentityConfirm");
            }
        }

        return null;
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
