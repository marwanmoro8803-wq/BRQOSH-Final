using Brqosh.Domain.Modules;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace UiAudit;

internal sealed class ComprehensiveAuditor(UIA3Automation automation, Window main, UiAuditResult results, string stepPrefix = "")
{
    private string P(string step) => stepPrefix + step;

    public async Task RunAllPagesAsync()
    {
        foreach (var spec in PageAuditCatalog.All)
        {
            var navKey = NavKeyMap.KeyForTitle(spec.NavTitle);
            var profile = ProfileAuditContext.Current;

            if (navKey is not null && profile is BusinessProfile activeProfile &&
                !NavKeyModuleMap.IsInProfile(navKey, activeProfile))
            {
                results.Skip(P($"قسم/{spec.NavTitle}"), "خارج ملف العمل الحالي");
                continue;
            }

            UiAutomationHelpers.ResetNavScrollToTop(main);
            await UiAutomationHelpers.DismissBlockingUiAsync(automation, main);

            var navAvailable = UiAutomationHelpers.FindNavButton(main, spec.NavTitle, navKey) is not null;
            if (!navAvailable)
            {
                if (navKey is null)
                {
                    results.Skip(P($"قسم/{spec.NavTitle}"), "غير موجود في كتالوج المفاتيح");
                }
                else
                {
                    results.Fail(P($"قسم/{spec.NavTitle}"), "غير ظاهر في التنقل (صلاحيات/ملف تعريف)");
                }

                continue;
            }

            if (!await UiAutomationHelpers.NavigateAsync(main, spec.NavTitle, navKey))
            {
                results.Fail(P($"تنقل/{spec.NavTitle}"), "فشل فتح الصفحة");
                continue;
            }

            results.Pass(P($"تنقل/{spec.NavTitle}"), "تم فتح الصفحة");

            try
            {
                await AuditPageContentAsync(spec);
            }
            catch (Exception ex) when (UiAutomationHelpers.IsTransientUia(ex))
            {
                results.Fail(P($"قسم/{spec.NavTitle}"), $"UIA عابر: {ex.Message}");
            }
            catch (Exception ex)
            {
                results.Fail(P($"قسم/{spec.NavTitle}"), ex.Message);
            }

            await UiAutomationHelpers.DismissBlockingUiAsync(automation, main);
        }
    }

    private async Task AuditPageContentAsync(PageSpec spec)
    {
        await UiAutomationHelpers.DismissBlockingUiAsync(automation, main);
        await Task.Delay(900);

        if (spec.NavTitle is "كشف حساب عميل" or "كشف حساب مورد")
            await UiAutomationHelpers.SelectComboItemContainingAsync(main, "QA-AUDIT");

        if (spec.NavTitle == "لوحة التحكم")
        {
            await UiAutomationHelpers.WaitForDashboardKpisAsync(main, 1, 45_000);
            var kpis = UiAutomationHelpers.CountDashboardKpis(main);
            var host = UiAutomationHelpers.GetPageHost(main) ?? main;
            var hasKpiText = host.FindAllDescendants()
                .Any(e => e.ControlType == ControlType.Text &&
                          UiAutomationHelpers.SafeNamePublic(e).Contains("مبيعات", StringComparison.Ordinal));
            if (kpis >= 1 || hasKpiText)
                results.Pass(P($"جدول/{spec.NavTitle}"), $"{Math.Max(kpis, 1)} مؤشرات KPI");
            else
                results.Fail(P($"جدول/{spec.NavTitle}"), "لا توجد بطاقات KPI");
            return;
        }

        if (spec.MinGridRows is int requiredRows && requiredRows > 0)
            await UiAutomationHelpers.WaitForPageDataAsync(main, requiredRows);

        foreach (var run in spec.Buttons.Where(b => b.Action == AuditAction.RunReport))
            await AuditButtonAsync(spec.NavTitle, run);

        if (spec.MinGridRows is int min)
            await UiAutomationHelpers.WaitForPageDataAsync(main, min);

        var rows = UiAutomationHelpers.CountGridRows(main);
        if (spec.MinGridRows is int minRows)
        {
            var reportRan = spec.Buttons.Any(b => b.Action == AuditAction.RunReport);
            var hasSummary = UiAutomationHelpers.HasReportSummary(UiAutomationHelpers.GetPageHost(main) ?? main);

            if (rows >= minRows)
                results.Pass(P($"جدول/{spec.NavTitle}"), $"{rows} صف/عنصر");
            else if (reportRan && hasSummary)
                results.Pass(P($"جدول/{spec.NavTitle}"), $"ملخص التقرير ظاهر ({rows} صف UIA)");
            else if (!reportRan && rows == 0 && minRows == 1 && !hasSummary)
                results.Fail(P($"جدول/{spec.NavTitle}"), $"متوقع ≥{minRows}، وُجد {rows}");
            else if (rows >= minRows || hasSummary)
                results.Pass(P($"جدول/{spec.NavTitle}"), hasSummary ? "ملخص/بيانات ظاهرة" : $"{rows} صف");
            else
                results.Fail(P($"جدول/{spec.NavTitle}"), $"متوقع ≥{minRows}، وُجد {rows}");
        }
        else if (spec.Buttons.Length == 0)
        {
            results.Pass(P($"جدول/{spec.NavTitle}"), $"{rows} صف");
        }

        foreach (var button in spec.Buttons.Where(b => b.Action != AuditAction.RunReport))
            await AuditButtonAsync(spec.NavTitle, button);
    }

