using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// (م-٢ · ن-٠٢ / ن-٠٣) <b>حاجزُ قارئٍ-كاتبٍ على دفتر سنةٍ واحدة — يفصل الترحيلَ عن الإقفال.</b>
///
/// <para><b>العطل:</b> الترحيلُ والإقفالُ كانا يأخذان أقفالاً استشاريّةً <b>لا تتقاطع</b>:
/// المرحّلُ يأخذ <c>PERIOD{شهر}</c> والمُقفلُ السنويُّ يأخذ <c>YEAREND</c>، والمفتاحان
/// مشتقّان بـFNV-1a من نصّين مختلفين <b>فهما قفلان مختلفان تماماً</b>. أي أنّ حمايةً
/// كاملةَ المظهر كانت قائمةً بين مُقفلٍ ومُقفل، <b>ومعدومةً بين مُقفلٍ ومرحّل</b>.</para>
///
/// <para><b>وقيس بالرقم:</b> إقفالٌ سنويٌّ يقرأ قائمةَ الدخل ثمّ يكتب قيدَه بعد عشراتِ
/// السطور؛ وفاتورةٌ تُرحَّل في تلك النافذة تدخل الدفترَ <b>ولا تدخل القيد</b>. صافي الدخل
/// المُرحَّل إلى الأرباح المبقاة: <b>١٣٠٠ بدل ٢٢٠٠</b>. والفاتورةُ تبقى في فترةٍ أُقفلت
/// بعدها مباشرةً، <b>فلا يجرفها إقفالٌ لاحقٌ أبداً</b> — والفرقُ يستقرّ صامتاً في حقوق
/// الملكية، إذ تستثني قائمةُ الدخل قيودَ الإقفال فلا يظهر فيها شيء.</para>
///
/// <para><b>ولمَ لا قفلٌ حصريٌّ واحد:</b> لأنّ ذلك يُجمّد ترحيلَ المنشأة كلِّها بينما يترحّل
/// مرحّلٌ آخر — وهو اعتراضٌ مكتوبٌ بيد من كتب <c>YearEndCloseService</c>: «إقفالُ سنةٍ لا
/// يجوز أن يُجمّد ترحيلَ كلّ قيدٍ فيها طوالَ مدّته». <b>والاعتراضُ صحيح.</b> فالحاجزُ هنا
/// متبادل: المرحّلون يأخذون <b>المشترَك</b> فلا ينتظر أحدُهم أحداً؛ والمُقفلُ وحدَه يأخذ
/// <b>الحصريّ</b> فينتظر الطائرين ويحجب الجدد <b>مدّةَ إقفاله وحدَها</b>.</para>
///
/// <para><b>وترتيبُ الأقفال جزءٌ من العقد:</b> هذا القفلُ يُؤخذ <b>أوّلاً</b> دائماً — قبل
/// <c>PERIOD</c> و<c>YEAREND</c> و<c>INV</c> وقفلِ التدقيق. فترتيبٌ واحدٌ في كلّ المسارات
/// يمنع الجمودَ المتبادل بناءً لا بالحظّ.</para>
///
/// <para>والمفتاحُ لكلّ (منشأة · سنة): إقفالُ ٢٠٢٥ لا يمسّ ترحيلَ ٢٠٢٦.</para>
/// </summary>
internal static class LedgerYearLock
{
    private const string Prefix = "LEDGER";

    /// <summary>المفتاحُ الثاني — مشتقٌّ بالاشتقاق الثابت نفسِه (FNV-1a) لا بـ<c>GetHashCode</c>.</summary>
    internal static int KeyFor(int year) => DocumentSequenceLock.KeyFor(Prefix, year);

    /// <summary>
    /// <b>المرحّل.</b> مشترَكٌ: لا يحجب مرحّلاً آخر إطلاقاً، ويحجبه الإقفالُ وحدَه.
    /// </summary>
    internal static async Task AcquireForPostingAsync(
        DatabaseFacade database, long companyId, int year, CancellationToken ct = default)
    {
        if (!Applies(database, year, "الترحيل")) return;

        await database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock_shared({0}::int, {1}::int)",
            new object[] { companyId, KeyFor(year) },
            ct);
    }

    /// <summary>
    /// <b>المُقفل.</b> حصريّ: ينتظر كلَّ ترحيلٍ طائرٍ حتى يلتزم، ويحجب الجديدَ حتى يلتزم هو.
    /// </summary>
    internal static async Task AcquireForClosingAsync(
        DatabaseFacade database, long companyId, int year, CancellationToken ct = default)
    {
        if (!Applies(database, year, "الإقفال")) return;

        await database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}::int, {1}::int)",
            new object[] { companyId, KeyFor(year) },
            ct);
    }

    /// <summary>
    /// لا أثرَ على SQLite — الكتابةُ هناك متسلسلةٌ بطبيعتها، والقفلُ الاستشاريُّ من
    /// خصائص PostgreSQL وحدَه. <b>أمّا غيابُ المعاملة فيُرفض صراحةً</b>: قفلٌ من طراز
    /// <c>_xact_</c> خارجَ معاملةٍ يُحرَّر بانتهاء العبارة نفسِها، <b>فيبقى ظاهرُ الحماية
    /// ويزول فعلُها</b> — وهو بعينه ما جعل هذا العطلَ يمرّ سنةً بلا كاشف.
    /// </summary>
    private static bool Applies(DatabaseFacade database, int year, string what)
    {
        if (!database.IsNpgsql()) return false;

        if (database.CurrentTransaction is null)
            throw new InvalidOperationException(
                $"‏{what} في دفتر سنة {year} يجري خارج معاملة — وقفلُ الدفتر يُحرَّر فوراً فلا يحمي شيئاً. "
                + "يُنقل العملُ إلى داخل معاملةٍ واحدةٍ تشمل القراءةَ والكتابة.");

        return true;
    }
}

/// <summary>
/// نافذةُ اختبارٍ على اشتقاق المفتاح وحدَه — فتقاطعُ الأقفال شرطُ عملها، <b>وعدمُ تقاطعِها
/// هو العطلُ نفسُه</b>، ولا يُقاس إلا بمقارنة المفاتيح صراحة.
/// </summary>
public static class LedgerYearLockProbe
{
    public static int KeyFor(int year) => LedgerYearLock.KeyFor(year);
}
