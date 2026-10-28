using Brqosh.Domain.Accounting;

namespace Brqosh.Infrastructure.Operations;

/// <summary>حالةُ جدولِ النسخ كما تُقرأ من مجلّد النسخ نفسِه.</summary>
/// <param name="LastBackupUtc">وقتُ أحدثِ نسخةٍ موجودة، أو <c>null</c> إن لم توجد نسخةٌ قطّ.</param>
/// <param name="Age">عمرُ أحدثِ نسخة، أو <c>null</c> إن لم توجد.</param>
/// <param name="IsDue">هل حان وقتُ نسخةٍ جديدة.</param>
/// <param name="IsOverdue">هل تجاوز العمرُ حدَّ الإنذار — وهو ما يُعرض للمالك.</param>
public sealed record BackupScheduleState(
    DateTime? LastBackupUtc, TimeSpan? Age, bool IsDue, bool IsOverdue)
{
    /// <summary>نصٌّ عربيٌّ قصيرٌ يصلح لشارةٍ على لوحة التحكّم.</summary>
    public string Badge => LastBackupUtc is null
        ? "لا نسخةَ احتياطيّةً قطّ"
        : Age!.Value.TotalHours < 24
            ? "آخرُ نسخة: اليوم"
            : $"آخرُ نسخة: قبل {(int)Age.Value.TotalDays} يوماً";
}

/// <summary>
/// <b>النسخُ الاحتياطيّ صار يقع من نفسِه — وكان زرّاً ينتظر من يضغطه.</b>
///
/// <para><b>ما كان:</b> <c>BackupService</c> مبنيٌّ ومُختبَرٌ بهويّة «تُتلَف القاعدةُ ثمّ
/// تُستعاد»، <b>ومستدعيه الوحيد زرٌّ في <c>BackupPage</c></b>. لا مؤقّت، ولا مهمّة مجدولة،
/// ولا شارةٌ تقول متى كانت آخرُ نسخة. وكتيّبُ التركيب يَعِد بنسخةٍ <b>يوميّة</b>.</para>
///
/// <para><b>وأثرُه ليس خطأً في حساب:</b> منشأةٌ تعمل شهراً ولم يضغط أحدٌ الزرّ، ثمّ يتلف
/// القرص — <b>فتضيع الدفاتر كلُّها</b>. وهذا البندُ الوحيد في السجلّ الذي لا يُعوَّض.</para>
///
/// <para><b>ولا حالةَ جديدةٌ تُخزَّن:</b> الشاهدُ على وقوع النسخة هو <b>ملفُّ النسخة نفسُه</b>.
/// فيُقرأ أحدثُ ملفٍّ في المجلّد — وهو المصدرُ الذي تقرأه شاشةُ النسخ أصلاً. وسجلٌّ منفصلٌ
/// للتواريخ يفتح بابَ الكذب: صفٌّ يقول «نُسخ» وملفٌّ غيرُ موجود.</para>
///
/// <para><b>والفشلُ لا يُبتلع:</b> <c>RunIfDueAsync</c> يُعيد الحصيلةَ ولا يرمي، فمؤقّتُ
/// الواجهة لا يُسقط التطبيق؛ <b>لكنّ الإخفاق يُعاد ليُعرض</b> لا ليُنسى.</para>
/// </summary>
public sealed class BackupScheduleService(BackupService backups)
{
    /// <summary>الفاصلُ الافتراضيّ — يوميّاً، كما يَعِد الكتيّب.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(24);

    /// <summary>حدُّ الإنذار: بعده تصير الشارةُ تحذيراً لا خبراً.</summary>
    public static readonly TimeSpan OverdueAfter = TimeSpan.FromHours(48);

    /// <param name="Pruned">
    /// (م-٤ · ن-٣٦) عددُ النسخ القديمة التي حذفتها هذه التشغيلة — <b>يُعاد ليُسجَّل</b>.
    /// فمُؤقِّتٌ يحذف ملفّاتِ نسخٍ يوميّاً بلا أثرٍ في أيّ مكان هو عينُ شكلِ ن-٤١.
    /// </param>
    /// <param name="PruneFailures">ما تعذّر حذفُه — تكرارُه يعني أنّ الحدَّ لا يعمل.</param>
    public sealed record RunOutcome(
        bool Ran, string? Path, string? Failure, BackupScheduleState State,
        int Pruned = 0, int PruneFailures = 0);

