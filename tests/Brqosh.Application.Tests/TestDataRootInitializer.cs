using System.Runtime.CompilerServices;

// اختبارات التطبيق تتشارك حالة ساكنة عامة (SecurityContext، AppEnvironmentConfig، مسارات الملفات)
// فالتوازي بينها غير آمن — نفس سابقة Brqosh.Api.Tests. (CEEF-202)
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Brqosh.Application.Tests;

/// <summary>
/// (CEEF-202) يوجّه تخزين ملفات ZATCA إلى مجلد مؤقت خاص بالتشغيل.
///
/// كانت الاختبارات تكتب في %APPDATA%\BRQOSH\zatca الحقيقي للمستخدم، فتتصادم على نفس المسار
/// (نفس رقم الشركة ورقم الفاتورة في قواعد معزولة) وتلوّث بياناته الفعلية.
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
