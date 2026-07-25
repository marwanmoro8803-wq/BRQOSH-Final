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
            catch (DbUpdateConcurrencyException ex)
            {
                await tx.RollbackAsync(ct);

                // (ع-D1، CEEF-205) رمز التزامن كشف أن صفاً تغيّر بين القراءة والكتابة —
                // مستخدم آخر رحّل على نفس الصنف/الرصيد. العملية ارتدّت كاملة، فلا فساد.
                // لا نُعيد المحاولة تلقائياً: السياق يحمل كيانات قديمة، وإعادة ترحيل بحالة
                // بائتة في نظام محاسبي أخطر من خطأ واضح يعيده المستخدم بنفسه.
                throw new Domain.Accounting.AccountingException(
                    "تعذّر إتمام العملية: عُدِّلت البيانات من جلسة أخرى أثناء الحفظ. " +
                    "لم يُحفظ أي تغيير — أعد فتح الشاشة وحاول مرة أخرى.", ex);
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
