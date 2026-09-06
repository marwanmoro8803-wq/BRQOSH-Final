using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (ن-٤١) <b>سببُ فشلِ النسخ المجدول كان يُحسب ويُعاد ثمّ يُرمى.</b>
///
/// <para><c>BackupScheduleService</c> يَعِد في تعليقه حرفيّاً: «الفشلُ لا يُبتلع…
/// <b>لكنّ الإخفاق يُعاد ليُعرض لا ليُنسى</b>». و<c>RunIfDueAsync</c> يُعيد
/// <c>RunOutcome(Ran, Path, Failure, State)</c> فعلاً. <b>والمستدعي الوحيدُ في المستودع
/// كان يقرأ <c>State</c> ولا يقرأ <c>Failure</c>.</b></para>
///
/// <para><b>فالوعدُ محقَّقٌ في الخدمة ومنقوضٌ عند المستدعي</b> — وهو النمطُ الذي دفع هذا
/// التدقيقُ ثمنَه مراراً: قيمةٌ تُحسب بعنايةٍ ولا يقرؤها أحد. والمالكُ يرى شارةَ «تأخّر
/// النسخ» <b>ولا يعرف لماذا</b>: أقرصٌ ممتلئ؟ أم <c>pg_dump</c> مفقود؟ أم صلاحيّةُ
/// مجلّد؟ <b>والفرقُ بينها هو الفرقُ بين إصلاحٍ في دقيقةٍ وبلاغِ دعمٍ في أسبوع.</b></para>
///
/// <para><b>ولمَ إثباتُ أكوادٍ ثابتٍ لا اختبارُ سلوك:</b> المستدعي كودٌ خلف واجهة WPF
/// (<c>MainWindow.xaml.cs</c>) ولا يُشغَّل بلا واجهة. <b>والقيمةُ المهملةُ لا يكشفها
/// اختبارُ سلوكٍ أصلاً</b> — فإهمالُها لا يُغيّر مخرَجاً يُقاس، وهذا بعينه ما جعلها
/// تمرّ سنة.</para>
/// </summary>
public class ScheduledBackupFailureIsShownTests
{
    private static string MainWindowSource =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "MainWindow.xaml.cs"));

    /// <summary><b>القياس:</b> السببُ يُقرأ من الحصيلة.</summary>
    [Fact]
    public void TheScheduledBackupCaller_ReadsTheFailureReason()
    {
        Assert.Contains("outcome.Failure", MainWindowSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ولا يُقرأ ليُهمَل:</b> يُكتب في سجلّ الأعطال <b>ويُعرض على الشارة</b>.
    ///
    /// <para>فقراءةٌ إلى متغيّرٍ لا يُستعمل تُرضي التوكيدَ الأوّلَ ولا تُصلح شيئاً.</para>
    /// </summary>
    [Fact]
    public void TheFailureReason_IsBothLoggedAndSurfaced()
    {
        var source = MainWindowSource;

        Assert.Contains("scheduled-backup-failed", source, StringComparison.Ordinal);
        Assert.Contains("ShowBackupBadge(outcome.State, outcome.Failure)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وبدونه التوكيدان أعلاه خبرٌ عن ماسحٍ أعمى.</b>
    ///
    /// <para>فمسارٌ خاطئ أو ملفٌّ نُقل يُعطي نصّاً فارغاً، و<c>Assert.Contains</c> عليه
    /// يسقط لسببٍ لا علاقةَ له بالبند. فيُشترط أنّ الماسحَ يقرأ ملفّاً حقيقيّاً يحوي
    /// ما نعلم أنّه فيه.</para>
    /// </summary>
    [Fact]
    public void TheScanner_ActuallyReadsTheWindow()
    {
        var source = MainWindowSource;

        Assert.True(source.Length > 10_000, $"‏المصدرُ المقروءُ {source.Length} محرفاً — مشكوكٌ فيه.");
        Assert.Contains("RunIfDueAsync", source, StringComparison.Ordinal);
        Assert.Contains("outcome.State", source, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
