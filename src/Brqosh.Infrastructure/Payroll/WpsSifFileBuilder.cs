using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>
/// ‏بياناتُ المنشأة في ترويسة ملفّ الأجور — <b>من الإعداد لا من الكود</b> (Wps:* أو BRQOSH_WPS_*).
/// </summary>
/// <param name="BankSarieId">‏[DEST-ID] رمزُ SARIE لبنك المنشأة (أربعة أحرف، مثل RJHI).</param>
/// <param name="EstablishmentId">‏[ESTB-ID] رقمُ المنشأة لدى بنكها.</param>
/// <param name="EstablishmentIban">‏[BANK-ACC] آيبانُ حساب الرواتب الذي يُخصم منه.</param>
/// <param name="MolEstablishmentId">‏[MOL-ESTBID] رقمُ المنشأة في وزارة الموارد البشريّة (مثل 1-2345678).</param>
public sealed record WpsSifEstablishment(
    string BankSarieId, string EstablishmentId, string EstablishmentIban, string MolEstablishmentId)
{
    public static WpsSifEstablishment FromConfiguration(Func<string, string?> read)
    {
        string Get(string key, string env) =>
            (read(key) ?? Environment.GetEnvironmentVariable(env) ?? string.Empty).Trim();
        return new WpsSifEstablishment(
            Get("Wps:BankSarieId", "BRQOSH_WPS_BANK_SARIE_ID").ToUpperInvariant(),
            Get("Wps:EstablishmentId", "BRQOSH_WPS_ESTABLISHMENT_ID"),
            Get("Wps:EstablishmentIban", "BRQOSH_WPS_ESTABLISHMENT_IBAN").Replace(" ", "").ToUpperInvariant(),
            Get("Wps:MolEstablishmentId", "BRQOSH_WPS_MOL_ESTABLISHMENT_ID"));
    }
}

/// <summary>‏الملفُّ، واسمُه، وما لم يُصدَّر مُسمّىً — ومحتوى فارغٌ إن كانت الترويسةُ نفسُها ناقصة.</summary>
public sealed record WpsSifResult(string Content, string FileName, int Records, decimal TotalNet, IReadOnlyList<string> Refusals);

/// <summary>
/// <b>ملفُّ الأجور بتخطيط نظام حماية الأجور (SIF) الذي يُرفع إلى مُدد / البنك.</b>
///
/// <para>‏التخطيطُ من «WPS Wages File Technical Specification» (وزارة الموارد البشريّة): مجموعةُ ترويسة
/// [DEST-ID] [ESTB-ID] [BANK-ACC] [32A-CCY] [32A-VAL] [32A-AMT] [D-DATE] [FILE-REF] [FILE-REJCDE] [MOL-ESTBID]،
/// ثمّ سطرٌ لكلّ موظّف [32B-AMT] [59-ACC] [59-NAME] [57-BANK] [70-DET] [RET-CODE] [MOL-BAS] [MOL-HAL] [MOL-OEA]
/// [MOL-DED] [MOL-ID] [TRN-REF] [TRN-STATUS] [TRN-DATE] — وحقولُ الإرجاع (RET-CODE · FILE-REJCDE · TRN-*) يملؤها البنك فتبقى فارغة.</para>
///
/// <para>‏<b>والمتطابقةُ تُفرض سطراً سطراً</b>: الأساسيّ + السكن + الأخرى − الاستقطاعات = الصافي، والغيابُ في الاستقطاعات؛
/// ومجموعُ الصوافي = [32A-AMT]. وآيبانٌ لا يجتاز فحصَ mod-97 أو بنكٌ لا يُعرف رمزُه يُسمّى ولا يُصدَّر.</para>
///
/// <para>‏<b>وبنكٌ بعينه قد يطلب فاصلاً أو ترتيباً آخر</b> — هذا التخطيطُ المرجعيّ بفاصلةٍ ونقطةٍ عشريّة؛ يُراجَع مع البنك قبل أوّل رفع.</para>
/// </summary>
public class WpsSifFileBuilder(BrqoshDbContext db, TimeProvider? clock = null)
{
    public const string HeaderTags =
        "[DEST-ID],[ESTB-ID],[BANK-ACC],[32A-CCY],[32A-VAL],[32A-AMT],[D-DATE],[FILE-REF],[FILE-REJCDE],[MOL-ESTBID]";

