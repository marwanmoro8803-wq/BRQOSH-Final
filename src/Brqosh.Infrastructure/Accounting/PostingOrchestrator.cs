using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// منسّق قواعد الترحيل — نقطة دخول موحّدة قبل كل ترحيل مالي (Accounting Rules Engine v1).
/// </summary>
public sealed class PostingOrchestrator(BrqoshDbContext db, IPostingPolicy? policy = null)
{
    private readonly IPostingPolicy _policy = policy ?? new SaudiPostingPolicy(db);

    public Task ValidateDocumentPostAsync(
        long companyId,
        DateOnly documentDate,
        PostingDocumentKind kind,
        IEnumerable<long>? accountIds = null,
        string? documentKey = null,
        CancellationToken ct = default) =>
        _policy.ValidateBeforePostAsync(new PostingValidationRequest(
            companyId, documentDate, kind, accountIds?.Distinct().ToList(), documentKey), ct);

    public Task ValidateJournalEntryAsync(JournalEntry entry, CancellationToken ct = default) =>
        ValidateDocumentPostAsync(
            entry.CompanyId,
            entry.EntryDate,
            PostingDocumentKind.JournalEntry,
            entry.Lines.Select(l => l.AccountId),
            entry.Description,
            ct);

    public Task<long> ResolveAccountAsync(long companyId, string systemCode, CancellationToken ct = default) =>
        _policy.ResolveAccountIdAsync(companyId, systemCode, ct);
}
