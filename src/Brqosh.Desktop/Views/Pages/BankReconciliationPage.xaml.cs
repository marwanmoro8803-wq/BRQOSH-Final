using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Application.Treasury;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Microsoft.Win32;

namespace Brqosh.Desktop.Views.Pages;

public partial class BankReconciliationPage : UserControl
{
    public record BankOption(long Id, string Display);

    public class ReconRow : INotifyPropertyChanged
    {
        public long JournalEntryId { get; init; }
        public string EntryDate { get; init; } = "";
        public string? EntryNumber { get; init; }
        public string? Description { get; init; }
        public decimal Debit { get; init; }
        public decimal Credit { get; init; }

        private bool _isReconciled;
        public bool IsReconciled
        {
            get => _isReconciled;
            set { _isReconciled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReconciled))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class StatementRow
    {
        public int LineIndex { get; init; }
        public string TransactionDate { get; init; } = "";
        public string Description { get; init; } = "";
        public decimal SignedAmount { get; init; }
        public string AmountDisplay => SignedAmount.ToString("N2");
        public string MatchStatus { get; init; } = "";
        public bool IsOpen { get; init; }
    }

    private readonly ObservableCollection<ReconRow> _rows = [];
    private readonly ObservableCollection<StatementRow> _statementRows = [];
    private bool _loading;
    private long? _lastImportedStatementId;
    private int? _selectedStatementLineIndex;

