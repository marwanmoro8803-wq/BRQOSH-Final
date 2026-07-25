namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// خطّاف ما بعد الالتزام لتفريغ طابور الأحداث (Outbox — ع-D2 / CEEF-204).
///
/// <see cref="UnitOfWork"/> ساكن ولا يملك حاوية حقن، فيُوصَل المرسِل من تسجيل DI
/// على نمط <c>DomainEventHub</c> المتّبع في المشروع. إن لم يُوصَل — كما في اختبارات
/// الوحدة — لا يحدث شيء، وتبقى الأحداث في الطابور بأمان.
/// </summary>
public static class OutboxDispatchHook
{
    private static Func<BrqoshDbContext, CancellationToken, Task>? _handler;

    public static void Wire(Func<BrqoshDbContext, CancellationToken, Task> handler) =>
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    public static void Reset() => _handler = null;

    internal static async Task OnCommittedAsync(BrqoshDbContext db, CancellationToken ct)
    {
        var handler = _handler;
        if (handler is null)
            return;

        try
        {
            await handler(db, ct);
        }
        catch
        {
            // الإرسال الخارجي لا يُفشل عملية محاسبية ملتزمة بالفعل.
            // الأحداث غير المُرسَلة تبقى بعلامة IsPendingDispatch لإعادة المحاولة.
        }
    }
}
