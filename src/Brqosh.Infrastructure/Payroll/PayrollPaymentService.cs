using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٨٤ · <c>treasury-payroll-assets#3</c>) <b>صرفُ الرواتب وسدادُ التأمينات ودفعُ مستحقّ نهاية الخدمة — وكان
/// <c>2108</c> و<c>2109</c> يُدانان ولا يُمَسّان.</b>
///
/// <para><b>ما كان:</b> ترحيلُ الدورة يُقيّد الصافيَ دائناً في <c>2108</c> والتأميناتِ (حصّتا الموظّف وصاحب العمل) في <c>2109</c>،
/// وتصفيةُ نهاية الخدمة تُقيّد المستحقَّ في <c>2108</c> — <b>ولا مسارَ واحدٌ يُدينهما</b>: سندُ الصرف يدفع للموردين وحدَهم. فالرواتبُ
/// تُدفع خارج الدفاتر، <b>والالتزامُ يتراكم شهراً بعد شهرٍ في الميزانيّة</b>، والنقدُ في الأستاذ أكبرُ ممّا في البنك بمجموع الرواتب كلِّها.</para>
///
/// <para><b>والمبلغُ من الأستاذ لا من الدورة</b>: ما دائنَه قيدُ الترحيل نفسُه على الحساب، ناقصاً ما صُرف تحت مفتاح الدورة —
/// فلا يُصرف مرّتين، ولا يُصرف لدورةٍ عُكست (عكسُها يُقاصّ قيدَها). <b>وعكسُ دورةٍ صُرفت يُرفض</b> في <see cref="PayrollReversalService"/>.</para>
/// </summary>
public class PayrollPaymentService(BrqoshDbContext db)
{
    public const string SalariesPrefix = "PayrollSalariesPaid:";
    public const string GosiPrefix = "PayrollGosiPaid:";
    public const string EndOfServicePrefix = "EndOfServicePaid:";

    private const string SalariesPayable = "2108";
    private const string GosiPayable = "2109";

    /// <summary>ما بقي من صافي رواتب الدورة بلا صرف.</summary>
    public Task<decimal> SalariesDueAsync(long companyId, long runId, CancellationToken ct = default) =>
        DueOnRunAsync(companyId, runId, SalariesPayable, SalariesPrefix, ct);

    /// <summary>ما بقي من تأمينات الدورة بلا سداد — حصّتا الموظّف وصاحب العمل.</summary>
    public Task<decimal> GosiDueAsync(long companyId, long runId, CancellationToken ct = default) =>
        DueOnRunAsync(companyId, runId, GosiPayable, GosiPrefix, ct);

    /// <summary>ما بقي من مستحقّ نهاية خدمة الموظّف بلا دفع — المكافأةُ وبدلُ الإجازة.</summary>
    public async Task<decimal> EndOfServiceDueAsync(long companyId, long employeeId, CancellationToken ct = default) =>
        await CreditedAsync(companyId, $"{EndOfServiceSettlementService.SourceDocumentPrefix}{employeeId}", SalariesPayable, ct)
        - await PaidAsync(companyId, $"{EndOfServicePrefix}{employeeId}", SalariesPayable, ct);

