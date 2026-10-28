using System.IO;

namespace Brqosh.VendorIssuer;

public record SelfTestRow(string Name, bool Ok, string Detail);

/// <summary>
/// <b>الاختبارُ الذاتيّ — لأنّ نسخةَ قواعدٍ لا تُقاس تفارق أصلَها صامتة.</b>
///
/// <para>هذه الأداةُ تحمل نسخةً من ترميز المنتج (<c>NumericLicense.cs</c>)، ونسخةٌ كهذه
/// لا تنكسر بضجيج: تُصدر مفاتيحَ تبدو سليمةً وتُرفض على أجهزة العملاء. فتُقاس هنا
/// بمتّجهاتٍ <b>موجَبةٍ وسالبةٍ معاً</b> — والسالبةُ هي المهمّة: بوّابةٌ لا تَرفض شيئاً
/// عمياءُ وإن قالت «نجح».</para>
///
/// <para>وإن أخفق فحصٌ واحدٌ <b>يُمنع الإصدار</b> — لا يُعرض تحذيرٌ يُتجاوز.</para>
/// </summary>
public static class SelfTest
{
    private const string Company = "شركة برقوش للإنشاءات";
    private const string OtherCompany = "منشأة أخرى";
    private const string Machine = "CF0304A7C4C4ABA1";
    private const string OtherMachine = "0123456789ABCDEF";
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    public static List<SelfTestRow> RunAll()
    {
        var rows = new List<SelfTestRow>();
        void Row(string name, bool ok, string detail) => rows.Add(new SelfTestRow(name, ok, detail));

        var annual = LicenseCodec.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 10), Machine, Company);
        var trial = LicenseCodec.Encode(LicensePlan.Trial7, new DateOnly(2026, 1, 1), Machine, Company);
        var lifetime = LicenseCodec.Encode(LicensePlan.Lifetime, new DateOnly(2026, 1, 1), Machine, Company);

        // ① ذهابٌ وعودة: الخطةُ وتاريخُ الانتهاء كما وُضعا.
        var verdict = LicenseCodec.Decode(annual, Machine, Company, Now, out var plan, out var expires, out var issued);
        Row("السنويّ يُفكّ إلى خطته وتاريخه",
            verdict == NumericLicenseVerdict.Valid && plan == LicensePlan.Annual
            && issued == new DateOnly(2026, 1, 10)
            && expires == new DateTime(2027, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            $"{verdict} · {LicenseCodec.FormatDate(expires)}");

        // ② التجريبيُّ سبعةُ أيام: صالحٌ في السادس، منتهٍ في الثامن.
        var daySix = LicenseCodec.Decode(trial, Machine, Company,
            new DateTime(2026, 1, 6, 23, 59, 0, DateTimeKind.Utc), out _, out _, out _);
        var dayEight = LicenseCodec.Decode(trial, Machine, Company,
            new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc), out _, out _, out _);
        Row("التجريبيّ ينتهي بعد سبعة أيام بالتمام",
            daySix == NumericLicenseVerdict.Valid && dayEight == NumericLicenseVerdict.Expired,
            $"{daySix} / {dayEight}");

        // ③ الدائمُ لا ينتهي.
        Row("الدائمُ صالحٌ في 2099",
            LicenseCodec.Decode(lifetime, Machine, Company, new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                out var lifePlan, out var lifeExpires, out _) == NumericLicenseVerdict.Valid
            && lifePlan == LicensePlan.Lifetime && lifeExpires == DateTime.MaxValue,
            LicenseCodec.FormatDate(lifeExpires));

        // ④–⑥ متّجهاتٌ سالبة: جهازٌ آخر، منشأةٌ أخرى، رقمٌ محرَّف.
        Row("مفتاحُ جهازٍ آخر يُرفض",
            LicenseCodec.Decode(annual, OtherMachine, Company, Now, out _, out _, out _)
            == NumericLicenseVerdict.DeviceMismatch,
            "DeviceMismatch");

        Row("مفتاحُ منشأةٍ أخرى يُرفض",
            LicenseCodec.Decode(annual, Machine, OtherCompany, Now, out _, out _, out _)
            == NumericLicenseVerdict.BadIntegrity,
            "BadIntegrity");

        var last = annual[^1];
        var tampered = annual[..^1] + (last == '9' ? '0' : (char)(last + 1));
        Row("رقمٌ واحدٌ محرَّف يُكشف",
            LicenseCodec.Decode(tampered, Machine, Company, Now, out _, out _, out _)
            == NumericLicenseVerdict.BadIntegrity,
            "BadIntegrity");

        // ⑦–⑨ حقولٌ غير معروفةٍ أو تالفة.
        var future = LicenseCodec.Encode(LicensePlan.Annual, DateOnly.FromDateTime(Now.AddDays(3)), Machine, Company);
        Row("تاريخُ إصدارٍ مستقبليّ يُرفض",
            LicenseCodec.Decode(future, Machine, Company, Now, out _, out _, out _)
            == NumericLicenseVerdict.NotYetValid,
            "NotYetValid");

        var value = UInt128.Parse(annual);
        var unknownVersion = (value + ((UInt128)1 << 81)).ToString().PadLeft(LicenseCodec.DigitCount, '0');
        var unknownPlan = (value - ((UInt128)1 << 80)).ToString().PadLeft(LicenseCodec.DigitCount, '0');
        Row("نسخةُ ترميزٍ unknown تُرفض",
            LicenseCodec.Decode(unknownVersion, Machine, Company, Now, out _, out _, out _)
            == NumericLicenseVerdict.UnknownVersion, "UnknownVersion");
        Row("خطةٌ غير معروفة تُرفض",
            LicenseCodec.Decode(unknownPlan, Machine, Company, Now, out _, out _, out _)
            == NumericLicenseVerdict.UnknownPlan, "UnknownPlan");
        Row("نصٌّ ليس ٢٥ رقماً يُرفض",
            LicenseCodec.Decode("1234-5678", Machine, Company, Now, out _, out _, out _)
            == NumericLicenseVerdict.Malformed, "Malformed");

        // ⑩ العرضُ والطباعة.
        var formatted = LicenseCodec.Format(annual);
        Row("التنسيقُ خمسُ مجموعاتٍ والعودةُ منه تُطبّع",
            formatted.Length == 29 && formatted.Count(c => c == '-') == 4
            && LicenseCodec.Normalize(formatted) == annual,
            formatted);

        // ⑪ الأرقامُ العربيةُ تُقبل.
        var arabic = new string(annual.Select(ch =>
            ch is >= '0' and <= '9' ? (char)(ch - '0' + '\u0660') : ch).ToArray());
        Row("الأرقامُ العربيةُ الهندية تُطبَّع",
            LicenseCodec.Decode(arabic, Machine, Company, Now, out _, out _, out _)
            == NumericLicenseVerdict.Valid, "Valid");

        // ⑫ لا تسلسل: يومان متتاليان ليسا خطوةً صغيرة.
        var nextDay = LicenseCodec.Encode(LicensePlan.Annual, new DateOnly(2026, 1, 11), Machine, Company);
        var a = UInt128.Parse(annual);
        var b = UInt128.Parse(nextDay);
        var gap = a < b ? b - a : a - b;
        Row("المفاتيحُ غيرُ متسلسلة", gap > 1_000_000, $"الفرق {gap}");

        // ⑬ السجلُّ: صياغةُ السطر وقراءتُه — **بلا وسيطٍ على القرص**.
        //
        // ‏كان الفحص يُجرّب كتابةَ ملفٍّ في مجلّد الملفّات المؤقّتة، **فأوقف الإصدارَ على
        // ‏جهازٍ منع فيه ويندوز الكتابةَ هناك** — وهو عطلٌ في المجلد المؤقّت لا في الترخيص.
        // ‏والكتابةُ على القرص تُجرَّب عند الإصدار نفسِه، وفشلُها لا يمنع تسليم المفتاح.
        try
        {
            var row = new IssueLedger.LedgerRow(annual, Company, Machine, LicensePlan.Annual,
                "2026-01-10", "2027-01-10", DateTime.UtcNow);
            var line = IssueLedger.Serialize(row);
            var back = IssueLedger.Parse(line);
            Row("سجلُّ الإصدار يُكتب ويُقرأ",
                back is not null && back.Code == annual && back.Plan == LicensePlan.Annual
                && back.Company == Company && back.MachineId == Machine,
                back is null ? "سطرٌ لم يُقرأ" : "سطرٌ مطابق");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Row("سجلُّ الإصدار يُكتب ويُقرأ", false, ex.Message);
        }

        return rows;
    }
}
