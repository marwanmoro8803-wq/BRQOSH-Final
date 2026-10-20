using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (م-٥ · مسحُ الوصل) <b>لكلّ مُضيفٍ مستدعٍ إنتاجيّ — لا أنّ الوصلَ يعمل.</b>
///
/// <para><b>ما تطلبه م-٥ حرفيّاً</b>، وبذرتُه <c>DomainEventWiringReachesProductionTests</c>:
/// «اشتراطُ <b>مستدعٍ إنتاجيٍّ لكلّ مُضيف</b>، لا مجرّدِ أنّ الوصل يعمل. فاختبارُ السلوك
/// يُنشئ الحاويةَ ويصلها بيده — <b>فيقيس أنّ الوصلَ يعمل، لا أنّ أحداً يستدعيه</b>، وذلك
/// بعينه ما مرّ سنةً».</para>
///
/// <para><b>وصنفُ العطل مسمًّى:</b> مُضيفٌ ساكنٌ <b>بقيمةٍ افتراضيّةٍ صامتة</b>. فـ
/// <c>DomainEventHub</c> يبدأ بناشرٍ ذاكريّ و<c>OutboxDispatchHook</c> بمعالجٍ معدوم —
/// <b>فإن لم يصلْهما أحدٌ بدا كلُّ شيءٍ سليماً</b> ولم يصل حدثٌ واحد. وأصدقُ تجسيدٍ لذلك
/// كان اسمَ دالّة: <c>WireDomainEventPersistence</c> — اسمُها «وصل» وجسمُها لا يصل.</para>
///
/// <para><b>وطبقتان لأنّ واحدةً لا تكفي:</b> الأولى تُثبت أنّ لكلّ مُضيفٍ <b>مُعلَنٍ</b>
/// مستدعياً في <c>src/</c>؛ والثانية تُثبت أنّ <b>قائمةَ المُعلَنين هي كلُّ من في المستودع</b> —
/// <b>وبلا الثانية يكفي أن يُنسى مُضيفٌ جديدٌ فتمرّ البوّابةُ خضراءَ وهي لا تراه</b>،
/// وذلك بعينه «الخضرةُ بالغياب» التي عُضّ بها هذا المستودعُ من قبل.</para>
/// </summary>
public class EveryHostHasAProductionCallerTests
{
    /// <summary>
    /// المُضيفون المُعلَنون — <b>ولكلٍّ سببُ إدراجه</b>. وإضافةُ سطرٍ هنا اعترافٌ بأنّ
    /// حالةً ساكنةً جديدةً تحتاج وصلاً، لا تهرّبٌ من فشل.
    /// </summary>
    private static readonly (string File, string Mutator, string Why)[] Hosts =
    [
        ("src/Brqosh.Infrastructure/Platform/DomainEventHub.cs", "Current",
         "‏ناشرُ أحداث النطاق — افتراضُه ذاكريٌّ صامت، فبلا وصلٍ لا يصل حدثٌ واحد."),
        ("src/Brqosh.Infrastructure/Persistence/OutboxDispatchHook.cs", "Wire",
         "‏مُفرِّغُ الصندوق الصادر — افتراضُه معدوم، فبلا وصلٍ يتراكم الطابورُ صامتاً."),
        ("src/Brqosh.Desktop/Ui/UiLocalizationHost.cs", "Register",
         "‏مُضيفُ التعريب — بلا تسجيلٍ تبقى الواجهةُ على لغة الإقلاع."),
        ("src/Brqosh.Desktop/UiFeedback.cs", "AttachToastHost",
         "‏مُضيفُ التنبيهات — بلا وصلٍ تختفي رسائلُ النجاح والتحذير بلا أثر."),
        // ‏(PR #8 · fix/ui-layout-audit) ‏مُسجِّلُ ملاءمة النوافذ — حالتُه علَمُ تسجيلٍ لمعالجٍ عامٍّ على كلّ نافذة.
        ("src/Brqosh.Desktop/Ui/WindowFit.cs", "Register",
         "‏ملاءمةُ النوافذ لمساحة الشاشة — بلا تسجيلٍ تتجاوز النوافذُ الشاشةَ وتُخفي أزرارَ الحفظ والإلغاء."),
    ];

