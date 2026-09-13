using System.IO;
using System.Text.Json;
using Brqosh.Application.Setup;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Application.Tests.Setup;

/// <summary>
/// ‏(م-٥ · ن-٤٩ · ن-٥٠) <b>عقدُ ملفّ الإعداد — رقمٌ على القرص، وملفٌّ لا يُفهَم لا يُعامَل
/// أوّلَ تشغيل.</b>
///
/// <para><b>ن-٤٩:</b> <c>BusinessProfile</c> كان بلا قيمةٍ صريحةٍ واحدة، و<c>setup.json</c>
/// يخزّن الترتيب. <b>فإدراجُ عضوٍ في المنتصف يُعيد تفسيرَ كلّ تركيبٍ قائم</b> — بينما
/// القاعدةُ تخزّن الاسمَ نصّاً فتنجو (<c>BrqoshDbContext.cs:160</c>).</para>
///
/// <para><b>ن-٥٠:</b> <c>catch (JsonException) → null</c> يجعل الملفَّ التالفَ «أوّلَ تشغيل»،
/// فيُعرض المعالجُ فوق دفاترَ حيّة. <b>والقاعدةُ تنجو</b> (المُهيّئُ يعود عند أوّل منشأة)،
/// <b>لكنّ هويّةَ الترخيص تُفقد وحارسَ الهويّة يُطفأ</b>.</para>
///
/// <para><b>والقيدُ الحاكمُ على الإصلاح مقيسٌ لا مُفترَض:</b> كتابةُ نظام العمل <b>اسماً</b>
/// تجعل الملفَّ غيرَ مقروءٍ لكلّ نسخةٍ شُحنت — <c>JsonException</c> ثمّ «أوّلُ تشغيل». <b>فالرقمُ
/// يبقى على السلك</b>، ويُثبَّت هنا رقماً رقماً.</para>
/// </summary>
public sealed class SetupStateSurvivesAnUpgradeTests : IDisposable
{
    private readonly string _folder;
    private readonly string _path;

