using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;

namespace Brqosh.Domain.Tests.Hospitality;

/// <summary>
/// (CEEF-230 · ط3-٣د) <b>وعاءُ الإكراميات — مالُ النُّدُل يُقيَّد إيراداً للمطعم.</b>
///
/// <para><b>ما كان:</b> لا شيءَ في النظام يعرف الإكرامية. فالنقديّةُ تدخل الدرج
/// <b>فتظهر فائضَ صندوقٍ يبدو خطأً في العدّ</b>، وإكراميةُ البطاقة تصل البنكَ مع التحصيل
/// <b>فلا يجد لها النظامُ إلا الإيرادَ موضعاً</b> — فتُدفع عنها ضريبةٌ وزكاةٌ على مالٍ
/// ليس للمنشأة.</para>
///
/// <para><b>وهويّةُ هـ-١٩:</b> <c>الرصيدُ = المحصَّلُ − الموزَّع</c> ولا يكون سالباً —
/// وهي هويّةُ <c>هـ-١٣</c> نفسُها: <b>مالٌ في اليد ليس مالَ صاحب اليد</b>.</para>
/// </summary>
public class TipPoolTests
{
    // ── هـ-١٩ ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: توزيعٌ يتجاوز المحصَّلَ يُرفض.</b>
    ///
    /// <para><b>ويدفع للنُّدُل مالاً لم يتركه أحد</b> — فيصير رصيدُ الالتزام سالباً،
    /// وتُصبح المنشأةُ دائنةً لنفسها بفرقٍ لا سببَ له في أيّ مستند.</para>
    /// </summary>
    [Fact]
    public void DistributingMoreThanCollected_IsRefusedByBothNumbers()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Card, 300m);

        var error = Assert.Throws<AccountingException>(() => pool.Distribute(5L, 400m));

        Assert.Contains("400.00", error.Message);
        Assert.Contains("300.00", error.Message);
    }

    /// <summary>
    /// <b>وتوزيعان متتاليان يتجاوزان معاً يُرفض ثانيهما.</b>
    ///
    /// <para><b>والحارسُ على المجموع لا على المبلغ الواحد</b> — فكلُّ توزيعٍ وحدَه
    /// أصغرُ من المحصَّل، <b>ومجموعُهما أكبر</b>.</para>
    /// </summary>
    [Fact]
    public void TwoDistributionsThatTogetherExceed_AreRefusedOnTheSecond()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 100m);
        pool.Distribute(5L, 60m);

        var error = Assert.Throws<AccountingException>(() => pool.Distribute(6L, 60m));

        Assert.Contains("ولا يُوزَّع ما لم يُترك", error.Message);
        Assert.Equal(60m, pool.TotalDistributed);
    }

    /// <summary>
    /// <b>والرصيدُ يُطبق المحصَّلَ ناقصَ الموزَّع دائماً.</b>
    /// </summary>
    [Fact]
    public void UndistributedAlwaysExhaustsCollectedMinusDistributed()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 120m);
        pool.Collect(TipSource.Card, 80m);
        pool.Distribute(5L, 130m);

        Assert.Equal(200m, pool.TotalCollected);
        Assert.Equal(70m, pool.Undistributed);
        Assert.Equal(pool.TotalCollected - pool.TotalDistributed, pool.Undistributed);
    }

    /// <summary>
    /// <b>والنقديُّ يُفصل عن البطاقة.</b>
    ///
    /// <para><b>لأنّ تسويةَ كلٍّ منهما من حسابٍ آخر</b>: النقديّةُ تخرج من الدرج،
    /// وإكراميةُ البطاقة من البنك بعد وصول التحصيل. <b>وجمعُهما في رقمٍ واحدٍ يُخرج
    /// قيداً يصرف من صندوقٍ لا يملك المبلغ</b>.</para>
    /// </summary>
    [Fact]
    public void CashAndCardTips_AreCountedApart()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 120m);
        pool.Collect(TipSource.Card, 80m);
        pool.Collect(TipSource.Cash, 30m);

        Assert.Equal(150m, pool.CollectedCash);
        Assert.Equal(80m, pool.CollectedCard);
        Assert.Equal(230m, pool.TotalCollected);
    }

    // ── التوزيع ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وحصّتا الموظّف الواحد تُجمعان لا تُستبدلان.</b>
    ///
    /// <para>نادلٌ يأخذ حصّةً من الغداء وأخرى من العشاء. <b>وإسنادٌ بدل جمعٍ يُسقط
    /// إحداهما صامتاً</b> — فيبقى في الوعاء مالٌ لا يعرف أحدٌ لمن هو. <b>وهو العطلُ
    /// نفسُه الذي وقع في وصفات المطبخ</b>.</para>
    /// </summary>
    [Fact]
    public void TwoSharesForOneEmployee_Accumulate()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 200m);

        pool.Distribute(5L, 70m);
        pool.Distribute(5L, 50m);

        Assert.Single(pool.Distributions);
        Assert.Equal(120m, pool.TotalDistributed);
        Assert.Equal(80m, pool.Undistributed);
    }

    /// <summary>
    /// <b>والقسمةُ بالتساوي تُقفل الوعاءَ بالضبط — وآخرُ حصّةٍ تأخذ الباقي.</b>
    ///
    /// <para>‏١٠٠ على ٣ تُنتج ٣٣٫٣٣ ثلاثَ مرّاتٍ ومجموعُها ٩٩٫٩٩ — <b>فيبقى فلسٌ يمنع
    /// الإقفال إلى الأبد</b>. وهو مذهبُ التوزيع نفسُه في أوعية الضريبة.</para>
    /// </summary>
    [Fact]
    public void DistributingEvenly_LeavesNothingBehind()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 100m);

        pool.DistributeEvenly([5L, 6L, 7L]);

        Assert.Equal(0m, pool.Undistributed);
        Assert.Equal(100m, pool.TotalDistributed);
        Assert.Equal(33.34m, pool.Distributions.Single(d => d.EmployeeId == 7L).Amount);
    }

    /// <summary>
    /// <b>والقسمةُ توزّع ما بقي لا المحصَّلَ كلَّه.</b>
    ///
    /// <para>ومَن وُزِّع له يدوياً لا يُعاد حسابُه في القسمة — <b>وتوزيعُ الكلّ ثانيةً
    /// يتجاوز المحصَّل</b>.</para>
    /// </summary>
    [Fact]
    public void DistributingEvenly_SplitsOnlyWhatRemains()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Card, 300m);
        pool.Distribute(5L, 100m);

        pool.DistributeEvenly([6L, 7L]);

        Assert.Equal(0m, pool.Undistributed);
        Assert.Equal(100m, pool.Distributions.Single(d => d.EmployeeId == 6L).Amount);
    }

    /// <summary>
    /// <b>وموظّفٌ مكرَّرٌ في قائمة القسمة يُرفض.</b>
    ///
    /// <para><b>وقبولُه يُعطيه حصّتين ويُنقص الباقين</b> بلا أن يظهر ذلك في أيّ رقم.</para>
    /// </summary>
    [Fact]
    public void ADuplicateEmployeeInTheSplit_IsRefused()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 90m);

        var error = Assert.Throws<AccountingException>(() => pool.DistributeEvenly([5L, 5L, 6L]));

        Assert.Contains("موظّفٌ مكرَّرٌ في قائمة التوزيع", error.Message);
    }

    /// <summary>
    /// <b>وقسمةُ وعاءٍ فارغٍ تُرفض.</b>
    /// </summary>
    [Fact]
    public void DistributingAnEmptyPool_IsRefused()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 50m);
        pool.Distribute(5L, 50m);

        var error = Assert.Throws<AccountingException>(() => pool.DistributeEvenly([6L]));

        Assert.Contains("لا شيءَ في الوعاء", error.Message);
    }

    // ── الإقفال ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وبقيّةٌ غيرُ موزَّعةٍ تمنع الإقفال.</b>
    ///
    /// <para><b>التزامٌ بلا صاحب</b>: يبقى في حسابٍ لا أحدَ يُطالب به، ثمّ يُشطب بعد
    /// سنواتٍ إيراداً — <b>وهو أخذُ مالِ النُّدُل بمرور الوقت</b>.</para>
    /// </summary>
    [Fact]
    public void ClosingWithAnUndistributedRemainder_IsRefusedByAmount()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 200m);
        pool.Distribute(5L, 150m);

        var error = Assert.Throws<AccountingException>(pool.Close);

        Assert.Contains("50.00", error.Message);
    }

    /// <summary>
    /// <b>ووعاءٌ بلا تحصيلٍ لا يُقفل.</b>
    ///
    /// <para>فرصيدُه صفرٌ والفرقُ صفر، <b>فيمرّ حارسُ البقيّة</b> — ويُقفل وعاءٌ لم يُفتح
    /// على مال، فيُلوّث العدّ بأوعيةٍ فارغة.</para>
    /// </summary>
    [Fact]
    public void ClosingAPoolThatCollectedNothing_IsRefused()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));

        var error = Assert.Throws<AccountingException>(pool.Close);

        Assert.Contains("بلا تحصيل", error.Message);
    }

    /// <summary>
    /// <b>ويُقفل حين لا يبقى ريال.</b>
    /// </summary>
    [Fact]
    public void AFullyDistributedPool_Closes()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 200m);
        pool.DistributeEvenly([5L, 6L]);

        pool.Close();

        Assert.Equal(TipPoolStatus.Closed, pool.Status);
        Assert.Equal(0m, pool.Undistributed);
    }

    /// <summary>
    /// <b>ولا يُعدَّل وعاءٌ أُقفل.</b>
    ///
    /// <para>وتحصيلٌ بعد الإقفال يُنشئ بقيّةً في وعاءٍ <b>قُيّد صرفُه كاملاً</b> —
    /// فيفترق الحسابُ عن قيده.</para>
    /// </summary>
    [Fact]
    public void CollectingAfterClose_IsRefused()
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 100m);
        pool.Distribute(5L, 100m);
        pool.Close();

        var error = Assert.Throws<AccountingException>(() => pool.Collect(TipSource.Cash, 20m));

        Assert.Contains("مُقفل", error.Message);
    }

    /// <summary>
    /// <b>وتحصيلٌ بصفرٍ أو سالبٍ يُرفض.</b>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-25)]
    public void ANonPositiveCollection_IsRefused(decimal amount)
    {
        var pool = TipPool.Open(1L, new DateOnly(2026, 6, 1));

        var error = Assert.Throws<AccountingException>(() => pool.Collect(TipSource.Cash, amount));

        Assert.Contains("موجباً", error.Message);
    }
}
