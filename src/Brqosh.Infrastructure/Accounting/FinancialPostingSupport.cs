using Brqosh.Application.Accounting;
using Brqosh.Infrastructure.Persistence;

using Brqosh.Infrastructure.Security;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>مساعد موحّد لقواعد الترحيل وسجل التدقيق — Financial Integrity Layer.</summary>
public static class FinancialPostingSupport
{
    public static Task ValidateAsync(
        BrqoshDbContext db,
        long companyId,
        DateOnly documentDate,
        PostingDocumentKind kind,
        IEnumerable<long>? accountIds = null,
        string? documentKey = null,
        CancellationToken ct = default) =>
        new PostingOrchestrator(db).ValidateDocumentPostAsync(
            companyId, documentDate, kind, accountIds, documentKey, ct);

    public static async Task AuditPostAsync(
        BrqoshDbContext db,
        long companyId,
        string entityType,
        string entityKey,
        string? reason = null,
        string details = "",
        CancellationToken ct = default)
    {
        await AuditPostingHelper.LogPostAsync(
            db, companyId, entityType, entityKey, reason: reason ?? $"ترحيل {entityType}", details: details, ct: ct);
        await new Platform.DocumentVersionService(db).RecordPostAsync(companyId, entityType, entityKey, ct);
    }
}
