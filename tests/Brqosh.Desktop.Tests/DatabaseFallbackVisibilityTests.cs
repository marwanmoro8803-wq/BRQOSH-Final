using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ع-D3 (CEEF-202) — التحويل الاضطراري من PostgreSQL إلى SQLite يجب ألّا يكون صامتاً.
///
/// العمل على قاعدة غير التي هيّأها المستخدم يعني كتابة القيود في دفتر آخر: خطر انقسام
/// دفاتر محاسبية. الحارس ثابت (على المصدر) لأن المسار يتطلب فشل اتصال حقيقي وقت التشغيل.
/// </summary>
public class DatabaseFallbackVisibilityTests
{
    private static string DesktopRoot()
    {
        var dir = AppContext.BaseDirectory;
        var root = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"Desktop source root not found: {root}");
        return root;
    }

    private static string Read(string relativePath)
    {
        var path = Path.Combine(DesktopRoot(), relativePath);
        Assert.True(File.Exists(path), $"Expected file missing: {relativePath}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void AppServices_ExposesFallbackWarning()
    {
        var source = Read("AppServices.cs");
        Assert.Contains("DatabaseFallbackWarning", source);
    }

    /// <summary>فرع التحويل نفسه يجب أن يملأ التحذير — لا أن يبدّل المزوّد بصمت.</summary>
    [Fact]
    public void FallbackBranch_SetsWarning_NotSilent()
    {
        var source = Read("AppServices.cs");

        var branchStart = source.IndexOf("if (_provider == DatabaseProvider.PostgreSql)", StringComparison.Ordinal);
        Assert.True(branchStart > 0, "لم يُعثر على فرع التحويل إلى SQLite في AppServices.");

        var branch = source[branchStart..Math.Min(source.Length, branchStart + 1200)];
        Assert.Contains("DatabaseFallbackWarning =", branch);
    }

    /// <summary>ولا قيمة للتحذير إن لم يُعرض للمستخدم عند الإقلاع.</summary>
    [Fact]
    public void Startup_SurfacesFallbackWarning_ToUser()
    {
        var source = Read("App.xaml.cs");

        Assert.Contains("AppServices.DatabaseFallbackWarning", source);
        var idx = source.IndexOf("AppServices.DatabaseFallbackWarning", StringComparison.Ordinal);
        var after = source[idx..Math.Min(source.Length, idx + 500)];
        // (CEEF-229) ‏الشرطُ «يبلغ المستخدمَ في نافذةٍ حاجزة» لا «يستدعي MessageBox باسمه».
        // ‏وقد صار الطريقُ إلى تلك النافذة `UiDialog` — غلافاً يترجم النصَّ عند الاستدعاء،
        // ‏لأنّ MessageBox نافذةُ Win32 لا يبلغها مترجِمُ الواجهة. فلو بقي الشرطُ على الاسم
        // ‏القديم لمنع الإصلاحَ باسم الحراسة.
        Assert.True(
            after.Contains("UiDialog.Show", StringComparison.Ordinal)
            || after.Contains("MessageBox.Show", StringComparison.Ordinal),
            "تحذيرُ الإقلاع لا يُعرض في نافذةٍ حاجزة — فقد يمضي المستخدمُ على قاعدةٍ غير التي يظنّ.");
    }
}
