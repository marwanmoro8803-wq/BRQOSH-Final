using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// تنفيذ عمليات ذرية على قاعدة البيانات — كل SaveChanges داخل المعاملة تُلغى عند الفشل.
/// </summary>
public static class UnitOfWork
{
    public static async Task<T> ExecuteInTransactionAsync<T>(
        BrqoshDbContext db,
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct = default)
    {
        // معاملة متداخلة: السياق مربوط أصلاً من المستوى الخارجي.
        if (db.Database.CurrentTransaction is not null)
            return await action(ct);

        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            // Outbox (ع-D2، CEEF-204): ربط السياق يجعل أحداث النطاق تُكتب داخل هذه المعاملة،
            // فتُلغى مع المستند إن ارتدّت — بدل حدث «شبح» عن فاتورة لا وجود لها.
            using var ambient = AmbientDbContext.Bind(db);

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var value = await action(ct);
                await tx.CommitAsync(ct);
                return value;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        // الإرسال الخارجي بعد الالتزام حصراً — لا يُخطَر أحد بما قد يُلغى.
        // فشل الإرسال لا يُفشل العملية المحاسبية؛ الأحداث تبقى في الطابور لإعادة المحاولة.
        await OutboxDispatchHook.OnCommittedAsync(db, ct);
        return result;
    }

    public static Task ExecuteInTransactionAsync(
        BrqoshDbContext db,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default) =>
        ExecuteInTransactionAsync(db, async token =>
        {
            await action(token);
            return true;
        }, ct);
}
