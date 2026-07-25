using System.Runtime.CompilerServices;

namespace Brqosh.Api.Tests;

/// <summary>
/// (CEEF-203) يوجّه جذر بيانات المستخدم إلى مجلد مؤقت خاص بالتشغيل.
///
/// بُرهن تجريبياً أن تشغيلة واحدة لهذه الاختبارات كانت تضيف شركات إلى قاعدة المستخدم
/// الحقيقية: Brqosh.Api يقرأ ConnectionStrings:Brqosh عند إنشاء الـbuilder — قبل أن يدمج
/// مصنع الاختبار مساره — فيسقط إلى القاعدة الافتراضية في %APPDATA%\BRQOSH.
/// توجيه الجذر يجعل هذا السقوط يقع في مجلد مؤقت لا في بيانات المستخدم.
/// </summary>
internal static class TestDataRootInitializer
{
    [ModuleInitializer]
    internal static void RedirectDataRoot()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BRQOSH_DATA_ROOT")))
            return;

        var root = Path.Combine(Path.GetTempPath(), "brqosh-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("BRQOSH_DATA_ROOT", root);
    }
}
