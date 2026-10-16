using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (ح-١٤١ · installer-deployment-ops#12) <b>القناةُ تُحفظ مرّةً — لا يُختار ملفٌّ في كلّ فحص.</b>
///
/// <para><b>ما كان:</b> فحصُ التحديث مربعُ «اختر ملفّاً» في شاشة التشخيص، في كلّ مرّة،
/// <b>ولا يُفحص شيءٌ إن لم يفتح أحدٌ تلك الشاشة</b>. فقناةٌ موقَّعةٌ مقيسةٌ لا يبلغها عميلٌ
/// أبداً، والتحديثُ الأمنيُّ يبقى في مجلّد الوكيل.</para>
///
/// <para><b>وما صار:</b> المصدرُ (مسارُ ملفٍّ أو مشاركةٌ أو <c>https://</c>) يُحفظ في جذر
/// البيانات، ويُفحص عند الإقلاع وفي الشاشة. <b>والحفظُ لا يمنح ثقة</b>: من يكتب هذا الملفَّ
/// يوجّه الفحصَ إلى قناته، <b>والتوقيعُ بمرساة التحديث هو ما يحكم</b> — فقناةٌ مزوَّرةٌ تُقرأ
/// «غيرُ موثوق» ولا تُنزِّل شيئاً.</para>
/// </summary>
public static class UpdateChannelSettings
{
    /// <summary>اسمُ الملفّ في جذر البيانات.</summary>
    public const string FileName = "update-channel.txt";

    /// <summary>مجلّدُ تهيئة الحزم في جذر البيانات — <b>يُنزَّل إليه ولا يُنصَّب منه آليّاً</b>.</summary>
    public const string StagingFolderName = "updates";

    public static string FilePath(string? dataRoot = null) =>
        Path.Combine(dataRoot ?? BrqoshDataRoot.Path(), FileName);

    public static string StagingDirectory(string? dataRoot = null) =>
        Path.Combine(dataRoot ?? BrqoshDataRoot.Path(), StagingFolderName);

    /// <summary>القناةُ المحفوظة — أو <c>null</c> إن لم تُهيَّأ.</summary>
    public static string? Load(string? dataRoot = null)
    {
        var path = FilePath(dataRoot);
        if (!File.Exists(path))
            return null;

        var source = File.ReadAllText(path).Trim();
        return source.Length == 0 ? null : source;
    }

    /// <summary>
    /// يحفظ القناة — <b>و<c>http://</c> يُرفض هنا أيضاً</b>، لا عند أوّل فحصٍ بعد أسابيع.
    /// </summary>
    public static string Save(string source, string? dataRoot = null)
    {
        var trimmed = (source ?? "").Trim();
        if (trimmed.Length == 0)
            throw new AccountingException("مصدرُ قناة التحديث فارغ.");

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new AccountingException(
                "قناةُ التحديث على http غيرِ مُعمّى مرفوضة — استعمل https أو مسارَ مشاركة.");

        var path = FilePath(dataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, trimmed);
        return trimmed;
    }
}
