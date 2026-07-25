namespace PromptAuditEngine;

/// <summary>Universal stack detection — principles from universal-coding/reference.md</summary>
internal static class StackDetector
{
    public static IReadOnlyList<StackInfo> Detect(string repoRoot)
    {
        var stacks = new List<StackInfo>
        {
            DetectDotNet(repoRoot),
            DetectNode(repoRoot),
            DetectPython(repoRoot),
            DetectRust(repoRoot),
            DetectGo(repoRoot),
            DetectJvm(repoRoot),
        };
        return stacks.Where(s => s.Detected).ToList();
    }

    private static StackInfo DetectDotNet(string root)
    {
        var sln = Directory.GetFiles(root, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        var csproj = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains("obj", StringComparison.Ordinal) && !f.Contains("bin", StringComparison.Ordinal));
        var path = sln ?? csproj;
        return new("dotnet", ".NET", path, path is not null);
    }

    private static StackInfo DetectNode(string root)
    {
        var pkg = Path.Combine(root, "package.json");
        return new("node", "Node.js/TypeScript", File.Exists(pkg) ? pkg : null, File.Exists(pkg));
    }

    private static StackInfo DetectPython(string root)
    {
        var modern = Path.Combine(root, "pyproject.toml");
        var legacy = Path.Combine(root, "requirements.txt");
        if (File.Exists(modern)) return new("python", "Python", modern, true);
        if (File.Exists(legacy)) return new("python", "Python", legacy, true);
        return new("python", "Python", null, false);
    }

    private static StackInfo DetectRust(string root)
    {
        var cargo = Path.Combine(root, "Cargo.toml");
        return new("rust", "Rust", File.Exists(cargo) ? cargo : null, File.Exists(cargo));
    }

    private static StackInfo DetectGo(string root)
    {
        var mod = Path.Combine(root, "go.mod");
        return new("go", "Go", File.Exists(mod) ? mod : null, File.Exists(mod));
    }

    private static StackInfo DetectJvm(string root)
    {
        var maven = Path.Combine(root, "pom.xml");
        var gradle = Directory.GetFiles(root, "build.gradle*", SearchOption.TopDirectoryOnly).FirstOrDefault();
        var path = File.Exists(maven) ? maven : gradle;
        return new("jvm", "JVM (Maven/Gradle)", path, path is not null);
    }
}