    public const string ContentTags =
        "[32B-AMT],[59-ACC],[59-NAME],[57-BANK],[70-DET],[RET-CODE],[MOL-BAS],[MOL-HAL],[MOL-OEA],[MOL-DED],[MOL-ID],[TRN-REF],[TRN-STATUS],[TRN-DATE]";

    /// <summary>‏رمزُ البنك في الآيبان السعوديّ (الخانتان 5-6) ⟵ رمزُ SARIE.</summary>
    public static readonly IReadOnlyDictionary<string, string> SarieByIbanBankCode = new Dictionary<string, string>
    {
        ["05"] = "INMA", ["10"] = "NCBK", ["15"] = "ALBI", ["20"] = "RIBL", ["30"] = "ARNB", ["40"] = "SAMB",
        ["45"] = "SABB", ["50"] = "AAAL", ["55"] = "BSFR", ["60"] = "BJAZ", ["65"] = "SIBC", ["80"] = "RJHI",
    };

    private static readonly Regex SaudiIbanShape = new(@"^SA\d{22}$", RegexOptions.CultureInvariant);
    private static readonly Regex MolEstablishmentShape = new(@"^\d{1,3}-\d{1,10}$", RegexOptions.CultureInvariant);
    private static readonly Regex GovernmentIdShape = new(@"^[12]\d{9}$", RegexOptions.CultureInvariant);

    public async Task<WpsSifResult> BuildAsync(
        long companyId, long payrollRunId, WpsSifEstablishment establishment, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == payrollRunId && r.CompanyId == companyId, ct)
            ?? throw new AccountingException("دورة الرواتب غير موجودة.");

        if (run.Status != PayrollRunStatus.Posted)
            throw new AccountingException(
                "لا يُصدَّر ملفُّ الأجور إلا من دورةٍ مرحّلة — والمسودّةُ تتغيّر بعد التصدير.");

        var fileName = $"WPS_{Safe(establishment.MolEstablishmentId)}_{Safe(run.PeriodCode)}.csv";
        var headerProblems = ValidateEstablishment(establishment);
        if (headerProblems.Count > 0)
            return new WpsSifResult(string.Empty, fileName, 0, 0m, headerProblems);

        var employeeIds = run.Lines.Where(l => l.EmployeeId is not null).Select(l => l.EmployeeId!.Value).ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        var refusals = new List<string>();
        var body = new StringBuilder();
        var records = 0;
        var totalNet = 0m;
        var detail = Field($"SALARY {run.PeriodCode}");

