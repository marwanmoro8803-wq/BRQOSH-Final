using Brqosh.Domain.Platform;
using Brqosh.Infrastructure;
using Brqosh.Infrastructure.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (م-٣ · ن-٠٩) <b>أحداثُ النطاق والصندوقُ الصادر والـwebhooks كانت لا تعمل — ولا يشتكي شيء.</b>
///
/// <para><b>العطل:</b> <c>DomainEventHub.Current</c> و<c>OutboxDispatchHook.Wire</c> كانا
/// يُضبطان <b>داخل مصنعِ الحقن</b> لـ<c>IDomainEventPublisher</c>.
/// و<c>AddSingleton&lt;T&gt;(factory)</c> <b>كسول</b>: لا يعمل حتى يُطلب النوع.
/// <b>ولم يطلبه أحدٌ في الإنتاج</b> — الطالبُ الوحيد في المستودع ملفُّ اختبار. فبقي
/// <c>DomainEventHub.Current</c> على <c>InMemoryDomainEventPublisher</c>، <b>ولم يصل
/// حدثٌ واحد</b>.</para>
///
/// <para><b>وأصدقُ تجسيدٍ للعطل كان اسمَ دالّة:</b> <c>AppServices.WireDomainEventPersistence()</c>
/// — تُستدعى من الإقلاع، وتعليقُها يقول «يربط <c>DomainEventHub</c> بناشرٍ يحفظ الأحداث» —
/// <b>وجسمُها يبني الحاويةَ ولا يطلب الناشرَ قطّ</b>. <b>دالّةٌ اسمُها «وصل» ولا تصل</b>،
/// واسمُها وتعليقُها صادقان عن النيّة فلا يشكّ من يقرأ.</para>
///
/// <para><b>ولمَ إثباتُ أكوادٍ ثابتٍ لا اختبارُ سلوكٍ وحدَه:</b> لأنّ اختبارَ السلوك
/// <b>يُنشئ الحاويةَ ويصلها بنفسه</b> — فيقيس أنّ الوصلَ <i>يعمل</i>، <b>لا أنّ أحداً
/// يستدعيه</b>. وذلك بعينه ما مرّ سنةً: <c>OrchestratorEventWiringEvidenceTests</c> يُثبت
/// أنّ تسعَ عشرةَ خدمةً <b>تنشر</b> الأحداث، ويمرّ أخضر — <b>ولا شيءَ يُثبت أنّ أحداً
/// يسمعها</b>.</para>
/// </summary>
public sealed class DomainEventWiringReachesProductionTests : IDisposable
{
    private readonly IDomainEventPublisher _publisherBefore = DomainEventHub.Current;

    public void Dispose()
    {
        DomainEventHub.Current = _publisherBefore;
        GC.SuppressFinalize(this);
    }

    // ── الوصلُ يعمل ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>بعد النداء الصريح</b> يحمل المحورُ ناشراً يحفظ، لا الناشرَ الذاكريَّ الافتراضيّ.
    /// </summary>
    [Fact]
    public void AfterTheExplicitCall_TheHubHoldsThePersistingPublisher()
    {
        using var provider = BuildInfrastructure();

        provider.WireDomainEventPublishing();

        Assert.IsType<PersistingDomainEventPublisher>(DomainEventHub.Current);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو لبُّ البند:</b> بناءُ الحاوية وحدَه <b>لا يصل شيئاً</b>.
    ///
    /// <para>فلو وصل، لكان العطلُ غيرَ موجودٍ أصلاً، ولكان النداءُ الصريحُ زينة. وهذا
    /// التوكيدُ هو ما يُثبّت أنّ الكسلَ حقيقيّ.</para>
    /// </summary>
    [Fact]
    public void BuildingTheContainerAlone_WiresNothing()
    {
        DomainEventHub.Current = new InMemoryDomainEventPublisher();

        using var provider = BuildInfrastructure();

        Assert.IsType<InMemoryDomainEventPublisher>(DomainEventHub.Current);
    }

    // ── الوصلُ يصل ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>الحارسُ الحقيقيّ: لكلّ مُضيفٍ مستدعٍ إنتاجيّ.</b>
    ///
    /// <para>واختبارُ السلوك أعلاه يصل الحاويةَ بيده، <b>فلا يرى أنّ الإنتاج لا يصلها</b>.
    /// وهذا هو النمطُ الجامعُ لهذا التدقيق كلِّه — «مبنيٌّ ولا يصل» — <b>ولا يُكشف إلّا
    /// باشتراطِ مستدعٍ خارجَ الاختبارات</b>.</para>
    /// </summary>
    [Theory]
    [InlineData("Brqosh.Api", "Program.cs")]
    [InlineData("Brqosh.Desktop", "AppServices.cs")]
    public void EachHost_CallsTheWiringStep(string project, string file)
    {
        var source = ReadSource(project, file);

        Assert.Contains("WireDomainEventPublishing()", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والمصنعُ لا يعود يصل شيئاً</b> — فأثرٌ جانبيٌّ في مصنعٍ كسولٍ هو الفخُّ نفسُه:
    /// يعمل متى طُلب، ولا يعمل متى لم يُطلب، <b>ولا شيءَ في التوقيع يقول ذلك</b>.
    /// </summary>
    [Fact]
    public void TheDependencyFactory_CarriesNoWiringSideEffect()
    {
        var source = ReadSource("Brqosh.Infrastructure", "InfrastructureServiceCollectionExtensions.cs");

        var code = string.Join('\n', source
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(l => !l.TrimStart().StartsWith("///", StringComparison.Ordinal)));

        var factoryStart = code.IndexOf("services.AddSingleton<IDomainEventPublisher>", StringComparison.Ordinal);
        Assert.True(factoryStart >= 0, "‏لم يُعثر على تسجيل الناشر — تغيّر الملفُّ فليُراجَع الحارس.");

        var wiringStart = code.IndexOf("WireDomainEventPublishing", StringComparison.Ordinal);
        Assert.True(wiringStart > factoryStart,
            "‏دالّةُ الوصل يجب أن تكون خارجَ المصنع وبعدَه — فأثرٌ جانبيٌّ فيه لا يعمل حتى يُطلب.");

        var factoryBody = code[factoryStart..wiringStart];
        Assert.DoesNotContain("DomainEventHub.Current =", factoryBody, StringComparison.Ordinal);
        Assert.DoesNotContain("OutboxDispatchHook.Wire", factoryBody, StringComparison.Ordinal);
    }

    // ── مساعدات ──────────────────────────────────────────────────────────────

    private static ServiceProvider BuildInfrastructure()
    {
        var services = new ServiceCollection();
        services.AddBrqoshInfrastructure(
            $"Data Source={Path.Combine(Path.GetTempPath(), $"brqosh-wire-{Guid.NewGuid():N}.db")}",
            usePostgres: false);
        return services.BuildServiceProvider();
    }

    private static string ReadSource(string project, string file)
    {
        var path = Path.Combine(FindRepoRoot(), "src", project, file);
        Assert.True(File.Exists(path), $"‏ملفُّ المصدر غيرُ موجود: {path}");
        return File.ReadAllText(path);
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
