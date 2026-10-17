namespace Brqosh.Infrastructure.Zatca;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٥٦ · ‏`tax-zatca#8`) <b>متى تُعاد محاولةُ إرسال المعلَّق في الخلفيّة.</b>
///
/// <para>‏كانت إعادةُ الإرسال زرّاً في سجلّ الفواتير الإلكترونيّة — فمستندٌ تعطّل إرسالُه ليلةَ جمعةٍ يبقى معلَّقاً حتى يتذكّره
/// ‏أحد، ومهلةُ الإبلاغ عن المبسّطة ٢٤ ساعة. ‏فصار المؤقّتُ يحاول من نفسه: <b>كلَّ خمس دقائق ما دام لا شيءَ يفشل</b>،
/// ‏<b>ويتباعد بعد كلّ فشلٍ متتالٍ</b> (٥ ثمّ ١٠ ثمّ ٢٠ ثمّ ٤٠ ثمّ ساعةٌ سقفاً) — فخادمُ الهيئة المعطَّلُ لا يُقصف كلَّ دقيقة،
/// ‏واعتمادٌ مرفوضٌ (٤٠١) لا يملأ السجلَّ. ‏<b>وأوّلُ نجاحٍ يُعيد الإيقاعَ إلى خمس دقائق.</b></para>
///
/// <para>‏حالةٌ في الذاكرة لكلّ جلسة عمداً: إعادةُ تشغيل البرنامج محاولةٌ فوريّة، وهي ما يريده من أعاد التشغيلَ بعد عطل.</para>
/// </summary>
public sealed class ZatcaRetryBackoff
{
    public static readonly TimeSpan BaseInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxInterval = TimeSpan.FromHours(1);

    private DateTime? _nextDueUtc;

    public int ConsecutiveFailures { get; private set; }

    public DateTime? NextDueUtc => _nextDueUtc;

    public bool IsDue(DateTime nowUtc) => _nextDueUtc is not DateTime due || nowUtc >= due;

    /// <summary>‏محاولةٌ لم يبقَ بعدها معلَّق — أو تخطّاها القفلُ لأنّ محطّةً أخرى ترسل.</summary>
    public void RecordSuccess(DateTime nowUtc)
    {
        ConsecutiveFailures = 0;
        _nextDueUtc = nowUtc + BaseInterval;
    }

    /// <summary>‏محاولةٌ وقفت عند فشل — والتالية بعد ضعف سابقتها حتى السقف.</summary>
    public void RecordFailure(DateTime nowUtc)
    {
        ConsecutiveFailures++;
        _nextDueUtc = nowUtc + Delay(ConsecutiveFailures);
    }

    public static TimeSpan Delay(int consecutiveFailures)
    {
        if (consecutiveFailures <= 0)
            return BaseInterval;
        var minutes = BaseInterval.TotalMinutes * Math.Pow(2, Math.Min(consecutiveFailures - 1, 10));
        return TimeSpan.FromMinutes(Math.Min(minutes, MaxInterval.TotalMinutes));
    }
}
