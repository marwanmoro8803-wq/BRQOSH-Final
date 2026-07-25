namespace Brqosh.Infrastructure.Companies;

/// <summary>فرع الجلسة التشغيلي — يُضبط من Desktop ويُقرأ عند الترحيل.</summary>
public static class BranchSessionContext
{
    private static readonly AsyncLocal<long?> PreferredBranchSlot = new();

    public static long? PreferredBranchId
    {
        get => PreferredBranchSlot.Value;
        set => PreferredBranchSlot.Value = value;
    }
}