        foreach (var line in run.Lines)
        {
            if (line.EmployeeId is not long id || !employees.TryGetValue(id, out var employee))
            {
                refusals.Add($"«{line.EmployeeName}» — سطرٌ بلا سجلِّ موظّف، فلا هويّةَ ولا آيبان.");
                continue;
            }

            var iban = (employee.Iban ?? string.Empty).Replace(" ", "").ToUpperInvariant();
            if (!IsValidSaudiIban(iban))
            {
                refusals.Add($"«{employee.NameAr}» ({employee.Code}) — آيبانٌ مفقودٌ أو غيرُ صحيح.");
                continue;
            }

            if (!SarieByIbanBankCode.TryGetValue(iban.Substring(4, 2), out var bank))
            {
                refusals.Add($"«{employee.NameAr}» ({employee.Code}) — رمزُ بنك الآيبان {iban.Substring(4, 2)} غيرُ معروف.");
                continue;
            }

            if (!GovernmentIdShape.IsMatch(employee.NationalId ?? string.Empty))
            {
                refusals.Add($"«{employee.NameAr}» ({employee.Code}) — رقمُ الهويّة/الإقامة ليس عشرةَ أرقامٍ تبدأ بـ1 أو 2.");
                continue;
            }

            var basic = line.BasicSalary;
            var housing = line.HousingAllowance;
            var other = line.TransportAllowance + line.OtherAllowance;
            var deductions = line.GosiEmployee + line.AdvanceDeduction + line.AbsenceDeduction;
            if (basic + housing + other - deductions != line.NetPay)
            {
                refusals.Add($"«{employee.NameAr}» ({employee.Code}) — المكوّناتُ لا تساوي الصافي.");
                continue;
            }

            body.AppendLine(string.Join(',',
                Money(line.NetPay), iban, Field(employee.NameAr), bank, detail, string.Empty,
                Money(basic), Money(housing), Money(other), Money(deductions), employee.NationalId,
                string.Empty, string.Empty, string.Empty));
            records++;
            totalNet += line.NetPay;
        }

        totalNet = Math.Round(totalNet, 2, MidpointRounding.AwayFromZero);
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var fileRef = $"BRQ{run.Id}T{now:yyyyMMddHHmmss}";

        var content = new StringBuilder();
        content.AppendLine(HeaderTags);
        content.AppendLine(string.Join(',',
            establishment.BankSarieId, establishment.EstablishmentId, establishment.EstablishmentIban, "SAR",
            run.PayDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture), Money(totalNet), string.Empty,
            fileRef, string.Empty, establishment.MolEstablishmentId));
        content.AppendLine(ContentTags);
        content.Append(body);

        return new WpsSifResult(records == 0 ? string.Empty : content.ToString(), fileName, records, totalNet, refusals);
    }

    public static IReadOnlyList<string> ValidateEstablishment(WpsSifEstablishment e)
    {
        var problems = new List<string>();
        if (!SarieByIbanBankCode.Values.Contains(e.BankSarieId))
            problems.Add("[DEST-ID] رمزُ SARIE لبنك المنشأة مفقودٌ أو غيرُ معروف (Wps:BankSarieId).");
        if (string.IsNullOrWhiteSpace(e.EstablishmentId) || !e.EstablishmentId.All(char.IsAsciiLetterOrDigit))
            problems.Add("[ESTB-ID] رقمُ المنشأة لدى البنك مفقود (Wps:EstablishmentId).");
        if (!IsValidSaudiIban(e.EstablishmentIban))
            problems.Add("[BANK-ACC] آيبانُ حساب الرواتب مفقودٌ أو غيرُ صحيح (Wps:EstablishmentIban).");
        if (!MolEstablishmentShape.IsMatch(e.MolEstablishmentId ?? string.Empty))
            problems.Add("[MOL-ESTBID] رقمُ المنشأة في الوزارة بصيغة 1-2345678 مفقود (Wps:MolEstablishmentId).");
        return problems;
    }

    /// <summary>‏آيبانٌ سعوديّ: SA + 22 رقماً، ويجتاز فحصَ ISO 13616 (mod 97 = 1).</summary>
    public static bool IsValidSaudiIban(string? iban)
    {
        if (iban is null || !SaudiIbanShape.IsMatch(iban)) return false;
        var rearranged = iban[4..] + iban[..4];
        var digits = new StringBuilder();
        foreach (var ch in rearranged)
            digits.Append(char.IsDigit(ch) ? ch.ToString() : (ch - 'A' + 10).ToString(CultureInfo.InvariantCulture));
        return BigInteger.Parse(digits.ToString(), CultureInfo.InvariantCulture) % 97 == 1;
    }

    private static string Field(string value)
    {
        var clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Contains(',') || clean.Contains('"') ? '"' + clean.Replace("\"", "\"\"") + '"' : clean;
    }

    private static string Safe(string value) =>
        new(value.Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '_').ToArray());

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
