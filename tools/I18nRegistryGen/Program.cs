using System.Text;
using System.Text.RegularExpressions;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var desktopRoot = Path.Combine(repoRoot, "src", "Brqosh.Desktop");
var outFile = Path.Combine(desktopRoot, "Ui", "UiBilingualRegistry.g.cs");
var missingFile = Path.Combine(repoRoot, "dist", "evidence", "i18n-audit", "registry-missing.txt");

var glossary = LoadGlossary(Path.Combine(repoRoot, "assets", "i18n", "erp-glossary.json"));
MergeGlossary(glossary, Path.Combine(repoRoot, "assets", "i18n", "erp-glossary-extended.json"));

// (CEEF-223) ترجمات عبارات كاملة بخطّ اليد — تُدمج **أخيراً** فتغلب قاموس الكلمات.
// الاستبدال كلمةً بكلمة يُنتج ركاكة لكل عبارة غير مغطّاة بالكامل ("12 حرفاً on الأقل
// with حرف كبير…")، وكانت 188 من 799 مدخلاً تحوي عربياً في نصّها الإنجليزيّ.
// وجودها في ملف بيانات يعني أنّ إعادة التوليد لا تمحوها.
var overridesPath = Path.Combine(repoRoot, "assets", "i18n", "ui-overrides.json");
MergeGlossary(glossary, overridesPath);
glossary.Remove("_comment");
glossary.Remove("_comment2");
glossary.Remove("_comment3");
glossary.Remove("_comment4");

var overrideKeys = LoadGlossary(overridesPath).Keys
    .Where(k => !k.StartsWith('_') && ContainsArabic(k))
    .ToList();
var navMap = ParseNavMap(Path.Combine(desktopRoot, "Navigation", "NavigationCatalog.cs"));
var emptyMap = ParseEmptyState(Path.Combine(desktopRoot, "Ui", "EmptyStateCatalog.cs"));

var strings = new HashSet<string>(StringComparer.Ordinal);
foreach (var xaml in Directory.EnumerateFiles(desktopRoot, "*.xaml", SearchOption.AllDirectories))
{
    var raw = File.ReadAllText(xaml);
    var text = StripXmlComments(raw);
    foreach (Match m in Regex.Matches(text,
                 @"(?:Text|Content|Header|ToolTip|Title|PlaceholderText|Watermark)=""([^""]*[\u0600-\u06FF][^""]*)""",
                 RegexOptions.IgnoreCase))
    {
        var s = m.Groups[1].Value;
        if (IsValidUiString(s))
            strings.Add(s);
    }
}

