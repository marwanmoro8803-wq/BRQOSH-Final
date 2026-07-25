using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Brqosh.Api.Tests;

public class ProductionApiSecurityStartupTests
{
    [Fact]
    public void Production_without_api_key_fails_startup()
    {
        using var factory = new IsolatedApiFactory(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [Brqosh.Infrastructure.Security.ApiSecuritySettings.ProvisioningKeyEnv] = "provision-secret",
                });
            });
        });

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public void Production_without_provisioning_key_fails_startup()
    {
        using var factory = new IsolatedApiFactory(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [Brqosh.Infrastructure.Security.ApiSecuritySettings.ApiKeyEnv] = "api-secret",
                });
            });
        });

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
}
