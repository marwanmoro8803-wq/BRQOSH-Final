namespace Brqosh.Domain;

/// <summary>عمليات الحذف المنطقي الموحّدة.</summary>
public static class SoftDelete
{
    public static void MarkDeleted(Action<DateTime> setDeletedAtUtc, Action setInactive)
    {
        setDeletedAtUtc(DateTime.UtcNow);
        setInactive();
    }

    public static void Restore(Action clearDeletedAtUtc, Action setActive)
    {
        clearDeletedAtUtc();
        setActive();
    }
}
