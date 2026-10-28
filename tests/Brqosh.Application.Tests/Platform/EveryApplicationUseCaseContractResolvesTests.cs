using Brqosh.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// ‏كلُّ عقد حالة استخدامٍ في Application (خدمات الترحيل وأمر البيع وإذن التسليم) يُحَلّ من الحاوية الحقيقية —
/// ‏فعقدٌ يُضاف بلا تسجيلٍ يسقط هنا لا في يد المستخدم.
/// </summary>
public class EveryApplicationUseCaseContractResolvesTests
{
    [Fact]
    public void EveryUseCaseContract_ResolvesFromTheRealContainer()
    {
        var contracts = typeof(Brqosh.Application.ApplicationServiceCollectionExtensions).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.IsPublic
                        && (t.Name.EndsWith("PostingService", StringComparison.Ordinal)
                            || t.Name is "ISalesOrderService" or "IDeliveryNoteService"))
            .ToList();
        Assert.True(contracts.Count >= 10, $"لم يُعثر إلا على {contracts.Count} عقداً.");

        var services = new ServiceCollection();
        services.AddBrqoshInfrastructure(
            $"Data Source={Path.Combine(Path.GetTempPath(), $"brqosh-contracts-{Guid.NewGuid():N}.db")}",
            usePostgres: false);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var unresolved = contracts.Where(c => scope.ServiceProvider.GetService(c) is null).Select(c => c.Name).ToList();
        Assert.Empty(unresolved);
    }
}
