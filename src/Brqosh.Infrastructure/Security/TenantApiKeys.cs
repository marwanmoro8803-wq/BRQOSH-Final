namespace Brqosh.Infrastructure.Security;

/// <summary>
/// ‏(تقويةُ الـAPI · S-4) <b>مفاتيحُ API مربوطةٌ بمستأجر — فالمستأجرُ يُؤخذ من المفتاح لا من ترويسةٍ يكتبها المستدعي.</b>
///
/// <para>‏كان <c>X-Tenant-Id</c> مصدرَ المستأجر الوحيد خلف مفتاحٍ مشتركٍ واحد: من حمل المفتاحَ اختار أيَّ مستأجرٍ بكتابة رقمه،
/// <b>والعزلُ المفروضُ يفرض وجودَ الترويسة لا صدقَها</b>. والصيغةُ <c>tenantGuid:key;tenantGuid:key</c> في
/// <see cref="Env"/>، <b>وإعدادٌ معيبٌ يُسقط الإقلاع</b> — لا يُتجاهل فيعمل الخادمُ بمفاتيحَ أقلَّ ممّا ظنّ المُشغِّل.</para>
/// </summary>
public sealed class TenantApiKeys
{
    public const string Env = "BRQOSH_TENANT_API_KEYS";

    /// <summary>‏أقصرُ مفتاحٍ يُقبل — مفتاحٌ يُخمَّن ليس حدّاً بين مستأجرين.</summary>
    public const int MinimumKeyLength = 24;

    private readonly IReadOnlyList<(Guid Tenant, string Key)> _entries;

    private TenantApiKeys(IReadOnlyList<(Guid Tenant, string Key)> entries) => _entries = entries;

    public bool IsEmpty => _entries.Count == 0;

    public static TenantApiKeys Parse(string? setting)
    {
        var entries = new List<(Guid Tenant, string Key)>();
        if (string.IsNullOrWhiteSpace(setting))
            return new TenantApiKeys(entries);

        foreach (var raw in setting.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = raw.IndexOf(':');
            if (colon <= 0 || !Guid.TryParse(raw[..colon], out var tenant))
                throw Malformed("كلُّ مدخلٍ «مُعرِّفُ المستأجر:المفتاح»، والمُعرِّفُ GUID");

            var key = raw[(colon + 1)..].Trim();
            if (key.Length < MinimumKeyLength)
                throw Malformed($"مفتاحُ المستأجر {tenant} أقصرُ من {MinimumKeyLength} محرفاً");
            if (entries.Any(e => e.Tenant == tenant))
                throw Malformed($"المستأجرُ {tenant} مذكورٌ مرّتين");
            if (entries.Any(e => SecretComparer.Equals(e.Key, key)))
                throw Malformed("المفتاحُ نفسُه مربوطٌ بمستأجرين — فأيُّهما يحكم؟");

            entries.Add((tenant, key));
        }

        return new TenantApiKeys(entries);
    }

    /// <summary>‏مفتاحٌ مربوطٌ يساوي المشتركَ ملتبس — فيُرفض عند الإقلاع.</summary>
    public TenantApiKeys EnsureDistinctFrom(string? sharedKey)
    {
        if (_entries.Any(e => SecretComparer.Equals(e.Key, sharedKey)))
            throw Malformed($"مفتاحٌ مربوطٌ بمستأجرٍ يساوي {ApiSecuritySettings.ApiKeyEnv}");
        return this;
    }

    /// <summary>‏المستأجرُ المربوطُ بالمفتاح — أو <c>null</c>. ومقارنةٌ ثابتةُ الزمن مع كلّ مدخل، فلا يُكشف أيُّها اقترب.</summary>
    public Guid? TenantFor(string? presented)
    {
        Guid? match = null;
        foreach (var (tenant, key) in _entries)
            if (SecretComparer.Equals(key, presented))
                match = tenant;
        return match;
    }

    /// <summary>‏تحذيرُ الإقلاع: عزلٌ مفروضٌ ولا مفتاحَ مربوط ⟵ المستأجرُ من الترويسة وحدَها.</summary>
    public static string? HeaderOnlyWarning(bool enforceTenantIsolation, bool hasTenantKeys) =>
        enforceTenantIsolation && !hasTenantKeys
            ? $"عزلُ المستأجرين مفروضٌ والمستأجرُ يُؤخذ من X-Tenant-Id وحدَها خلف مفتاحٍ مشترك — من يحمل {ApiSecuritySettings.ApiKeyEnv} "
              + $"يختار أيَّ مستأجر. اربط لكلّ مستأجرٍ مفتاحَه في {Env}."
            : null;

    private static InvalidOperationException Malformed(string why) =>
        new($"{Env} معيب: {why}. لا يُقلع الخادمُ بمفاتيحَ لم تُفهم.");
}
