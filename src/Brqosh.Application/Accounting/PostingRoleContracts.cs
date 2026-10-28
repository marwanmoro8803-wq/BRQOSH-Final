namespace Brqosh.Application.Accounting;

/// <summary>‏دورُ ترحيلٍ وحسابُه الفعّال في المنشأة — لشاشة «حسابات الترحيل».</summary>
public record PostingRoleAccountDto(string Role, string DefaultCode, string AccountCode, string? AccountNameAr, bool IsCustom);

public interface IPostingRoleService
{
    Task<IReadOnlyList<PostingRoleAccountDto>> ListAsync(long companyId, CancellationToken ct = default);

    /// <summary>‏يربط الدورَ بحسابٍ تفصيليٍّ نشطٍ في دليل المنشأة.</summary>
    Task SetAsync(long companyId, string role, string accountCode, CancellationToken ct = default);

    /// <summary>‏يُعيد الدورَ إلى رمزه الافتراضيّ.</summary>
    Task ResetAsync(long companyId, string role, CancellationToken ct = default);
}
