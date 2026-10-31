using System.IO;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (لا نافذةَ اتصالٍ لمن قاعدتُه مدمجة) قاعدةٌ مدمجةٌ معدّةٌ وخادمُها المشحونُ غائبٌ بجوار البرنامج — كتشغيل نسخةِ البناء — لا يُصلحها نموذجُ
/// «الخادم والمنفذ وكلمة المرور»؛ فيُقال السببُ ويُغلق البرنامجُ. وهذا الاختبارُ يحرس القرارَ وأنّ الإقلاعَ يستعمله قبل النافذة.
/// </summary>
public class EmbeddedBundleMissingTests
{
    [Theory]
    [InlineData(true, false, true)]    // معدّةٌ وخادمُها غائب ← يُفسَّر ويُغلق
    [InlineData(true, true, false)]    // معدّةٌ وخادمُها حاضر ← يُشغَّل صامتاً
    [InlineData(false, false, false)]  // غيرُ معدّة (خادمٌ خارجيّ) ← النافذةُ للإصلاح كما كانت
    [InlineData(false, true, false)]   // غيرُ معدّةٍ والخادمُ مشحون ← المعالجُ يُعدّها
    public void TheDecision_OnlyFiresWhenItIsProvisionedAndTheBundleIsAbsent(bool provisioned, bool bundle, bool expected) =>
        Assert.Equal(expected, EmbeddedPostgres.NeedsBundleButMissingFor(provisioned, bundle));

    [Fact]
    public void TheStartup_ChecksTheDecision_BeforeItOpensTheConnectionWindow()
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "App.xaml.cs"));
        var guard = source.IndexOf("EmbeddedPostgres.NeedsBundleButMissing", StringComparison.Ordinal);
        var window = source.IndexOf("new DatabaseConnectionWindow(", StringComparison.Ordinal);
        Assert.True(guard > 0, "الإقلاعُ لا يستشير NeedsBundleButMissing.");
        Assert.True(guard < window, "الفحصُ يجب أن يسبق بناءَ نافذة الاتصال.");
    }

    [Fact]
    public void TheStartup_NeverOpensTheConnectionWindow_WhenAProvisionedEmbeddedServerFailsToStart()
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "App.xaml.cs"));
        var flag = source.IndexOf("embeddedStartFailed = Brqosh.Infrastructure.Persistence.EmbeddedPostgres.IsProvisioned", StringComparison.Ordinal);
        var guard = source.IndexOf("if (embeddedStartFailed)", StringComparison.Ordinal);
        var window = source.IndexOf("new DatabaseConnectionWindow(", StringComparison.Ordinal);
        Assert.True(flag > 0, "الإقلاعُ لا يسجّل إخفاقَ تشغيل القاعدة المدمجة.");
        Assert.True(guard > flag && guard < window, "فحصُ الإخفاق يجب أن يسبق بناءَ نافذة الاتصال.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
