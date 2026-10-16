using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-231 · ط5-٦ب) <b>حالةُ النظام — وما يسأل عنه الدعمُ أوّلَ مكالمة.</b>
///
/// <para><b>ما كان:</b> لا شاشةَ في المستودع تعرض <b>إصدارَ التطبيق، ولا مسارَ القاعدة،
/// ولا محرّكها، ولا آخرَ هجرةٍ طُبِّقت، ولا البيئة</b>. والموجودُ نقطةٌ ملوّنةٌ في شريط
/// الحالة تقول «متّصل» أو لا. <b>فمكالمةُ دعمٍ تبدأ بأسئلةٍ لا يستطيع المستخدمُ الإجابةَ
/// عنها</b>، وتنتهي بطلب لقطةِ شاشةٍ من مدير المهام.</para>
///
/// <para><b>وزرُّ النسخ ليس تفصيلاً</b>: المستخدمُ لا يُملي عشرين سطراً في الهاتف، <b>وما
/// لا يُنسخ لا يُرسَل</b>.</para>
/// </summary>
public partial class DiagnosticsPage : UserControl
{
    public record InfoRow(string Label, string Value);

    private List<InfoRow> _rows = [];

    public DiagnosticsPage()
    {
        InitializeComponent();
        ChannelLabel.Text = LocaleManager.T("قناة التحديث", "Update channel");
        BrowseChannelBtn.Content = LocaleManager.T("استعراض…", "Browse…");
        StageBtn.Content = LocaleManager.T("تنزيل التحديث", "Download update");
        ChannelBox.Text = UpdateChannelSettings.Load() ?? "";
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var rows = new List<InfoRow>
        {
            new("إصدار البرنامج", Version()),
            new("البيئة", AppEnvironmentConfig.IsProduction ? "إنتاج" : "تطوير"),
            new("محرك قاعدة البيانات", AppServices.ActiveProviderLabel),
            new("جذر البيانات", Safe(() => BrqoshDataRoot.Path())),
            new("مجلد السجلات", Safe(() => CrashLog.Directory)),
            new("النظام", $"{Environment.OSVersion} · {(Environment.Is64BitProcess ? "x64" : "x86")}"),
            new("إطار دوت نت", Environment.Version.ToString()),
            new("الجهاز", $"{Environment.UserName}@{Environment.MachineName}"),
        };

        // ‏**وحالةُ القاعدة تُقرأ لا تُفترَض**: شاشةُ تشخيصٍ تقول «سليم» بلا استعلامٍ
        // ‏تكذب في اللحظة التي يُحتاج فيها إليها.
        try
        {
            await using var db = AppServices.CreateDb();

            rows.Add(new InfoRow("الاتصال بالقاعدة",
                await db.Database.CanConnectAsync() ? "متصل" : "غير متصل"));

            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

            rows.Add(new InfoRow("هجرات مطبَّقة", applied.Count.ToString()));
            rows.Add(new InfoRow("آخر هجرة", applied.Count > 0 ? applied[^1] : "—"));

            // ‏**والمعلَّقةُ تُسمّى لا تُعدّ**: رقمٌ وحدَه لا يقول أيُّها، **وأيُّها هو
            // ‏ما يُشخَّص به**.
            rows.Add(new InfoRow("هجرات معلَّقة",
                pending.Count == 0 ? "لا شيء" : string.Join("، ", pending)));

            // ‏(جاهزيّةُ الإطلاق · ح-١٢٥) ‏**وأدواتُ النسخ تُفحص هنا من أوّل يوم** — وكان غيابُها لا يُعرف إلّا بأوّل نسخةٍ فاشلة،
            // ‏والأقدمُ من الخادم كالغائب. والإصدارُ من الخادم نفسِه لا من الإعداد.
            if (db.Database.IsNpgsql())
            {
                var serverVersion = await db.Database
                    .SqlQuery<string>($"SELECT current_setting('server_version_num') AS \"Value\"")
                    .SingleAsync();
                var verdict = BackupToolReadiness.Check(BackupToolReadiness.ServerMajor(serverVersion));
                rows.Add(new InfoRow("أدوات النسخ الاحتياطي", (verdict.Ready ? "✔ " : "✖ ") + verdict.Message));
            }
        }
        catch (Exception ex)
        {
            rows.Add(new InfoRow("الاتصال بالقاعدة", $"تعذّر — {ex.Message}"));
        }

        rows.Add(new InfoRow("سجلات أعطال محفوظة", CrashLogCount().ToString()));

        // (CEEF-231 · ط5-١٠) ‏**وخطواتُ ترقيةِ مخطّطٍ ابتُلع فشلُها** — والتسامحُ مقصود،
        // ‏**والصمتُ عنه كان يجعل ترقيةً فاشلةً تُقرأ نجاحاً**.
        var schema = BrqoshSchemaUpdater.LastRunFailures;
        rows.Add(new InfoRow("خطوات مخطط ابتُلع فشلها",
            schema.Count == 0
                ? "لا شيء"
                : $"{schema.Count} — أولها: {schema[0].Step}"));

        // (CEEF-231 · O-36) ‏**وطابورُ الأحداث المعلَّقة وسببُ آخر فشلٍ فيه.**
        // ‏فالتسامحُ في `OutboxDispatchHook` مقصود، **والصمتُ عنه كان يجعل نقطةَ استقبالٍ
        // ‏ماتت منذ شهورٍ تُقرأ «كلُّ شيءٍ على ما يرام»** — والطابورُ يتراكم بلا سطرٍ يقول لماذا.
        rows.Add(new InfoRow("آخر فشل في تفريغ الطابور",
            OutboxDispatchHook.LastFailure ?? "لا شيء"));

        // ‏(م-٥ · ن-٤٢) **ثلاثةُ صفوفٍ كان الدعمُ يسأل عنها ولا تجيب الشاشة.**
        //
        // ‏«سجلات أعطال محفوظة: 30» **رقمٌ بلا محتوى**: لا يقول متى وقع آخرُ عطلٍ ولا أين.
        // ‏و«حجم الدفتر» يُفرّق **بطئاً من ضخامة**، وبلا it يُطارَد عطلُ أداءٍ لا وجودَ له.
        // ‏و«آخر نسخة» يُفرّق **عطلاً يُستعاد منه من عطلٍ يُفقَد معه العمل**.
        rows.Add(new InfoRow("آخر عطل مسجّل", Safe(NewestCrashLabel)));
        rows.Add(new InfoRow("تقارير مخطط محفوظة", SchemaReportCount().ToString()));
        rows.Add(new InfoRow("حجم الدفتر", Safe(LedgerSizeLabel)));
        rows.Add(new InfoRow("آخر ملف نسخة", Safe(NewestBackupLabel)));

        _rows = rows;
        Grid.ItemsSource = _rows;
        UiTestAutomation.PublishGridCount(Grid, _rows.Count);
    }

