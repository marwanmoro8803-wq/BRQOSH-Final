namespace Brqosh.Infrastructure.Security;

public static class WhatsAppNotificationChannelFactory
{
    private static IWhatsAppNotificationChannel? _cached;

    public static IWhatsAppNotificationChannel Resolve() =>
        _cached ??= new InAppOtpNotificationChannel();

    internal static void ResetForTests() => ResetCache();

    public static void ResetCache() => _cached = null;
}
