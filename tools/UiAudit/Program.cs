using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace UiAudit;

internal static class Program
{
    private static int ResolveTimeoutMs()
    {
        var raw = Environment.GetEnvironmentVariable("BRQOSH_UIAUDIT_TIMEOUT_MS");
        if (int.TryParse(raw, out var ms) && ms >= 30_000)
            return ms;

        var all = Environment.GetEnvironmentVariable("BRQOSH_AUDIT_ALL_PROFILES");
        if (string.Equals(all, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(all, "true", StringComparison.OrdinalIgnoreCase))
            return 180_000;

        return 120_000;
    }

    public static async Task<int> Main()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var desktopProj = Path.Combine(repoRoot, "src", "Brqosh.Desktop", "Brqosh.Desktop.csproj");
        var seedTool = Path.Combine(repoRoot, "tools", "FullScenarioAudit", "FullScenarioAudit.csproj");

        Console.WriteLine("=== BRQOSH Comprehensive UI Audit ===");

        var skipBuild = string.Equals(Environment.GetEnvironmentVariable("BRQOSH_SKIP_DESKTOP_BUILD"), "1", StringComparison.OrdinalIgnoreCase);
        if (!skipBuild)
        {
            Console.WriteLine("بناء Desktop Release قبل التدقيق...");
            var build = Process.Start(new ProcessStartInfo("dotnet", $"build \"{desktopProj}\" -c Release -v q")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = repoRoot,
            });
            if (build is not null)
            {
                await build.WaitForExitAsync();
                if (build.ExitCode != 0)
                {
                    Console.WriteLine("فشل بناء Desktop — إيقاف التدقيق.");
                    return 1;
                }
            }
        }

        var profiles = ResolveProfiles();
        var aggregate = new UiAuditResult();
        var exitCode = 0;
        var interProfileMs = int.TryParse(Environment.GetEnvironmentVariable("BRQOSH_UIAUDIT_PROFILE_GAP_MS"), out var gap) && gap >= 5000
            ? gap
            : 20_000;

        foreach (var profile in profiles)
        {
            Console.WriteLine();
            Console.WriteLine($"=== ملف العمل: {profile} ===");
            Console.WriteLine("المرحلة 1: تجهيز بيانات QA-AUDIT...");

            var seedPsi = new ProcessStartInfo("dotnet", $"run --project \"{seedTool}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = repoRoot,
            };
            seedPsi.Environment["BRQOSH_AUDIT_PROFILE"] = profile;

            var seed = Process.Start(seedPsi);
            if (seed is not null)
            {
                var seedOut = await seed.StandardOutput.ReadToEndAsync();
                await seed.WaitForExitAsync();
                Console.WriteLine(seedOut);
                if (seed.ExitCode != 0)
                    Console.WriteLine("تحذير: فشل جزئي في تجهيز البيانات — يستمر التدقيق.");
            }

            Console.WriteLine("المرحلة 2: تشغيل الواجهة واختبار الصفحات...");

            ProfileAuditContext.SetFromEnvironment();
            if (!string.IsNullOrWhiteSpace(profile))
                Environment.SetEnvironmentVariable("BRQOSH_AUDIT_PROFILE", profile);

            var profileResult = await RunLiveAuditAsync(repoRoot, desktopProj, $"[{profile}] ", ResolveTimeoutMs());
            ProfileAuditContext.Clear();
            aggregate.MergeFrom(profileResult);

            if (profileResult.Failed > 0)
                exitCode = 1;

            foreach (var proc in Process.GetProcessesByName("Brqosh.Desktop"))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            }