// (CEEF-229 · م2-٢٣) **ونصوصُ الشيفرة الخلفية تُمسَح أيضاً — كانت البوّابةُ عمياء عنها.**
//
// كان المسحُ يقتصر على وسوم XAML، فأيّ نصٍّ عربيّ يُسنَد إلى `.Text` أو `.Header` من
// الشيفرة الخلفية يبقى عربياً عند التبديل **بلا أن تشتكي البوّابة**. وكشفه أنّي وسمتُ
// صنفاً موقوفاً بكلمة «موقوف» في code-behind فمرّت صامتة، ونقلُها إلى XAML أسقطها فوراً.
//
// **والمسحُ هنا محدودٌ بإسنادات خصائص العناصر عمداً.** فـ`MessageBox.Show` نافذةُ Win32
// لا عنصرٌ في شجرة الواجهة، ولا يبلغها `XamlUiLocalizer` مهما وُضع في السجلّ. وإدخالُ
// نصوصها هنا كان يُخضِّر البوّابةَ والنصُّ عربيٌّ كما هو — أي يجعل الأداةَ تكذب. وعددُها
// مقيسٌ ومسجَّلٌ بنداً مفتوحاً، وعلاجُها آليّةٌ أخرى لا هذا السجلّ.
foreach (var cs in Directory.EnumerateFiles(desktopRoot, "*.cs", SearchOption.AllDirectories))
{
    if (cs.EndsWith(".g.cs", StringComparison.Ordinal)
        || cs.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || cs.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        continue;

    var source = File.ReadAllText(cs);

    foreach (Match m in Regex.Matches(source,
                 @"\.(?:Text|Content|Header|ToolTip|Title|PlaceholderText)\s*=\s*""([^""]*[؀-ۿ][^""]*)"""))
    {
        var s = m.Groups[1].Value;
        if (IsValidUiString(s))
            strings.Add(s);
    }

    // (CEEF-229) ‏نصوصُ الحوارات — وكانت خارج السجلّ كلِّها.
    //
    // ‏MessageBox نافذةُ Win32 لا يبلغها XamlUiLocalizer، فكان إدخالُ نصوصها في السجلّ
    // ‏عبثاً يُخضِّر البوّابةَ والنصُّ عربيٌّ كما هو. وقد بُني UiDialog يترجم **عند
    // ‏الاستدعاء** بالبحث في هذا السجلّ — فصار للمدخل معنى.
    //
    // ‏ويُلتقط كلُّ اقتباسٍ حرفيٍّ فيه عربية داخل الاستدعاء: النصُّ والوسمُ معاً، فكلاهما
    // ‏يُعرض. والمركَّبُ (‏$"…") لا يُلتقط — لا يُطابَق ككلّ، وهو معدودٌ ومحروسٌ في
    // ‏DialogLocalizationTests.
    foreach (Match call in Regex.Matches(source, @"\bUiDialog\.Show\s*\("))
    {
        var slice = source[call.Index..Math.Min(source.Length, call.Index + 600)];
        var end = slice.IndexOf(");", StringComparison.Ordinal);
        if (end > 0) slice = slice[..end];

        foreach (Match lit in Regex.Matches(slice, @"(?<!\$)""([^""]*[؀-ۿ][^""]*)"""))
        {
            var s = lit.Groups[1].Value;
            if (IsValidUiString(s))
                strings.Add(s);
        }
    }
}

// (CEEF-225) نصوص تُضبط في **الكود** لا في XAML (زرّ الثيم، تسمية الدور، نمط العمل)
// تدخل السجلّ أيضاً. السبب: `XamlUiLocalizer` يحفظ أوّل نصّ عربيّ رآه على العنصر
// كـ«أصل»، ثم يعيد الحلّ منه في كل مرور — فيكتب فوق ما ضبطه الكود بالإنجليزية.
// وجود مفتاح في السجلّ يجعل المترجم نفسه يُنتج الإنجليزية بدل أن يتصارع مع الكود.
foreach (var key in overrideKeys)
    strings.Add(key);

var registry = new Dictionary<string, string>(StringComparer.Ordinal);
var missing = new List<string>();
foreach (var ar in strings.OrderBy(s => s, StringComparer.Ordinal))
{
    var en = Translate(ar, glossary, navMap, emptyMap);
    registry[ar] = en;
    if (en == ar && ContainsArabic(ar))
        missing.Add(ar);
}

Directory.CreateDirectory(Path.GetDirectoryName(missingFile)!);
await File.WriteAllLinesAsync(missingFile, missing.OrderBy(s => s, StringComparer.Ordinal), Encoding.UTF8);

var sb = new StringBuilder();
sb.AppendLine("// <auto-generated> — tools/I18nRegistryGen");
sb.AppendLine("namespace Brqosh.Desktop.Ui;");
sb.AppendLine();
sb.AppendLine("/// <summary>G-27 bilingual registry — all static XAML Arabic strings.</summary>");
sb.AppendLine("public static partial class UiBilingualRegistry");
sb.AppendLine("{");
sb.AppendLine("    public static IReadOnlyDictionary<string, string> Map { get; } = new Dictionary<string, string>(StringComparer.Ordinal)");
sb.AppendLine("    {");
foreach (var kv in registry)
    sb.AppendLine($"        [{Escape(kv.Key)}] = {Escape(kv.Value)},");
sb.AppendLine("    };");
sb.AppendLine();
sb.AppendLine("    public static string Lookup(string arabic) =>");
sb.AppendLine("        Map.TryGetValue(arabic, out var en) ? en : arabic;");
sb.AppendLine("}");

await File.WriteAllTextAsync(outFile, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

var coverage = strings.Count == 0 ? 100.0 : 100.0 * (strings.Count - missing.Count) / strings.Count;
Console.WriteLine($"Generated {registry.Count} entries -> {outFile}");
Console.WriteLine($"Coverage: {coverage:F1}% ({missing.Count} untranslated)");
Console.WriteLine($"Missing: {missingFile}");
return missing.Count > 0 ? 1 : 0;

static string StripXmlComments(string xaml) =>
    Regex.Replace(xaml, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

static bool IsValidUiString(string s) =>
    s.Length is > 0 and <= 180
    && !s.Contains('<')
    && !s.Contains('>')
    && !s.Contains('{')
    && !s.Contains('}')
    && !s.Contains('\\')
    && ContainsArabic(s);

static string Escape(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

static bool ContainsArabic(string text) =>
    text.Any(ch => ch is >= '\u0600' and <= '\u06FF');

static void MergeGlossary(Dictionary<string, string> target, string path)
{
    if (!File.Exists(path)) return;
    foreach (var kv in LoadGlossary(path))
        target[kv.Key] = kv.Value;
}

static Dictionary<string, string> LoadGlossary(string path)
{
    if (!File.Exists(path))
        return new Dictionary<string, string>(StringComparer.Ordinal);
    var json = File.ReadAllText(path);
    return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
           ?? new Dictionary<string, string>(StringComparer.Ordinal);
}

static Dictionary<string, string> ParseNavMap(string navCs)
{
    var content = File.ReadAllText(navCs);
    var keyToEn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (Match m in Regex.Matches(content, @"\[""([^""]+)""\] = ""([^""]+)"""))
        keyToEn[m.Groups[1].Value] = m.Groups[2].Value;

    var arToEn = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (Match m in Regex.Matches(content, @"new\(""([^""]+)"",\s*""([^""]+)""\)"))
    {
        var key = m.Groups[1].Value;
        var ar = m.Groups[2].Value;
        if (keyToEn.TryGetValue(key, out var en))
            arToEn[ar] = en;
    }
    return arToEn;
}

static Dictionary<string, string> ParseEmptyState(string emptyCs)
{
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (Match m in Regex.Matches(emptyCs,
                 @"new\(""([^""]+)"",\s*""([^""]+)"",\s*""[^""]+"",\s*""([^""]+)"",\s*""([^""]+)""\)"))
    {
        map[m.Groups[1].Value] = m.Groups[3].Value;
        map[m.Groups[2].Value] = m.Groups[4].Value;
    }
    return map;
}

static string Translate(
    string ar,
    Dictionary<string, string> glossary,
    Dictionary<string, string> navMap,
    Dictionary<string, string> emptyMap)
{
    if (glossary.TryGetValue(ar, out var g)) return g;
    if (navMap.TryGetValue(ar, out var n)) return n;
    if (emptyMap.TryGetValue(ar, out var e)) return e;

    if (ar.EndsWith(':'))
    {
        var stem = ar[..^1];
        if (glossary.TryGetValue(stem, out var gColon))
            return gColon + ":";
        if (navMap.TryGetValue(stem, out var nColon))
            return nColon + ":";
    }

    var normalized = ar.StartsWith("＋") ? "+ " + ar[1..].TrimStart() : ar;
    if (glossary.TryGetValue(normalized.Trim(), out var g2)) return g2;

    var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim('·', '*', '—', '–', '(', ')', '،', '.'))
        .Select(p => glossary.TryGetValue(p, out var t) ? t : p);
    var en = string.Join(' ', parts).Trim();
    return en == ar ? ar : en;
}