    public async Task<JournalEntry> PaySalariesAsync(long companyId, long runId, DateOnly paidOn, ReceiptMethod method,
        long? bankAccountId = null, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var run = await PostedRunAsync(companyId, runId, token);
            var due = await SalariesDueAsync(companyId, runId, token);
            return await PayAsync(companyId, paidOn, method, bankAccountId, SalariesPayable, due,
                $"{SalariesPrefix}{runId}", $"صرف رواتب {run.PeriodCode}", "PayrollRun", run.PeriodCode, token);
        }, ct);
    }

    public async Task<JournalEntry> PayGosiAsync(long companyId, long runId, DateOnly paidOn, ReceiptMethod method,
        long? bankAccountId = null, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var run = await PostedRunAsync(companyId, runId, token);
            var due = await GosiDueAsync(companyId, runId, token);
            return await PayAsync(companyId, paidOn, method, bankAccountId, GosiPayable, due,
                $"{GosiPrefix}{runId}", $"سداد التأمينات الاجتماعية {run.PeriodCode}", "PayrollRun", run.PeriodCode, token);
        }, ct);
    }

    public async Task<JournalEntry> PayEndOfServiceAsync(long companyId, long employeeId, DateOnly paidOn, ReceiptMethod method,
        long? bankAccountId = null, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var employee = await db.Employees
                .SingleOrDefaultAsync(e => e.CompanyId == companyId && e.Id == employeeId, token)
                ?? throw new AccountingException("الموظّف غير موجود.");
            var due = await EndOfServiceDueAsync(companyId, employeeId, token);
            return await PayAsync(companyId, paidOn, method, bankAccountId, SalariesPayable, due,
                $"{EndOfServicePrefix}{employeeId}", $"دفع مستحقّ نهاية خدمة {employee.NameAr}", "EndOfService", employee.Code, token);
        }, ct);
    }

    /// <summary>هل صُرف من الدورة شيء — رواتبُ أو تأمينات. <b>وحارسُ العكس يسأله</b>.</summary>
    internal async Task<bool> AnyPaidAsync(long companyId, long runId, CancellationToken ct) =>
        await PaidAsync(companyId, $"{SalariesPrefix}{runId}", SalariesPayable, ct) > 0m
        || await PaidAsync(companyId, $"{GosiPrefix}{runId}", GosiPayable, ct) > 0m;

    private async Task<PayrollRun> PostedRunAsync(long companyId, long runId, CancellationToken ct)
    {
        var run = await db.PayrollRuns.SingleOrDefaultAsync(r => r.CompanyId == companyId && r.Id == runId, ct)
                  ?? throw new AccountingException("دورة الرواتب غير موجودة.");
        if (run.Status != PayrollRunStatus.Posted)
            throw new AccountingException("لا يُصرف إلا لدورة رواتب مرحّلة.");
        return run;
    }

    private async Task<decimal> DueOnRunAsync(long companyId, long runId, string accountCode, string prefix, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking()
            .SingleOrDefaultAsync(r => r.CompanyId == companyId && r.Id == runId, ct);
        if (run is null || run.Status != PayrollRunStatus.Posted || run.JournalEntryId is not long entryId)
            return 0m;

        var credited = (await db.JournalEntries
                .Where(e => e.CompanyId == companyId && e.Id == entryId)
                .SelectMany(e => e.Lines)
                .Where(l => l.AccountCode == accountCode)
                .Select(l => new { l.Debit, l.Credit })
                .ToListAsync(ct))
            .Sum(l => l.Credit - l.Debit);

        return credited - await PaidAsync(companyId, $"{prefix}{runId}", accountCode, ct);
    }

    /// <summary>ما دائنَه مصدرٌ على الحساب — والمعكوسُ يُقاصّ عكسَه (<c>WhereReportable</c>).</summary>
    private async Task<decimal> CreditedAsync(long companyId, string source, string accountCode, CancellationToken ct) =>
        (await LinesAsync(companyId, source, accountCode, ct)).Sum(l => l.Credit - l.Debit);

    /// <summary>ما صُرف تحت مفتاحٍ على الحساب — والمعكوسُ يُقاصّ عكسَه.</summary>
    private async Task<decimal> PaidAsync(long companyId, string key, string accountCode, CancellationToken ct) =>
        (await LinesAsync(companyId, key, accountCode, ct)).Sum(l => l.Debit - l.Credit);

    private async Task<List<(decimal Debit, decimal Credit)>> LinesAsync(
        long companyId, string source, string accountCode, CancellationToken ct) =>
        (await db.JournalEntries
            .WhereReportable(companyId)
            .Where(e => e.SourceDocument == source)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == accountCode)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync(ct))
        .Select(l => (l.Debit, l.Credit))
        .ToList();

    private async Task<JournalEntry> PayAsync(long companyId, DateOnly paidOn, ReceiptMethod method, long? bankAccountId,
        string payableCode, decimal due, string key, string description, string auditType, string auditKey, CancellationToken ct)
    {
        if (due <= 0m)
            throw new AccountingException("لا مستحقَّ قائماً — صُرف كلُّه أو لا ترحيلَ له.");

        var payable = await SystemAccountProvisioning.EnsureAsync(db, companyId, payableCode, ct);
        var cashOrBank = method == ReceiptMethod.Cash
            ? await SystemAccountProvisioning.EnsureAsync(db, companyId, SaudiChartOfAccounts.SystemCodes.CashOnHand, ct)
            : await BankGlAccountResolver.ResolveAsync(db, companyId, bankAccountId, ct);

        await FinancialPostingSupport.ValidateAsync(db, companyId, paidOn, PostingDocumentKind.JournalEntry,
            accountIds: [payable.Id, cashOrBank.Id], ct: ct);

        var entry = JournalEntry.CreateDraft(companyId, paidOn, description, key);
        entry.AddLine(payable, due, 0m, description);
        entry.AddLine(cashOrBank, 0m, due, description);
        entry.Post(await new JournalEntryNumberGenerator(db).NextAsync(companyId, paidOn, ct));
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        await FinancialPostingSupport.AuditPostAsync(db, companyId, auditType, auditKey,
            reason: description, details: $"المبلغ {due:N2}", ct: ct);
        return entry;
    }
}
