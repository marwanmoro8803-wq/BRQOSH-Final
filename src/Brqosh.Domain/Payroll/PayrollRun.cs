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

    /// <summary>
    /// (CEEF-230 · ط2-٥) <b>قسطُ السُّلفة المُستقطَع هذا الشهر.</b>
    ///
    /// <para><b>وبغير موضعٍ له كان الموظّفُ يقبض راتبَه كاملاً وقد أخذ سُلفتَه</b> —
    /// فتدفع المنشأةُ مرّتين، ولا يُكتشف إلا عند الاستقالة.</para>
    /// </summary>
    public decimal AdvanceDeduction { get; private set; }

    public decimal GrossPay =>
        BasicSalary + HousingAllowance + TransportAllowance + OtherAllowance;

    /// <summary><b>والصافي بعد التأمينات والسُّلفة معاً</b> — وكلاهما يخرج من جيب الموظّف.</summary>
    public decimal NetPay => GrossPay - GosiEmployee - AdvanceDeduction;

    private PayrollLine()
    {
        EmployeeName = null!;
    }

    /// <summary>بدلاتٌ أخرى — كانت تُطوى في الأساسيّ فيختفي أثرُها.</summary>
    public decimal OtherAllowance { get; private set; }

    internal PayrollLine(string employeeName, decimal basic, decimal housing, decimal transport,
        decimal gosiEmployee, decimal gosiEmployer, long? employeeId = null, decimal other = 0m,
        decimal advanceDeduction = 0m)
    {
        AdvanceDeduction = advanceDeduction;
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

    /// <summary>(CEEF-230 · ط2-٥) مجموعُ أقساط السُّلَف — <b>وهو ما يُعفي حسابَ 1116</b>.</summary>
    public decimal TotalAdvanceDeduction => _lines.Sum(l => l.AdvanceDeduction);

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
        long? employeeId = null, decimal other = 0m, decimal advanceDeduction = 0m)
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
            Math.Round(other, 2, MidpointRounding.AwayFromZero),
            Math.Round(advanceDeduction, 2, MidpointRounding.AwayFromZero)));

        // (CEEF-230 · ط2-٥) ‏**واستقطاعٌ يبتلع الصافيَ يُرفض.**
        //
        // ‏الاستقطاعُ يُنقص ما يقبضه الموظّف، **وصافٍ سالبٌ يعني أنّ الموظّف يدفع للمنشأة
        // ‏من جيبه** — ويُخرج ملفَّ حماية الأجور بمبلغٍ سالبٍ ترفضه المنظومة. والحدُّ
        // ‏هنا **صفرٌ لا أقلّ**: قسطٌ يبلغ الصافيَ كلَّه معناه أنّ الشهرَ كلَّه ذهب للسُّلفة،
        // ‏**وهو قرارٌ يجوز** — أمّا ما دونه فلا.
        var line = _lines[^1];
        if (line.NetPay < 0m)
        {
            _lines.RemoveAt(_lines.Count - 1);
            throw new AccountingException(
                $"استقطاعُ «{employeeName.Trim()}» {advanceDeduction:N2} يتجاوز صافيَ أجره "
                + $"{line.GrossPay - line.GosiEmployee:N2} — والصافي لا يكون سالباً.");
        }
    }

    /// <summary>
    /// (CEEF-230 · ط2-٢) <b>يُضيف موظّفاً من سجلّه، وتُحسب تأميناتُه.</b>
    ///
    /// <para><b>وهذا هو الفرق كلُّه:</b> كان صاحبُ المنشأة يكتب حصّتَي التأمينات بيده في
    /// ورقةٍ خارجية كلَّ شهر. <b>وهنا تُحسب من الأجر الخاضع والجنسية والنِّسَب المُخزَّنة</b>
    /// — فلا تُنسى، ولا تُنسخ خطأً، ولا تختلف بين موظّفٍ وآخر بلا سبب.</para>
    /// </summary>
    public void AddEmployee(Employee employee, GosiPolicy policy, decimal advanceDeduction = 0m)
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
            employee.Id, employee.OtherAllowance, advanceDeduction);
    }

    /// <summary>الأساسيُّ وحده — وهو ما يذهب إلى حساب الرواتب والأجور.</summary>
    public decimal TotalBasic => _lines.Sum(l => l.BasicSalary);

    /// <summary>البدلاتُ كلُّها — سكنٌ ونقلٌ وغيرُهما.</summary>
    public decimal TotalAllowances =>
        _lines.Sum(l => l.HousingAllowance + l.TransportAllowance + l.OtherAllowance);

    /// <param name="employeeCostCenters">
    /// (CEEF-232 · م-١) مركزُ تكلفةِ كلِّ موظّف — <b>وبه تُفكَّك مصروفاتُ الأجر على مراكزها</b>.
    ///
    /// <para><b>وكان الحقلُ موجوداً ولا يقرؤه أحد:</b> <c>Employee.CostCenterId</c> مُخزَّنٌ
    /// في المخطَّط <b>و<c>AssignCostCenter</c> بلا مستدعٍ واحد</b>، وقيدُ الرواتب سطرٌ
    /// مُجمَّعٌ بلا بُعد. <b>فالميزانُ سليمٌ وكلُّ قائمةِ مركزِ تكلفةٍ تُسقط أكبرَ بنودها</b> —
    /// إذ <c>CostCenterStatementQuery</c> يُرشّح على <c>CostCenterId</c> في السطر.</para>
    ///
    /// <para><b>ومَن لا مركزَ له يبقى في سطرٍ بلا بُعد</b> — لا يُنسب إلى مركزٍ بالتخمين.
    /// <b>واختياريٌّ في التوقيع</b>: مستدعٍ لا يُمرّره يسلك السلوكَ القديم حرفاً.</para>
    /// </param>
    public JournalEntry Post(Account salariesExpense, Account gosiExpense, Account salariesPayable,
        Account gosiPayable, Account? allowancesExpense = null, Account? advancesReceivable = null,
        IReadOnlyDictionary<long, long?>? employeeCostCenters = null)
    {
        if (Status != PayrollRunStatus.Draft)
            throw new AccountingException("الدورة مرحّلة مسبقاً.");
        if (_lines.Count == 0)
            throw new AccountingException("لا توجد سطور رواتب.");

        var entry = JournalEntry.CreateDraft(CompanyId, PayDate,
            $"رواتب {PeriodCode}", $"Payroll:{PeriodCode}");

        // (CEEF-230 · ط2-٤) ‏**البدلاتُ تُفصَل عن الأساسيّ.**
        //
        // ‏كان الإجماليُّ كلُّه يذهب إلى `5201`، و`5202 بدلات السكن والانتقال` **حسابٌ
        // ‏ميّتٌ في الشجرة**. فمالكٌ يسأل «كم أنفقتُ على بدلات السكن؟» لا يجد جواباً —
        // ‏والرقمُ موجودٌ في كلّ سطرِ رواتبَ ولا يخرج منه.
        //
        // ‏**والمجموعُ لا يتغيّر**، فلا تنكسر هويّةٌ ولا يتحرّك صافي الربح. وحسابُ البدلات
        // ‏اختياريٌّ في التوقيع: مستدعٍ لا يُمرّره يسلك السلوكَ القديم حرفاً.
        if (allowancesExpense is not null && TotalAllowances > 0m)
        {
            AddExpense(entry, salariesExpense, l => l.BasicSalary, "مصروف رواتب", employeeCostCenters);
            AddExpense(entry, allowancesExpense,
                l => l.HousingAllowance + l.TransportAllowance + l.OtherAllowance,
                "بدلات السكن والانتقال", employeeCostCenters);
        }
        else
        {
            AddExpense(entry, salariesExpense,
                l => l.BasicSalary + l.HousingAllowance + l.TransportAllowance + l.OtherAllowance,
                "مصروف رواتب", employeeCostCenters);
        }
        if (TotalGosiEmployer > 0)
            AddExpense(entry, gosiExpense, l => l.GosiEmployer,
                "تأمينات اجتماعية — صاحب عمل", employeeCostCenters);
        entry.AddLine(salariesPayable, 0m, TotalNet, "رواتب مستحقة");

        // (CEEF-230 · ط2-٥) ‏**والاستقطاعُ يُعفي السُّلفةَ لا يُنقص المصروف.**
        //
        // ‏المصروفُ وقع كاملاً — الموظّفُ استحقّ أجرَه كلَّه. **والمُستقطَعُ سدادُ دَينٍ
        // ‏سابق**، فيُقيَّد دائناً على `1116` لا خصماً من `5201`. **وإنقاصُ المصروف
        // ‏يُظهر شهراً برواتبَ أقلَّ بلا سببٍ تشغيليّ**، ويُبقي الأصلَ في الميزانية بعد
        // ‏استرداده.
        if (TotalAdvanceDeduction > 0m)
        {
            if (advancesReceivable is null)
                throw new AccountingException(
                    "استقطاعُ سُلفٍ بلا حساب «سُلَف الموظفين» — والقيدُ لا يُعفي ما لا حسابَ له.");

            entry.AddLine(advancesReceivable, 0m, TotalAdvanceDeduction, "سداد سُلَف بالاستقطاع");
        }
        var gosiTotal = _lines.Sum(l => l.GosiEmployee) + TotalGosiEmployer;
        if (gosiTotal > 0)
            entry.AddLine(gosiPayable, 0m, gosiTotal, "تأمينات مستحقة");

        Status = PayrollRunStatus.Posted;
        return entry;
    }

    /// <summary>
    /// (CEEF-232 · م-١) يُضيف مصروفَ أجرٍ <b>مفكَّكاً على مراكز التكلفة</b> — أو سطراً
    /// واحداً حين لا خريطةَ ولا مركز.
    /// </summary>
    /// <remarks>
    /// <b>والمجموعُ لا يتغيّر بريال</b>: التفكيكُ يوزّع المبلغَ نفسَه على أبعادٍ، فلا
    /// تتحرّك قائمةُ الدخل ولا ينكسر توازن. <b>والصفرُ لا يُضاف سطراً</b> — ومركزٌ لا
    /// موظّفَ له في هذا الشهر لا يُذكر.
    ///
    /// <para><b>والترتيبُ ثابتٌ بالمعرّف</b>: قيدٌ يُعاد توليدُه يُخرج السطورَ بالترتيب
    /// نفسِه، <b>ولولا ذلك اختلف رقمُ السطر بين تشغيلين</b> فتعذّرت المقارنة.</para>
    /// </remarks>
    private void AddExpense(JournalEntry entry, Account account,
        Func<PayrollLine, decimal> amountOf, string description,
        IReadOnlyDictionary<long, long?>? employeeCostCenters)
    {
        var total = Math.Round(_lines.Sum(amountOf), 2, MidpointRounding.AwayFromZero);
        if (total <= 0m) return;

        if (employeeCostCenters is null || employeeCostCenters.Count == 0)
        {
            entry.AddLine(account, total, 0m, description);
            return;
        }

        var buckets = _lines
            .GroupBy(l => l.EmployeeId is long id && employeeCostCenters.TryGetValue(id, out var cc)
                ? cc
                : null)
            .Select(g => (CostCenterId: g.Key,
                          Amount: Math.Round(g.Sum(amountOf), 2, MidpointRounding.AwayFromZero)))
            .Where(b => b.Amount != 0m)
            .OrderBy(b => b.CostCenterId ?? long.MaxValue)
            .ToList();

        // ‏**وفرقُ التقريب يُحمَل على أكبر وعاء** — فمجموعُ الأوعية المقرَّبة قد يخالف
        // ‏المجموعَ المقرَّب بهللة، **وهللةٌ واحدةٌ تُسقط القيدَ كلَّه** عند حارس التوازن.
        var drift = total - buckets.Sum(b => b.Amount);
        if (drift != 0m && buckets.Count > 0)
        {
            var largest = buckets
                .Select((b, i) => (b, i))
                .OrderByDescending(x => Math.Abs(x.b.Amount))
                .First().i;
            buckets[largest] = (buckets[largest].CostCenterId, buckets[largest].Amount + drift);
        }

        foreach (var (costCenterId, amount) in buckets)
            entry.AddLine(account, amount, 0m, description, costCenterId);
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
