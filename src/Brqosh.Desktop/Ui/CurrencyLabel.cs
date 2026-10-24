using Brqosh.Domain.Companies;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · العملة) <b>ما يُكتب بجوار المبالغ في الشاشات = عملةُ دفاتر المنشأة الجارية.</b>
///
/// <para><b>ما كان:</b> رمزُ الريال مكتوبٌ نصّاً في نحو ثلاثين موضعاً من الشاشات، فمنشأةٌ عملةُ دفاترها غيرُ الريال كانت ترى
/// مبالغَها بالريال في اللوحة والقوائم وملخّصات الصفحات.</para>
///
/// <para><b>الآن:</b> تُقرأ عملةُ القياس المحاسبيّ للمنشأة الجارية مرّةً عند اختيار المنشأة (وبعد حفظ الإعدادات)، وتُسأل الشاشاتُ
/// عنها بلا قاعدة بيانات. ما لم تُحمَّل بعدُ فالريالُ، وهو أصلُ المنشآت.</para>
/// </summary>
public static class CurrencyLabel
{
    private static string _code = "SAR";

    /// <summary>رمز ISO لعملة الدفاتر الجارية (SAR مثلاً).</summary>
    public static string Code => _code;

    /// <summary>ما يُكتب بجوار المبلغ باللغة الجارية: الرمزُ المختصر بالعربية (للريال: ر + نقطة + س) والرمزُ الدوليّ بالإنجليزية.</summary>
    public static string Short => CurrencyCatalog.Short(_code, LocaleManager.IsEnglish);

    /// <summary>مبلغٌ مع رمزه، مثل 1,250.00 ثم الرمز.</summary>
    public static string Format(decimal amount, string numberFormat = "N2") =>
        $"{amount.ToString(numberFormat)} {Short}";

    /// <summary>يضبط العملة يدوياً — للاختبارات ولما بعد حفظ الإعدادات.</summary>
    public static void Set(string? code) =>
        _code = CurrencyCatalog.IsKnown(code) ? code!.Trim().ToUpperInvariant() : "SAR";

    /// <summary>يقرأ عملة المنشأة الجارية من القاعدة. فشلُ القراءة يبقي ما كان ويُسجَّل — لا يُسقط الشاشة.</summary>
    public static void Load()
    {
        try
        {
            using var db = AppServices.CreateDb();
            var companyId = AppServices.CompanyId;
            Set(db.Companies.Where(c => c.Id == companyId).Select(c => c.FunctionalCurrencyCode).FirstOrDefault());
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "CurrencyLabel.Load");
        }
    }
}
