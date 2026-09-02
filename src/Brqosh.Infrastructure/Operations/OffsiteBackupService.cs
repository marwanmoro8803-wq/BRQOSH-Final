using System.Security.Cryptography;
using Brqosh.Domain.Accounting;

namespace Brqosh.Infrastructure.Operations;

/// <summary>حصيلةُ نسخةٍ إلى وجهةٍ واحدة — <b>والفشلُ يُسمّى ولا يُبتلع</b>.</summary>
public sealed record OffsiteCopyResult(
    string Destination,
    bool Succeeded,
    bool IsOffMachine,
    string? CopiedPath,
    long SizeBytes,
    string? Failure,
    int Pruned);

/// <summary>
/// (CEEF-231 · ط5-٤) <b>النسخةُ الاحتياطية خارج الجهاز — والقرصُ الواحد ليس نسخة.</b>
///
/// <para><b>ما كان:</b> <c>BackupService</c> يكتب ملفّاً <b>إلى مجلّدٍ على القرص نفسِه</b>،
/// وشاشةُ النسخ تنصح المالكَ نصّاً بأن «يحتفظ بنسخةٍ على قرصٍ آخر». <b>ونصيحةٌ ليست
/// ميزة</b>: القرصُ الذي تتلف عليه القاعدةُ تتلف عليه نسختُها، <b>والفديةُ التي تُشفّر
/// المجلّدَ تُشفّر مجلّدَ النسخ الذي بجواره</b>. فمنشأةٌ تظنّ نفسَها محميّةً وهي عارية.</para>
///
/// <para><b>والوجهةُ على القرص نفسِه تُنسَخ إليها ويُقال إنّها ليست خارج الجهاز</b> — لا
/// تُرفض. فقرصٌ ثانٍ داخليٌّ أفضلُ من لا شيء، <b>والادّعاءَ بأنّه حمايةٌ من تلف الجهاز
/// كذبٌ</b>. والفرقُ يُعلَن في الحصيلة، ويراه المالكُ.</para>
///
/// <para><b>وهويّةُ ح-٧:</b> نسخةٌ لم تُتحقَّق ببصمتها ليست نسخة. <b>فالنجاحُ لا يُعلَن
/// إلا بعد قراءةِ الملفّ المنسوخ وحسابِ <c>SHA-256</c> ومطابقتِه بالأصل</b> — ووجودُ
/// ملفٍّ بالحجم نفسِه لا يكفي: النقلُ عبر الشبكة يقطع، والقرصُ يكتب أصفاراً.</para>
///
/// <para><b>والحذفُ بعد التحقّق لا قبله</b>: تنظيفُ القديم ثمّ فشلُ الجديد <b>يترك المنشأةَ
/// بلا نسخةٍ واحدة</b> — وهو أسوأُ من عدم التنظيف بمراحل.</para>
/// </summary>
public sealed class OffsiteBackupService
{
    /// <summary>
    /// ينسخ نسخةً احتياطيةً مُتحقَّقاً منها إلى وجهاتٍ عدّة.
    /// </summary>
    /// <param name="keepPerDestination">
    /// كم نسخةً تبقى في كلّ وجهة. <b>و<c>0</c> يعني لا حذف</b>.
    /// </param>
    /// <remarks>
    /// <b>ووجهةٌ تفشل لا تُسقط البقية</b>: منشأةٌ لها ثلاثُ وجهاتٍ وواحدةٌ مفصولة تبقى
    /// محميّةً باثنتين. <b>وسقوطٌ عند أوّل فشلٍ يُفقد ما كان سينجح.</b>
    /// </remarks>
    public async Task<IReadOnlyList<OffsiteCopyResult>> ReplicateAsync(
        string sourcePath, IReadOnlyList<string> destinations, int keepPerDestination = 7,
        CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
            throw new AccountingException($"النسخةُ المصدر غير موجودة: {sourcePath}");
        if (destinations.Count == 0)
            throw new AccountingException("لا وجهةَ للنسخ — ونسخةٌ على القرص نفسِه ليست نسخة.");

        var sourceHash = await HashAsync(sourcePath, ct);
        var fileName = Path.GetFileName(sourcePath);
        var results = new List<OffsiteCopyResult>();

        foreach (var destination in destinations)
        {
            results.Add(await CopyOneAsync(sourcePath, sourceHash, fileName, destination,
                keepPerDestination, ct));
        }

        return results;
    }

