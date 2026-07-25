namespace Brqosh.Application.Setup;

/// <summary>وضع تشغيل التطبيق — يتحكم في سلوك قاعدة البيانات والترخيص.</summary>
public enum AppEnvironment
{
    Development = 0,
    Test = 1,
    Production = 2,
}

public static class AppEnvironmentConfig
{
    private static AppEnvironment? _cached;
    private static AppEnvironment? _override;

    public static AppEnvironment Current
    {
        get
        {
            if (_override is AppEnvironment forced)
                return forced;
            _cached ??= Resolve();
            return _cached.Value;
        }
    }

    public static bool IsProduction => Current == AppEnvironment.Production;

    /// <summary>للاختبارات فقط — يتجاوز متغير البيئة دون تلويث العملية إن أُزيل في finally.</summary>
    public static void ForceForTests(AppEnvironment? environment)
    {
        _override = environment;
        _cached = null;
    }

    public static void ResetForTests()
    {
        _override = null;
        _cached = null;
    }

    private static AppEnvironment Resolve()
    {
        var raw = Environment.GetEnvironmentVariable("BRQOSH_ENV");
        if (string.IsNullOrWhiteSpace(raw))
            return AppEnvironment.Development;

        return raw.Trim().ToLowerInvariant() switch
        {
            "production" or "prod" => AppEnvironment.Production,
            "test" => AppEnvironment.Test,
            _ => AppEnvironment.Development,
        };
    }
}
