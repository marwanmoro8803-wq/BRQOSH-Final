using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// (CEEF-230 · ط1-٧) <b>مركزُ تكلفةٍ موجودٌ ونشطٌ وللمنشأة نفسِها.</b>
    ///
    /// <para><b>ولا مفتاحَ أجنبيّ في القاعدة</b> — وذلك قرارٌ لا سهو: المركزُ يُوقَف حذفاً
    /// ناعماً، ومفتاحٌ أجنبيٌّ إمّا يمنع إيقافَ مركزٍ عليه حركةٌ تاريخية أو يترك المرجعَ
    /// معلّقاً. <b>فالتحقّقُ في زمن الترحيل هو الطبقةُ الصحيحة</b>: يمنع دخولَ مرجعٍ خاطئ،
    /// ولا يمنع إيقافَ مركزٍ استُعمل من قبل.</para>
    /// </summary>
    public static async Task EnsureCostCenterAsync(
        BrqoshDbContext db, long companyId, long? costCenterId, CancellationToken ct = default)
    {
        if (costCenterId is not long id) return;

        var exists = await db.CostCenters
            .AnyAsync(c => c.Id == id && c.CompanyId == companyId && c.IsActive, ct);

        if (!exists)
            throw new AccountingException($"مركزُ التكلفة غيرُ معروفٍ أو موقوف: {id}.");
    }

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
