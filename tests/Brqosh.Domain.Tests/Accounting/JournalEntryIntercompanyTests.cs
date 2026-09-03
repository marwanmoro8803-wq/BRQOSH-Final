using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Tests.Accounting;

public class JournalEntryIntercompanyTests
{
    [Fact]
    public void MarkIntercompany_sets_flags_on_draft()
    {
        var entry = JournalEntry.CreateDraft(1, new DateOnly(2026, 7, 1), "اختبار");
        entry.MarkIntercompany(2);
        Assert.True(entry.IsIntercompany);
        Assert.Equal(2L, entry.CounterpartyCompanyId);
    }

    [Fact]
    public void MarkIntercompany_rejects_same_company()
    {
        var entry = JournalEntry.CreateDraft(1, new DateOnly(2026, 7, 1), "اختبار");
        Assert.Throws<AccountingException>(() => entry.MarkIntercompany(1));
    }

    /// <summary>
    /// (CEEF-232 · O-32ج) <b>وعكسُ قيدٍ بين الشركات يبقى بينها — وإلّا نجا من الاستبعاد.</b>
    ///
    /// <para><c>IntercompanyEliminationHelper</c> يختار على <c>IsIntercompany</c> <b>ويشمل
    /// المعكوسَ عمداً</b>. فإن لم يرث العكسُ الوسمَ <b>استُبعد الأصلُ عند التوحيد ولم يُستبعد
    /// عكسُه</b>: بيعٌ بين شركتين بمئة ألفٍ ثمّ عُكس يترك <b>مئةَ ألفٍ إيراداً وهمياً</b> في
    /// قائمة الدخل الموحَّدة — وهو ربحٌ لم يقع خارج المجموعة، <b>يُوزَّع ويُزكّى عليه</b>.</para>
    ///
    /// <para><b>ولا يشكو الميزان</b>: القيدُ متوازنٌ في الحالتين — والعطلُ في بُعدٍ لا في مبلغ.</para>
    /// </summary>
    [Fact]
    public void Reversal_inherits_the_intercompany_mark()
    {
        var entry = PostedEntry(e => e.MarkIntercompany(2));

        var reversal = entry.CreateReversal(new DateOnly(2026, 7, 2));

        Assert.True(reversal.IsIntercompany,
            "لم يرث العكسُ وسمَ ما بين الشركات — فيُستبعد الأصلُ عند التوحيد ويبقى عكسُه إيراداً وهمياً.");
        Assert.Equal(2L, reversal.CounterpartyCompanyId);
    }

    /// <summary>
    /// (CEEF-232 · O-32ب) <b>والفرعُ يُورَث — وإلّا وسم العكسَ فرعُ الجلسة.</b>
    ///
    /// <para><b>وهذا أخفى نصفَي O-32</b>: العكسُ يُضاف بفرعٍ فارغ، <b>و<c>BrqoshDbContext</c>
    /// يسم كلَّ مُضافٍ بفرع الجلسة</b>. فعكسُ قيدِ الرياض من جلسةٍ في جدّة يُنزل المدينَ في
    /// الرياض والدائنَ في جدّة — <b>فيظهر ربحٌ في فرعٍ وخسارةٌ في آخر وكلاهما وهم</b>،
    /// و<c>BranchProfitabilityQuery</c> يجمع على <c>BranchId</c> فيُصدّقهما.</para>
    /// </summary>
    [Fact]
    public void Reversal_inherits_the_branch()
    {
        var entry = PostedEntry(e => e.AssignBranch(7));

        var reversal = entry.CreateReversal(new DateOnly(2026, 7, 2));

        Assert.Equal(7L, reversal.BranchId);
    }

    private static readonly Account Cash =
        Account.Create("1101", "النقدية بالصندوق", "Cash on Hand", AccountType.Asset);

    private static readonly Account Sales =
        Account.Create("4101", "إيرادات المبيعات", "Sales Revenue", AccountType.Revenue);

    /// <summary>قيدٌ مرحَّل — <b>والوسمُ يقع على المسودّة</b>، فكلا الحقلين يرفض المرحَّل.</summary>
    private static JournalEntry PostedEntry(Action<JournalEntry> markWhileDraft)
    {
        var entry = JournalEntry.CreateDraft(1, new DateOnly(2026, 7, 1), "اختبار");
        entry.AddLine(Cash, debit: 100m, credit: 0m);
        entry.AddLine(Sales, debit: 0m, credit: 100m);
        markWhileDraft(entry);
        entry.Post("JV-IC-1");
        return entry;
    }
}