    public SetupStateSurvivesAnUpgradeTests()
    {
        // ‏مجلّدٌ خاصٌّ بهذا الاختبار — **لا يُكتب في جذر بيانات الجهاز ولا في %APPDATA%**.
        _folder = Path.Combine(Path.GetTempPath(), "brqosh-n49-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "setup.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
    }

    // ── ١: الأرقامُ مثبَّتةٌ لأنّها تُكتب على القرص ─────────────────────────

    /// <summary>
    /// <b>كلُّ رقمٍ يُثبَّت.</b> فإدراجُ عضوٍ في المنتصف <b>يُحمِّر هذا الاختبار</b> بدل أن
    /// يُعيد تفسيرَ ملفّاتِ العملاء صامتاً.
    /// </summary>
    [Theory]
    [InlineData(BusinessProfile.General, 0)]
    [InlineData(BusinessProfile.Construction, 1)]
    [InlineData(BusinessProfile.Manufacturing, 2)]
    [InlineData(BusinessProfile.Retail, 3)]
    [InlineData(BusinessProfile.Healthcare, 4)]
    [InlineData(BusinessProfile.Logistics, 5)]
    [InlineData(BusinessProfile.RealEstate, 6)]
    [InlineData(BusinessProfile.Hospitality, 7)]
    public void EveryProfile_KeepsTheNumberItWasWrittenWith(BusinessProfile profile, int onDisk) =>
        Assert.Equal(onDisk, (int)profile);

    /// <summary>‏وعضوٌ جديدٌ يأخذ رقماً جديداً — فالعددُ جزءٌ من العقد.</summary>
    [Fact]
    public void TheProfileSet_IsExactlyEight() =>
        Assert.Equal(8, Enum.GetValues<BusinessProfile>().Length);

    // ── ٢: الشكلُ الحيُّ على القرص يُقرأ كما هو ─────────────────────────────

    /// <summary>
    /// <b>الشكلُ الحيُّ اليوم</b> — رقمٌ، بلا وسمِ صيغة، وبلا <c>LicensedCompanyName</c>
    /// (وهو غائبٌ فعلاً من ملفّ جهاز التطوير). <b>وأيُّ إصلاحٍ لا يقرأ هذا ليس إصلاحاً.</b>
    /// </summary>
    [Fact]
    public void TheShapeOnDiskToday_IsStillUnderstood()
    {
        File.WriteAllText(_path, """
            {
              "IsCompleted": true,
              "Profile": 2,
              "CompanyName": "المعالم العصرية",
              "VatRegistrationNumber": "310123456700003",
              "CompletedAtUtc": "2026-09-01T10:00:00Z",
              "CountryTemplateId": "saudi-arabia"
            }
            """);

        var state = new JsonSetupStateStore(_path).Load();

        Assert.NotNull(state);
        Assert.True(state!.IsCompleted);
        Assert.Equal(BusinessProfile.Manufacturing, state.Profile);
        Assert.Equal("المعالم العصرية", state.CompanyName);
        Assert.Null(state.LicensedCompanyName);
        Assert.Equal(1, state.SchemaVersion);   // ‏الملفُّ غيرُ الموسوم هو الصيغةُ ١ شكلاً
    }

    /// <summary>
    /// <b>وما يُكتب يبقى مقروءاً للنسخ المشحونة.</b> فلو كُتب نظامُ العمل اسماً لصار الملفُّ
    /// عند أوّل تراجعٍ عن إصدارٍ «أوّلَ تشغيل» — <b>أي أنّ العلاجَ يصنع العطلَ الذي يُغلقه</b>.
    /// </summary>
    [Fact]
    public void WhatWeWrite_KeepsTheProfileAsANumber()
    {
        new JsonSetupStateStore(_path).Save(new SetupState(
            IsCompleted: true, Profile: BusinessProfile.Hospitality,
            CompanyName: "فندق", VatRegistrationNumber: "310000000000003",
            CompletedAtUtc: DateTime.UtcNow));

        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        var profile = doc.RootElement.GetProperty("Profile");

        Assert.Equal(JsonValueKind.Number, profile.ValueKind);
        Assert.Equal(7, profile.GetInt32());
    }

    /// <summary>‏ورحلةُ ذهابٍ وإياب تحفظ هويّةَ الترخيص — وهي ما كان المعالجُ يُسقطه.</summary>
    [Fact]
    public void ARoundTrip_KeepsTheLicensedName()
    {
        var store = new JsonSetupStateStore(_path);
        store.Save(new SetupState(true, BusinessProfile.Retail, "متجر", "310000000000003",
            DateTime.UtcNow, "saudi-arabia", LicensedCompanyName: "متجرٌ باسمِ الترخيص"));

        Assert.Equal("متجرٌ باسمِ الترخيص", store.Load()!.LicensedCompanyName);
    }

    // ── ٣: ما لا يُفهَم يُرفَض باسمِه ───────────────────────────────────────

    /// <summary>
    /// <b>وثلاثةُ مداخلَ كانت تمرّ، لا واحد.</b> فالالتقاطُ كان على <c>JsonException</c>
    /// وحدَه، <b>و«null» نصّاً لا يرمي</b>، <b>ورقمٌ خارج التعداد لا يرمي</b> — بل يموت
    /// بعيداً عن سببه في بناء القائمة.
    /// </summary>
    [Theory]
    [InlineData("{ \"IsCompleted\": true, \"Profile\": 2", "مقطوع")]
    [InlineData("null", "null نصّاً")]
    [InlineData("<html>خطأ</html>", "ليس JSON")]
    [InlineData("{ \"IsCompleted\": true, \"Profile\": 99 }", "رقمٌ خارج التعداد")]
    [InlineData("{ \"IsCompleted\": true, \"Profile\": -1 }", "رقمٌ سالب")]
    [InlineData("{ \"IsCompleted\": true, \"Profile\": 2, \"SchemaVersion\": 9 }", "صيغةٌ أحدث")]
    public void AFileThatCannotBeUnderstood_IsRefusedByName(string content, string why)
    {
        File.WriteAllText(_path, content);

        var ex = Assert.Throws<InvalidOperationException>(() => new JsonSetupStateStore(_path).Load());

        Assert.Contains(_path, ex.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(why));
    }

    /// <summary>‏شاهدُ ضبطٍ موجب: غيابُ الملفّ يبقى أوّلَ تشغيل — فالرفضُ ليس شاملاً.</summary>
    [Fact]
    public void AnAbsentFile_IsStillAFirstRun() =>
        Assert.Null(new JsonSetupStateStore(_path).Load());

    // ── ٤: الكتابةُ ذرّيّة ──────────────────────────────────────────────────

    /// <summary>
    /// <b>تُكتب جانباً ثمّ تُنقل.</b> وانقطاعٌ في منتصف كتابةٍ مباشرةٍ يُنتج الملفَّ المقطوعَ
    /// الذي كان يصير «أوّلَ تشغيل» — <b>فالعطلان يتغذّيان</b>.
    /// </summary>
    [Fact]
    public void SaveLeavesNoTemporaryRemnant_AndTheFileParses()
    {
        new JsonSetupStateStore(_path).Save(new SetupState(
            true, BusinessProfile.General, "منشأة", "310000000000003", DateTime.UtcNow));

        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
        Assert.NotNull(new JsonSetupStateStore(_path).Load());
    }

    // ── ٥: اختبارٌ ذاتيّ — أكانت الخوارزميّةُ القديمةُ تبتلع هذا فعلاً؟ ─────

    /// <summary>
    /// <b>وحارسٌ يقرأ ملفّاً صحيحاً لا يُثبت أنّه يرفض ملفّاً خاطئاً.</b> فتُعاد هنا قراءةُ
    /// ما قبل الإصلاح حرفيّاً، ويُطالَب كلُّ مُدخَلٍ مرفوضٍ اليومَ بأن <b>يمرّ عليها</b> —
    /// إمّا عدماً يصير «أوّلَ تشغيل»، أو قيمةً حيّةً خاطئة.
    /// </summary>
    [Theory]
    [InlineData("{ \"IsCompleted\": true, \"Profile\": 2", true)]     // ‏مقطوع ⟵ null
    [InlineData("null", true)]                                        // ‏«null» ⟵ null
    [InlineData("<html>خطأ</html>", true)]                             // ‏ليس JSON ⟵ null
    [InlineData("{ \"IsCompleted\": true, \"Profile\": 99 }", false)]  // ‏يُقبل بقيمةٍ حيّةٍ خاطئة
    public void ThePreFixReader_SwallowedEveryOneOfThese(string content, bool becameNull)
    {
        File.WriteAllText(_path, content);

        var state = PreFixLoad(_path);

        Assert.Equal(becameNull, state is null);
        if (!becameNull)
            Assert.False(Enum.IsDefined(state!.Profile),
                "‏الشاهدُ أجوف — القيمةُ داخلُ التعداد، فلا عطلَ فيها.");
    }

    /// <summary>‏نسخةُ <c>Load</c> كما كانت عند <c>746d9ee</c> — سطراً بسطر.</summary>
    private static SetupState? PreFixLoad(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<SetupState>(File.ReadAllText(path)); }
        catch (JsonException) { return null; }
    }

