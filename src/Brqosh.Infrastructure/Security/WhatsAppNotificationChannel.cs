using System.Text;

namespace Brqosh.Infrastructure.Security;

/// <summary>قناة تسليم رمز التحقق — العرض داخل التطبيق (بدون واتساب أو SMS).</summary>
public interface IWhatsAppNotificationChannel
{
    WhatsAppSendResult SendOtp(string phoneE164, string messageAr);
}

public sealed record WhatsAppSendResult(bool Success, string? DevHint, string? Error);

/// <summary>يعرض الرمز في الواجهة — لا إرسال خارجي.</summary>
public sealed class InAppOtpNotificationChannel : IWhatsAppNotificationChannel
{
    public WhatsAppSendResult SendOtp(string phoneE164, string messageAr)
    {
        var hint = ExtractCode(messageAr);
        // أمن (إغلاق ح-1، CEEF-200): لا يُكشف الرمز في الواجهة في الإنتاج — يمنع الاستيلاء
        // على الحساب عبر «نسيت كلمة المرور». الإنتاج يتطلب قناة إرسال خارجية أو إعادة تعيين بيد المدير.
        var revealed = Brqosh.Application.Setup.AppEnvironmentConfig.IsProduction ? null : hint;
        return new WhatsAppSendResult(true, revealed, null);
    }

    internal static string? ExtractCode(string message)
    {
        var marker = "الرمز:";
        var idx = message.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;
        return message[(idx + marker.Length)..].Trim().Split(' ', '\n', '\r')[0];
    }
}

[Obsolete("استخدم InAppOtpNotificationChannel — لا واتساب.")]
public sealed class LocalWhatsAppNotificationChannel : IWhatsAppNotificationChannel
{
    public WhatsAppSendResult SendOtp(string phoneE164, string messageAr) =>
        new InAppOtpNotificationChannel().SendOtp(phoneE164, messageAr);
}
