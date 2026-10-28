using System.Text.Json;
using System.Text.Json.Serialization;

namespace Brqosh.Infrastructure.Security;

/// <summary>إعدادات قناة واتساب — من متغيرات البيئة أو %APPDATA%\BRQOSH\whatsapp.json</summary>
public sealed record WhatsAppChannelOptions
{
    public string AccessToken { get; init; } = "";
    public string PhoneNumberId { get; init; } = "";
    public string ApiVersion { get; init; } = "v21.0";
    public string TemplateName { get; init; } = "brqosh_otp";
    public string TemplateLanguage { get; init; } = "ar";
    /// <summary>قالب مصادقة Meta (زر نسخ الرمز) — يُفضّل لرموز OTP.</summary>
    public bool UseAuthenticationTemplate { get; init; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(PhoneNumberId);

    public static WhatsAppChannelOptions? TryLoad()
    {
        var fromEnv = FromEnvironment();
        if (fromEnv.IsConfigured)
            return fromEnv;

        return FromConfigFile();
    }

    public static WhatsAppChannelOptions FromEnvironment()
    {
        return new WhatsAppChannelOptions
        {
            AccessToken = Environment.GetEnvironmentVariable("BRQOSH_WHATSAPP_ACCESS_TOKEN") ?? "",
            PhoneNumberId = Environment.GetEnvironmentVariable("BRQOSH_WHATSAPP_PHONE_NUMBER_ID") ?? "",
            ApiVersion = Environment.GetEnvironmentVariable("BRQOSH_WHATSAPP_API_VERSION") ?? "v21.0",
            TemplateName = Environment.GetEnvironmentVariable("BRQOSH_WHATSAPP_TEMPLATE_NAME") ?? "brqosh_otp",
            TemplateLanguage = Environment.GetEnvironmentVariable("BRQOSH_WHATSAPP_TEMPLATE_LANG") ?? "ar",
            UseAuthenticationTemplate = !string.Equals(
                Environment.GetEnvironmentVariable("BRQOSH_WHATSAPP_AUTH_TEMPLATE"),
                "0",
                StringComparison.Ordinal),
        };
    }

    private static WhatsAppChannelOptions? FromConfigFile()
    {
        var path = ConfigFilePath;
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<WhatsAppConfigFile>(json, JsonOptions);
            if (file is null)
                return null;

            return new WhatsAppChannelOptions
            {
                // ‏ يُفَكُّ ما كان محميّاً، ويمرّ ما كان صريحاً كما هو —
                // ‏فملفٌّ قديمٌ لا يتوقّف عن العمل، ويُعاد حفظُه محميّاً عند أوّل كتابة.
                AccessToken = LocalSecretProtector.Unprotect(file.AccessToken ?? ""),
                PhoneNumberId = file.PhoneNumberId ?? "",
                ApiVersion = file.ApiVersion ?? "v21.0",
                TemplateName = file.TemplateName ?? "brqosh_otp",
                TemplateLanguage = file.TemplateLanguage ?? "ar",
                UseAuthenticationTemplate = file.UseAuthenticationTemplate ?? true,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string ConfigFilePath => Path.Combine(
        Brqosh.Infrastructure.Persistence.BrqoshDataRoot.Path(), "whatsapp.json");

    public static void SaveToConfigFile(WhatsAppChannelOptions options)
    {
        var dir = Path.GetDirectoryName(ConfigFilePath)!;
        Directory.CreateDirectory(dir);
        var file = new WhatsAppConfigFile
        {
            // ‏ **الرمزُ يُحفَظ محميّاً كسائر الأسرار.**
            // ‏كان يُكتب صريحاً في %APPDATA%\BRQOSH\whatsapp.json بينما أسرارُ ZATCA
            // ‏وقاعدةِ البيانات محميّةٌ بـDPAPI — ورمزُ Meta يُرسل رسائلَ باسم المنشأة
            // ‏ويصلح لانتحالها، فهو ليس أدنى منها خطراً.
            AccessToken = LocalSecretProtector.Protect(options.AccessToken),
            PhoneNumberId = options.PhoneNumberId,
            ApiVersion = options.ApiVersion,
            TemplateName = options.TemplateName,
            TemplateLanguage = options.TemplateLanguage,
            UseAuthenticationTemplate = options.UseAuthenticationTemplate,
        };
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(file, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class WhatsAppConfigFile
    {
        public string? AccessToken { get; set; }
        public string? PhoneNumberId { get; set; }
        public string? ApiVersion { get; set; }
        public string? TemplateName { get; set; }
        public string? TemplateLanguage { get; set; }
        public bool? UseAuthenticationTemplate { get; set; }
    }
}
