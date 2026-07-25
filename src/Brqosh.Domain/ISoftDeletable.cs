namespace Brqosh.Domain;

/// <summary>
/// كيان يدعم الحذف المنطقي — <see cref="DeletedAtUtc"/> هو المصدر الموحّد؛
/// <see cref="IsActive"/> يبقى متزامناً للتوافق مع الاستعلامات الحالية.
/// </summary>
public interface ISoftDeletable
{
    DateTime? DeletedAtUtc { get; }
    bool IsActive { get; }
}
