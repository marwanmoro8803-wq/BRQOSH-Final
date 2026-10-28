namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// ‏(Phase 7) <c>BrqoshSchemaUpdater</c> مقسومٌ على ملفّاتٍ جزئيّة
/// (<c>BrqoshSchemaUpdater.cs</c> + <c>BrqoshSchemaUpdater.*.cs</c>) — <b>فالحرّاسُ الساكنةُ تقرأها كلَّها</b>،
/// وإلا صار ما نُقل إلى جزءٍ آخر خارجَ القياس بصمت.
/// </summary>
internal static class SchemaUpdaterSourceFiles
{
    public static IReadOnlyList<string> Paths(string repoRoot)
    {
        var dir = Path.Combine(repoRoot, "src", "Brqosh.Infrastructure", "Persistence");
        var main = Path.Combine(dir, "BrqoshSchemaUpdater.cs");
        var parts = Directory.GetFiles(dir, "BrqoshSchemaUpdater.*.cs").Order(StringComparer.Ordinal);
        return [main, .. parts];
    }

    public static string ReadAll(string repoRoot) =>
        string.Join("\n", Paths(repoRoot).Select(File.ReadAllText));
}