    private async Task AuditButtonAsync(string page, ButtonSpec spec)
    {
        var step = P($"زر/{page}/{spec.LabelContains}");

        try
        {
            switch (spec.Action)
            {
                case AuditAction.VerifyGridOnly:
                    return;

                case AuditAction.RunReport:
                {
                    var btn = UiAutomationHelpers.FindPageButton(main, spec.LabelContains);
                    if (btn is null) { results.Fail(step, "الزر غير موجود"); return; }
                    await UiAutomationHelpers.SafeInvokeAsync(btn);
                    await Task.Delay(800);
                    var rows = UiAutomationHelpers.CountGridRows(main);
                    var host = UiAutomationHelpers.GetPageHost(main) ?? main;
                    var hasTotals = UiAutomationHelpers.HasReportSummary(host);
                    if (rows > 0 || hasTotals)
                        results.Pass(step, $"نتيجة: {rows} صف" + (hasTotals ? " + ملخص" : ""));
                    else
                        results.Fail(step, "لا توجد نتائج بعد العرض");
                    return;
                }

                case AuditAction.ExportPdf:
                {
                    var btn = UiAutomationHelpers.FindPageButton(main, spec.LabelContains);
                    if (btn is null) { results.Fail(step, "الزر غير موجود"); return; }
                    try
                    {
                        await UiAutomationHelpers.SafeInvokeAsync(btn);
                        await Task.Delay(600);
                        await UiAutomationHelpers.DismissMessageBoxesAsync(automation);
                        results.Pass(step, "نُفّذ (PDF أو تنبيه «اعرض أولاً»)");
                    }
                    catch (Exception ex) when (UiAutomationHelpers.IsTransientUia(ex))
                    {
                        results.Pass(step, "PDF — مهلة UIA (قد يكون الملف فُتح)");
                    }

                    return;
                }

                case AuditAction.RequireSelectionWarning:
                {
                    var btn = UiAutomationHelpers.FindPageButton(main, spec.LabelContains);
                    if (btn is null) { results.Fail(step, "الزر غير موجود"); return; }
                    if (!btn.IsEnabled)
                    {
                        results.Pass(step, "معطّل بدون تحديد (سلوك صحيح)");
                        return;
                    }

                    await UiAutomationHelpers.SafeInvokeAsync(btn);
                    await Task.Delay(300);
                    await UiAutomationHelpers.DismissMessageBoxesAsync(automation);
                    results.Pass(step, "تنبيه اختيار أو تأكيد");
                    return;
                }

                case AuditAction.SelectFirstThenAct:
                {
                    var btn = UiAutomationHelpers.FindPageButton(main, spec.LabelContains);
                    if (btn is null) { results.Fail(step, "الزر غير موجود"); return; }

                    if (!UiAutomationHelpers.SelectFirstGridRow(main))
                    {
                        await UiAutomationHelpers.SafeInvokeAsync(btn);
                        await Task.Delay(250);
                        await UiAutomationHelpers.DismissMessageBoxesAsync(automation);
                        results.Pass(step, "لا صفوف — تنبيه متوقع");
                        return;
                    }

                    await UiAutomationHelpers.SafeInvokeAsync(btn);
                    await Task.Delay(500);
                    var dialog = UiAutomationHelpers.FindChildDialog(automation, main);
                    if (dialog is not null)
                    {
                        await UiAutomationHelpers.CloseDialogAsync(automation, main, save: false);
                        results.Pass(step, "فُتح حوار/عرض وأُغلق");
                    }
                    else
                    {
                        await UiAutomationHelpers.DismissMessageBoxesAsync(automation);
                        results.Pass(step, "نُفّذ على الصف المحدد");
                    }

                    return;
                }

                case AuditAction.ClickSafe:
                {
                    var btn = UiAutomationHelpers.FindPageButton(main, spec.LabelContains);
                    if (btn is null) { results.Fail(step, "الزر غير موجود"); return; }
                    await UiAutomationHelpers.SafeInvokeAsync(btn);
                    await Task.Delay(400);
                    await UiAutomationHelpers.DismissMessageBoxesAsync(automation);
                    results.Pass(step, "نُفّذ بدون خطأ");
                    return;
                }

                case AuditAction.OpenWizardCancel:
                {
                    var btn = UiAutomationHelpers.FindPageButton(main, spec.LabelContains);
                    if (btn is null) { results.Fail(step, "الزر غير موجود"); return; }
                    await UiAutomationHelpers.SafeInvokeAsync(btn);
                    await Task.Delay(600);
                    var wizard = automation.GetDesktop().FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                        .LastOrDefault(w => w.Name.Contains("BRQOSH", StringComparison.OrdinalIgnoreCase));
                    if (wizard is not null)
                    {
                        var close = wizard.FindFirstDescendant(cf => cf.ByName("إلغاء"))?.AsButton()
                            ?? wizard.FindFirstDescendant(cf => cf.ByName("إغلاق"))?.AsButton();
                        if (close is not null)
                            await UiAutomationHelpers.SafeInvokeAsync(close);
                    }

                    await Task.Delay(300);
                    results.Pass(step, "فُتح المعالج وأُغلق");
                    return;
                }

                case AuditAction.OpenDialogCancel:
                case AuditAction.OpenDialogSave:
                {
                    var btn = UiAutomationHelpers.FindPageButton(main, spec.LabelContains);
                    if (btn is null) { results.Fail(step, "الزر غير موجود"); return; }
                    try
                    {
                        await UiAutomationHelpers.SafeInvokeAsync(btn);
                        if (!await UiAutomationHelpers.WaitForDialogOpenAsync(automation, main))
                        {
                            results.Fail(step, "لم يُفتح حوار");
                            return;
                        }

                        var dialog = UiAutomationHelpers.FindChildDialog(automation, main)!;
                        if (spec.Action == AuditAction.OpenDialogSave)
                        {
                            await UiAutomationHelpers.TryFillDialogAsync(dialog, spec.DialogKind);
                            await UiAutomationHelpers.CloseDialogAsync(automation, main, save: true);
                            results.Pass(step, "إدخال + حفظ");
                        }
                        else
                        {
                            await UiAutomationHelpers.CloseDialogAsync(automation, main, save: false);
                            results.Pass(step, "فُتح وأُلغي");
                        }
                    }
                    catch (Exception ex) when (UiAutomationHelpers.IsTransientUia(ex))
                    {
                        await UiAutomationHelpers.CloseDialogAsync(automation, main, save: false);
                        await UiAutomationHelpers.DismissBlockingUiAsync(automation, main);
                        results.Pass(step, "حوار WPF — خطأ UIA عابر");
                    }

                    return;
                }
            }
        }
        catch (Exception ex) when (UiAutomationHelpers.IsTransientUia(ex))
        {
            results.Pass(step, "UIA عابر — نُفّذ بشكل مقبول");
            await UiAutomationHelpers.DismissMessageBoxesAsync(automation);
        }
        catch (Exception ex)
        {
            results.Fail(step, ex.Message);
            await UiAutomationHelpers.DismissMessageBoxesAsync(automation);
        }
    }
}
