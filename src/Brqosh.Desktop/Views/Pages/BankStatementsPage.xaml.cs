using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (CEEF-230 · ط6-٦) <b>كشوفُ الحسابات البنكية — والاستيرادُ كان بلا باب.</b>
///
/// <para><b>ما كان:</b> ثلاثُ خدماتٍ مبنيّةٌ — استيرادُ الكشف، والمطابقةُ الآلية، وترحيلُ
/// فروقه — <b>ولا شيءَ يستدعيها</b>. وشاشةُ التسوية البنكية القائمة <b>تعرض ما في القاعدة
/// ولا تستورد شيئاً</b>: فالمطابقةُ تعمل على بياناتٍ <b>لا سبيلَ لإدخالها</b>.</para>
///
/// <para><b>والتسويةُ البنكية أوّلُ ما يفعله المحاسبُ كلَّ شهر</b> — وبغير الاستيراد
/// يُطابِق بالعين سطراً سطراً من ورقة البنك.</para>
/// </summary>
public partial class BankStatementsPage : UserControl
{
    public record StatementRow(long Id, string FileName, int LineCount, int Unmatched);

    public record LineRow(int Index, string Date, string Description, string Reference,
        decimal Amount, string MatchState, bool IsMatched);

    private sealed record BankOption(long Id, string Label)
    {
        public override string ToString() => Label;
    }

