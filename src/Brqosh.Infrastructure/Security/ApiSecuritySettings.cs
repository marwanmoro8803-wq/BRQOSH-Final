namespace Brqosh.Infrastructure.Security;

/// <summary>إعدادات أمن API من المتغيرات البيئية — CEEF-138 AUDIT-SEC-001..004.</summary>
public static class ApiSecuritySettings
{
    public const string ApiKeyEnv = "BRQOSH_API_KEY";
    public const string ProvisioningKeyEnv = "BRQOSH_PROVISIONING_KEY";
    public const string SaaSModeEnv = "BRQOSH_SAAS_MODE";

    public static string? ReadApiKey() => Environment.GetEnvironmentVariable(ApiKeyEnv);

    public static string? ReadProvisioningKey() => Environment.GetEnvironmentVariable(ProvisioningKeyEnv);

    public static bool IsSaaSMode()
    {
        var raw = Environment.GetEnvironmentVariable(SaaSModeEnv);
        return string.Equals(raw, "1", StringComparison.Ordinal)
               || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureProductionApiKey(string? apiKey, bool isProduction)
    {
        if (isProduction && string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"{ApiKeyEnv} is required in Production.");
    }

    public static void EnsureProductionProvisioningKey(string? provisioningKey, bool isProduction)
    {
        if (isProduction && string.IsNullOrWhiteSpace(provisioningKey))
            throw new InvalidOperationException($"{ProvisioningKeyEnv} is required in Production.");
    }
}
