using System.Text;
using Brqosh.Domain.Accounting;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (جاهزيّةُ الإنتاج · C) <b>الملفُّ الذي يختاره المستخدمُ للاستيراد يُقاس قبل أن يُقرأ.</b>
///
/// <para><b>ما كان:</b> شاشاتُ ترحيل البيانات والكشف البنكيّ والتسوية تقرأ الملفَّ كلَّه في الذاكرة بـ<c>ReadAllTextAsync</c>
/// بلا سقف، وشاشةُ الإعدادات تفتح أيَّ صورةٍ شعاراً. ملفٌّ اختير خطأً (نسخةٌ احتياطيّة، فيديو، تصديرٌ بالغيغابايت) يُجمّد الشاشةَ
/// ثمّ يُسقط البرنامجَ بنفاد الذاكرة — <b>والحدُّ هنا يُقال بالعربيّة قبل أيّ قراءة</b>.</para>
///
/// <para><b>والسقفان واسعان عمداً</b>: كشفُ بنكٍ لسنةٍ كاملة أو دليلُ أصنافٍ بمئة ألف سطرٍ دون 10 ميجابايت؛ والسقفُ يمنع الخطأ لا العمل.</para>
/// </summary>
public static class ImportFileGuard
{
    /// <summary>سقفُ ملفّ CSV المستورَد — 50 ميجابايت.</summary>
    public const long MaxCsvBytes = 50L * 1024 * 1024;

    /// <summary>سقفُ صورة الشعار — 10 ميجابايت.</summary>
    public const long MaxImageBytes = 10L * 1024 * 1024;

    /// <summary>يرمي <see cref="AccountingException"/> إن غاب الملفُّ أو كان فارغاً أو تجاوز <paramref name="maxBytes"/>.</summary>
    public static void EnsureWithinLimit(string path, long maxBytes)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new AccountingException($"الملفّ غير موجود: {info.Name}");
        if (info.Length == 0)
            throw new AccountingException($"الملفّ فارغ: {info.Name}");
        if (info.Length > maxBytes)
            throw new AccountingException(
                $"الملفّ «{info.Name}» أكبر من المسموح للاستيراد ({info.Length / 1024.0 / 1024.0:N1} من {maxBytes / 1024 / 1024} ميجابايت) — تأكّد أنّه الملفّ المقصود.");
    }

    /// <summary>يقيس الملفَّ ثمّ يقرؤه نصّاً — UTF-8 افتراضيّاً مع كشف علامة الترتيب.</summary>
    public static async Task<string> ReadCsvAsync(string path, Encoding? encoding = null, CancellationToken ct = default)
    {
        EnsureWithinLimit(path, MaxCsvBytes);
        return encoding is null
            ? await File.ReadAllTextAsync(path, ct)
            : await File.ReadAllTextAsync(path, encoding, ct);
    }
}
