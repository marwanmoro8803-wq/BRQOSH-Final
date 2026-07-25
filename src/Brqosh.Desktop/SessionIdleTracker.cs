namespace Brqosh.Desktop;

/// <summary>تتبع آخر نشاط مستخدم لقفل الجلسة عند الخمول.</summary>
public static class SessionIdleTracker
{
    private static DateTime _lastActivityUtc = DateTime.UtcNow;

    public static void MarkActivity() => _lastActivityUtc = DateTime.UtcNow;

    public static bool IsIdle(TimeSpan timeout) =>
        DateTime.UtcNow - _lastActivityUtc > timeout;
}