            await Task.Delay(interProfileMs);
        }

        var reportDir = Path.Combine(repoRoot, "dist", "evidence", "live-ui-audit");
        var reportPath = Path.Combine(reportDir, $"audit-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        aggregate.SaveReport(reportPath);
        Console.WriteLine($"تقرير محفوظ: {reportPath}");
        aggregate.Print();
        return exitCode;
    }

    private static string[] ResolveProfiles()
    {
        var all = Environment.GetEnvironmentVariable("BRQOSH_AUDIT_ALL_PROFILES");
        if (string.Equals(all, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(all, "true", StringComparison.OrdinalIgnoreCase))
            return ["Manufacturing", "Construction", "Retail"];

        var multi = Environment.GetEnvironmentVariable("BRQOSH_AUDIT_PROFILES");
        if (!string.IsNullOrWhiteSpace(multi))
            return multi.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var single = Environment.GetEnvironmentVariable("BRQOSH_AUDIT_PROFILE");
        return [string.IsNullOrWhiteSpace(single) ? "Manufacturing" : single.Trim()];
    }

    private static async Task<UiAuditResult> RunLiveAuditAsync(string repoRoot, string desktopProj, string prefix, int timeoutMs)
    {
        UiAuditResult? last = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            last = await RunLiveAuditOnceAsync(repoRoot, desktopProj, prefix, timeoutMs);
            if (last.Failed == 0)
                return last;

            var timedOut = last.Items.Any(i =>
                i.Detail.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                i.Detail.Contains("0x80131505", StringComparison.Ordinal));
            if (!timedOut || attempt == 2)
                return last;

            Console.WriteLine($"{prefix}إعادة محاولة بعد مهلة COM (محاولة {attempt + 1}/2)...");
            await Task.Delay(4000);
        }

        return last!;
    }

    private static async Task<UiAuditResult> RunLiveAuditOnceAsync(string repoRoot, string desktopProj, string prefix, int timeoutMs)
    {
        var results = new UiAuditResult();
        var desktopBin = Path.Combine(repoRoot, "src", "Brqosh.Desktop", "bin");
        var exeDebug = Path.Combine(desktopBin, "Debug", "net9.0-windows", "Brqosh.Desktop.exe");
        var exeRelease = Path.Combine(desktopBin, "Release", "net9.0-windows", "Brqosh.Desktop.exe");
        var exePath = File.Exists(exeRelease) ? exeRelease : exeDebug;

        ProcessStartInfo psi;
        if (File.Exists(exePath))
        {
            psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
            };
        }
        else
        {
            psi = new ProcessStartInfo("dotnet", $"run --project \"{desktopProj}\"")
            {
                UseShellExecute = false,
                WorkingDirectory = repoRoot,
            };
        }

        Environment.SetEnvironmentVariable("BRQOSH_UITEST", "1");
        SeedProcessEnvironment(psi);
        psi.Environment["BRQOSH_UITEST"] = "1";
        Environment.SetEnvironmentVariable("BRQOSH_UITEST", "1");
        CopyEnv(psi, "BRQOSH_TEST_USERNAME");
        CopyEnv(psi, "BRQOSH_TEST_PASSWORD");
        CopyEnv(psi, "BRQOSH_AUDIT_PROFILE");
        CopyEnv(psi, "BRQOSH_SKIP_STRESS");
        CopyEnv(psi, "BRQOSH_TEST");
        CopyEnv(psi, "BRQOSH_UIAUDIT_TIMEOUT_MS");

        foreach (var proc in Process.GetProcessesByName("Brqosh.Desktop"))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }

        await Task.Delay(1500);

        var appProcess = Process.Start(psi);
        if (appProcess is null)
        {
            results.Fail($"{prefix}تشغيل", "تعذر تشغيل التطبيق");
            return results;
        }

        await WaitForProcessReadyAsync(appProcess, Math.Min(timeoutMs, 90_000));

        using var automation = new UIA3Automation();
        Application? attachedApp = null;

        psi.Environment.TryGetValue("BRQOSH_UITEST", out var uitestRaw);
        var uitestMode = string.Equals(uitestRaw, "1", StringComparison.Ordinal);

        try
        {
            Console.WriteLine($"{prefix}انتظار النافذة الرئيسية (مهلة {timeoutMs / 1000}s, UITEST={uitestMode})...");
            Window? main = null;
            var attachUntil = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < attachUntil && main is null)
            {
                if (UiAutomationHelpers.TryAttachMainWindow(
                        automation, appProcess, out attachedApp, out var attached, TimeSpan.FromSeconds(6))
                    && attached is not null
                    && attached.IsAvailable
                    && attached.Name.Contains("BRQOSH ERP", StringComparison.OrdinalIgnoreCase))
                {
                    main = attached;
                    break;
                }

                attachedApp?.Dispose();
                attachedApp = null;

                main = UiAutomationHelpers.WaitForBrqoshWindow(automation, "BRQOSH ERP", 2000);
                if (main is not null)
                    break;

                await Task.Delay(400);
            }

            if (main is null && !uitestMode)
            {
                var login = UiAutomationHelpers.WaitForBrqoshWindow(automation, "تسجيل الدخول", timeoutMs)
                    ?? UiAutomationHelpers.WaitForBrqoshWindow(automation, "BRQOSH —", timeoutMs)
                    ?? UiAutomationHelpers.WaitForBrqoshWindow(automation, "BRQOSH", timeoutMs);
                if (login is null)
                {
                    main = UiAutomationHelpers.WaitForBrqoshWindow(automation, "BRQOSH ERP", timeoutMs);
                    if (main is null)
                    {
                        results.Fail($"{prefix}تسجيل الدخول", "نافذة الدخول أو الرئيسية لم تظهر");
                        return results;
                    }
                }
                else
                {
                    var testUser = Environment.GetEnvironmentVariable("BRQOSH_TEST_USERNAME") ?? "marwan";
                    var testPass = Environment.GetEnvironmentVariable("BRQOSH_TEST_PASSWORD") ?? "UiAudit9!";
                    await UiAutomationHelpers.LoginAsync(login, testUser, testPass);
                    await UiAutomationHelpers.DismissMessageBoxesAsync(automation);

                    var (loggedInMain, loginFail) = await UiAutomationHelpers.WaitForPostLoginAsync(
                        automation, appProcess, login, testPass, Math.Min(timeoutMs, 90_000));
                    if (loginFail is not null)
                    {
                        results.Fail($"{prefix}تسجيل الدخول", loginFail);
                        return results;
                    }

                    main = loggedInMain;
                }
            }
            else if (main is null && uitestMode)
            {
                var diag = ReadUiTestDiag();
                results.Fail($"{prefix}النافذة الرئيسية",
                    string.IsNullOrEmpty(diag)
                        ? "BRQOSH ERP لم تظهر خلال المهلة — UITEST دون LoginAsync"
                        : $"BRQOSH ERP لم تظهر — UITEST diag: {diag}");
                return results;
            }

            if (main is null)
            {
                var license = UiAutomationHelpers.WaitForBrqoshWindow(automation, "ترخيص", 5000)
                    ?? UiAutomationHelpers.WaitForBrqoshWindow(automation, "License", 3000);
                if (license is not null)
                    results.Fail($"{prefix}النافذة الرئيسية", "نافذة الترخيص تمنع الدخول");
                else
                    results.Fail($"{prefix}النافذة الرئيسية", "لم تُفتح — تحقق من الاعتماد");
                return results;
            }

            await Task.Delay(2000);
            var navCount = UiAutomationHelpers.CountNavButtons(main);
            if (navCount == 0)
            {
                results.Fail($"{prefix}أزرار التنقل", "0 أزرار");
                return results;
            }

            results.Pass($"{prefix}تسجيل الدخول", Environment.GetEnvironmentVariable("BRQOSH_TEST_USERNAME") ?? "marwan");
            results.Pass($"{prefix}النافذة الرئيسية", "جاهزة");
            results.Pass($"{prefix}أزرار التنقل", $"{navCount} زر");

            var auditor = new ComprehensiveAuditor(automation, main, results, prefix);
            try
            {
                await auditor.RunAllPagesAsync();
            }
            catch (Exception ex) when (UiAutomationHelpers.IsTransientUia(ex))
            {
                results.Fail($"{prefix}استثناء عام", $"UIA عابر أثناء التدقيق: {ex.Message}");
            }
        }
        catch (Exception ex) when (UiAutomationHelpers.IsTransientUia(ex))
        {
            results.Fail($"{prefix}استثناء عام", $"UIA عابر: {ex.Message}");
        }
        catch (Exception ex)
        {
            results.Fail($"{prefix}استثناء عام", ex.Message);
        }
        finally
        {
            attachedApp?.Dispose();
            try
            {
                if (!appProcess.HasExited)
                    appProcess.Kill(entireProcessTree: true);
                await appProcess.WaitForExitAsync();
            }
            catch { /* ignore */ }
        }

        return results;
    }

    private static async Task WaitForProcessReadyAsync(Process process, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                process.Refresh();
                if (process.HasExited)
                    return;

                // (CEEF-209) الاستعداد = وجود نافذة فعلية، لا مجرد استجابة العملية:
                // كونسول `dotnet run` يستجيب فوراً بينما التطبيق لم يبدأ بعد، فكان الانتظار
                // ينتهي مبكراً ويبدأ البحث عن نافذة غير موجودة.
                if (UiAutomationHelpers.ResolveWindowOwner(process).MainWindowHandle != IntPtr.Zero)
                    return;
            }
            catch
            {
                /* process may still be starting */
            }

            await Task.Delay(250);
        }
    }

    private static void SeedProcessEnvironment(ProcessStartInfo psi)
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var name = entry.Key.ToString()!;
            psi.Environment[name] = entry.Value?.ToString() ?? "";
        }
    }

    private static void CopyEnv(ProcessStartInfo psi, string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrEmpty(value))
            psi.Environment[name] = value;
    }

    private static string ReadUiTestDiag()
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "brqosh-uitest-last.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }
        catch
        {
            return "";
        }
    }

    private static string PickNewestExe(string debugPath, string releasePath)
    {
        var candidates = new[] { debugPath, releasePath }
            .Where(File.Exists)
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();
        return candidates.FirstOrDefault()?.FullName ?? debugPath;
    }
}
