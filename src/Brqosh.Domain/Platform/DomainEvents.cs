namespace Brqosh.Domain.Platform;

/// <summary>حدث نطاق — أساس event bus لاحقاً (مرحّل/ملغى/معتمد).</summary>
public abstract record DomainEvent(DateTime OccurredAtUtc)
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record JournalEntryPostedEvent(
    long CompanyId, long JournalEntryId, string EntryNumber, DateOnly EntryDate)
    : DomainEvent(DateTime.UtcNow);

public sealed record DocumentPostedEvent(
    long CompanyId, string DocumentType, long DocumentId, string DocumentNumber)
    : DomainEvent(DateTime.UtcNow);

public sealed record DocumentCancelledEvent(
    long CompanyId, string DocumentType, long DocumentId, string DocumentNumber, string Reason)
    : DomainEvent(DateTime.UtcNow);
