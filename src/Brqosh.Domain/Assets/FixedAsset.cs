using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Assets;

public enum DepreciationMethod
{
    StraightLine = 0,
}

/// <summary>أصل ثابت — يُهلك شهرياً بخط مستقيم.</summary>
public class FixedAsset : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public DateOnly AcquisitionDate { get; private set; }
    public decimal AcquisitionCost { get; private set; }
    public decimal SalvageValue { get; private set; }
    public int UsefulLifeMonths { get; private set; }
    public decimal AccumulatedDepreciation { get; private set; }
    public long GlAssetAccountId { get; private set; }
    public long GlDepreciationAccountId { get; private set; }
    public long? AcquisitionJournalEntryId { get; private set; }
    public long? DisposalJournalEntryId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public decimal BookValue => AcquisitionCost - AccumulatedDepreciation;
    public decimal MonthlyDepreciation =>
        UsefulLifeMonths <= 0 ? 0m
        : Math.Round((AcquisitionCost - SalvageValue) / UsefulLifeMonths, 2, MidpointRounding.AwayFromZero);

    private FixedAsset()
    {
        Code = null!;
        NameAr = null!;
    }

    private FixedAsset(long companyId, string code, string nameAr, DateOnly acquisitionDate,
        decimal acquisitionCost, decimal salvageValue, int usefulLifeMonths,
        long glAssetAccountId, long glDepreciationAccountId)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        AcquisitionDate = acquisitionDate;
        AcquisitionCost = acquisitionCost;
        SalvageValue = salvageValue;
        UsefulLifeMonths = usefulLifeMonths;
        GlAssetAccountId = glAssetAccountId;
        GlDepreciationAccountId = glDepreciationAccountId;
    }

    public static FixedAsset Create(long companyId, string code, string nameAr, DateOnly acquisitionDate,
        decimal acquisitionCost, decimal salvageValue, int usefulLifeMonths,
        long glAssetAccountId, long glDepreciationAccountId)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود الأصل مطلوب.");
        if (acquisitionCost <= 0)
            throw new AccountingException("تكلفة الاقتناء يجب أن تكون أكبر من صفر.");
        if (usefulLifeMonths <= 0)
            throw new AccountingException("العمر الإنتاجي بالأشهر مطلوب.");

        return new FixedAsset(companyId, code.Trim(), nameAr.Trim(), acquisitionDate,
            Math.Round(acquisitionCost, 2, MidpointRounding.AwayFromZero),
            Math.Round(salvageValue, 2, MidpointRounding.AwayFromZero),
            usefulLifeMonths, glAssetAccountId, glDepreciationAccountId);
    }

    /// <summary>
    /// (CEEF-232 · ع-٢) <b>يُرسمِل تحسيناً على الأصل — فتزيد تكلفتُه ويزيد قسطُه.</b>
    /// </summary>
    /// <remarks>
    /// <b>والقسطُ يُعاد حسابُه على التكلفة الجديدة بالعمر الأصليّ</b> — وهو ما يفعله
    /// <c>MonthlyDepreciation</c> بالبناء، فلا يلزم تدخّل. <b>والمُهلَك سابقاً لا يُمَسّ</b>:
    /// التحسينُ يقع اليومَ لا بأثرٍ رجعيّ، <b>وتعديلُ الماضي يُعيد فتحَ سنواتٍ أُقفلت</b>.
    ///
    /// <para><b>ولا يُرسمَل على أصلٍ مُستبعَد</b>: أصلٌ خرج من الدفاتر لا تُضاف إليه تكلفة —
    /// <b>وإضافتُها تُعيد إحياءَ أصلٍ ميّت</b> بقيمةٍ دفتريّةٍ مُختلَقة.
    /// </para>
    /// </remarks>
    public void Capitalize(decimal amount)
    {
        if (amount <= 0m)
            throw new AccountingException("مبلغ الرسملة يجب أن يكون أكبر من صفر.");
        if (!IsActive)
            throw new AccountingException("لا تُرسمَل تكلفةٌ على أصلٍ مُستبعَد.");

        AcquisitionCost = Math.Round(
            AcquisitionCost + amount, 2, MidpointRounding.AwayFromZero);
    }

    public void Rename(string nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الأصل مطلوب.");
        NameAr = nameAr.Trim();
    }

    public void LinkAcquisitionJournal(long journalEntryId) => AcquisitionJournalEntryId = journalEntryId;

    public void LinkDisposalJournal(long journalEntryId) => DisposalJournalEntryId = journalEntryId;

    public void MarkDisposed()
    {
        if (!IsActive)
            throw new AccountingException("الأصل مستبعد مسبقاً.");
        IsActive = false;
    }

    /// <summary>
    /// (CEEF-229 · م0-١٥) الاستبعاد مسموح في أيّ وقت من عمر الأصل.
    ///
    /// <para>كان هنا شرطٌ يرفض الاستبعاد ما دامت القيمة الدفترية أعلى من قيمة الخردة — أي
    /// <b>ما دام الأصل لم يُستهلَك بالكامل</b>. وهو يمنع أكثر حالات الاستبعاد وقوعاً: بيع سيارة
    /// بعد سنتين من عمرٍ إنتاجيّ عشر، أو استبعاد جهاز تلف. ورسالتُه تقترح علاجاً يُفسد الدفاتر:
    /// «شغّل الإهلاك أولاً» — أي تشغيلُه ستّاً وتسعين مرّة، فيُحمَّل إهلاكُ ثمان سنوات على شهرٍ
    /// واحد وتنحرف قوائم دخل تلك السنوات كلها.</para>
    ///
    /// <para><b>ولا مبرّر محاسبيّ له:</b> قيد الاستبعاد يوازن بالبناء في كلتا الحالتين — إذ
    /// يُخرج التكلفة كاملةً ويُقفل مجمّع الإهلاك مهما بلغ، والفرق يقع على الربح (4106) أو
    /// الخسارة (5215). فبيعٌ بأقلّ من القيمة الدفترية <b>خسارةٌ تُقيَّد</b> لا عمليةٌ تُمنَع.
    /// وحضورُ فرع الربح في المُرحِّل بلا مسارٍ يبلغه كان أوضح دليل على أن الحارس زائد.</para>
    ///
    /// <para><b>حدٌّ مسجَّل لا مُصلَح هنا:</b> الربح/الخسارة يُحسب على القيمة الدفترية <b>وقت آخر
    /// إهلاك مُرحَّل</b>. ولأن الإهلاك يُشغَّل بزرّ لا بالزمن، فأشهرُ ما بين آخر تشغيل وتاريخ
    /// الاستبعاد لا تُهلَك — فتُبالَغ القيمة الدفترية وتُبالَغ الخسارة. عيبٌ في محرّك الإهلاك
    /// (م3-٩) لا في الاستبعاد، ولا يُصلحه إبقاءُ حارسٍ يمنع العملية أصلاً.</para>
    /// </summary>
    public void EnsureReadyForDisposal()
    {
        if (!IsActive)
            throw new AccountingException("الأصل غير نشط.");
    }

    public decimal ApplyDepreciation()
    {
        var monthly = MonthlyDepreciation;
        if (monthly <= 0 || BookValue <= SalvageValue)
            return 0m;

        var amount = Math.Min(monthly, BookValue - SalvageValue);
        AccumulatedDepreciation += amount;
        return amount;
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
