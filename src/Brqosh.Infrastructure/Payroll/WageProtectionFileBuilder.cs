using System.Globalization;
using System.Text;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>سطرُ موظّفٍ في ملفّ الأجور، وما تعذّر تصديرُه يُسمّى.</summary>
public record WageProtectionResult(string Csv, int Records, decimal TotalNet, IReadOnlyList<string> Refusals);

/// <summary>
/// <b>ملفُّ حماية الأجور.</b>
///
/// <para><b>ما كان:</b> صفرٌ تام — لا ملفَّ ولا حقلاً ولا ذكراً. <b>ونظامُ حماية الأجور
/// إلزاميٌّ في السعودية</b>: الأجورُ تُدفع عبره، والمنشأةُ التي لا تُقدّم ملفَّها تُعلَّق
/// خدماتُها. فمنشأةٌ على هذا النظام كانت تُعدّ ملفَّها في جدولٍ خارجيّ من بياناتٍ ليست
/// في النظام أصلاً.</para>
///
/// <para><b>وما يمنع التصدير يُسمّى ولا يُبتلع:</b> موظّفٌ بلا آيبان، أو سطرٌ يدويٌّ بلا
/// سجلٍّ. <b>وملفٌّ ناقصٌ يُرفَض عند الجهة بلا بيانِ سبب</b> — فيُقال السببُ هنا، بالاسم،
/// قبل أن يُرسَل.</para>
///
/// <para><b>والتخطيطُ يختلف بالبنك</b>: هذا مخرَجٌ قياسيٌّ بالحقول التي يفرضها النظام —
/// هويّةٌ واسمٌ وآيبانٌ وأساسيٌّ وسكنٌ وبدلاتٌ واستقطاعاتٌ وصافٍ — <b>ويُقرّ بأنّ بنكاً
/// بعينه قد يطلب ترتيباً آخر</b>. وهو إقرارٌ لا اعتذار: البديلُ ادّعاءُ توافقٍ مع كلّ بنك.</para>
/// </summary>
public class WageProtectionFileBuilder(BrqoshDbContext db)
{
    public async Task<WageProtectionResult> BuildAsync(
        long companyId, long payrollRunId, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == payrollRunId && r.CompanyId == companyId, ct)
            ?? throw new AccountingException("دورة الرواتب غير موجودة.");

        if (run.Status != PayrollRunStatus.Posted)
            throw new AccountingException(
                "لا يُصدَّر ملفُّ الأجور إلا من دورةٍ مرحّلة — والمسودّةُ تتغيّر بعد التصدير.");

        var company = await db.Companies.AsNoTracking()
            .SingleAsync(c => c.Id == companyId, ct);

        var employeeIds = run.Lines.Where(l => l.EmployeeId is not null)
            .Select(l => l.EmployeeId!.Value).ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        var refusals = new List<string>();
        var rows = new StringBuilder();

        // ‏ترويسةٌ صريحةُ الأسماء — فملفٌّ بلا ترويسةٍ يُقرأ بالترتيب وحده، وعمودٌ يزيح
        // ‏عموداً يُرسل بدلَ السكن استقطاعاً بلا ما يكشفه.
        rows.AppendLine("NationalId,EmployeeName,Iban,BasicSalary,HousingAllowance,"
                        + "OtherAllowances,Deductions,NetSalary");

        var records = 0;
        decimal totalNet = 0m;

        foreach (var line in run.Lines)
        {
            if (line.EmployeeId is not long id || !employees.TryGetValue(id, out var employee))
            {
                refusals.Add($"«{line.EmployeeName}» — سطرٌ بلا سجلِّ موظّف، فلا هويّةَ ولا آيبان.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(employee.Iban))
            {
                refusals.Add($"«{employee.NameAr}» ({employee.Code}) — بلا آيبان.");
                continue;
            }

            var other = line.TransportAllowance + line.OtherAllowance;

            // ‏**والاستقطاعاتُ كلُّها، لا التأميناتِ وحدَها.**
            //
            // ‏أضافت `ط2-٥` استقطاعَ السُّلفة إلى `NetPay` **ولم تُضفه إلى هذا العمود**.
            // ‏فيخرج الملفُّ بمتطابقةٍ مكسورة — `الإجماليّ − الاستقطاعات ≠ الصافي` —
            // ‏**والجهةُ تفحصها فترفض الملفَّ كلَّه**، وسببُ الرفض لا يظهر في النظام.
            var deductions = line.GosiEmployee + line.AdvanceDeduction;

            rows.AppendLine(string.Join(',',
                employee.NationalId,
                Csv(employee.NameAr),
                employee.Iban,
                Money(line.BasicSalary),
                Money(line.HousingAllowance),
                Money(other),
                Money(deductions),
                Money(line.NetPay)));

            records++;
            totalNet += line.NetPay;
        }

        return new WageProtectionResult(rows.ToString(), records,
            Math.Round(totalNet, 2, MidpointRounding.AwayFromZero), refusals);
    }

    /// <summary>‏اسمٌ فيه فاصلةٌ يُقتبَس — وإلا أزاح كلَّ عمودٍ بعده.</summary>
    private static string Csv(string value) =>
        value.Contains(',') || value.Contains('"')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;

    private static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);
}
