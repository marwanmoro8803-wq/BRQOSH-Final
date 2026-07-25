using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Payroll;

public enum PayrollRunStatus
{
    Draft = 0,
    Posted = 1,
    Reversed = 2,
}

/// <summary>سطر راتب موظف في دورة رواتب.</summary>
public class PayrollLine
{
    public string EmployeeName { get; private set; }
    public decimal BasicSalary { get; private set; }
    public decimal HousingAllowance { get; private set; }
    public decimal TransportAllowance { get; private set; }
    public decimal GosiEmployee { get; private set; }
    public decimal GosiEmployer { get; private set; }

    public decimal GrossPay => BasicSalary + HousingAllowance + TransportAllowance;
    public decimal NetPay => GrossPay - GosiEmployee;

    private PayrollLine()
    {
        EmployeeName = null!;
    }

    internal PayrollLine(string employeeName, decimal basic, decimal housing, decimal transport,
        decimal gosiEmployee, decimal gosiEmployer)
    {
        EmployeeName = employeeName;
        BasicSalary = basic;
        HousingAllowance = housing;
        TransportAllowance = transport;
        GosiEmployee = gosiEmployee;
        GosiEmployer = gosiEmployer;
    }
}

/// <summary>دورة رواتب شهرية — تُرحَّل بقيد استحقاق.</summary>
public class PayrollRun : IBranchScoped
{
    private readonly List<PayrollLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public string PeriodCode { get; private set; }
    public DateOnly PayDate { get; private set; }
    public PayrollRunStatus Status { get; private set; } = PayrollRunStatus.Draft;
    public long? JournalEntryId { get; private set; }

    public IReadOnlyList<PayrollLine> Lines => _lines.AsReadOnly();
    public decimal TotalGross => _lines.Sum(l => l.GrossPay);
    public decimal TotalNet => _lines.Sum(l => l.NetPay);
    public decimal TotalGosiEmployer => _lines.Sum(l => l.GosiEmployer);

    private PayrollRun()
    {
        PeriodCode = null!;
    }

    private PayrollRun(long companyId, string periodCode, DateOnly payDate)
    {
        CompanyId = companyId;
        PeriodCode = periodCode;
        PayDate = payDate;
    }

    public static PayrollRun CreateDraft(long companyId, int year, int month, DateOnly payDate)
    {
        return new PayrollRun(companyId, $"{year:D4}-{month:D2}", payDate);
    }

    public void AddLine(string employeeName, decimal basic, decimal housing = 0m, decimal transport = 0m,
        decimal gosiEmployee = 0m, decimal gosiEmployer = 0m)
    {
        if (Status != PayrollRunStatus.Draft)
            throw new AccountingException("لا يمكن تعديل دورة مرحّلة.");
        if (string.IsNullOrWhiteSpace(employeeName))
            throw new AccountingException("اسم الموظف مطلوب.");

        _lines.Add(new PayrollLine(employeeName.Trim(),
            Math.Round(basic, 2, MidpointRounding.AwayFromZero),
            Math.Round(housing, 2, MidpointRounding.AwayFromZero),
            Math.Round(transport, 2, MidpointRounding.AwayFromZero),
            Math.Round(gosiEmployee, 2, MidpointRounding.AwayFromZero),
            Math.Round(gosiEmployer, 2, MidpointRounding.AwayFromZero)));
    }

    public JournalEntry Post(Account salariesExpense, Account gosiExpense, Account salariesPayable, Account gosiPayable)
    {
        if (Status != PayrollRunStatus.Draft)
            throw new AccountingException("الدورة مرحّلة مسبقاً.");
        if (_lines.Count == 0)
            throw new AccountingException("لا توجد سطور رواتب.");

        var entry = JournalEntry.CreateDraft(CompanyId, PayDate,
            $"رواتب {PeriodCode}", $"Payroll:{PeriodCode}");

        entry.AddLine(salariesExpense, TotalGross, 0m, "مصروف رواتب");
        if (TotalGosiEmployer > 0)
            entry.AddLine(gosiExpense, TotalGosiEmployer, 0m, "تأمينات اجتماعية — صاحب عمل");
        entry.AddLine(salariesPayable, 0m, TotalNet, "رواتب مستحقة");
        var gosiTotal = _lines.Sum(l => l.GosiEmployee) + TotalGosiEmployer;
        if (gosiTotal > 0)
            entry.AddLine(gosiPayable, 0m, gosiTotal, "تأمينات مستحقة");

        Status = PayrollRunStatus.Posted;
        return entry;
    }

    public void LinkJournal(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkReversed()
    {
        if (Status != PayrollRunStatus.Posted)
            throw new AccountingException("لا يُعكس إلا دورة مرحّلة.");
        Status = PayrollRunStatus.Reversed;
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        if (Status != PayrollRunStatus.Draft)
            throw new AccountingException("لا يُعيَّن الفرع إلا على دورة رواتب مسودة.");
        BranchId = branchId;
    }

}
