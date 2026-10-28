using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (i18n step 1) Every key used through <c>{loc:Str Key=…}</c> or <c>Loc.T("…")</c> exists in both
/// Resources/Strings.resx (Arabic) and Strings.en.resx (English), and the two files carry the same keys.
/// </summary>
public class LocResxKeysTests
{
    private static readonly Regex XamlKey = new(@"\{loc:Str\s+(?:Key=)?([A-Za-z0-9_.]+)\s*\}", RegexOptions.Compiled);
    private static readonly Regex CodeKey = new(@"\b(?:Loc|LocaleManager)\.T\(\s*""([A-Za-z][A-Za-z0-9_]*\.[A-Za-z0-9_.]+)""\s*\)", RegexOptions.Compiled);
    private static readonly Regex Arabic = new(@"[\u0600-\u06FF]", RegexOptions.Compiled);

    private static string DesktopRoot()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"Desktop source root not found: {root}");
        return root;
    }

    private static Dictionary<string, string> ReadResx(string fileName) =>
        XDocument.Load(Path.Combine(DesktopRoot(), "Resources", fileName))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(DesktopRoot(), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static HashSet<string> UsedKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in SourceFiles("*.xaml"))
            foreach (Match m in XamlKey.Matches(File.ReadAllText(f)))
                keys.Add(m.Groups[1].Value);
        foreach (var f in SourceFiles("*.cs"))
            foreach (Match m in CodeKey.Matches(File.ReadAllText(f)))
                keys.Add(m.Groups[1].Value);
        return keys;
    }

    [Fact]
    public void EveryUsedKey_ExistsInBothResxFiles()
    {
        var ar = ReadResx("Strings.resx");
        var en = ReadResx("Strings.en.resx");
        var used = UsedKeys();
        Assert.True(used.Count >= 20, $"Only {used.Count} keys found: the scan is probably broken.");

        var missing = used.Where(k => !ar.ContainsKey(k) || !en.ContainsKey(k)).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, "Keys missing from a resx file:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void ResxFiles_HaveSameKeys_AndRealTranslations()
    {
        var ar = ReadResx("Strings.resx");
        var en = ReadResx("Strings.en.resx");
        Assert.Equal(ar.Keys.OrderBy(k => k, StringComparer.Ordinal), en.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(ar, kv => Assert.Matches(Arabic, kv.Value));
        Assert.All(en, kv => Assert.True(kv.Value.Length > 0 && !Arabic.IsMatch(kv.Value), $"{kv.Key}: '{kv.Value}'"));
    }

    [Fact]
    public void Loc_ReadsBothLanguages_AndFallsBackForUnknownKeys()
    {
        Assert.Equal("Save", Loc.TryGet("Common.Save", english: true));
        Assert.Equal("حفظ", Loc.TryGet("Common.Save", english: false));
        Assert.Null(Loc.TryGet("No.Such.Key", english: true));
        Assert.Equal("No.Such.Key", Loc.T("No.Such.Key"));
    }
}
