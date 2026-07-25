using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Application;

/// <summary>تسجيل خدمات طبقة التطبيق — التنفيذ في Infrastructure عبر AddBrqoshInfrastructure.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBrqoshApplication(this IServiceCollection services)
    {
        // العقود المطلوبة تُسجَّل من Infrastructure
        return services;
    }
}