    /// <summary>امتدادُ ملفّ النسخة حسب المزوّد.</summary>
    private static string ExtensionFor(bool isPostgres) => isPostgres ? "dump" : "db";

    /// <summary>
    /// يقرأ حالةَ الجدول من المجلّد. <b>لا يكتب شيئاً</b> — صالحٌ للاستدعاء من الواجهة بحرّية.
    /// </summary>
    public static BackupScheduleState ReadState(
        string backupFolder, TimeSpan? interval = null, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var every = interval ?? DefaultInterval;

        var newest = NewestBackupUtc(backupFolder);
        if (newest is null)
            return new BackupScheduleState(null, null, IsDue: true, IsOverdue: true);

        var age = now - newest.Value;

        // ‏عمرٌ سالبٌ يعني ساعةً رُجِّعت أو ملفّاً نُسخ بطابعٍ مستقبليّ — يُعامَل «حديثاً»
        // ‏لا «مستحقّاً»، فلا يُشعل نسخاً متكرّراً في حلقة.
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;

        return new BackupScheduleState(newest, age, IsDue: age >= every, IsOverdue: age >= OverdueAfter);
    }

    /// <summary>أحدثُ ملفِّ نسخةٍ في المجلّد — أو <c>null</c> إن لم يوجد مجلّدٌ أو ملفّ.</summary>
    internal static DateTime? NewestBackupUtc(string backupFolder)
    {
        if (string.IsNullOrWhiteSpace(backupFolder) || !Directory.Exists(backupFolder)) return null;

        // ‏الاسمُ شرط: أيُّ ملفِّ قاعدةٍ يُلقى في المجلّد كان يُحسب نسخةً — بما فيها قاعدةٌ
        // ‏حيّةٌ نُسخت إلى هناك يدوياً. فيُشترط عُرفُ التسمية الذي يكتب به المنتجُ نفسُه.
        DateTime? newest = null;
        foreach (var pattern in new[] { "brqosh_*.dump", "brqosh_*.db" })
        {
            foreach (var file in Directory.EnumerateFiles(backupFolder, pattern))
            {
                // ‏ملفٌّ فارغٌ ليس نسخة — و<c>CreateAsync</c> يرفضه أصلاً، لكنّ بقايا محاولةٍ
                // ‏مقطوعةٍ قد تبقى في المجلّد فتُوهم بحماية.
                var info = new FileInfo(file);
                if (info.Length == 0) continue;
                var stamp = info.LastWriteTimeUtc;
                if (newest is null || stamp > newest) newest = stamp;
            }
        }
        return newest;
    }

    /// <summary>
    /// ينسخ إن حان الوقت. <b>ولا يرمي</b>: الإخفاقُ يُعاد في <see cref="RunOutcome.Failure"/>.
    /// </summary>
    public async Task<RunOutcome> RunIfDueAsync(
        string backupFolder, bool isPostgres, TimeSpan? interval = null,
        DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var state = ReadState(backupFolder, interval, nowUtc);
        if (!state.IsDue)
            return new RunOutcome(Ran: false, Path: null, Failure: null, State: state);

        try
        {
            Directory.CreateDirectory(backupFolder);
            var stamp = (nowUtc ?? DateTime.UtcNow).ToString("yyyyMMdd_HHmmss");
            var target = Path.Combine(backupFolder, $"brqosh_auto_{stamp}.{ExtensionFor(isPostgres)}");
            var result = await backups.CreateAsync(target, ct);
            return new RunOutcome(
                Ran: true, Path: result.Path, Failure: null,
                State: ReadState(backupFolder, interval, nowUtc),
                Pruned: result.Pruned, PruneFailures: result.PruneFailures);
        }
        // ‏ومسارٌ غيرُ صالحٍ من الإعدادات لا يُسقط التطبيق: ArgumentException وNotSupportedException
        // ‏يخرجان من Path/Directory على مسارٍ فيه محارفُ ممنوعة أو اسمُ جهازٍ محجوز.
        catch (Exception ex) when (ex is AccountingException or IOException
                                      or UnauthorizedAccessException or OperationCanceledException
                                      or ArgumentException or NotSupportedException)
        {
            // ‏الحالةُ المُعادة هي حالةُ ما قبل المحاولة — فالمالكُ يرى أنّ النسخةَ ما زالت متأخّرة.
            return new RunOutcome(Ran: false, Path: null, Failure: ex.Message, State: state);
        }
    }
}
