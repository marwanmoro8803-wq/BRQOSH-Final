using System.IO;
using System.Text;
using Brqosh.Desktop.Navigation;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Security;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(م-٤ط · ن-١٨) <b>مفتاحٌ منسيٌّ في جدول الأدوار يُطفئ وحدةً كاملة — صامتاً.</b>
///
/// <para><b>ما كان:</b> <c>NavigationCatalog</c> فيه ١٠٦ مفاتيح و<c>ScreenRoles</c> فيه ٧١،
/// <b>وخمسةٌ وثلاثون بلا مدخل</b>. و<see cref="PermissionService.CanAccess"/> يسقط إلى
/// <c>false</c> عند مفتاحٍ مجهول، و<c>BuildNavigation</c> (MainWindow.xaml.cs:557) يُسقط
/// المجموعةَ كلَّها إن خلت <b>بـ<c>continue</c> بلا رسالة</b>. فكانت النتيجةُ لا إخفاءَ
/// خمسٍ وثلاثين شاشةً بل <b>إطفاءَ أربعِ وحداتٍ كاملة</b> لكلِّ من ليس مديراً — والرواتبُ
/// تُعرض ١/٧ في المِلفّات الثمانية جميعاً.</para>
///
/// <para><b>ولمَ لم يَرَه حارسٌ سلوكيّ:</b> تسعةُ مداخلَ مشروعةٍ قيمتُها <c>[Administrator]</c>،
/// فـ<c>CanAccess</c> يُعيد لها <c>false</c> لكلّ دورٍ آخر — <b>تماماً كما يفعل بالمنسيّ</b>.
/// فالمُعلَنُ والمنسيُّ لا يُفرَّقان من خارج الصنف، <b>ولذلك أُضيف
/// <see cref="PermissionService.DecidedKeys"/></b>: القرارُ يُعلَن ليُقاس.</para>
///
/// <para><b>ولا تخطٍّ صامتاً هنا:</b> حارسٌ لا يجد ما يحرسه <b>يسقط</b> — ولكلِّ قراءةٍ
/// شاهدُ ضبطٍ موجب، لأنّ جدولاً فارغاً يجعل كلَّ مفتاحٍ يبدو منسيّاً وملفّاً ضائعاً
/// يجعل كلَّ حارسٍ يبدو مفقوداً، <b>وكلاهما يُقرأ كبراءةٍ إن لم يُرمَ</b>.</para>
/// </summary>
public class ScreenRoleCoverageTests
{
    private static readonly string[] ReactivatingPages =
        ["CustomersPage.xaml.cs", "ProductsPage.xaml.cs", "SuppliersPage.xaml.cs"];

    // ── القرار يُتَّخذ لكلّ مفتاح ───────────────────────────────────────────────

