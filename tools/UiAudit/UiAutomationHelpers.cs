using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace UiAudit;

internal static class UiAutomationHelpers
{
    public const string MainGridId = "BrqoshMainGrid";
    public const string ReportSummaryId = "BrqoshReportSummary";
    public const string KpiGridId = "BrqoshKpiGrid";

    private static readonly string[] SummaryKeywords =
    [
        "إجمالي", "صافي", "رصيد", "متوازن", "مستحق", "مدين", "دائن", "مبيعات", "أصول", "الالتزامات",
    ];

    public static Window? WaitForBrqoshWindow(UIA3Automation automation, string titlePart, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var win = automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                    .Select(w => w.AsWindow())
                    .FirstOrDefault(w => w.IsAvailable && w.Name.Contains(titlePart, StringComparison.OrdinalIgnoreCase));
                if (win is not null)
                    return win;
            }
            catch
            {
                /* FlaUI COM timeout while app is starting */
            }

            Thread.Sleep(350);
        }

        return null;
    }

    /// <summary>
    /// العملية التي تملك نافذة التطبيق فعلاً.
    ///
    /// (CEEF-209) حين يُشغَّل التطبيق بـ <c>dotnet run</c> تكون العملية المُعادة هي المُشغِّل
    /// (كونسول) و<c>MainWindowHandle</c> فيها صفر دائماً — فينتظر التدقيق نافذةً على العملية
    /// الخطأ إلى أن تنقضي المهلة، بينما النافذة تخصّ العملية الابنة Brqosh.Desktop.
    /// هذا سبب تعطّل تدقيق الواجهة الحيّ.
    /// </summary>
    public static Process ResolveWindowOwner(Process launched)
    {
        try
        {
            launched.Refresh();
            if (!launched.HasExited && launched.MainWindowHandle != IntPtr.Zero)
                return launched;
        }
        catch { /* العملية قد تنتهي أثناء الفحص */ }

        foreach (var candidate in Process.GetProcessesByName("Brqosh.Desktop"))
        {
            try
            {
                candidate.Refresh();
                if (!candidate.HasExited && candidate.MainWindowHandle != IntPtr.Zero)
                    return candidate;
            }
            catch { /* تجاهل عملية تعذّر فحصها */ }
        }

        return launched;
    }

    public static bool TryAttachMainWindow(
        UIA3Automation automation,
        Process process,
        out Application attachedApp,
        out Window? mainWindow,
        TimeSpan wait)
    {
        attachedApp = null!;
        mainWindow = null;
        try
        {
            // اربط بالعملية صاحبة النافذة لا بالمُشغِّل (CEEF-209).
            process = ResolveWindowOwner(process);

            process.Refresh();
            if (process.HasExited || process.MainWindowHandle == IntPtr.Zero)
                return false;

            attachedApp = Application.Attach(process.Id);
            mainWindow = attachedApp.GetMainWindow(automation, wait);
            return mainWindow is not null && mainWindow.IsAvailable;
        }
        catch
        {
            attachedApp?.Dispose();
            attachedApp = null!;
            mainWindow = null;
            return false;
        }
    }

    public static async Task<bool> NavigateAsync(Window main, string pageTitleAr, string? navKey = null)
    {
        try
        {
            var btn = FindNavButton(main, pageTitleAr, navKey);
            if (btn is null)
                return false;

            try { btn.Patterns.ScrollItem.Pattern.ScrollIntoView(); } catch { /* ignore */ }
            await SafeInvokeAsync(btn.AsButton());
            await Task.Delay(650);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 5000)
            {
                var title = main.FindFirstDescendant(cf => cf.ByAutomationId("PageTitle"));
                if (SafeName(title) == pageTitleAr)
                    return true;
                await Task.Delay(200);
            }

            return SafeName(main.FindFirstDescendant(cf => cf.ByAutomationId("PageTitle")))
                .Contains(pageTitleAr, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static int CountNavButtons(Window main) =>
        main.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
            .Count(b => TryAutomationId(b).StartsWith("Nav_", StringComparison.Ordinal));

    public static AutomationElement? FindNavButton(Window main, string pageTitleAr, string? navKey = null)
    {
        navKey ??= NavKeyMap.KeyForTitle(pageTitleAr);

        if (!string.IsNullOrEmpty(navKey))
        {
            var byId = main.FindFirstDescendant(cf => cf.ByAutomationId($"Nav_{navKey}"));
            if (byId is not null)
            {
                try { byId.Patterns.ScrollItem.Pattern.ScrollIntoView(); } catch { /* ignore */ }
                return byId;
            }
        }

        var direct = ScanNavButtons(main, pageTitleAr);
        if (direct is not null)
            return direct;

        foreach (var scrollHost in main.FindAllDescendants(cf => cf.ByControlType(ControlType.Pane))
                     .Concat(main.FindAllDescendants(cf => cf.ByControlType(ControlType.Group))))
        {
            if (!scrollHost.Patterns.Scroll.IsSupported)
                continue;

            try
            {
                var scroll = scrollHost.Patterns.Scroll.Pattern;
                scroll.SetScrollPercent(-1, 0);
                for (var i = 0; i < 24; i++)
                {
                    var found = ScanNavButtons(main, pageTitleAr);
                    if (found is not null)
                        return found;

                    if (!string.IsNullOrEmpty(navKey))
                    {
                        var byId = main.FindFirstDescendant(cf => cf.ByAutomationId($"Nav_{navKey}"));
                        if (byId is not null)
                        {
                            try { byId.Patterns.ScrollItem.Pattern.ScrollIntoView(); } catch { /* ignore */ }
                            return byId;
                        }
                    }

                    scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeIncrement);
                    Thread.Sleep(80);
                }
            }
            catch { /* ignore */ }
        }

        return null;
    }

    public static void ResetNavScrollToTop(Window main)
    {
        foreach (var scroll in main.FindAllDescendants().Where(e => e.Patterns.Scroll.IsSupported))
        {
            try
            {
                var pattern = scroll.Patterns.Scroll.Pattern;
                if (pattern.VerticallyScrollable)
                    pattern.SetScrollPercent(-1, 0);
            }
            catch { /* ignore */ }
        }
    }

    public static void EnsurePageHostVisible(Window main)
    {
        var host = GetPageHost(main);
        if (host is null)
            return;

        try { host.Patterns.ScrollItem.Pattern.ScrollIntoView(); } catch { /* ignore */ }

        foreach (var scroll in host.FindAllDescendants().Where(e => e.Patterns.Scroll.IsSupported))
        {
            try
            {
                var pattern = scroll.Patterns.Scroll.Pattern;
                pattern.SetScrollPercent(-1, 100);
                pattern.SetScrollPercent(-1, 0);
            }
            catch { /* ignore */ }
        }
    }

    private static AutomationElement? ScanNavButtons(Window main, string pageTitleAr)
    {
        foreach (var btn in main.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)))
        {
            if (TryAutomationId(btn).StartsWith("Nav_", StringComparison.Ordinal) &&
                ButtonMatchesLabel(btn, pageTitleAr))
                return btn;

            if (ButtonMatchesLabel(btn, pageTitleAr))
                return btn;
        }

        return null;
    }

    private static bool ButtonMatchesLabel(AutomationElement element, string labelContains)
    {
        if (SafeName(element).Contains(labelContains, StringComparison.OrdinalIgnoreCase))
            return true;

        return element.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Any(t => SafeName(t).Contains(labelContains, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task WaitForPageDataAsync(Window main, int minRows, int timeoutMs = 10000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (CountGridRows(main) >= minRows || HasReportSummary(main))
                return;
            await Task.Delay(300);
        }
    }

    public static async Task<bool> WaitForDashboardKpisAsync(Window main, int minKpis = 1, int timeoutMs = 45_000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (CountDashboardKpis(main) >= minKpis)
                    return true;
            }
            catch
            {
                /* COM while dashboard loads */
            }

            await Task.Delay(500);
        }

        try { return CountDashboardKpis(main) >= minKpis; }
        catch { return false; }
    }

    public static string SafeNamePublic(AutomationElement? el) => SafeName(el);

    private static string SafeName(AutomationElement? el)
    {
        if (el is null) return string.Empty;
        try { return el.Name ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string TryAutomationId(AutomationElement el)
    {
        try { return el.AutomationId ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string TryItemStatus(AutomationElement el)
    {
        try { return el.ItemStatus ?? string.Empty; }
        catch { return string.Empty; }
    }

    public static AutomationElement? GetPageHost(Window main) =>
        main.FindFirstDescendant(cf => cf.ByAutomationId("PageHost"));

    public static int CountGridRows(Window main)
    {
        var host = GetPageHost(main);
        if (host is null) return 0;

        foreach (var el in host.FindAllDescendants())
        {
            var id = TryAutomationId(el);
            if (id is not (MainGridId or KpiGridId or ReportSummaryId))
                continue;

            var fromName = id == KpiGridId
                ? ParseCountPrefix(SafeName(el), "KpiCount:")
                : ParseCountPrefix(SafeName(el), "GridRows:");
            if (fromName >= 0)
                return fromName;

            if (el.ControlType == ControlType.Text && !string.IsNullOrWhiteSpace(SafeName(el)))
            {
                fromName = ParseCountPrefix(SafeName(el), "GridRows:");
                if (fromName >= 0) return fromName;
                fromName = ParseCountPrefix(SafeName(el), "KpiCount:");
                if (fromName >= 0) return fromName;
            }
        }

        var mainGrid = host.FindFirstDescendant(cf => cf.ByAutomationId(MainGridId))
            ?? host.FindAllDescendants().FirstOrDefault(e => TryAutomationId(e) == MainGridId);
        if (mainGrid is not null)
        {
            var fromName = ParseCountPrefix(SafeName(mainGrid), "GridRows:");
            if (fromName >= 0) return fromName;
            if (int.TryParse(TryItemStatus(mainGrid), out var published))
                return published;
        }

        var kpiGrid = host.FindFirstDescendant(cf => cf.ByAutomationId(KpiGridId))
            ?? host.FindFirstDescendant(cf => cf.ByAutomationId("KpiGrid"))
            ?? host.FindAllDescendants().FirstOrDefault(e =>
            {
                var id = TryAutomationId(e);
                return id is KpiGridId or "KpiGrid";
            });
        if (kpiGrid is not null)
        {
            var fromName = ParseCountPrefix(SafeName(kpiGrid), "KpiCount:");
            if (fromName >= 0) return fromName;
            if (int.TryParse(TryItemStatus(kpiGrid), out var kpiCount))
                return kpiCount;
        }

        if (HasReportSummary(host))
            return 1;

        var dataItems = host.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
        if (dataItems.Length > 0)
            return dataItems.Length;

        var grids = host.FindAllDescendants(cf => cf.ByControlType(ControlType.DataGrid));
        var total = 0;
        foreach (var grid in grids)
        {
            try
            {
                var fromName = ParseCountPrefix(SafeName(grid), "GridRows:");
                if (fromName >= 0) return fromName;
                total += grid.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem)).Length;
            }
            catch { /* ignore */ }
        }

        if (total == 0)
        {
            var tree = host.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
            if (tree is not null)
                total = tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).Length;
        }

        return total;
    }

    private static int ParseCountPrefix(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
            return -1;
        return int.TryParse(name[prefix.Length..], out var n) ? n : -1;
    }

    public static bool HasReportSummary(Window main) =>
        GetPageHost(main) is { } host && HasReportSummary(host);

    public static int CountDashboardKpis(Window main)
    {
        var host = GetPageHost(main);
        if (host is null) return 0;

        var kpiGrid = host.FindFirstDescendant(cf => cf.ByAutomationId(KpiGridId))
            ?? host.FindFirstDescendant(cf => cf.ByAutomationId("KpiGrid"))
            ?? host.FindAllDescendants().FirstOrDefault(e =>
            {
                var id = TryAutomationId(e);
                return id is KpiGridId or "KpiGrid";
            });
        if (kpiGrid is not null)
        {
            var fromName = ParseCountPrefix(SafeName(kpiGrid), "KpiCount:");
            if (fromName >= 0) return fromName;
            if (int.TryParse(TryItemStatus(kpiGrid), out var count))
                return count;
        }

        return host.FindAllDescendants()
            .Count(e => e.ControlType == ControlType.Text &&
                        SummaryKeywords.Any(k => SafeName(e).Contains(k, StringComparison.Ordinal)));
    }

    public static bool HasReportSummary(AutomationElement root)
    {
        var summary = root.FindFirstDescendant(cf => cf.ByAutomationId(ReportSummaryId));
        if (summary is not null && !string.IsNullOrWhiteSpace(SafeName(summary)))
            return true;

        return root.FindAllDescendants()
            .Any(t =>
            {
                var n = SafeName(t);
                return !string.IsNullOrWhiteSpace(n) &&
                       SummaryKeywords.Any(k => n.Contains(k, StringComparison.Ordinal));
            });
    }

    public static Button? FindPageButton(Window main, string labelContains)
    {
        EnsurePageHostVisible(main);

        var roots = new List<AutomationElement>();
        var host = GetPageHost(main);
        if (host is not null) roots.Add(host);
        roots.Add(main);

        foreach (var root in roots)
        {
            var btn = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .FirstOrDefault(b => ButtonMatchesLabel(b, labelContains))
                ?.AsButton();
            if (btn is not null)
                return btn;
        }

        return null;
    }

    public static async Task SelectComboItemContainingAsync(Window main, string textPart)
    {
        var host = GetPageHost(main);
        if (host is null) return;

        var combo = host.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox))?.AsComboBox();
        if (combo is null) return;

        try
        {
            var items = combo.Items;
            for (var i = 0; i < items.Length; i++)
            {
                if (SafeName(items[i]).Contains(textPart, StringComparison.OrdinalIgnoreCase))
                {
                    combo.Select(i);
                    await Task.Delay(400);
                    return;
                }
            }

            if (items.Length > 0)
            {
                combo.Select(0);
                await Task.Delay(400);
            }
        }
        catch { /* ignore */ }
    }

    public static bool SelectFirstGridRow(Window main)
    {
        var host = GetPageHost(main);
        if (host is null) return false;

        var row = host.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem))
            ?? host.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                ?.FindFirstChild(cf => cf.ByControlType(ControlType.DataItem));
        if (row is null) return false;

        try
        {
            row.Click();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task SafeInvokeAsync(Button button, int retries = 4)
    {
        Exception? last = null;
        for (var i = 0; i < retries; i++)
        {
            try
            {
                if (button.Patterns.Invoke.IsSupported)
                    button.Invoke();
                else
                    button.Click();
                return;
            }
            catch (Exception ex) when (IsTransientUia(ex) || IsUnsupportedInvoke(ex))
            {
                last = ex;
                try { button.Click(); return; }
                catch (Exception clickEx) { last = clickEx; }
                await Task.Delay(300 + i * 150);
            }
        }

        if (last is not null)
            throw last;
        button.Click();
    }

    private static bool IsUnsupportedInvoke(Exception ex) =>
        ex.Message.Contains("Invoke", StringComparison.OrdinalIgnoreCase) &&
        ex.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase);

    public static bool IsTransientUia(Exception ex) =>
        ex.HResult == unchecked((int)0x80040201) ||
        ex.Message.Contains("80040201", StringComparison.Ordinal) ||
        ex.Message.Contains("subscribers", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);

    public static async Task DismissBlockingUiAsync(UIA3Automation automation, Window main)
    {
        await DismissMessageBoxesAsync(automation, 5);
        for (var i = 0; i < 3; i++)
        {
            var dialog = FindChildDialog(automation, main)?.AsWindow();
            if (dialog is null)
                return;

            var cancel = dialog.FindFirstDescendant(cf => cf.ByName("إلغاء"))?.AsButton()
                ?? dialog.FindFirstDescendant(cf => cf.ByName("إغلاق"))?.AsButton()
                ?? dialog.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton();
            if (cancel is not null)
            {
                try { await SafeInvokeAsync(cancel); }
                catch { /* ignore */ }
            }
            else
            {
                try { dialog.Close(); } catch { /* ignore */ }
            }

            await Task.Delay(250);
        }
    }

    public static async Task DismissMessageBoxesAsync(UIA3Automation automation, int maxAttempts = 4)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(200);
            var desktop = automation.GetDesktop();
            var dialog = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(w =>
                {
                    var name = SafeName(w);
                    if (string.IsNullOrWhiteSpace(name)) return false;
                    if (name.Contains("BRQOSH ERP", StringComparison.OrdinalIgnoreCase)) return false;
                    if (name.Contains("تسجيل الدخول", StringComparison.Ordinal)) return false;
                    if (name.Contains("BRQOSH", StringComparison.OrdinalIgnoreCase)) return true;
                    return w.FindFirstDescendant(cf => cf.ByName("OK")) is not null
                        || w.FindFirstDescendant(cf => cf.ByName("موافق")) is not null;
                });

            if (dialog is null)
                return;

            var ok = dialog.FindFirstDescendant(cf => cf.ByName("OK"))?.AsButton()
                ?? dialog.FindFirstDescendant(cf => cf.ByName("موافق"))?.AsButton();
            ok?.Invoke();
        }
    }

    public static AutomationElement? FindChildDialog(UIA3Automation automation, Window main)
    {
        var mainHandle = main.Properties.NativeWindowHandle;
        return automation.GetDesktop().FindAllDescendants(cf => cf.ByControlType(ControlType.Window))
            .Where(w =>
            {
                if (w.Properties.NativeWindowHandle == mainHandle)
                    return false;

                var name = SafeName(w);
                if (string.IsNullOrWhiteSpace(name))
                    return false;
                if (name.Contains("BRQOSH ERP", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (name.Contains("تسجيل الدخول", StringComparison.Ordinal))
                    return false;
                return true;
            })
            .OrderByDescending(HasDialogButtons)
            .FirstOrDefault();
    }

    private static int HasDialogButtons(AutomationElement w) =>
        w.FindFirstDescendant(cf => cf.ByName("حفظ")) is not null ? 3 :
        w.FindFirstDescendant(cf => cf.ByName("إلغاء")) is not null ? 2 :
        w.FindFirstDescendant(cf => cf.ByName("موافق")) is not null ? 1 : 0;

    public static async Task<bool> CloseDialogAsync(UIA3Automation automation, Window main, bool save = false)
    {
        var dialog = FindChildDialog(automation, main)?.AsWindow();
        if (dialog is null)
            return false;

        var btnName = save ? "حفظ" : "إلغاء";
        var btn = dialog.FindFirstDescendant(cf => cf.ByName(btnName))?.AsButton()
            ?? dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .FirstOrDefault(b => b.Name.Contains(btnName, StringComparison.OrdinalIgnoreCase))
                ?.AsButton();

        if (btn is null && !save)
        {
            var close = dialog.FindFirstDescendant(cf => cf.ByName("إغلاق"))?.AsButton();
            if (close is not null)
                await SafeInvokeAsync(close);
        }
        else if (btn is not null)
        {
            await SafeInvokeAsync(btn);
        }

        await Task.Delay(350);
        await DismissMessageBoxesAsync(automation);
        return true;
    }

    public static void SetText(AutomationElement root, string labelHint, string value)
    {
        if (labelHint.Contains("كلمة المرور", StringComparison.Ordinal))
        {
            SetPassword(root, value);
            return;
        }

        var edits = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        foreach (var edit in edits)
        {
            var name = SafeName(edit);
            if (name.Contains(labelHint, StringComparison.OrdinalIgnoreCase))
            {
                edit.AsTextBox().Text = value;
                return;
            }

            var parent = edit.Parent;
            var labels = parent?.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)) ?? [];
            if (labels.Any(l => l.Name.Contains(labelHint, StringComparison.OrdinalIgnoreCase)))
            {
                edit.AsTextBox().Text = value;
                return;
            }
        }

        var byName = root.FindFirstDescendant(cf => cf.ByName(labelHint))?.AsTextBox();
        if (byName is not null)
        {
            byName.Text = value;
            return;
        }

        var first = edits.FirstOrDefault();
        if (first is not null)
            first.AsTextBox().Text = value;
    }

    public static async Task LoginAsync(Window login, string username, string password)
    {
        var userBox = login.FindFirstDescendant(cf => cf.ByAutomationId("LoginUsername"))?.AsTextBox()
            ?? login.FindFirstDescendant(cf => cf.ByName("اسم المستخدم"))?.AsTextBox();
        if (userBox is not null)
            userBox.Text = username;

        var reveal = login.FindFirstDescendant(cf => cf.ByAutomationId("LoginPasswordReveal"));
        var passPlain = login.FindFirstDescendant(cf => cf.ByAutomationId("LoginPasswordPlain"))?.AsTextBox();

        if (reveal is not null && reveal.Patterns.Toggle.IsSupported)
        {
            if (reveal.Patterns.Toggle.Pattern.ToggleState == ToggleState.Off)
                reveal.Patterns.Toggle.Pattern.Toggle();
            await Task.Delay(300);
            if (passPlain is not null)
                passPlain.Text = password;
            await Task.Delay(200);
            if (reveal.Patterns.Toggle.Pattern.ToggleState == ToggleState.On)
                reveal.Patterns.Toggle.Pattern.Toggle();
            await Task.Delay(300);
        }
        else if (passPlain is not null)
        {
            passPlain.Text = password;
        }

        SetPassword(login, password);

        var passTarget = login.FindFirstDescendant(cf => cf.ByAutomationId("LoginPasswordPlain"))
            ?? login.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)).ElementAtOrDefault(1);
        if (passTarget is not null)
        {
            try
            {
                passTarget.Focus();
                FlaUI.Core.Input.Keyboard.Type(password);
            }
            catch { /* fallback already applied */ }
        }

        await Task.Delay(250);

        var submit = login.FindFirstDescendant(cf => cf.ByAutomationId("LoginSubmit"))?.AsButton()
            ?? login.FindAllDescendants(cf => cf.ByName("تسجيل الدخول")).LastOrDefault()?.AsButton();
        if (submit is not null)
            await SafeInvokeAsync(submit);
        else
            FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ENTER);
    }

    public static async Task<(Window? Main, string? FailReason)> WaitForPostLoginAsync(
        UIA3Automation automation,
        Process appProcess,
        Window login,
        string testPass,
        int timeoutMs = 90_000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                appProcess.Refresh();
                if (appProcess.HasExited)
                    return (null, $"التطبيق أُغلق (exit {appProcess.ExitCode})");
            }
            catch
            {
                /* process handle may be stale */
            }

            var main = WaitForBrqoshWindow(automation, "BRQOSH ERP", 400);
            if (main is not null)
                return (main, null);

            var pwdChange = WaitForBrqoshWindow(automation, "تغيير كلمة المرور", 250)
                ?? WaitForBrqoshWindow(automation, "Password", 250);
            if (pwdChange is not null)
            {
                SetText(pwdChange, "كلمة المرور الجديدة", testPass);
                SetText(pwdChange, "تأكيد", testPass);
                var saveBtn = pwdChange.FindAllDescendants(cf => cf.ByName("حفظ")).LastOrDefault()?.AsButton()
                    ?? pwdChange.FindFirstDescendant(cf => cf.ByName("موافق"))?.AsButton();
                if (saveBtn is not null)
                    await SafeInvokeAsync(saveBtn);
                await Task.Delay(2000);
                continue;
            }

            var loginErr = TryReadLoginError(login);
            if (loginErr is not null)
                return (null, loginErr);

            await Task.Delay(500);
        }

        var late = WaitForBrqoshWindow(automation, "BRQOSH ERP", 3000);
        return late is not null ? (late, null) : (null, "مهلة النافذة الرئيسية بعد الدخول");
    }

    private static string? TryReadLoginError(Window login)
    {
        try
        {
            if (!login.IsAvailable)
                return null;

            var err = login.FindFirstDescendant(cf => cf.ByAutomationId("LoginError"));
            if (err is not null)
            {
                var text = SafeName(err);
                if (!string.IsNullOrWhiteSpace(text) &&
                    (text.Contains("غير صحيحة", StringComparison.Ordinal) ||
                     text.Contains("خطأ", StringComparison.Ordinal) ||
                     text.Contains("incorrect", StringComparison.OrdinalIgnoreCase)))
                    return text;
            }
        }
        catch
        {
            /* login window may have closed */
        }

        return null;
    }

    private static void SetPassword(AutomationElement root, string password)
    {
        foreach (var toggle in root.FindAllDescendants().Where(e => e.Patterns.Toggle.IsSupported))
        {
            try
            {
                if (toggle.Patterns.Toggle.Pattern.ToggleState == ToggleState.Off)
                    toggle.Patterns.Toggle.Pattern.Toggle();
            }
            catch { /* ignore */ }
        }

        Thread.Sleep(250);

        var edits = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        AutomationElement? passwordEdit = null;
        foreach (var edit in edits)
        {
            var name = SafeName(edit);
            if (name.Contains("كلمة المرور", StringComparison.OrdinalIgnoreCase))
            {
                passwordEdit = edit;
                break;
            }

            var parent = edit.Parent;
            var labels = parent?.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)) ?? [];
            if (labels.Any(l => l.Name.Contains("كلمة المرور", StringComparison.OrdinalIgnoreCase)))
            {
                passwordEdit = edit;
                break;
            }
        }

        passwordEdit ??= edits.Skip(1).FirstOrDefault() ?? edits.FirstOrDefault();
        if (passwordEdit is null)
            return;

        try
        {
            passwordEdit.AsTextBox().Text = password;
            return;
        }
        catch { /* PasswordBox may reject Text */ }

        try
        {
            passwordEdit.Focus();
            FlaUI.Core.Input.Keyboard.Type(password);
        }
        catch { /* ignore */ }
    }

    public static async Task<bool> TryFillDialogAsync(AutomationElement dialog, string? kind)
    {
        if (string.IsNullOrEmpty(kind))
            return true;

        var stamp = DateTime.Now.ToString("HHmmss");
        switch (kind)
        {
            case "customer":
                SetText(dialog, "اسم العميل", $"UI-AUDIT عميل {stamp}");
                SetText(dialog, "الرقم الضريبي", "311122233300003");
                return true;
            case "supplier":
                SetText(dialog, "اسم المورد", $"UI-AUDIT مورد {stamp}");
                SetText(dialog, "الرقم الضريبي", "312233344400003");
                return true;
            case "product":
                SetText(dialog, "كود", $"UI-P-{stamp}");
                SetText(dialog, "اسم الصنف", $"صنف واجهة {stamp}");
                return true;
            case "warehouse":
                SetText(dialog, "الكود", $"WH-{stamp}");
                SetText(dialog, "الاسم", $"مستودع {stamp}");
                return true;
            case "cost_center":
                SetText(dialog, "الكود", $"CC-{stamp}");
                SetText(dialog, "الاسم", $"مركز {stamp}");
                return true;
            case "bank_account":
                SetText(dialog, "الكود", $"BNK-{stamp}");
                SetText(dialog, "الاسم", $"بنك {stamp}");
                SetText(dialog, "رقم الحساب", "SA1234567890");
                return true;
            default:
                await Task.CompletedTask;
                return true;
        }
    }

    public static async Task<bool> WaitForDialogOpenAsync(UIA3Automation automation, Window main, int ms = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (FindChildDialog(automation, main) is not null)
                return true;
            await Task.Delay(100);
        }

        return false;
    }
}
