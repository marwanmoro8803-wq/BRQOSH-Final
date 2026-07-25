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

    public void EnsureReadyForDisposal()
    {
        if (!IsActive)
            throw new AccountingException("الأصل غير نشط.");
        if (BookValue > SalvageValue + 0.01m)
            throw new AccountingException(
                $"لا يمكن استبعاد '{NameAr}' — القيمة الدفترية {BookValue:N2} تتجاوز المتبقية {SalvageValue:N2}. شغّل الإهلاك أولاً.");
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