    /// <summary>
    /// <b>كلُّ بندٍ في القائمة له قرارُ دورٍ مكتوب.</b> ومفتاحٌ بلا قرارٍ لا يُخفي بنداً
    /// فحسب: إن خلت مجموعتُه <b>اختفت المجموعةُ كلُّها بلا رسالة</b>.
    /// </summary>
    [Fact]
    public void EveryMenuItem_HasADecidedRoleSet()
    {
        var decided = PermissionService.DecidedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ‏شاهدُ ضبطٍ موجب: جدولٌ فارغٌ يجعل كلَّ مفتاحٍ «منسيّاً» فيُقرأ العطلُ عطلاً أكبر.
        Assert.True(decided.Count >= 71,
            $"‏جدولُ الأدوار عاد بـ{decided.Count} مفتاحاً — القياسُ أجوف، لا المستودعُ سليم.");

        var forgotten = MenuKeys()
            .Where(key => !decided.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(forgotten.Count == 0,
            $"‏{forgotten.Count} مفتاحَ قائمةٍ بلا قرارِ دور — تختفي شاشاتُها صامتةً لكلِّ "
            + "من ليس مديراً، وتختفي مجموعتُها كلُّها إن خلت:\n  "
            + string.Join("\n  ", forgotten));
    }

    /// <summary>
    /// <b>ولا قرارَ لمفتاحٍ لا وجودَ له.</b> مدخلٌ يبقى بعد حذف شاشته يُوهم المُراجعَ
    /// أنّ ثمّة سياسةً حيث لا شاشة — <b>وهو أثرُ حذفٍ نُسي لا قرار</b>.
    /// </summary>
    [Fact]
    public void NoDecidedKey_IsAbsentFromTheMenu()
    {
        var keys = MenuKeys().ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(keys.Count >= 100,
            $"‏القائمةُ عادت بـ{keys.Count} مفتاحاً — القياسُ أجوف.");

        var stale = PermissionService.DecidedKeys
            .Where(key => !keys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            "‏قراراتُ أدوارٍ لمفاتيحَ لا وجودَ لها في القائمة — أثرُ حذفٍ نُسي:\n  "
            + string.Join("\n  ", stale));
    }

    /// <summary>
    /// <b>و«للمدير وحدَه» قرارٌ يبقى قائماً.</b> فلو مُلئ الجدولُ يوماً بمنحِ كلِّ مفتاحٍ
    /// لكلِّ دور، لمرَّ الحارسان أعلاه خضراوين <b>وقد انفتح البابُ كلُّه</b>.
    /// </summary>
    [Fact]
    public void TheAdministratorOnlyDecisions_SurviveTheFill()
    {
        string[] mustStayAdministratorOnly =
            ["audit_log", "users", "zatca_settings", "backup", "organizations",
             "company_settings", "platform_events", "pricing_plans",
             "soft_deleted_rows", "data_migration", "diagnostics", "gosi_policy"];

        var widened = mustStayAdministratorOnly
            .Where(key => PermissionService.CanAccess(Accountant, key)
                          || PermissionService.CanAccess(Cashier, key)
                          || PermissionService.CanAccess(Viewer, key))
            .ToList();

        // ‏شاهدُ ضبطٍ موجب: لولاه لمرَّ الحارسُ على مفاتيحَ محذوفةٍ بصمت.
        foreach (var key in mustStayAdministratorOnly)
            Assert.True(PermissionService.DecidedKeys.Contains(key),
                $"‏«{key}» غاب عن الجدول — الحارسُ يقيس ما ليس هناك.");

        Assert.True(widened.Count == 0,
            "‏مفاتيحُ كانت للمدير وحدَه بقرارٍ مكتوبٍ فاتّسعت:\n  " + string.Join("\n  ", widened));
    }

    /// <summary>
    /// (أسئلةُ المالك 2026-10-24) <b>الدعمُ والتفضيلاتُ مفتوحان لكلّ دور بقرارٍ مكتوب</b> — من يواجه مشكلةً أو يريد مظهراً آخر
    /// ليس المديرَ بالضرورة. وكان الدعمُ «للمدير وحده» فلا يرفع الصرّافُ شكوى من البرنامج نفسِه.
    /// </summary>
    [Theory]
    [InlineData("customer_support")]
    [InlineData("preferences")]
    public void SupportAndPreferences_AreOpenToEveryRole(string key)
    {
        Assert.True(PermissionService.DecidedKeys.Contains(key), $"‏«{key}» غاب عن الجدول.");
        Assert.True(PermissionService.CanAccess(Accountant, key));
        Assert.True(PermissionService.CanAccess(Cashier, key));
        Assert.True(PermissionService.CanAccess(Viewer, key));
    }

    /// <summary>
    /// <b>والصرّافُ لا يُمنَح سجلَّ المرضى.</b> شبكةُ <c>PatientsPage</c> تعرض رقمَ الهوية
    /// وتاريخَ الميلاد، <b>وحمايةُ البيانات الشخصيّة (G-30) لم تبدأ</b> — فالتوسيعُ قرارُ
    /// خصوصيّةٍ يملكه المُشغّل. <b>وهذا الحارسُ يجعله قراراً يُنقَض عمداً لا يُنزلَق إليه.</b>
    /// </summary>
    [Fact]
    public void TheDemographicRegister_StaysNarrowUntilPrivacyRules()
    {
        Assert.True(PermissionService.DecidedKeys.Contains("patients"),
            "‏«patients» غاب عن الجدول — الحارسُ يقيس ما ليس هناك.");

        Assert.False(PermissionService.CanAccess(Cashier, "patients"),
            "‏سجلُّ المرضى يعرض رقمَ الهوية وتاريخَ الميلاد، ومنحُه لأدنى الأدوار ثقةً "
            + "قرارُ خصوصيّةٍ يملكه المُشغّل — وG-30 لم تبدأ. يُوسَّع بقرارٍ مكتوبٍ لا بسطر.");

        // ‏شاهدُ ضبطٍ موجب: `visits` ممنوحٌ للصرّاف نقلاً عن VisitBillingService.
        Assert.True(PermissionService.CanAccess(Cashier, "visits"),
            "‏«visits» نقلٌ عن `VisitBillingService` = `EnsureCanPost` — فإن سقط هنا فالقياسُ أجوف.");
    }

    // ── الحارس عند نقطة الكتابة ────────────────────────────────────────────────

    /// <summary>
    /// <b>إخفاءُ الزرِّ ليس تخويلاً.</b> <c>ApplyActionVisibility</c> يعمل مرّةً في
    /// <c>Loaded</c>، ثمّ <c>OnToggleInactive</c> يُعيد كتابة <c>Visibility</c>
    /// <b>بلا حدِّ دورٍ</b> — <b>والضغطةُ التي تُشغّله هي الضغطةُ الوحيدةُ التي تُبلِّغ
    /// الزرَّ أصلاً</b>. فالحارسُ الذي يصمد هو الذي في المُعالِج.
    /// </summary>
    [Theory]
    [InlineData("CustomersPage.xaml.cs")]
    [InlineData("ProductsPage.xaml.cs")]
    [InlineData("SuppliersPage.xaml.cs")]
    public void EveryReactivateHandler_ChecksTheRoleBeforeItWrites(string page)
    {
        var source = ReadPage(page);

        // ‏شاهدُ ضبطٍ موجب: يُثبت أنّ الملفَّ قُرئ وأنّ التقنيةَ تجد الحراسةَ القائمة.
        Assert.Contains("DeactivateBtn, PermissionService", source, StringComparison.Ordinal);

        Assert.True(GuardsBeforeItWrites(source, "OnReactivate"),
            $"‏{page}: `OnReactivate` يكتب بلا فحصِ دور. وزرُّ الإيقاف محروسٌ منذ م2-٢٣ "
            + "وزرُّ العودة ليس كذلك — **سقّاطةٌ في الاتجاه الخطأ**.");
    }

    /// <summary>
    /// <b>والمُبدِّلُ يحمل حدَّ الدور.</b> بدونه يُعيد الزرَّ مرئيّاً لكلّ دورٍ عند أوّل تأشيرٍ
    /// على «إظهار الموقوف» — <b>فيُدهَس حارسُ التحميل قبل أن يُستعمَل</b>.
    /// </summary>
    [Theory]
    [InlineData("CustomersPage.xaml.cs")]
    [InlineData("ProductsPage.xaml.cs")]
    [InlineData("SuppliersPage.xaml.cs")]
    public void EveryInactiveToggle_CarriesTheRoleTerm(string page)
    {
        var source = ReadPage(page);
        var body = MethodBody(Blank(source), "OnToggleInactive");

        Assert.Contains("ReactivateBtn.Visibility", body, StringComparison.Ordinal);
        Assert.Contains("PermissionService.Can", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وحارسٌ يقرأ ملفّاً صحيحاً لا يُثبت أنّه يرفض ملفّاً خاطئاً.</b> فالمُحلِّلُ يُجرَّب
    /// على ستّ صيغٍ — أربعٌ منها <b>تمرّ على المطابقة النصّيّة الساذجة</b>: الحارسُ في
    /// تعليق، وفي نصٍّ حرفيّ، وفي دالّةٍ شقيقة، وبعد الكتابة لا قبلها.
    /// </summary>
    [Theory]
    [MemberData(nameof(ParserCases))]
    public void TheParser_Discriminates(string label, string source, bool expected) =>
        Assert.True(GuardsBeforeItWrites(source, "OnReactivate") == expected, label);

    public static TheoryData<string, string, bool> ParserCases()
    {
        const string Guard = """
                    if (!PermissionService.CanPostAccounting(AppServices.CurrentUser))
                    {
                        UiFeedback.ShowWarning("منع");
                        return;
                    }
            """;
        const string Write = "        await db.SaveChangesAsync();";

        static string Handler(string body) => """
            private void OnToggleInactive(object sender, RoutedEventArgs e)
            {
                if (!PermissionService.CanPostAccounting(AppServices.CurrentUser)) return;
            }

            private async void OnReactivate(object sender, RoutedEventArgs e)
            {
            #BODY#
            }
            """.Replace("#BODY#", body, StringComparison.Ordinal);

        return new TheoryData<string, string, bool>
        {
            { "‏الحارسُ قبل الكتابة — يُقبَل", Handler(Guard + "\n" + Write), true },
            { "‏لا حارسَ إطلاقاً — يُرفَض", Handler(Write), false },
            { "‏الحارسُ بعد الكتابة — يُرفَض", Handler(Write + "\n" + Guard), false },
            { "‏الحارسُ في تعليق — يُرفَض", Handler("        // " + Guard.Trim() + "\n" + Write), false },
            { "‏الحارسُ في نصٍّ حرفيّ — يُرفَض",
              Handler("        var todo = \"if (!PermissionService.CanPostAccounting(u)) return;\";\n" + Write), false },
            // ‏الشقيقةُ `OnToggleInactive` تحمل حارساً في كلِّ الحالات — فلو كان القياسُ
            // ‏على الملفّ لا على الكتلة لمرَّت هذه خضراء.
            { "‏الحارسُ في دالّةٍ شقيقةٍ وحدَها — يُرفَض", Handler(Write), false },
            { "‏حارسٌ بلا `return` — يُرفَض",
              Handler("        if (!PermissionService.CanPostAccounting(u)) { UiFeedback.ShowWarning(\"م\"); }\n" + Write), false },
        };
    }

    // ── الأدوات ────────────────────────────────────────────────────────────────

    private static AppUser Accountant => User(UserRole.Accountant);
    private static AppUser Cashier => User(UserRole.Cashier);
    private static AppUser Viewer => User(UserRole.Viewer);

    private static AppUser User(UserRole role) =>
        AppUser.Create(1, $"probe_{role}", "hash", role.ToString(), role);

    private static IEnumerable<string> MenuKeys() =>
        NavigationCatalog.Groups.SelectMany(g => g.Pages).Select(p => p.Key).Distinct();

    /// <summary>
    /// ‏هل تُنادى <c>PermissionService.Can*</c> ثمّ يُخرَج، <b>قبل أوّل كتابةٍ في جسم
    /// المُعالِج نفسِه</b>؟ — والمصدرُ يُمحى منه التعليقُ والنصُّ الحرفيُّ أوّلاً.
    /// </summary>
    private static bool GuardsBeforeItWrites(string source, string handler)
    {
        var body = MethodBody(Blank(source), handler);

        var write = body.IndexOf("SaveChangesAsync", StringComparison.Ordinal);
        if (write < 0) write = body.IndexOf("await _vm.", StringComparison.Ordinal); // after MVVM the write lives in the ViewModel
        if (write < 0) return false;

        var call = body.IndexOf("PermissionService.Can", StringComparison.Ordinal);
        if (call < 0 || call > write) return false;

        var exit = body.IndexOf("return", call, StringComparison.Ordinal);
        return exit >= 0 && exit < write;
    }

    /// <summary>جسمُ الدالّة المُسمّاة — <b>بعدَّ الأقواس لا بنهاية الملفّ</b>.</summary>
    private static string MethodBody(string cleaned, string handler)
    {
        var signature = cleaned.IndexOf($" {handler}(", StringComparison.Ordinal);
        Assert.True(signature >= 0, $"‏لم يُعثر على `{handler}` — تغيّر اسمُها فبطل القياس.");

        var open = cleaned.IndexOf('{', signature);
        Assert.True(open >= 0, $"‏`{handler}` بلا جسم — القياسُ أجوف.");

        var depth = 0;
        for (var i = open; i < cleaned.Length; i++)
        {
            if (cleaned[i] == '{') depth++;
            else if (cleaned[i] == '}' && --depth == 0) return cleaned[open..i];
        }

        throw new InvalidOperationException($"‏كتلةُ `{handler}` غيرُ مُغلقة — المصدرُ لا يُحلَّل.");
    }

    /// <summary>
    /// يمسح التعليقاتِ والنصوصَ الحرفيّةَ مُبقياً الأطوالَ والأسطر — <b>فحارسٌ في تعليقٍ
    /// ليس حارساً</b>، وهي بعينُها الصيغةُ التي تجعل المطابقةَ النصّيّةَ زينة.
    /// </summary>
    private static string Blank(string source)
    {
        var b = new StringBuilder(source);

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

    private static string ReadPage(string name)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages", name);
        Assert.True(File.Exists(path), $"‏لم يُعثر على {name} — الحارسُ أعمى، ولا يمرّ أعمى.");
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
