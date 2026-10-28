using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// <b>قفلٌ استشاريّ على تسلسل المستندات — لكلّ (منشأة · نوع · سنة).</b>
///
/// <para><b>العطل:</b> كلُّ مولّدات الأرقام تقرأ الأكبرَ ثمّ تكتب التالي. وبلا قفل يقرأ
/// كاتبان الرقمَ نفسَه، فيربح أحدُهما ويموت الآخر على الفهرس الفريد. <b>وقيس على PostgreSQL:
/// ستّةُ كتّابٍ متزامنين ⟵ نجح ٣ وأخفق ٣</b> بـ<c>23505</c> خامّةٍ لا تُترجَم. أي أنّ نصفَ
/// المبيعات المتزامنة تموت برسالةٍ إنجليزية غير مفهومة.</para>
///
/// <para><b>ولماذا لكلّ نوعٍ مفتاحُه:</b> قفلٌ واحدٌ للمنشأة كلِّها يجعل ترحيلَ سند قبضٍ
/// ينتظر ترحيلَ فاتورة — تسلسلٌ لا لزوم له. فالمفتاحُ الثاني مشتقٌّ من البادئة والسنة.</para>
///
/// <para><b>وأخطرُ ما في هذا الملفّ سطرُ الاشتقاق.</b> <c>string.GetHashCode()</c> في .NET
/// <b>مُعشّى لكلّ عملية تشغيل</b> (randomized per process): جهازان يحسبان مفتاحين مختلفين
/// للبادئة نفسها، <b>فلا يتزاحمان على قفلٍ واحد ولا يحمي القفلُ شيئاً</b> — ويبقى ظاهرُه
/// سليماً. ولذلك يُشتقّ هنا بـFNV-1a على البايتات: دالّةٌ ثابتةٌ عبر العمليات والأجهزة
/// والإصدارات. وثباتُها مقيسٌ باختبارٍ يُثبّت قيمةً معلومة.</para>
/// </summary>
internal static class DocumentSequenceLock
{
    /// <summary>
    /// يحجز القفلَ حتى نهاية المعاملة الجارية. ولا أثرَ له على SQLite — فالكتابةُ هناك
    /// متسلسلةٌ بطبيعتها، والقفلُ الاستشاريّ من خصائص PostgreSQL وحده.
    /// </summary>
    internal static async Task AcquireAsync(
        DatabaseFacade database, long companyId, string prefix, int year, CancellationToken ct = default)
    {
        if (!database.IsNpgsql())
            return;

        // **قفلٌ بلا معاملةٍ قفلٌ ميّت — ويُرفض صراحةً لا يُشحن صامتاً.**
        //
        // `pg_advisory_xact_lock` مربوطٌ بالمعاملة: خارجَها تُنهي العبارةُ نفسُها المعاملةَ
        // الضمنية فيُحرَّر القفلُ فوراً، فيقرأ المتزاحمان الرقمَ نفسَه ويبقى ظاهرُ الحماية
        // قائماً. وهذا بعينه ما وقع في م1-٦: قفلٌ **حاضرٌ وميّت** على مسارَي إلغاء، مرّ
        // بلا كاشفٍ لأنّ لا اختبارَ تزامنٍ كان في المستودع.
        //
        // فالرمي هنا مقصود: لا يُقبل موضعُ ترقيمٍ يظنّ نفسَه محروساً وليس كذلك.
        if (database.CurrentTransaction is null)
            throw new InvalidOperationException(
                $"ترقيمُ «{prefix}» يجري خارج معاملة — والقفلُ الاستشاريّ يُحرَّر فوراً فلا يحمي شيئاً. "
                + "يُنقل تخصيصُ الرقم إلى داخل معاملة الكتابة.");

        // المعاملاتُ تُمرَّر مصفوفةً صريحة، وإلا ابتلع التحميلُ الزائد رمزَ الإلغاء
        // معاملَ SQL ثالثاً. والصبُّ صريح: توقيعُ PostgreSQL هو pg_advisory_xact_lock(int4, int4).
        await database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}::int, {1}::int)",
            new object[] { companyId, KeyFor(prefix, year) },
            ct);
    }

    /// <summary>
    /// مفتاحٌ ثابتٌ من البادئة والسنة — <b>FNV-1a لا <c>GetHashCode</c></b>، للسبب المشروح أعلاه.
    /// </summary>
    internal static int KeyFor(string prefix, int year)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;

            var hash = offset;
            foreach (var b in System.Text.Encoding.UTF8.GetBytes($"{prefix}|{year}"))
            {
                hash ^= b;
                hash *= prime;
            }

            return (int)hash;
        }
    }
}

/// <summary>
/// نافذةٌ للاختبار على اشتقاق المفتاح وحدَه — لا على القفل.
/// فثباتُ الاشتقاق عبر العمليات شرطُ عمل القفل، ولا يُقاس على جهازٍ واحد بغير تثبيت قيمة.
/// </summary>
public static class DocumentSequenceLockProbe
{
    public static int KeyFor(string prefix, int year) => DocumentSequenceLock.KeyFor(prefix, year);
}
