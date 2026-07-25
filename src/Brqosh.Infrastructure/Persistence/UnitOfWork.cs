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
        if (db.Database.CurrentTransaction is not null)
            return await action(ct);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await action(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
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
