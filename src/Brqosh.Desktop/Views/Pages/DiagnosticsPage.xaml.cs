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

        _rows = rows;
        Grid.ItemsSource = _rows;
        UiTestAutomation.PublishGridCount(Grid, _rows.Count);
    }

    /// <summary>
    /// (CEEF-231 · ط5-٥) يفحص قناةَ التحديث — <b>والتوقيعُ شرطٌ لا تحذير</b>.
    /// </summary>
    /// <remarks>
    /// <b>والمصدرُ مسارُ ملفٍّ يُختار</b>: منشآتٌ كثيرةٌ في هذا السوق على شبكةٍ مغلقة،
    /// <b>ومشاركةُ شبكةٍ تصلها تحديثُ الوكيل</b>. والتوقيعُ هو ما يحمي لا القناة.
    /// </remarks>
    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختر بيان الإصدار (update.json)",
            Filter = "بيان الإصدار|*.json",
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            // ‏**والخدمةُ من الحقن** — واصطلاحُ المستودع مقيسٌ بحارسٍ.
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<UpdateCheckService>()
                .CheckAsync(dialog.FileName);

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

    private static string Version() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? typeof(DiagnosticsPage).Assembly.GetName().Version?.ToString(3)
        ?? "غير معروف";

    private static string Safe(Func<string> read)
    {
        try { return read(); }
        catch (Exception) { return "غير معروف"; }
    }
}