    // ── ٦: تثبيتان بنيويّان — فالسلوكُ وحدَه لا يرى غيابَ رقمٍ صريح ────────

    /// <summary>
    /// <b>كلُّ عضوٍ يحمل رقمَه مكتوباً.</b> واختبارُ السلوك <b>لا يرى الفرق</b>: التعدادُ
    /// بلا أرقامٍ صريحةٍ يُعطي القيمَ نفسَها اليوم — <b>والفرقُ يظهر يومَ يُدرَج عضو</b>،
    /// أي بعد أن تُكتب ملفّاتُ العملاء. فيُقاس الإعلانُ لا الناتج.
    /// </summary>
    [Fact]
    public void EveryProfileMember_DeclaresItsNumberInSource()
    {
        var body = EnumBody(ReadSource("Brqosh.Domain", "Modules", "BusinessProfile.cs"), "BusinessProfile");

        var members = MemberDeclarations(body);
        Assert.Equal(8, members.Count);

        var bare = members.Where(m => !m.Contains('=', StringComparison.Ordinal)).ToList();
        Assert.True(bare.Count == 0,
            "‏أعضاءٌ بلا رقمٍ صريح — وترتيبُها يُكتب في `setup.json`، فإدراجُ عضوٍ يُعيد "
            + "‏تفسيرَ كلّ تركيبٍ قائم:\n  " + string.Join("\n  ", bare));
    }

