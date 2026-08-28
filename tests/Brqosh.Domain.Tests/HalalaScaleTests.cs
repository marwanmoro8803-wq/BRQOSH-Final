using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Tests;

/// <summary>
/// (CEEF-229 · م2-٨) <b>سُلَّم الهللة — القيد المتوازن في الذاكرة والمختلّ في القاعدة.</b>
///
/// <para><b>هذا البند دخل الخطة بادّعاءٍ خاطئ.</b> قيل إن حارس التوازن «ميتٌ رياضياً» لأن كل
/// قيدٍ متوازنٌ بالبناء، فيُستبدل بفحصٍ ذي معنى. والقياس دحض الادّعاء ووجد ما هو أخطر: الحارس
/// <b>يعضّ فعلاً</b>، على مدخلٍ حقيقيّ متاحٍ اليوم من شاشة القيد اليدويّ.</para>
///
/// <para><b>الآلية:</b> <c>IsBalanced</c> يقارن قيمتَين في الذاكرة، ومساواةُ <c>decimal</c>
/// تتجاهل فرق السُّلَّم — فـ<c>20.010m == 20.01m</c> صحيح. لكن عمودَي المبلغ
/// <c>numeric(18,2)</c>، فيُقرِّب PostgreSQL <b>كل سطرٍ على حدة</b> قبل الجمع.</para>
///
/// <para><b>والقياس أُجري على PostgreSQL حيّة لا استنتاجاً:</b>
/// <c>SELECT 10.005::numeric(18,2) + 10.005::numeric(18,2), 20.01::numeric(18,2);</c>
/// ⟵ <c>20.02 | 20.01</c>. أي أن الأستاذ يفقد توازنه بهللة، والقيد مرحَّلٌ نظاميّاً.</para>
///
/// <para><b>ولماذا لم يكشفه اختبارٌ قطّ:</b> الاختبارات كلُّها على SQLite، وهو يخزّن
/// <c>decimal</c> بدقّته الكاملة ويتجاهل <c>HasPrecision</c> — فالتقريب الذي يقع في الإنتاج
/// لا يقع في الاختبار أبداً. ولذلك يعيش هذا الملف في <b>اختبارات النطاق</b>: لا قاعدة بيانات
/// فيه أصلاً، فلا مزوّدَ يُخفي الأثر.</para>
/// </summary>
public class HalalaScaleTests
{
    private static Account Postable(string code) =>
        Account.Create(code, $"حساب {code}", $"Account {code}", AccountType.Expense, null, companyId: 1);

    private static JournalEntry Draft() =>
        JournalEntry.CreateDraft(1, new DateOnly(2026, 7, 9), "قيد اختبار");

    /// <summary>
    /// <b>القياس المركزيّ.</b> مبلغٌ بثلاث خانات عشرية يُرفض عند بابه — قبل أن يصير قيداً،
    /// لا بعد أن يصير أستاذاً مختلاً.
    /// </summary>
    [Theory]
    [InlineData(10.005)]
    [InlineData(0.001)]
    [InlineData(99.999)]
    public void SubHalalaAmount_IsRefusedAtTheLine(decimal amount)
    {
        var ex = Assert.Throws<AccountingException>(
            () => Draft().AddLine(Postable("5101"), amount, 0m));

        Assert.Contains("خانتَي الهللة", ex.Message);
    }

    /// <summary>وفي الطرف الدائن كذلك — فالخلل لا يعرف طرفاً.</summary>
    [Fact]
    public void SubHalalaCredit_IsRefusedToo()
    {
        Assert.Throws<AccountingException>(
            () => Draft().AddLine(Postable("2101"), 0m, 33.333m));
    }

    /// <summary>
    /// <b>حارسٌ للحارس — وهذا هو ما يمنع الإصلاح من أن يكون فظاً:</b> المقارنة بالقيمة لا
    /// بالسُّلَّم. فـ<c>10.0000m</c> مبلغٌ مشروع تماماً وإن حمل أربع خانات، لأنه <b>لا يتغيّر</b>
    /// بالتقريب. المرفوض ما يتغيّر، لا ما يبدو طويلاً.
    /// </summary>
    [Theory]
    [InlineData("10.0000")]
    [InlineData("10.00")]
    [InlineData("10")]
    [InlineData("0.10")]
    public void TrailingZeros_AreAccepted_BecauseRoundingDoesNotChangeThem(string literal)
    {
        var amount = decimal.Parse(literal, System.Globalization.CultureInfo.InvariantCulture);
        var entry = Draft();

        entry.AddLine(Postable("5101"), amount, 0m);

        Assert.Single(entry.Lines);
    }

    /// <summary>
    /// <b>وهذه هي الاختبارة التي تُثبت أن الادّعاء كان خاطئاً:</b> تُعيد بناء القيد المُختلّ
    /// بالضبط، وتقيس أن مجموع ما <b>ستخزّنه القاعدة</b> لا يساوي مجموع ما تراه الذاكرة.
    ///
    /// <para>لو نُزع <c>EnsureHalalaScale</c> لقُبل هذا القيد ومرّ <c>Post</c> بنجاح — لأن
    /// <c>IsBalanced</c> يراه متوازناً — ولذهب إلى الأستاذ مختلاً. والتقريب هنا
    /// <c>AwayFromZero</c> مطابقةً لسلوك <c>numeric</c> في PostgreSQL المقيس، لا لسلوك
    /// <c>decimal.Round</c> الافتراضيّ (المصرفيّ) الذي يختلف عنه.</para>
    /// </summary>
    [Fact]
    public void TheRefusedEntry_WouldHaveBeenStoredUnbalanced()
    {
        decimal[] debits = [10.005m, 10.005m];
        const decimal credit = 20.01m;

        // في الذاكرة: متوازن تماماً — وهذا هو الفخّ.
        Assert.Equal(credit, debits.Sum());

        // وفي القاعدة: كل سطرٍ يُقرَّب وحده قبل الجمع.
        var storedDebit = debits.Sum(d => Math.Round(d, 2, MidpointRounding.AwayFromZero));
        var storedCredit = Math.Round(credit, 2, MidpointRounding.AwayFromZero);

        Assert.Equal(20.02m, storedDebit);
        Assert.NotEqual(storedDebit, storedCredit);
    }

    /// <summary>
    /// والقيد الصحيح يمرّ ويُرحَّل — فالحارس يمنع الخلل ولا يمنع العمل.
    /// </summary>
    [Fact]
    public void ProperlyRoundedEntry_PostsNormally()
    {
        var entry = Draft();
        entry.AddLine(Postable("5101"), 10.01m, 0m);
        entry.AddLine(Postable("5102"), 10.00m, 0m);
        entry.AddLine(Postable("2101"), 0m, 20.01m);

        entry.Post("JV-2026-000001");

        Assert.True(entry.IsBalanced);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
    }
}
