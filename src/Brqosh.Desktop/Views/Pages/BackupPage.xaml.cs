using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-229 · م3-٢) شاشةُ النسخ الاحتياطي — <b>وبلا شاشةٍ تبقى الخدمةُ نصفَ إصلاح.</b>
///
/// <para>والاستعادةُ متلِفةٌ لا رجعةَ فيها، فتُطلب موافقةٌ صريحةٌ مرّتين: الأولى تشرح الأثر،
/// والثانية تطلب كتابةَ كلمةٍ بعينها. ونقرةٌ واحدة لا تكفي لمحو دفاتر منشأة.</para>
/// </summary>
public partial class BackupPage : UserControl
{
    private const string RestoreConfirmationWord = "استعادة";

    public BackupPage() => InitializeComponent();

    private static string BackupFolder =>
        Path.Combine(BrqoshDataRoot.Path(), "backups");

    private async void OnBackup(object sender, RoutedEventArgs e)
    {
        BackupBtn.IsEnabled = false;
        try
        {
            // ‏اسمٌ يحمل لحظتَه — فلا تُكتب نسخةٌ فوق نسخة، وهو أوّلُ ما يُفقد بالتسمية الثابتة.
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            var extension = AppServices.ActiveProviderLabel == "PostgreSQL" ? "dump" : "db";
            var target = Path.Combine(BackupFolder, $"brqosh_{stamp}.{extension}");

            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<BackupService>().CreateAsync(target);

            Show($"تمّت النسخة: {Path.GetFileName(result.Path)} "
                 + $"({result.SizeBytes / 1024.0 / 1024.0:N1} ميجابايت) — {result.Provider}");
        }
        catch (Exception ex)
        {
            Show(ex.Message, isError: true);
        }
        finally
        {
            BackupBtn.IsEnabled = true;
        }
    }

    /// <summary>
    /// (CEEF-231 · ط5-٤) ينسخ <b>أحدثَ نسخةٍ</b> إلى الوجهات المُدخَلة.
    /// </summary>
    /// <remarks>
    /// <para><b>وأحدثُ نسخةٍ لا نسخةٌ جديدة</b>: النسخُ عمليةٌ ثقيلة على قاعدةٍ كبيرة،
    /// <b>وأخذُ نسخةٍ ثانيةٍ لمجرّد نقلها يُضاعف الحملَ ويُنتج ملفّين لا يتطابقان</b>.
    /// فيُنقل ما أُخذ وتُحقَّق بصمتُه.</para>
    ///
    /// <para><b>ووجهةٌ على القرص نفسِه تُنسَخ إليها ويُقال إنّها ليست خارج الجهاز</b> —
    /// لا تُرفض. فقرصٌ ثانٍ داخليٌّ أفضلُ من لا شيء، <b>والادّعاءَ بأنّه حمايةٌ من تلف
    /// الجهاز كذب</b>.</para>
    /// </remarks>
    private async void OnReplicate(object sender, RoutedEventArgs e)
    {
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        try
        {
            var destinations = (DestinationsBox.Text ?? string.Empty)
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (destinations.Count == 0)
                throw new Brqosh.Domain.Accounting.AccountingException(
                    "أدخِلْ وجهةً واحدةً على الأقلّ — ونسخةٌ على القرص نفسِه ليست نسخة.");

            Directory.CreateDirectory(BackupFolder);
            var latest = new DirectoryInfo(BackupFolder)
                .GetFiles("brqosh_*")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()
                ?? throw new Brqosh.Domain.Accounting.AccountingException(
                    "لا نسخةَ لنقلها — خُذْ نسخةً أوّلاً.");

            using var facade = new DesktopAccountingFacade();
            var results = await facade.GetService<OffsiteBackupService>()
                .ReplicateAsync(latest.FullName, destinations);

            var lines = results.Select(r => r.Succeeded
                ? $"✔ {r.Destination} — {r.SizeBytes / 1024.0 / 1024.0:N1} ميجابايت"
                  + (r.IsOffMachine ? "" : " (على القرص نفسه — ليست حمايةً من تلف الجهاز)")
                  + (r.Pruned > 0 ? $" · حُذفت {r.Pruned} نسخةً قديمة" : "")
                : $"✘ {r.Destination} — {r.Failure}");

            OffsiteResultText.Text = string.Join(Environment.NewLine, lines);

            // ‏**وفشلُ وجهةٍ يُعلَن نصّاً لا لوناً**: المالكُ يقرأ السطرَ ولا يُحصي العلامات.
            var failed = results.Count(r => !r.Succeeded);
            Show(failed == 0
                ? $"نُقلت النسخةُ إلى {results.Count} وجهة."
                : $"نجحت {results.Count - failed} من {results.Count} — وفشلت {failed}، وأسبابُها أعلاه.",
                isError: failed > 0);
        }
        catch (Exception ex)
        {
            Show(ex.Message, isError: true);
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(BackupFolder);
        Process.Start(new ProcessStartInfo(BackupFolder) { UseShellExecute = true });
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختر ملفّ النسخة الاحتياطية",
            Filter = "نسخ برقوش (*.dump;*.db)|*.dump;*.db|كل الملفات (*.*)|*.*",
            InitialDirectory = Directory.Exists(BackupFolder) ? BackupFolder : null,
        };

        if (dialog.ShowDialog() != true)
            return;

        var confirm = UiDialog.Show(
            $"سيُمحى كلُّ ما في القاعدة الحالية ويحلّ محلَّه محتوى:\n{Path.GetFileName(dialog.FileName)}\n\n"
            + "لا رجعةَ في هذه العملية. هل أنت متأكّد؟",
            "استعادة نسخة احتياطية", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes)
            return;

        // ‏تأكيدٌ ثانٍ بالكتابة: نقرتان متتاليتان تقعان سهواً، وكتابةُ كلمةٍ لا تقع.
        var prompt = new TextInputDialog(
            "تأكيد الاستعادة",
            $"اكتب كلمة «{RestoreConfirmationWord}» للمتابعة:")
        {
            Owner = Window.GetWindow(this),
        };

        var typed = prompt.ShowDialog() == true ? prompt.Value : null;
        if (!string.Equals(typed?.Trim(), RestoreConfirmationWord, StringComparison.Ordinal))
        {
            Show("أُلغيت الاستعادة — لم تُكتب كلمةُ التأكيد.");
            return;
        }

        RestoreBtn.IsEnabled = false;
        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<BackupService>().RestoreAsync(dialog.FileName);
            Show("تمّت الاستعادة. أغلِق التطبيق وأعِد فتحه لتحميل البيانات المستعادة.");
        }
        catch (Exception ex)
        {
            Show(ex.Message, isError: true);
        }
        finally
        {
            RestoreBtn.IsEnabled = true;
        }
    }

    private void Show(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? System.Windows.Media.Brushes.Firebrick
            : System.Windows.Media.Brushes.SeaGreen;
        StatusText.Visibility = Visibility.Visible;
    }
}
