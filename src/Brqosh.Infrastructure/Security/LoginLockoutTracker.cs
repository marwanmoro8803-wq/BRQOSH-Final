using System.Collections.Concurrent;

namespace Brqosh.Infrastructure.Security;

/// <summary>قفل تسجيل الدخول بعد محاولات فاشلة — في الذاكرة لكل عملية تشغيل.</summary>
public static class LoginLockoutTracker
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private sealed class Entry
    {
        public int Failures;
        public DateTime? LockedUntilUtc;
    }

    private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);

    public static void ThrowIfLocked(long companyId, string username)
    {
        var key = Key(companyId, username);
        if (!Entries.TryGetValue(key, out var entry))
            return;

        if (entry.LockedUntilUtc is { } until && until > DateTime.UtcNow)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((until - DateTime.UtcNow).TotalMinutes));
            throw new Domain.Accounting.AccountingException(
                $"الحساب مقفل مؤقتاً بعد محاولات فاشلة. حاول بعد {minutes} دقيقة.");
        }
    }

    public static void RegisterFailure(long companyId, string username)
    {
        var key = Key(companyId, username);
        var entry = Entries.GetOrAdd(key, _ => new Entry());
        lock (entry)
        {
            entry.Failures++;
            if (entry.Failures >= MaxFailedAttempts)
            {
                entry.LockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
                entry.Failures = 0;
            }
        }
    }

    public static void RegisterSuccess(long companyId, string username)
    {
        Entries.TryRemove(Key(companyId, username), out _);
    }

    private static string Key(long companyId, string username) =>
        $"{companyId}|{username.Trim().ToLowerInvariant()}";
}
