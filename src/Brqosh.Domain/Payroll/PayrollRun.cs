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
    /// <summary>
    /// (CEEF-230 · ط2-١) معرّفُ الموظّف — <b>و<c>null</c> يعني سطراً كُتب يدوياً</b> قبل
    /// وجود سجلّ الموظفين، أو لشخصٍ لا سجلَّ له.
    ///
    /// <para><b>والاسمُ يبقى مُخزَّناً معه لا يُستبدَل به</b>: قسيمةُ شهرٍ مضى يجب أن
    /// تُقرأ بالاسم الذي كان — <b>وموظّفٌ يُعدَّل اسمُه لا يُعيد كتابة تاريخه</b>.</para>
    /// </summary>
    public long? EmployeeId { get; private set; }

    public string EmployeeName { get; private set; }
    public decimal BasicSalary { get; private set; }
    public decimal HousingAllowance { get; private set; }
    public decimal TransportAllowance { get; private set; }
    public decimal GosiEmployee { get; private set; }
    public decimal GosiEmployer { get; private set; }

    public decimal GrossPay =>
        BasicSalary + HousingAllowance + TransportAllowance + OtherAllowance;
    public decimal NetPay => GrossPay - GosiEmployee;

    private PayrollLine()
    {
        EmployeeName = null!;
    }

    /// <summary>بدلاتٌ أخرى — كانت تُطوى في الأساسيّ فيختفي أثرُها.</summary>
    public decimal OtherAllowance { get; private set; }

    internal PayrollLine(string employeeName, decimal basic, decimal housing, decimal transport,
        decimal gosiEmployee, decimal gosiEmployer, long? employeeId = null, decimal other = 0m)
    {
        EmployeeId = employeeId;
        OtherAllowance = other;
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
        decimal gosiEmployee = 0m, decimal gosiEmployer = 0m,
        long? employeeId = null, decimal other = 0m)
    {
        if (Status != PayrollRunStatus.Draft)
            throw new AccountingException("لا يمكن تعديل دورة مرحّلة.");
        if (string.IsNullOrWhiteSpace(employeeName))
            throw new AccountingException("اسم الموظف مطلوب.");

        // (CEEF-230 · ط2-١) ‏**ولا يُدرَج موظّفٌ مرّتين في دورةٍ واحدة**: راتبٌ مزدوجٌ
        // ‏لشخصٍ واحد، وملفُّ أجورٍ ترفضه الجهةُ المستقبِلة. والسطورُ اليدويةُ بلا معرّفٍ
        // ‏لا يشملها المنع — فلا هويّةَ تُقارَن.
        if (employeeId is long id && _lines.Any(l => l.EmployeeId == id))
            throw new AccountingException($"الموظف «{employeeName}» مُدرَجٌ مرّتين في هذه الدورة.");

        _lines.Add(new PayrollLine(employeeName.Trim(),
            Math.Round(basic, 2, MidpointRounding.AwayFromZero),
            Math.Round(housing, 2, MidpointRounding.AwayFromZero),
            Math.Round(transport, 2, MidpointRounding.AwayFromZero),
            Math.Round(gosiEmployee, 2, MidpointRounding.AwayFromZero),
            Math.Round(gosiEmployer, 2, MidpointRounding.AwayFromZero),
            employeeId,
            Math.Round(other, 2, MidpointRounding.AwayFromZero)));
    }

    /// <summary>
    /// (CEEF-230 · ط2-٢) <b>يُضيف موظّفاً من سجلّه، وتُحسب تأميناتُه.</b>
    ///
    /// <para><b>وهذا هو الفرق كلُّه:</b> كان صاحبُ المنشأة يكتب حصّتَي التأمينات بيده في
    /// ورقةٍ خارجية كلَّ شهر. <b>وهنا تُحسب من الأجر الخاضع والجنسية والنِّسَب المُخزَّنة</b>
    /// — فلا تُنسى، ولا تُنسخ خطأً، ولا تختلف بين موظّفٍ وآخر بلا سبب.</para>
    /// </summary>
    public void AddEmployee(Employee employee, GosiPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(policy);

        if (employee.CompanyId != CompanyId)
            throw new AccountingException($"الموظف «{employee.NameAr}» ليس من هذه المنشأة.");

        // ‏**ولا يُدرَج مَن ليس على رأس العمل في يوم الصرف**: موظّفٌ انتهت خدمتُه في
        // ‏مارس لا يُصرف له راتبُ أبريل. وحارسٌ في النطاق أوثقُ من ترشيحٍ في شاشة.
        if (!employee.IsEmployedOn(PayDate))
            throw new AccountingException(
                $"«{employee.NameAr}» ليس على رأس العمل في {PayDate:yyyy-MM-dd}.");

        var gosi = policy.ContributionFor(employee);

        AddLine(employee.NameAr, employee.BasicSalary, employee.HousingAllowance,
            employee.TransportAllowance, gosi.Employee, gosi.Employer,
            employee.Id, employee.OtherAllowance);
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
