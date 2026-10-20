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

    /// <summary>
    /// (م-٤ · ن-١٥) <b>يكتب العطلَ ثمّ يعرضه — مسارُ إبلاغٍ واحدٌ لا اثنان.</b>
    /// </summary>
    /// <remarks>
    /// <b>والتوحيدُ ليس ترتيباً:</b> معالجُ المُرسِل وحارسُ الإقلاع يفعلان الشيءَ نفسَه،
    /// <b>ونسختان تنزلقان</b> — فيُصلَح أحدُهما ويبقى الآخر يعرض رسالةً بلا سببٍ داخليٍّ
    /// أو بلا مسارِ سجلّ. <b>وبوّابةُ <c>DialogLocalizationTests</c> تُحصي نصوصَ الحوارات
    /// غيرَ القابلة للترجمة وتمنع نموَّها</b>، ونداءُ عرضٍ واحدٌ يخدم المسارين يُبقي
    /// العدَّ حيث كان — <b>فالسقفُ مِرصادٌ يُطاع لا رقمٌ يُرفع</b>.
    /// </remarks>
    private static void ReportCrash(Exception ex, string context, string headline)
    {
        var path = Brqosh.Infrastructure.Operations.CrashLog.Write(ex, context);

        var detail = ex.InnerException is null
            ? ex.Message
            : $"{ex.Message}\n\n{ex.InnerException.Message}";

        UiDialog.Show(
            $"{headline}\n{detail}" + CrashHint(path),
            "BRQOSH — خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Ui.UiLocalizationHost.Register();
        // ‏(UI-AUDIT · P1) كلُّ نافذةٍ تُحصر في مساحة العمل، وجسمُها يُمرَّر إن لم يتّسع.
        Ui.WindowFit.Register();

        // (CEEF-231 · ط5-٦) ‏**والعطلُ يُكتب على القرص قبل أن يُعرض.**
        //
        // ‏كان المعالجان يعرضان `ex.Message` **ولا يكتبان حرفاً**. فمنشأةٌ يقع عندها
        // ‏عطلٌ تقول للدعم «ظهرت رسالة» — **ولا أثرَ لِما وقع**: لا مكدّس، ولا إصدار،
        // ‏ولا محرّك قاعدة. **فيُطلب منها إعادةُ العطل** وهي تمسك دفاترَها الحقيقية.
        DispatcherUnhandledException += (_, args) =>
        {
            ReportCrash(args.Exception, "DispatcherUnhandledException", "خطأ غير متوقع:");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            // ‏(م-٤ · ن-١٥) ‏**وكانت هذه النسخةُ تعرض الرسالةَ بلا سببها الداخليّ** —
            // ‏وأكثرُ ما يبلغ هذا المعالجَ استثناءٌ ملفوف، فالرسالةُ الخارجيّةُ وحدَها
            // ‏تقول «فشلت العمليّة» ولا تقول لماذا. والمسارُ الموحَّد يُظهر الاثنين.
            if (args.ExceptionObject is Exception ex)
                ReportCrash(ex, "AppDomain.UnhandledException", "خطأ حرج:");
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


        // ‏(م-٤ · ن-١٥) **والإقلاعُ كان بلا نهايةٍ حاسمة — فعطلٌ فيه يترك ما لا نافذةَ له.**
        //
        // ‏`OnStartup` هو `async void`، و**مُولِّدُ حالته يلتقط كلَّ رميةٍ في الجسم كلِّه —
        // ‏قبل أوّلِ `await` وبعده** — ثمّ يبعثها عبر `SynchronizationContext.Post`. والسياقُ
        // ‏هنا سياقُ المُرسِل، فتُسلَّم إلى `DispatcherUnhandledException` و`Handled = true`
        // ‏يطويها. **ومع `ShutdownMode="OnExplicitShutdown"` لا شيءَ يُنهي العملية**: يرى
        // ‏المستخدمُ الحوارَ ويضغط «موافق»، **وتبقى العمليّةُ مقيمةً بلا نافذةٍ ولا سبيلَ
        // ‏إلى إغلاقها** إلّا بمدير المهامّ.
        //
        // ‏**ولماذا يفتح هنا لا عند `new MainWindow`:** `_setupStore.Load()` أدناه يفكّ JSON
        // ‏من القرص، **وهو يرمي على ملفٍّ محجوزٍ أو تالف** — وذلك قبل أوّلِ `await`. وما
        // ‏قبله لا يُغلَّف عمداً: `base.OnStartup` و`UiLocalizationHost.Register` يجريان
        // ‏**قبل تسجيل المعالج**، فرميةٌ فيهما تقتل العمليّةَ ولا تُخلّدها.
        //
        // ‏**وأخطرُ الصور** عند `new MainWindow`: مؤقّتا الخمول والنسخ يُسلَّحان في بانيها
        // ‏**قبل** السطر الذي يرمي (`MainWindow.xaml.cs:61` و`:72`) وتنظيفُهما معلَّقٌ على
        // ‏`Closed` — **حدثٌ لا ترفعه نافذةٌ لم تُعرض** — فتجري نسخةٌ احتياطيّةٌ كلَّ نصفِ
        // ‏ساعةٍ في عمليّةٍ لا يراها أحدٌ ولا يُغلقها أحد.
        //
        // ‏**ولماذا هنا لا داخل المعالج:** عَلَمٌ يُستشار في المعالج لا يُفرّق بين «الإقلاعُ
        // ‏نفسُه انهار» و«معالجٌ في نافذةٍ مشروطةٍ انهار» — والثاني يجب أن يستمرّ. ونوافذُ
        // ‏الإقلاع الثلاث **بلا `catch` إطلاقاً**: `SetupWizardWindow:64` و
        // ‏`DatabaseConnectionWindow:37` (`async void`)، و`LicenseActivationWindow:22`
        // ‏(`void` متزامن). **ورمياتُها لا تنفكّ عبر `ShowDialog()` إلى هنا**: الأُولَيان
        // ‏تُبعثان عمليّةَ إرسالٍ منفصلة، والثالثةُ يلتقطها `Dispatcher.WrappedInvoke` في
        // ‏حلقةِ الحوار المتداخلة. **فالتمييزُ بنيويٌّ لا شرطيّ**، ولذلك بقي الطيُّ كما هو.
        //
        // ‏**وبقيّةٌ تُقال صراحةً:** `Shutdown` لا يقتل `pg_dump` ابناً انطلق، فقد يبقى ملفٌّ
        // ‏مرحليّ — **ولا يُحسب نسخةً** إذ لا يطابق نمطَ `brqosh_*` (ن-٣١).
        try
        {
            // ‏(installer-deployment-ops#13) ‏**اتّفاقيّةُ الترخيص قبل كلّ شيء** — قبل الإعداد وقبل القاعدة؛ ونصٌّ تغيّر يُسأل عنه من جديد.
            var eula = System.IO.File.Exists(EulaAcceptance.TextPath) ? System.IO.File.ReadAllText(EulaAcceptance.TextPath) : null;
            if (eula is null)
            {
                UiDialog.Show("نص اتفاقية الترخيص غير موجود بجوار البرنامج — أعد التثبيت.",
                    "BRQOSH — خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
            if (!EulaAcceptance.IsAccepted(eula) && new Views.EulaWindow(eula).ShowDialog() != true)
            {
                Shutdown();
                return;
            }

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
                    if (isFreshInstall)
                    {
                        // ‏(جاهزيّةُ الإطلاق · ح-١٥٠ · ‏`installer-deployment-ops#9`) ‏**تركيبٌ جديدٌ على قاعدةٍ فيها منشأةٌ ينضمّ إليها بقرار** —
                        // ‏وكان يتجاوز هذا الفحصَ فيتبنّاها صامتاً ويُسقط المديرَ الذي أُدخل للتوّ.
                        var join = await JoinExistingCompanyAsync(setup!);
                        problem = join.Problem;
                        if (join.Joined is { } joined)
                        {
                            setup = joined;
                            adminUsername = null;
                            adminPassword = null;
                        }
                    }
                    else
                    {
                        problem = await DescribeIdentityProblemAsync(setup!, refuseVirgin: connectionWasRepaired);
                    }
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

                // ‏(security-licensing-tenancy#7) ‏مفتاحُ استعادة المدير الأوّل يُعرض الآن مرّةً — ولا تُغلق نافذتُه قبل تأكيد حفظه.
                if (result.RecoveryKey is { } recoveryKey)
                    new Views.RecoveryKeyWindow(recoveryKey).ShowDialog();

                // ‏(جاهزيّةُ الإطلاق · ح-٦٦ · sector-modules#10) **نظامُ العمل نظامُ المنشأة المخزَّن، لا اختيارُ هذا الجهاز.**
                //
                // ‏جهازٌ ثانٍ يُعَدّ على قاعدةٍ قائمة: معالجُه يبدأ على أوّل نظامٍ في القائمة، و`InitializeAsync`
                // ‏يُعيد المنشأةَ القائمةَ ويتجاهل الاختيار. فكانت القوائمُ تُبنى لنظامِ المعالج — **فتختفي شاشاتُ
                // ‏القطاع وبياناتُها في القاعدة** — والشارةُ بجوارها تُسمّي نظامَ المنشأة. فيُتبنّى المخزَّنُ ويُحفظ.
                if (result.Profile != setup.Profile)
                {
                    setup = setup with { Profile = result.Profile };
                    try
                    {
                        _setupStore.Save(setup);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // ‏تعذّرُ الحفظ لا يمنع الإقلاع — التبنّي قائمٌ في هذه الجلسة ويُعاد عند كلّ إقلاع.
                        CrashLog.Write(ex, "setup-profile-adopt");
                    }
                }
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

            // ‏(جاهزيّةُ الإطلاق · ح-١٢٧ · ‏`security-licensing-tenancy#1`) ‏**الصلاحيةُ على الوقت الفعّال** — أبعدُ الساعة وأحدثِ وقتٍ رآه
            // ‏التطبيق — **وكان الرجوعُ يُسجَّل ثمّ يُقاس على الساعة الراجعة نفسِها**، فيُمدَّد الترخيصُ. ولا إقفال: المنتهي يُفتح للقراءة.
            var effectiveNow = LicenseClock.EffectiveNowUtc(DateTime.UtcNow, clock.HighWaterUtc);

            if (!LicenseStore.ValidateForCompany(licensedName, setup.CompletedAtUtc, effectiveNow))
            {
                var license = new LicenseActivationWindow(licensedName);
                if (license.ShowDialog() != true)
                {
                    // ‏(جاهزيّةُ الإطلاق · ح-١٢٠ · ‏`commercial-readiness#6`) ‏**الترخيصُ الأصيلُ المنتهي يفتح الدفاترَ للقراءة**
                    // ‏ولا يُغلقها: المالكُ ملزَمٌ بحفظها وإتاحتها. والمزوَّرُ أو المنقولُ لا يُمنح القراءة (`IsGenuineButExpiredFor`).
                    if (!LicenseStore.IsGenuineButExpiredFor(licensedName, effectiveNow)
                        || UiDialog.Show(
                               "انتهت صلاحية الترخيص. هل تريد فتح الدفاتر للقراءة فقط؟ تُعرض وتُطبع وتُصدَّر، ولا يُضاف إليها شيء حتى يُجدَّد الترخيص.",
                               "BRQOSH — الترخيص منتهٍ", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        Shutdown();
                        return;
                    }

                    ReadOnlyBooks.Enter();
                }
            }
            else if (LicenseStore.ValidLicenseExpiresUtc(licensedName, effectiveNow) is DateTime expires
                     && LicenseExpiryNotice.DaysLeftToWarn(expires, effectiveNow) is not null)
            {
                // ‏(ح-١٢٠) ‏**والإنذارُ قبل ثلاثين يوماً** — فلا يُفاجأ المالكُ بيومٍ لا يُرحِّل فيه. والنصُّ حرفيٌّ ليُترجَم
                // ‏من السجلّ (`DialogLocalizationTests` يمنع نموَّ المركَّب)، وتاريخُ الانتهاء في نافذة التفعيل.
                UiDialog.Show(
                    "تقترب صلاحية الترخيص من الانتهاء (ثلاثون يوماً أو أقلّ). جدِّده قبل ذلك؛ وبعد انتهائه تُفتح الدفاتر للقراءة فقط.",
                    "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
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
        catch (Exception ex)
        {
            // ‏**والإغلاقُ في `finally` عمداً**: لو رمى عرضُ الحوار — وهو وارد؛ نفادُ
            // ‏الذاكرة يُفشل التخصيصَ الثاني كما أفشل الأوّل — لعادت الرميةُ إلى المعالج
            // ‏فطواها، **وعُدنا إلى العمليّة الخالدة بلا نافذة** وهي بعينها العلّة.
            try
            {
                ReportCrash(ex, "OnStartup", "تعذّر تشغيل BRQOSH:");
            }
            finally
            {
                // ‏رمزٌ غيرُ صفريّ: مُطلِقٌ أو مُنصِّبٌ يُفرّق «انهار» عن «أغلقه المستخدم».
                Shutdown(1);
            }
        }
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

    /// <summary>
    /// (ح-١٥٠) <b>تركيبٌ جديدٌ على قاعدةٍ فيها منشأة</b> — يُسأل المُعِدُّ: الانضمامُ (بهويّة القاعدة وبلا مديرٍ جديد) أو اختيارُ قاعدةٍ أخرى.
    /// </summary>
    private async Task<(SetupState? Joined, string? Problem)> JoinExistingCompanyAsync(SetupState setup)
    {
        DatabaseIdentity identity;
        try
        {
            using var facade = new DesktopAccountingFacade();
            identity = await facade.GetService<DatabaseBootstrapService>().InspectAsync();
        }
        catch (Exception ex)
        {
            return (null, $"تعذّر فحصُ محتوى القاعدة:\n{ex.Message}");
        }

        if (!JoiningAnExistingCompany.MustAsk(identity))
        {
            // ‏قاعدةٌ فارغةٌ لمنشأةٍ جديدة — **والإعدادُ مكتملٌ ثانيةً** إن رُفض انضمامٌ قبلها في الجلسة نفسِها.
            try { _setupStore.Save(setup); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Brqosh.Infrastructure.Operations.CrashLog.Write(ex, "SetupStateFreshInstall");
            }
            return (null, null);
        }

        var join = UiFeedback.Confirm(
            LocaleManager.T(
                "قاعدة البيانات المختارة فيها منشأة قائمة: ",
                "The selected database already holds a company: ")
            + identity.CompanyName
            + LocaleManager.T(
                "\n\nهل تريد ربط هذا الجهاز بها؟ لن يُنشأ حسابُ المدير الذي أدخلته — ادخل بحسابٍ موجودٍ فيها.\n«لا» يُعيدك لاختيار قاعدةٍ أخرى.",
                "\n\nConnect this computer to it? The administrator you entered will not be created — sign in with an account that already exists there.\n\"No\" lets you choose another database."));

        if (!join)
        {
            try { _setupStore.Save(JoiningAnExistingCompany.Declined(setup)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Brqosh.Infrastructure.Operations.CrashLog.Write(ex, "SetupStateDeclineJoin");
            }
            return (null, LocaleManager.T(
                "لم يُربط الجهاز بالمنشأة القائمة — اختر قاعدةً فارغةً لمنشأةٍ جديدة، أو قاعدةَ منشأتك.",
                "This computer was not connected to the existing company — choose an empty database for a new company, or your company's database."));
        }

        var joined = JoiningAnExistingCompany.Joined(setup, identity);
        try { _setupStore.Save(joined); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Brqosh.Infrastructure.Operations.CrashLog.Write(ex, "SetupStateJoin");
        }
        return (joined, null);
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
