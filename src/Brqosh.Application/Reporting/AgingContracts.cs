namespace Brqosh.Application.Reporting;

/// <summary>‏أعمارُ الذمم المدينة والدائنة — عقدان في Application، ومحوّلان في Infrastructure.</summary>
public record ReceivableAgingRowDto(long CustomerId, string CustomerNameAr,
    decimal Current, decimal Days31To60, decimal Days61To90, decimal Over90, decimal UnappliedCredit);

public record PayableAgingRowDto(long SupplierId, string SupplierNameAr,
    decimal Current, decimal Days31To60, decimal Days61To90, decimal Over90, decimal UnappliedDebit);

public interface IReceivableAgingQuery
{
    Task<IReadOnlyList<ReceivableAgingRowDto>> ExecuteAsync(long companyId, DateOnly asOf, CancellationToken ct = default);
}

public interface IPayableAgingQuery
{
    Task<IReadOnlyList<PayableAgingRowDto>> ExecuteAsync(long companyId, DateOnly asOf, CancellationToken ct = default);
}