    /// <summary>
    /// <b>وملفٌّ لا يُفهَم يُرفَض لا يُبتلع.</b> فالقراءةُ يجب أن تحمل رميةً — وغيابُها هو
    /// العطلُ نفسُه: «نعامل الحالة كأول تشغيل».
    /// </summary>
    [Fact]
    public void TheReader_RefusesRatherThanReturningNull()
    {
        var body = MethodBody(
            Blank(ReadSource("Brqosh.Infrastructure", "Setup", "JsonSetupStateStore.cs")),
            "public SetupState? Load()");

        Assert.Contains("throw", body, StringComparison.Ordinal);

        // ‏شاهدُ ضبطٍ موجب: الغيابُ ما زال يُعيد عدماً — فالرفضُ ليس شاملاً.
        Assert.Contains("return null", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>واختبارٌ ذاتيٌّ للمُحلِّلَين</b> — فحارسٌ يقرأ إعلاناً صحيحاً لا يُثبت أنّه يرفض
    /// إعلاناً ناقصاً، <b>وتعليقٌ ليس إعلاناً</b>.
    /// </summary>
    [Theory]
    [InlineData("    General = 0,\n    Retail = 3,", 2, 0)]
    [InlineData("    General,\n    Retail,", 2, 2)]
    [InlineData("    General = 0,\n    Retail,", 2, 1)]
    [InlineData("    /// <summary>تعليق</summary>\n    General = 0,", 1, 0)]
    [InlineData("    // General,\n    General = 0,", 1, 0)]
    public void TheMemberScanner_Discriminates(string body, int members, int bare)
    {
        var found = MemberDeclarations(Blank(body));

        Assert.Equal(members, found.Count);
        Assert.Equal(bare, found.Count(m => !m.Contains('=', StringComparison.Ordinal)));
    }

    // ── الأدوات ─────────────────────────────────────────────────────────────

    /// <summary>‏أعضاءُ التعداد — <b>سطرٌ يبدأ بحرفٍ كبيرٍ وينتهي بفاصلة</b>، بعد المحو.</summary>
    private static List<string> MemberDeclarations(string cleanedBody) =>
        cleanedBody.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 1 && char.IsUpper(l[0]) && l.EndsWith(',') && !l.StartsWith("//"))
            .ToList();

    private static string EnumBody(string source, string name)
    {
        var cleaned = Blank(source);
        var at = cleaned.IndexOf($"enum {name}", StringComparison.Ordinal);
        Assert.True(at >= 0, $"‏لم يُعثر على «enum {name}» — تغيّر الاسمُ فبطل القياس.");
        return BlockAt(cleaned, at);
    }

    private static string MethodBody(string cleaned, string signature)
    {
        var at = cleaned.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"‏لم يُعثر على «{signature}» — تغيّر التوقيعُ فبطل القياس.");
        return BlockAt(cleaned, at);
    }

    private static string BlockAt(string cleaned, int at)
    {
        var open = cleaned.IndexOf('{', at);
        Assert.True(open >= 0, "‏كتلةٌ بلا جسم — القياسُ أجوف.");

        var depth = 0;
        for (var k = open; k < cleaned.Length; k++)
        {
            if (cleaned[k] == '{') depth++;
            else if (cleaned[k] == '}' && --depth == 0) return cleaned[open..k];
        }

        throw new InvalidOperationException("‏كتلةٌ غيرُ مُغلقة — المصدرُ لا يُحلَّل.");
    }

    /// <summary>يمسح التعليقاتِ والنصوصَ الحرفيّةَ مُبقياً الأسطر — فتعليقٌ ليس إعلاناً.</summary>
    private static string Blank(string source)
    {
        var b = new System.Text.StringBuilder(source);

        void Wipe(int from, int to)
        {
            for (var k = from; k < to && k < source.Length; k++)
                if (source[k] != '\n' && source[k] != '\r') b[k] = ' ';
        }

        for (var i = 0; i < source.Length; i++)
        {
            var next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (source[i] == '/' && next == '/')
            {
                var end = source.IndexOf('\n', i);
                if (end < 0) end = source.Length;
                Wipe(i, end);
                i = end;
            }
            else if (source[i] == '/' && next == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Wipe(i, end);
                i = end - 1;
            }
            else if (source[i] is '"' or '\'')
            {
                var quote = source[i];
                var verbatim = quote == '"' && i > 0 && source[i - 1] == '@';
                var j = i + 1;

                while (j < source.Length)
                {
                    if (!verbatim && source[j] == '\\') { j += 2; continue; }
                    if (source[j] != quote) { j++; continue; }
                    if (verbatim && j + 1 < source.Length && source[j + 1] == quote) { j += 2; continue; }
                    break;
                }

                Wipe(i, Math.Min(j + 1, source.Length));
                i = j;
            }
        }

        return b.ToString();
    }

    private static string ReadSource(params string[] parts)
    {
        var path = Path.Combine(new[] { FindRepoRoot(), "src" }.Concat(parts).ToArray());
        Assert.True(File.Exists(path), $"‏لم يُعثر على {path} — الحارسُ أعمى، ولا يمرّ أعمى.");
        return File.ReadAllText(path);
    }

    /// <summary>‏رميةٌ لا تخطٍّ صامت: حارسٌ لا يجد ما يحرسه يجب أن يسقط، لا أن يمرَّ أخضر.</summary>
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
