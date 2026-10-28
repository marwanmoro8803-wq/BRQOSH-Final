using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// ‏ <b>وضعُ القراءة فقط يُفرض عند الحفظ — لا في كلّ شاشة.</b>
///
/// <para>‏حين يكون <see cref="ReadOnlyBooks.IsActive"/> يُرفض كلُّ حفظٍ يُضيف أو يُعدّل أو يحذف شيئاً من الدفاتر. <b>والمستثنى
/// ‏ما يلزم الدخولَ والمساءلةَ وحدَه</b>: سجلُّ التدقيق وسجلُّ الدخول ورموزُ التحقّق والمستخدمون (محاولاتُ الدخول وآخرُه) —
/// ‏فالمالكُ يدخل ويُسجَّل دخولُه، ولا يُكتب في دفتره. <b>ويُسجَّل حيث يُسجَّل حارسا التدقيق والفترة</b> — المصنعُ ومسارُ DI معاً.</para>
/// </summary>
public sealed class ReadOnlyBooksInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<Type> Allowed =
    [
        typeof(AuditLogEntry),
        typeof(LoginAuditEntry),
        typeof(OtpChallenge),
        typeof(AppUser),
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext? context)
    {
        if (!ReadOnlyBooks.IsActive || context is null)
            return;

        var written = context.ChangeTracker.Entries()
            .FirstOrDefault(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                                 && !Allowed.Contains(e.Metadata.ClrType));
        if (written is not null)
            throw new AccountingException(
                "انتهت صلاحيةُ الترخيص — الدفاترُ للقراءة فقط: تُعرض وتُطبع وتُصدَّر، ولا يُضاف إليها ولا يُعدَّل فيها شيء "
                + "حتى يُجدَّد الترخيص.");
    }
}
