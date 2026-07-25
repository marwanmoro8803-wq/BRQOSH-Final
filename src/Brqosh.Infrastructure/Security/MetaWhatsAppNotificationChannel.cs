using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Brqosh.Infrastructure.Security;

/// <summary>
/// إرسال رموز OTP عبر Meta WhatsApp Cloud API (graph.facebook.com).
/// يتطلب قالب مصادقة معتمد في Meta Business Manager.
/// </summary>
public sealed class MetaWhatsAppNotificationChannel : IWhatsAppNotificationChannel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly WhatsAppChannelOptions _options;

    public MetaWhatsAppNotificationChannel(WhatsAppChannelOptions options)
    {
        if (!options.IsConfigured)
            throw new InvalidOperationException("إعدادات واتساب غير مكتملة.");
        _options = options;
    }

    public WhatsAppSendResult SendOtp(string phoneE164, string messageAr)
    {
        var code = ExtractCode(messageAr);
        if (string.IsNullOrWhiteSpace(code))
            return new WhatsAppSendResult(false, null, "تعذر استخراج رمز OTP من الرسالة.");

        if (string.IsNullOrWhiteSpace(_options.TemplateName))
        {
            return new WhatsAppSendResult(false, null,
                "قالب واتساب مطلوب — عيّن BRQOSH_WHATSAPP_TEMPLATE_NAME أو TemplateName في whatsapp.json.");
        }

        try
        {
            var recipient = ToWhatsAppRecipient(phoneE164);
            var payload = BuildTemplatePayload(recipient, code);
            var url = $"https://graph.facebook.com/{_options.ApiVersion.Trim('/')}/{_options.PhoneNumberId}/messages";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            request.Content = JsonContent.Create(payload);

            var response = Http.Send(request);
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                var apiError = TryParseApiError(body);
                return new WhatsAppSendResult(false, null,
                    $"فشل إرسال واتساب ({(int)response.StatusCode}): {apiError ?? body}");
            }

            return new WhatsAppSendResult(true, null, null);
        }
        catch (Exception ex)
        {
            return new WhatsAppSendResult(false, null, ex.Message);
        }
    }

    internal static object BuildTemplatePayload(string recipient, string code, WhatsAppChannelOptions? options = null)
    {
        options ??= new WhatsAppChannelOptions();

        if (string.Equals(options.TemplateName, "hello_world", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                messaging_product = "whatsapp",
                to = recipient,
                type = "template",
                template = new
                {
                    name = "hello_world",
                    language = new { code = options.TemplateLanguage.Length > 0 ? options.TemplateLanguage : "en_US" },
                },
            };
        }

        var components = new List<object>
        {
            new
            {
                type = "body",
                parameters = new[] { new { type = "text", text = code } },
            },
        };

        if (options.UseAuthenticationTemplate)
        {
            components.Add(new
            {
                type = "button",
                sub_type = "url",
                index = "0",
                parameters = new[] { new { type = "text", text = code } },
            });
        }

        return new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = recipient,
            type = "template",
            template = new
            {
                name = options.TemplateName,
                language = new { code = options.TemplateLanguage },
                components,
            },
        };
    }

    internal static string ToWhatsAppRecipient(string phoneE164) =>
        phoneE164.TrimStart('+');

    internal static string? ExtractCode(string message)
    {
        var marker = "الرمز:";
        var idx = message.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;
        return message[(idx + marker.Length)..].Trim().Split(' ', '\n', '\r')[0];
    }

    private static string? TryParseApiError(string body)
    {
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<MetaErrorResponse>(body);
            return parsed?.Error?.Message;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed class MetaErrorResponse
    {
        [JsonPropertyName("error")]
        public MetaError? Error { get; set; }
    }

    private sealed class MetaError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error_user_msg")]
        public string? UserMessage { get; set; }
    }
}