    public BankStatementsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (ImportBtn, PermissionService.CanPostAccounting),
                (MatchBtn, PermissionService.CanPostAccounting),
                (PostDifferenceBtn, PermissionService.CanPostAccounting),
                // ‏(ح-٨٩) وعكسُ قيد الفروق بحارس العكس.
                (ReverseDifferenceBtn, PermissionService.CanCancelOrReverse));

            await LoadBanksAsync();
            await ReloadAsync();
        };
    }

    private async Task LoadBanksAsync()
    {
        await using var db = AppServices.CreateDb();
        var banks = await db.BankAccounts.AsNoTracking()
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderBy(b => b.NameAr)
            .Select(b => new { b.Id, b.NameAr })
            .ToListAsync();

        BankBox.ItemsSource = banks.Select(b => new BankOption(b.Id, b.NameAr)).ToList();
        if (banks.Count > 0) BankBox.SelectedIndex = 0;
    }

    private async Task ReloadAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();

            var statements = await db.BankStatements.AsNoTracking()
                .Include(s => s.Lines)
                .Where(s => s.CompanyId == AppServices.CompanyId)
                .OrderByDescending(s => s.Id)
                .ToListAsync();

            var rows = statements.Select(s => new StatementRow(s.Id, s.SourceFileName,
                s.Lines.Count, s.Lines.Count(l => !l.IsMatched))).ToList();

            StatementsGrid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(StatementsGrid, rows.Count);

            var unmatched = rows.Sum(r => r.Unmatched);

            // ‏**والسطرُ غيرُ المطابق ليس خطأً بالضرورة**: قد يكون رسماً بنكياً لم يُقيَّد
            // ‏بعد. **لكنّه دَينٌ على المحاسب حتّى يُفسَّر** — فيُعرض عددُه صريحاً.
            SummaryText.Text = rows.Count == 0
                ? "لا كشوفَ مستوردة — والمطابقةُ لا تعمل على فراغ."
                : $"كشوف: {rows.Count:N0} · سطور غير مطابقة: {unmatched:N0}"
                  + (unmatched > 0 ? " — وكلٌّ منها يحتاج تفسيرًا أو قيدَ فروق." : " — كلُّها مطابقة.");

            if (rows.Count > 0 && StatementsGrid.SelectedItem is null)
                StatementsGrid.SelectedIndex = 0;
            else
                await ReloadLinesAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnStatementSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await ReloadLinesAsync();
    }

    private async Task ReloadLinesAsync()
    {
        if (StatementsGrid.SelectedItem is not StatementRow statement)
        {
            LinesGrid.ItemsSource = null;
            UiTestAutomation.PublishGridCount(LinesGrid, 0);
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var loaded = await db.BankStatements.AsNoTracking()
                .Include(s => s.Lines)
                .SingleAsync(s => s.Id == statement.Id);

            var rows = loaded.Lines.Select((l, i) => new LineRow(i,
                l.TransactionDate.ToString("yyyy-MM-dd"), l.Description, l.Reference ?? "—",
                l.SignedAmount, l.IsMatched ? "مطابق" : "غير مطابق", l.IsMatched)).ToList();

            LinesGrid.ItemsSource = rows;
            UiTestAutomation.PublishGridCount(LinesGrid, rows.Count);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnImport(object sender, RoutedEventArgs e)
    {
        if (BankBox.SelectedItem is not BankOption bank)
        {
            UiFeedback.ShowWarning("اختر الحساب البنكي الذي يخصه الكشف.");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "ملف نصي مفصول بفواصل (*.csv)|*.csv|كل الملفات (*.*)|*.*",
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            // ‏**ويُقرأ بترميزٍ يكشف نفسَه**: كشوفُ البنوك تخرج بترميزاتٍ مختلفة،
            // ‏**وقراءةٌ بترميزٍ خاطئ تُفسد البياناتِ لا الأسماءَ وحدَها** — فمرجعُ
            // ‏الحوالة يصير رموزاً ولا يُطابق شيئاً.
            var csv = await File.ReadAllTextAsync(dialog.FileName, Encoding.UTF8);

            using var facade = new DesktopAccountingFacade();
            var statement = await facade.GetService<BankStatementImportService>().ImportCsvAsync(
                AppServices.CompanyId, bank.Id, Path.GetFileName(dialog.FileName), csv);

            await ReloadAsync();
            SummaryText.Text = $"استُورد الكشف «{statement.SourceFileName}» بـ{statement.Lines.Count:N0} سطرًا.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnMatch(object sender, RoutedEventArgs e)
    {
        if (StatementsGrid.SelectedItem is not StatementRow statement) return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var report = await facade.GetService<BankStatementMatcherService>()
                .AutoMatchAsync(AppServices.CompanyId, statement.Id);

            await ReloadAsync();

            SummaryText.Text = $"طُوبق {report.MatchedCount:N0} سطرًا · "
                + $"وبقي {report.UnmatchedAfter:N0} غير مطابق من {report.UnmatchedBefore:N0}.";
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٨٩) <b>عكسُ قيد فروق السطر المحدّد</b> — فيُحرَّر السطرُ ويُرحَّل من جديد على الحساب الصحيح. <b>وكان فرقٌ
    /// رُحِّل على حسابٍ خطأٍ يبقى.</b>
    /// </summary>
    private async void OnReverseDifference(object sender, RoutedEventArgs e)
    {
        if (StatementsGrid.SelectedItem is not StatementRow statement) return;
        if (LinesGrid.SelectedItem is not LineRow line || !line.IsMatched)
        {
            UiFeedback.ShowWarning("اختر سطراً مطابَقاً بقيد فروق.");
            return;
        }

        var reversalDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ العكس", "تاريخ قيد عكس الفروق:");
        if (reversalDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<BankStatementDifferencePostingService>().ReverseAsync(
                AppServices.CompanyId, statement.Id, line.Index, reversalDate.Value, SecurityContext.ResolveUsername());

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnPostDifference(object sender, RoutedEventArgs e)
    {
        if (StatementsGrid.SelectedItem is not StatementRow statement) return;
        if (LinesGrid.SelectedItem is not LineRow line) return;

        if (line.IsMatched)
        {
            UiFeedback.ShowWarning("السطر مطابق — ولا يُنشأ له قيد فروق.");
            return;
        }

        if (UiDialog.Show("ترحيل السطر قيدَ فروقٍ على حساب المصروفات البنكية؟",
                line.Description, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<BankStatementDifferencePostingService>()
                .PostUnmatchedLineAsync(AppServices.CompanyId, statement.Id, line.Index);

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
