namespace Brqosh.Application.MasterData;

public enum MasterDataEntityKind
{
    Customer = 0,
    Supplier = 1,
}

public sealed record SimilarNameMatch(
    MasterDataEntityKind EntityKind,
    long EntityId,
    string NameAr,
    double SimilarityScore);

public interface IMasterDataGovernanceService
{
    Task<bool> CheckDuplicateVatAsync(
        long companyId,
        string vat,
        long? excludeId,
        MasterDataEntityKind entityKind,
        CancellationToken ct = default);

    Task<IReadOnlyList<SimilarNameMatch>> FindSimilarNamesAsync(
        long companyId,
        string nameAr,
        double threshold,
        CancellationToken ct = default);
}
