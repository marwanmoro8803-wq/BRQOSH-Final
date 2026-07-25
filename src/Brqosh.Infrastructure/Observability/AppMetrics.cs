namespace Brqosh.Infrastructure.Observability;

public static class AppMetrics
{
    private static long _requests;
    private static long _errors;
    private static long _eventsStored;
    private static readonly DateTime StartedAtUtc = DateTime.UtcNow;

    public static void IncRequest() => Interlocked.Increment(ref _requests);

    public static void IncError() => Interlocked.Increment(ref _errors);

    public static void IncEventStored() => Interlocked.Increment(ref _eventsStored);

    public static object Snapshot() => new
    {
        requests = Interlocked.Read(ref _requests),
        errors = Interlocked.Read(ref _errors),
        eventsStored = Interlocked.Read(ref _eventsStored),
        startedAtUtc = StartedAtUtc,
    };
}