    private static async Task<OffsiteCopyResult> CopyOneAsync(string sourcePath, string sourceHash,
        string fileName, string destination, int keep, CancellationToken ct)
    {
        var offMachine = IsOffMachine(sourcePath, destination);

        try
        {
            Directory.CreateDirectory(destination);
            var target = Path.Combine(destination, fileName);

            // ‏**ويُكتب إلى اسمٍ مؤقّتٍ ثمّ يُنقل**: نسخٌ يُقطع في منتصفه يترك ملفّاً
            // ‏**بالاسم الصحيح وبمحتوىً ناقص** — ويُقرأ نسخةً سليمةً حين يُحتاج إليها.
            var staging = target + ".partial";
            if (File.Exists(staging)) File.Delete(staging);

            await using (var input = File.OpenRead(sourcePath))
            await using (var output = File.Create(staging))
                await input.CopyToAsync(output, ct);

            // ‏**والتحقّقُ ببصمة الملفّ المنسوخ نفسِه** — لا بحجمه. فالنقلُ عبر الشبكة
            // ‏يقطع، والقرصُ التالف يكتب أصفاراً بالطول نفسِه.
            if (!await MatchesAsync(staging, sourceHash, ct))
            {
                File.Delete(staging);
                return new OffsiteCopyResult(destination, false, offMachine, null, 0,
                    "بصمةُ النسخة المنقولة تخالف الأصل — والملفُّ وصل ناقصاً أو تالفاً.", 0);
            }

            if (File.Exists(target)) File.Delete(target);
            File.Move(staging, target);

            // ‏**والتنظيفُ بعد التحقّق** — وتنظيفٌ قبله يترك المنشأةَ بلا نسخةٍ واحدة
            // ‏إن فشل الجديد.
            var pruned = Prune(destination, fileName, keep);

            return new OffsiteCopyResult(destination, true, offMachine, target,
                new FileInfo(target).Length, null, pruned);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or NotSupportedException or ArgumentException)
        {
            // ‏**والفشلُ يُسمّى بوجهته**: «تعذّر النسخ» بلا اسمٍ يترك المالكَ يظنّ الثلاثَ
            // ‏كلَّها فشلت أو نجحت.
            return new OffsiteCopyResult(destination, false, offMachine, null, 0, ex.Message, 0);
        }
    }

    /// <summary>
    /// <b>هل الوجهةُ خارج الجهاز فعلاً.</b>
    /// </summary>
    /// <remarks>
    /// <para><b>ومسارٌ شبكيٌّ (<c>\\\\خادم\\مشاركة</c>) خارج الجهاز بحكم تعريفه.</b> وما
    /// عداه يُقاس <b>بجذر القرص</b>: وجهةٌ على الجذر نفسِه ليست حمايةً من تلف الجهاز —
    /// <b>والقرصُ الذي تتلف عليه القاعدةُ تتلف عليه نسختُها</b>.</para>
    ///
    /// <para><b>وقرصٌ ثانٍ داخليٌّ يُعدّ «خارج الحجم» لا «خارج الجهاز»</b> — والتمييزُ
    /// بينهما لا يُقاس بمسارٍ نصّيّ، <b>فيُقال ما يُعرف ولا يُدَّعى ما لا يُعرف</b>:
    /// جذرٌ مختلفٌ يُعلَن، وحكمُ «خارج الجهاز» يبقى للمالك.</para>
    /// </remarks>
    internal static bool IsOffMachine(string sourcePath, string destination)
    {
        if (destination.StartsWith(@"\\", StringComparison.Ordinal)
            || destination.StartsWith("//", StringComparison.Ordinal))
            return true;

        try
        {
            var sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
            var destinationRoot = Path.GetPathRoot(Path.GetFullPath(destination));

            return !string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// يحذف الأقدمَ ويُبقي <paramref name="keep"/> — <b>ويعمل على نسخِ هذه القاعدة وحدَها</b>.
    /// </summary>
    /// <remarks>
    /// <b>والامتدادُ جزءٌ من النمط</b>: مجلّدٌ مشتركٌ بين قاعدتَي عميلَين لا تُحذف نسخُ
    /// إحداهما بتنظيف الأخرى. <b>وحذفٌ يتجاوز نمطَه يُتلف نسخَ غيره.</b>
    /// </remarks>
    private static int Prune(string destination, string fileName, int keep)
    {
        if (keep <= 0) return 0;

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension)) return 0;

        var stale = new DirectoryInfo(destination)
            .GetFiles($"brqosh_*{extension}")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Skip(keep)
            .ToList();

        var pruned = 0;
        foreach (var file in stale)
        {
            try
            {
                file.Delete();
                pruned++;
            }
            catch (IOException)
            {
                // ‏**وملفٌّ مقفلٌ لا يُسقط التنظيف** — يبقى ويُحذف في المرّة التالية.
            }
        }

        return pruned;
    }

    /// <summary>
    /// <b>هل الملفُّ يُطابق البصمةَ المنتظَرة.</b>
    /// </summary>
    /// <remarks>
    /// <para><b>ومُستخرَجٌ ليُقاس مباشرةً</b>: فرعُ الفشل لا يقع في نسخٍ محلّيٍّ سليم،
    /// <b>وحارسٌ يمرّ لأنّ الفرعَ لا يُبلَغ لا يحرس شيئاً</b> — وقد قِيس ذلك بطفرةٍ نجت:
    /// استُبدل الحسابُ بالبصمة المصدر فبقيت الحزمةُ خضراء.</para>
    ///
    /// <para><b>وحدُّ القياس مُعلَن</b>: يُقاس هنا أنّ المقارنةَ تعمل، <b>ويُقاس في
    /// <c>ReplicateAsync</c> أنّها تُستدعى</b> — والتلفُ الحقيقيّ أثناء النقل يقيسه
    /// تشغيلٌ حيّ لا اختبارُ وحدة.</para>
    /// </remarks>
    internal static async Task<bool> MatchesAsync(string path, string expectedHash,
        CancellationToken ct = default) =>
        string.Equals(await HashAsync(path, ct), expectedHash, StringComparison.Ordinal);

    internal static async Task<string> FileHashAsync(string path, CancellationToken ct = default) =>
        await HashAsync(path, ct);

    private static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}
