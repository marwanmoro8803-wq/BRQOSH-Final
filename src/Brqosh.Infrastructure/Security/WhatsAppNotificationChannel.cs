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

/// <summary>
/// (CEEF-229 · م0-١١) لا قناة تسليم مهيّأة في الإنتاج — تفشل <b>صراحةً</b>.
///
/// <para>كان <see cref="InAppOtpNotificationChannel"/> يعيد في الإنتاج <c>Success: true</c> مع
/// <c>DevHint: null</c>: الرمز يُولَّد ويُحفَظ في القاعدة و<b>لا يُسلَّم لأحد</b>، والمستخدم يقرأ
/// «تم إرسال الرمز». فمسار «نسيت كلمة المرور» ميتٌ في كل تركيب حقيقيّ، ورسالةُ النجاح تُخفي
/// السبب فيظنّ المستخدم أن الرسالة تأخّرت — ويبقى مقفولاً خارج دفاتره.</para>
///
/// <para>الفشل الصريح لا يفتح الحساب لأحد، لكنه يقول الحقيقة ويوجّه إلى المخرج الفعليّ:
/// إعادة تعيين بيد مدير النظام، أو تهيئة قناة إرسال.</para>
/// </summary>
public sealed class UnavailableOtpNotificationChannel : IWhatsAppNotificationChannel
{
    public WhatsAppSendResult SendOtp(string phoneE164, string messageAr) =>
        new(false, null,
            "لا توجد قناة لإرسال رمز التحقق في هذا التركيب — راجع مدير النظام لإعادة تعيين كلمة المرور.");
}