namespace Brqosh.Infrastructure.Security;

/// <summary>
/// اختيار قناة تسليم رمز التحقق.
///
/// <para>كان المصنع يعيد <see cref="InAppOtpNotificationChannel"/> <b>دائماً</b>، و
/// <see cref="MetaWhatsAppNotificationChannel"/> مبنيّ كاملاً بلا مستدعٍ واحد. والنتيجة أن
/// المسار كان ميتاً في الإنتاج ويعلن نجاحاً كاذباً.</para>
///
/// <para>الترتيب الآن صريح: قناة حقيقية إن هُيّئت · وإلا فالعرض داخل التطبيق خارج الإنتاج
/// (حيث كشفُ الرمز مقصود ومقبول) · وإلا ففشلٌ صريح.</para>
/// </summary>
public static class WhatsAppNotificationChannelFactory
{
    private static IWhatsAppNotificationChannel? _cached;

    public static IWhatsAppNotificationChannel Resolve() => _cached ??= Build();

    private static IWhatsAppNotificationChannel Build()
    {
        var options = WhatsAppChannelOptions.TryLoad();
        if (options is not null && options.IsConfigured)
            return new MetaWhatsAppNotificationChannel(options);

        return Brqosh.Application.Setup.AppEnvironmentConfig.IsProduction
            ? new UnavailableOtpNotificationChannel()
            : new InAppOtpNotificationChannel();
    }

    internal static void ResetForTests() => ResetCache();

    public static void ResetCache() => _cached = null;
}