    private static string RepoRoot()
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

    private static IEnumerable<string> ProductionFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    /// <summary>
    /// مستدعٍ إنتاجيٌّ = ذكرٌ للاسم في <c>src/</c> <b>ليس هو سطرَ الإعلان</b>.
    /// <b>والاستدعاءُ داخل الملفّ نفسِه يُحتسب</b> — وهو الشكلُ الحقيقيّ في
    /// <c>AppServices</c>، وبحثٌ يشترط الاسمَ مؤهّلاً كان <b>يُخفي أربعةَ مستدعين</b>.
    /// </summary>
    private static List<string> CallersOf(string mutator, string declaringFileSuffix)
    {
        var declaration = new Regex(
            $@"(public|internal)\s+static\s+[^\r\n=;]*\b{Regex.Escape(mutator)}\b");

        var callers = new List<string>();

        foreach (var file in ProductionFiles())
        {
            var relative = Path.GetRelativePath(RepoRoot(), file).Replace('\\', '/');

            foreach (var (line, number) in File.ReadAllLines(file).Select((l, i) => (l, i + 1)))
            {
                var text = StripComment(line);
                if (!Regex.IsMatch(text, $@"\b{Regex.Escape(mutator)}\b")) continue;
                if (relative.EndsWith(declaringFileSuffix, StringComparison.Ordinal)
                    && declaration.IsMatch(text)) continue;

                callers.Add($"{relative}:{number}");
            }
        }

        return callers;
    }

    private static string StripComment(string line)
    {
        var marker = line.IndexOf("//", StringComparison.Ordinal);
        return marker < 0 ? line : line[..marker];
    }

    // ───────────────────── الطبقةُ الأولى: لكلّ مُعلَنٍ مستدعٍ ─────────────────────

