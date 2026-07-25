namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// جذر بيانات المستخدم الموحّد: قاعدة SQLite الافتراضية، ملفات ZATCA، الأسرار المحلية.
///
/// الافتراضي %APPDATA%\BRQOSH، ويُوجَّه بالكامل عبر متغير البيئة BRQOSH_DATA_ROOT.
///
/// (CEEF-203): بلا هذا التوجيه كانت الاختبارات تكتب في بيانات المستخدم الحقيقية —
/// بُرهن تجريبياً أن تشغيلة واحدة لاختبارات API تضيف شركات إلى قاعدته المحلية،
/// لأن Brqosh.Api يسقط إلى القاعدة الافتراضية حين لا يجد ConnectionStrings:Brqosh.
/// مفهوم واحد للجذر يجعل عزل كل بيانات الاختبار سطراً واحداً.
/// </summary>
public static class BrqoshDataRoot
{
    public const string OverrideEnvironmentVariable = "BRQOSH_DATA_ROOT";

    public static string Path()
    {
        var overridden = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(overridden)
            ? overridden
            : System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH");
    }

    /// <summary>مجلد فرعي داخل جذر البيانات، مُنشأ إن لم يوجد.</summary>
    public static string Directory(params string[] segments)
    {
        var path = segments.Length == 0
            ? Path()
            : System.IO.Path.Combine(new[] { Path() }.Concat(segments).ToArray());
        System.IO.Directory.CreateDirectory(path);
        return path;
    }
}