    /// <summary>
    /// (CEEF-231 · ط5-٥) يفحص قناةَ التحديث — <b>والتوقيعُ شرطٌ لا تحذير</b>.
    /// </summary>
    /// <remarks>
    /// (ح-١٤١) <b>والقناةُ المحفوظةُ أوّلاً</b>: كان مربعُ «اختر ملفّاً» يُفتح في كلّ فحص.
    /// والمصدرُ الآن من الحقل — مسارُ ملفٍّ أو مشاركةٌ أو <c>https://</c> — ويُحفظ بعد فحصٍ
    /// قرأ بياناً (لا بعد مصدرٍ لم يُوجد)، والاستعراضُ يملأ الحقلَ وحدَه.
    /// </remarks>
    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        StageBtn.Visibility = Visibility.Collapsed;
        var source = ChannelBox.Text.Trim();
        if (source.Length == 0 && !BrowseChannel()) return;
        source = ChannelBox.Text.Trim();

        try
        {
            // ‏**والخدمةُ من الحقن** — واصطلاحُ المستودع مقيسٌ بحارسٍ.
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<UpdateCheckService>().CheckAsync(source);

            if (result.Status != UpdateStatus.NoChannel)
                UpdateChannelSettings.Save(source);

            UpdateText.Text = result.Status switch
            {
                UpdateStatus.UpdateAvailable =>
                    $"يوجد إصدارٌ أحدث: {result.AvailableVersion} (المثبَّت {result.InstalledVersion})"
                    + (string.IsNullOrWhiteSpace(result.Notes) ? "" : $" — {result.Notes}"),
                UpdateStatus.UpToDate =>
                    $"المثبَّت {result.InstalledVersion} أحدثُ أو مساوٍ لما في القناة.",
                UpdateStatus.Untrusted =>
                    $"رُفض البيان: {result.Failure}",
                _ => result.Failure ?? "لا قناةَ تحديث.",
            };

            if (result.Status == UpdateStatus.UpdateAvailable)
                StageBtn.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            UpdateText.Text = ex.Message;
        }
    }

    private void OnBrowseChannel(object sender, RoutedEventArgs e) => BrowseChannel();

    private bool BrowseChannel()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocaleManager.T("اختر بيان الإصدار (manifest.json)", "Choose the release manifest (manifest.json)"),
            Filter = LocaleManager.T("بيان الإصدار", "Release manifest") + "|*.json",
        };

        if (dialog.ShowDialog() != true) return false;
        ChannelBox.Text = dialog.FileName;
        return true;
    }

    /// <summary>
    /// (ح-١٤١) <b>يُنزّل الحزمةَ ويتحقّق من بصمتها ويُريها — ولا يُنصّبها.</b>
    /// </summary>
    /// <remarks>
    /// كانت <c>StageAsync</c> مبنيّةً مختبَرةً <b>ولا يستدعيها شيء</b>. والتنصيبُ يبقى للمالك:
    /// إغلاقُ تطبيقٍ يُمسك دفاترَ منشأةٍ واستبدالُه قرارٌ ليس للبرنامج.
    /// </remarks>
    private async void OnStageUpdate(object sender, RoutedEventArgs e)
    {
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var staged = await facade.GetService<UpdateCheckService>()
                .StageFromChannelAsync(ChannelBox.Text.Trim(), UpdateChannelSettings.StagingDirectory());

            UpdateText.Text = LocaleManager.T(
                "نُزّلت الحزمةُ وطابقت بصمتُها البيانَ الموقَّع — أغلق البرنامجَ ثمّ شغّل المُنصِّب: ",
                "The package was downloaded and matches the signed manifest — close the program, then run the installer: ")
                + staged;
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{staged}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            UpdateText.Text = ex.Message;
        }
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(CrashLog.Directory);
            Process.Start(new ProcessStartInfo(CrashLog.Directory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = new StringBuilder("BRQOSH — حالة النظام");
            text.AppendLine();

            foreach (var row in _rows)
                text.AppendLine($"{row.Label}: {row.Value}");

            Clipboard.SetText(text.ToString());
            UiFeedback.ShowInfo("نُسخ التقرير — ألصِقْه في رسالة الدعم.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static int CrashLogCount()
    {
        try
        {
            return Directory.Exists(CrashLog.Directory)
                ? Directory.GetFiles(CrashLog.Directory, "crash-*.log").Length
                : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    // ‏(م-١ · ن-٣٧) ‏شاشةُ التشخيص هي ما يقرؤه الفنّيُّ في المكالمة — فتُظهر البصمة
    // ‏لا الرقمَ المُدوَّر. `AssemblyVersion` واحدٌ لكلّ بناءٍ من الإصدار نفسِه؛
    // ‏و`InformationalVersion` يحمل الالتزامَ فيُجيب «أيُّ كودٍ يعمل هنا؟».
    private static string Version()
    {
        var entry = Assembly.GetEntryAssembly() ?? typeof(DiagnosticsPage).Assembly;
        var informational = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational)) return informational;
        return entry.GetName().Version?.ToString(3) ?? "غير معروف";
    }

    private static int SchemaReportCount()
    {
        try
        {
            return Directory.Exists(CrashLog.Directory)
                ? Directory.GetFiles(CrashLog.Directory, "schema-*.log").Length
                : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>أحدثُ عطلٍ باسمه ووقته — <b>والاسمُ يحمل السياق، فيُضيّق البحثَ فوراً</b>.</summary>
    private static string NewestCrashLabel()
    {
        if (!Directory.Exists(CrashLog.Directory))
            return "لا شيء";

        var newest = new DirectoryInfo(CrashLog.Directory)
            .GetFiles("crash-*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        return newest is null
            ? "لا شيء"
            : $"{PrintText.Date(DateOnly.FromDateTime(newest.LastWriteTime))} — {newest.Name}";
    }

    /// <summary>
    /// حجمُ الدفتر. <b>ويُقاس لـSQLite من الملفّ</b>؛ وعلى PostgreSQL <b>لا يُدَّعى رقمٌ
    /// لا يُقاس من هنا</b> — فالقاعدةُ على خادمٍ قد لا يملك التطبيقُ حقَّ سؤاله عن حجمها.
    /// </summary>
    private static string LedgerSizeLabel()
    {
        var path = Path.Combine(BrqoshDataRoot.Path(), "brqosh.db");
        if (!File.Exists(path))
            return "على خادم — يُقاس من الخادم";

        var megabytes = new FileInfo(path).Length / 1024d / 1024d;
        return $"{megabytes:N1} م.ب";
    }

    /// <summary>
    /// آخرُ <b>ملفّ</b> نسخةٍ في المجلّد المحلّيّ — <b>بوقته، فـ«موجودة» لا تكفي</b>.
    ///
    /// <para><b>وامتدادان صريحان لا نمطٌ عارٍ.</b> كتبتُ أوّلاً <c>GetFiles("brqosh_*")</c>،
    /// <b>فأسقطتني بوّابةُ المستودع في دقائق</b>: النمطُ العاري يلتقط <c>.partial</c> و
    /// <c>-wal</c> و<c>.tmp</c>، <b>والبقيّةُ المرحليّةُ اليتيمةُ تكون دائماً الأحدث</b>.
    /// وهو درسُ ن-٤٤ مسجَّلاً في <c>BackupPage.xaml.cs:92-99</c> — <b>وقد أمسك كوداً كتبتُه
    /// بعده بساعات</b>.</para>
    ///
    /// <para><b>والعنوانُ يقول «ملفّ» لا «نسخة» عمداً</b>: هذا مسحٌ للقرص لا إثباتٌ للنسخة.
    /// <b>والإثباتُ عملُ <c>BackupService.NewestVerifiedAsync</c></b>، و«الشاشةُ لا تُقرّر ما
    /// النسخة» (ن-٤٤). فلا يُعطى المُشغِّلُ طمأنينةً لم تُقَس.</para>
    /// </summary>
    private static string NewestBackupLabel()
    {
        var folder = Path.Combine(BrqoshDataRoot.Path(), "backups");
        if (!Directory.Exists(folder))
            return "لا مجلّد نسخ";

        var directory = new DirectoryInfo(folder);
        var newest = directory.GetFiles("brqosh_*.dump")
            .Concat(directory.GetFiles("brqosh_*.db"))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        return newest is null
            ? "لا نسخة"
            : $"{PrintText.Date(DateOnly.FromDateTime(newest.LastWriteTime))} — {newest.Name} (ملفّ، غير مُثبَت)";
    }

    /// <summary>
    /// (م-٥ · ن-٤٢) <b>حزمةُ الدعم — ملفٌّ واحدٌ يُرسَل بدل مجلّدٍ يُفتَح.</b>
    /// </summary>
    private void OnExportBundle(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "حزمة دعم|*.zip",
                FileName = SupportBundle.SuggestedFileName(DateTime.Now),
            };

            if (dialog.ShowDialog() != true)
                return;

            var text = new StringBuilder("BRQOSH — حالة النظام");
            text.AppendLine();
            foreach (var row in _rows)
                text.AppendLine($"{row.Label}: {row.Value}");

            var (added, unreadable) = SupportBundle.Create(
                dialog.FileName, text.ToString(), CrashLog.Directory);

            // ‏**وما تعذّر يُقال** — فحزمةٌ ناقصةٌ يظنّها المُشغِّلُ كاملةً تُضيّع مكالمةً كاملة.
            UiFeedback.ShowInfo($"أُنشئت حزمةُ الدعم — التقرير و{added} ملفَّ سجلّ. أرسِلْها كما هي."
                + (unreadable > 0 ? $" (تعذّرت قراءةُ {unreadable} ملفّاً — كانت قيدَ الاستعمال.)" : ""));
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private static string Safe(Func<string> read)
    {
        try { return read(); }
        catch (Exception) { return "غير معروف"; }
    }
}