    public BankReconciliationPage()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDate.SelectedDate = new DateTime(today.Year, today.Month, 1);
        ToDate.SelectedDate = today;
        Grid.ItemsSource = _rows;
        StatementGrid.ItemsSource = _statementRows;
        StatementGrid.SelectionChanged += (_, _) =>
        {
            _selectedStatementLineIndex = StatementGrid.SelectedItem is StatementRow row && row.IsOpen
                ? row.LineIndex
                : null;
        };
        UiTestAutomation.PublishGridCount(Grid, _rows.Count);
        Grid.CurrentCellChanged += async (_, _) => await OnReconcileToggleAsync();
        Loaded += async (_, _) => {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (RunBtn, PermissionService.CanPostAccounting),
                (ImportBtn, PermissionService.CanPostAccounting),
                (AutoMatchBtn, PermissionService.CanPostAccounting),
                (PostDifferenceBtn, PermissionService.CanPostAccounting));
            await UiFeedback.RunSafeAsync(LoadBanksAsync);
            await UiFeedback.RunSafeAsync(RunAsync);
        };
    }

    private async Task LoadBanksAsync()
    {
        await using var db = AppServices.CreateDb();
        var banks = await db.BankAccounts
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderByDescending(b => b.IsDefault).ThenBy(b => b.Code)
            .Select(b => new BankOption(b.Id, $"{b.Code} — {b.NameAr}"))
            .ToListAsync();
        BankCombo.ItemsSource = banks;
        if (banks.Count > 0)
            BankCombo.SelectedIndex = 0;
    }

    private async void OnRun(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RunAsync);

    private async Task RunAsync()
    {
        if (BankCombo.SelectedValue is not long bankId || FromDate.SelectedDate is null || ToDate.SelectedDate is null)
            return;

        _loading = true;
        await using var db = AppServices.CreateDb();
        var query = new BankReconciliationQuery(db);
        var from = DateOnly.FromDateTime(FromDate.SelectedDate.Value);
        var to = DateOnly.FromDateTime(ToDate.SelectedDate.Value);

        var lines = await query.GetBookLinesAsync(AppServices.CompanyId, bankId, from, to);
        var summary = await query.GetSummaryAsync(AppServices.CompanyId, bankId, from, to);

        _rows.Clear();
        foreach (var line in lines)
        {
            _rows.Add(new ReconRow
            {
                JournalEntryId = line.JournalEntryId,
                EntryDate = line.EntryDate.ToString("yyyy-MM-dd"),
                EntryNumber = line.EntryNumber,
                Description = line.Description,
                Debit = line.Debit,
                Credit = line.Credit,
                IsReconciled = line.IsReconciled,
            });
        }

        await LoadStatementLinesAsync(db, bankId);
        UpdateSummaryTexts(summary);
        _loading = false;
    }

    private async Task LoadStatementLinesAsync(Brqosh.Infrastructure.Persistence.BrqoshDbContext db, long bankId)
    {
        _statementRows.Clear();
        if (_lastImportedStatementId is not long statementId)
        {
            MatchSummaryText.Text = LocaleManager.T(
                "لا يوجد كشف مستورد — استورد CSV للمطابقة.",
                "No imported statement — import CSV to match.");
            return;
        }

        var statement = await db.BankStatements
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == statementId && s.BankAccountId == bankId);
        if (statement is null)
            return;

        foreach (var (line, index) in statement.Lines.Select((line, index) => (line, index)))
        {
            _statementRows.Add(new StatementRow
            {
                LineIndex = index,
                TransactionDate = line.TransactionDate.ToString("yyyy-MM-dd"),
                Description = line.Description,
                SignedAmount = line.SignedAmount,
                MatchStatus = line.IsMatched
                    ? LocaleManager.T("مطابق", "Matched")
                    : LocaleManager.T("معلق", "Open"),
                IsOpen = !line.IsMatched,
            });
        }

        var matched = statement.Lines.Count(l => l.IsMatched);
        var total = statement.Lines.Count;
        MatchSummaryText.Text = LocaleManager.T(
            $"ملخص المطابقة: {matched}/{total} سطر مطابق · الفارق: {total - matched} معلّق",
            $"Match summary: {matched}/{total} matched · {total - matched} open");
    }

    private void UpdateSummaryTexts(BankReconciliationSummary summary)
    {
        SummaryText.Text =
            $"رصيد افتتاحي: {summary.OpeningBalance:N2}  |  حركة الفترة: {summary.PeriodMovement:N2}  |  رصيد ختامي: {summary.ClosingBalance:N2}  |  مُسوّى: {summary.ReconciledBalance:N2}  |  غير مُسوّى: {summary.UnreconciledBalance:N2}  |  ({summary.ReconciledCount}/{summary.TotalLines})";
    }

    private async Task OnReconcileToggleAsync()
    {
        if (_loading || BankCombo.SelectedValue is not long bankId)
            return;

        if (Grid.CurrentItem is not ReconRow row)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var query = new BankReconciliationQuery(db);
            if (row.IsReconciled)
                await query.ReconcileAsync(AppServices.CompanyId, bankId, row.JournalEntryId, DateOnly.FromDateTime(DateTime.Today));
            else
                await query.UnreconcileAsync(AppServices.CompanyId, bankId, row.JournalEntryId);

            var summary = await query.GetSummaryAsync(AppServices.CompanyId, bankId,
                DateOnly.FromDateTime(FromDate.SelectedDate!.Value),
                DateOnly.FromDateTime(ToDate.SelectedDate!.Value));
            UpdateSummaryTexts(summary);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
            await RunAsync();
        }
    }

    private async void OnImportStatement(object sender, RoutedEventArgs e) =>
        await UiFeedback.RunSafeAsync(ImportStatementAsync);

    private async Task ImportStatementAsync()
    {
        if (BankCombo.SelectedValue is not long bankId)
        {
            UiFeedback.ShowWarning(LocaleManager.T("اختر حساباً بنكياً.", "Select a bank account."));
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "CSV (*.csv)|*.csv|All files|*.*",
            Title = LocaleManager.T("استيراد كشف بنكي", "Import bank statement"),
        };
        if (dialog.ShowDialog() != true)
            return;

        var csv = await File.ReadAllTextAsync(dialog.FileName);
        using var scope = AppServices.CreateInfrastructureScope();
        var import = scope.ServiceProvider.GetRequiredService<IBankStatementImportUseCase>();
        var result = await import.ImportCsvAsync(
            AppServices.CompanyId, bankId, Path.GetFileName(dialog.FileName), csv);
        _lastImportedStatementId = result.StatementId;
        UiFeedback.ShowInfo(LocaleManager.T(
            $"تم استيراد {result.LineCount} سطر.",
            $"Imported {result.LineCount} lines."));
        await RunAsync();
    }

    private async void OnAutoMatch(object sender, RoutedEventArgs e) =>
        await UiFeedback.RunSafeAsync(AutoMatchAsync);

    private async Task AutoMatchAsync()
    {
        if (_lastImportedStatementId is not long statementId)
        {
            UiFeedback.ShowWarning(LocaleManager.T("استورد كشفاً بنكياً أولاً.", "Import a bank statement first."));
            return;
        }

        using var scope = AppServices.CreateInfrastructureScope();
        var matcher = scope.ServiceProvider.GetRequiredService<IBankStatementMatchUseCase>();
        var report = await matcher.AutoMatchAsync(AppServices.CompanyId, statementId);
        UiFeedback.ShowInfo(LocaleManager.T(
            $"مطابقة: {report.MatchedCount} · متبقٍ {report.UnmatchedAfter}",
            $"Matched: {report.MatchedCount} · remaining {report.UnmatchedAfter}"));
        await RunAsync();
    }

    private async void OnPostDifference(object sender, RoutedEventArgs e) =>
        await UiFeedback.RunSafeAsync(PostDifferenceAsync);

    private async Task PostDifferenceAsync()
    {
        if (_lastImportedStatementId is not long statementId)
        {
            UiFeedback.ShowWarning(LocaleManager.T("استورد كشفاً بنكياً أولاً.", "Import a bank statement first."));
            return;
        }

        if (_selectedStatementLineIndex is not int lineIndex)
        {
            UiFeedback.ShowWarning(LocaleManager.T(
                "اختر سطراً معلّقاً من كشف البنك.",
                "Select an open statement line."));
            return;
        }

        using var scope = AppServices.CreateInfrastructureScope();
        var posting = scope.ServiceProvider.GetRequiredService<IBankStatementDifferencePostingUseCase>();
        var result = await posting.PostUnmatchedLineAsync(AppServices.CompanyId, statementId, lineIndex);

        UiFeedback.ShowInfo(LocaleManager.T(
            $"تم ترحيل قيد الفروق {result.EntryNumber}.",
            $"Difference entry {result.EntryNumber} posted."));
        await RunAsync();
    }
}