    public static TheoryData<string, string, string> DeclaredHosts()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (file, mutator, why) in Hosts) data.Add(file, mutator, why);
        return data;
    }

    [Theory]
    [MemberData(nameof(DeclaredHosts))]
    public void EveryDeclaredHost_IsWiredFromProduction(string file, string mutator, string why)
    {
        var path = Path.Combine(RepoRoot(), file.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"‏مُضيفٌ مُعلَنٌ غيرُ موجود: {file}");

        var callers = CallersOf(mutator, file[(file.LastIndexOf('/') + 1)..]);

        Assert.True(callers.Count > 0,
            $"‏لا مستدعيَ إنتاجيّاً لـ«{mutator}» في {file}.\n  {why}\n"
            + "‏**واختبارُ سلوكٍ لا يُغني**: يُنشئ الحاويةَ ويصلها بيده، فيقيس أنّ الوصلَ يعمل "
            + "لا أنّ أحداً يستدعيه — وذلك بعينه ما مرّ سنةً في ن-٠٩.");
    }

    // ───────────────── الطبقةُ الثانية: والقائمةُ هي كلُّ من في المستودع ─────────────────

    /// <summary>
    /// <b>يكتشف المُضيفين بالشكل لا بالقائمة</b>: صفٌّ ساكنٌ فيه حقلٌ ساكنٌ خاصّ
    /// <b>وموصِّلٌ عامّ</b>. وبلا هذه الطبقة يكفي أن يُنسى مُضيفٌ جديدٌ فتمرّ البوّابةُ
    /// <b>خضراءَ وهي لا تراه</b>.
    /// </summary>
    [Fact]
    public void TheDeclaredList_IsEveryHostInTheRepository()
    {
        var discovered = new List<string>();

        foreach (var file in ProductionFiles())
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("static class", StringComparison.Ordinal)) continue;

            var hasPrivateState = Regex.IsMatch(text, @"(?m)^\s*private static [^(\r\n]*_[a-zA-Z]");
            var hasWiring = Regex.IsMatch(text,
                @"(?m)^\s*public static (void )?(Wire|Attach|Register)[A-Za-z]*\s*\(");
            var hasSettableCurrent = Regex.IsMatch(text, @"(?m)^\s*public static [A-Za-z<>?\[\]]+ Current");

            if (hasPrivateState && (hasWiring || hasSettableCurrent))
                discovered.Add(Path.GetRelativePath(RepoRoot(), file).Replace('\\', '/'));
        }

        // ‏شاهدُ ضبط: الكاشفُ يرى المُضيفَين المعروفَين — فالنتيجةُ عن مسحٍ لا عن عمى.
        Assert.Contains("src/Brqosh.Infrastructure/Platform/DomainEventHub.cs", discovered);
        Assert.Contains("src/Brqosh.Infrastructure/Persistence/OutboxDispatchHook.cs", discovered);

        var declared = Hosts.Select(h => h.File).ToHashSet(StringComparer.Ordinal);

        // ‏**والمعفَونَ يُسمَّون بسببهم** — لا يُحذفون من الكشف حتّى يبقى القرارُ مرئيّاً.
        var exempt = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/Brqosh.Application/Setup/AppEnvironment.cs"] =
                "‏ليس مُضيفاً: `_override` منفذُ اختبارٍ صريح، والقرارُ يُشتقّ من مصدرٍ رسميّ لا يُوصَل.",
            ["src/Brqosh.Desktop/AppServices.cs"] =
                "‏هو **المُوصِّل** نفسُه لا المُضيف — ووصلُه مقيسٌ في `DomainEventWiringReachesProductionTests`.",
            ["src/Brqosh.Infrastructure/Security/WhatsAppNotificationChannelFactory.cs"] =
                "‏ذاكرةٌ كسولةٌ (`Resolve`/`ResetCache`) تبني عند الطلب — لا حالةَ تُوصَل.",
            ["src/Brqosh.Infrastructure/Webhooks/WebhookSubscriptionRegistry.cs"] =
                "‏`Register` عمليّةُ بيانات (تسجيلُ اشتراك) لا وصلُ مُضيف.",
        };

        var unaccounted = discovered
            .Where(d => !declared.Contains(d) && !exempt.ContainsKey(d))
            .ToList();

        Assert.True(unaccounted.Count == 0,
            "‏حالةٌ ساكنةٌ قابلةٌ للوصل ظهرت ولم تُعلَن ولم تُعفَ — **وبلا قرارٍ بشريٍّ فيها "
            + "تمرّ البوّابةُ وهي لا تراها**:\n  " + string.Join("\n  ", unaccounted));
    }

    /// <summary>
    /// <b>الاختبارُ الذاتيُّ: الكاشفُ يشتكي من مُضيفٍ بلا مستدعٍ.</b> بلا هذا تكون
    /// «لكلٍّ مستدعٍ» دالّةً لا تشتكي أبداً.
    /// </summary>
    [Fact]
    public void TheCallerScanner_ReportsAMutatorNobodyCalls()
    {
        var callers = CallersOf("WireSomethingNobodyEverCalls", "DomainEventHub.cs");
        Assert.Empty(callers);

        // ‏وشاهدُ ضبط: التقنيةُ نفسُها تجد مستدعياً معروفاً — فالفراغُ عن بحثٍ يرى.
        Assert.NotEmpty(CallersOf("AttachToastHost", "UiFeedback.cs"));
    }

    /// <summary>
    /// <b>ولا يُحتسب سطرُ الإعلان مستدعياً</b> — وإلّا كان كلُّ مُضيفٍ «موصولاً» بنفسه،
    /// <b>وهي أدقُّ صور الخضرة بالغياب</b>.
    /// </summary>
    [Fact]
    public void TheDeclarationItself_IsNotCountedAsACaller()
    {
        var callers = CallersOf("AttachToastHost", "UiFeedback.cs");

        Assert.DoesNotContain(callers, c => c.StartsWith("src/Brqosh.Desktop/UiFeedback.cs:18", StringComparison.Ordinal));
        Assert.Contains(callers, c => c.StartsWith("src/Brqosh.Desktop/MainWindow.xaml.cs", StringComparison.Ordinal));
    }
}
