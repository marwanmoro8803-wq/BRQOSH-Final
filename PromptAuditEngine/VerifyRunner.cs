using System.Diagnostics;
using System.Text;

namespace PromptAuditEngine;

/// <summary>Stack-native verify — Codex/Gemini: run before claiming pass.</summary>
internal static class VerifyRunner
{
    private const int MaxOutputChars = 4000;
    private const int TimeoutMs = 120_000;

    public static IReadOnlyList<VerifyResult> Run(string repoRoot, IReadOnlyList<StackInfo> stacks, bool skipBuild)
    {
        if (skipBuild) return [];
        var results = new List<VerifyResult>();
        foreach (var stack in stacks)
        {
            foreach (var cmd in CommandsFor(stack.Id, repoRoot))
                results.Add(Execute(stack.Id, cmd, repoRoot));
        }
        return results;
    }

    private static IEnumerable<string> CommandsFor(string stackId, string repoRoot) => stackId switch
    {
        "dotnet" => DotNetCommands(repoRoot),
        "node" => ["npm test --if-present", "npm run build --if-present"],
        "python" => ["python -m pytest --co -q 2>nul || python -m compileall -q ."],
        "rust" => ["cargo check --quiet"],
        "go" => ["go test ./..."],
        "jvm" => File.Exists(Path.Combine(repoRoot, "mvnw")) || File.Exists(Path.Combine(repoRoot, "pom.xml"))
            ? ["mvn -q test -DskipTests=false"]
            : ["gradle test --quiet"],
        _ => [],
    };

    private static IEnumerable<string> DotNetCommands(string repoRoot)
    {
        var sln = Directory.GetFiles(repoRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (sln is not null)
        {
            yield return $"dotnet build \"{sln}\" -c Release --nologo -v q";
            yield return $"dotnet test \"{sln}\" -c Release --nologo -v q --no-build";
            yield break;
        }
        foreach (var proj in Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
                     .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                              && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Take(3))
        {
            yield return $"dotnet build \"{proj}\" -c Release --nologo -v q";
        }
    }

    private static VerifyResult Execute(string stackId, string command, string workingDir)
    {
        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var psi = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/sh",
                Arguments = isWindows ? $"/c {command}" : $"-c \"{command}\"",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return new(stackId, command, -1, false, "failed to start process");

            var output = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            if (!proc.WaitForExit(TimeoutMs))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return new(stackId, command, -2, false, "timeout");
            }

            var tail = Tail(output.ToString(), MaxOutputChars);
            return new(stackId, command, proc.ExitCode, proc.ExitCode == 0, tail);
        }
        catch (Exception ex)
        {
            return new(stackId, command, -1, false, ex.Message);
        }
    }

    private static string Tail(string text, int max) =>
        text.Length <= max ? text : text[^max..];
}
