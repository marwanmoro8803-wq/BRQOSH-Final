using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Brqosh.Infrastructure.Persistence;

public enum DatabaseProvider
{
    /// <summary>PostgreSQL — الخيار المعتمد للإنتاج (5M+ سجل).</summary>
    PostgreSql,

    /// <summary>SQLite — للتشغيل الفوري بدون خادم وللاختبارات؛ ليس خيار الإنتاج.</summary>
    Sqlite,
}

/// <summary>
/// مصنع سياق قاعدة البيانات. الإنتاج على PostgreSQL وفق ADR-001؛
/// SQLite يُستخدم تلقائياً إذا لم يتوفر خادم PostgreSQL حتى لا يتعطل المستخدم.
/// </summary>
public static class BrqoshDbContextFactory
{
    public static BrqoshDbContext Create(DatabaseProvider provider, string connectionString)
    {
        var builder = new DbContextOptionsBuilder<BrqoshDbContext>()
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            // نزاهة: حارس ثبات سجل التدقيق يجب أن يسري على كل السياقات — بما فيها مسار سطح المكتب
            // (المصنع)، لا مسار DI فقط. إغلاق ح-3 (CEEF-200).
            // ‏(جاهزيّةُ الإطلاق · ح-٧٨) ‏وحارسُ الفترة المقفلة عند الحفظ — بالسُّنّة نفسِها: المساران معاً.
            .AddInterceptors(new AuditLogImmutabilityInterceptor(), new ClosedPeriodPostingInterceptor(), new BooksCurrencyInterceptor(),
                new ReadOnlyBooksInterceptor());
        switch (provider)
        {
            case DatabaseProvider.PostgreSql:
            // (CEEF-231 · ط5-٧) ‏**إعادةُ المحاولة على الأعطال العابرة — وكانت الآليةُ
            // ‏مبنيّةً وخاملة.**
            //
            // ‏`UnitOfWork.ExecuteInTransactionAsync` يستدعي `CreateExecutionStrategy()`
            // ‏منذ بنائه — **ويحصل على `NpgsqlExecutionStrategy` الذي لا يُعيد المحاولة
            // ‏أبداً**، لأنّ `EnableRetryOnFailure` لم يُستدعَ في أيّ موضع. فالسباكةُ
            // ‏قائمةٌ والأثرُ صفر.
            //
            // ‏**وأثرُه في الميدان**: ومضةُ شبكةٍ أو إعادةُ تشغيل خادمٍ أثناء ترحيل فاتورة
            // ‏**تخرج خطأً صلباً في وجه المستخدم** — والمعاملةُ ارتدّت فالبياناتُ سليمة،
            // ‏لكنّ عملَه ضاع ولا يعرف لماذا.
            //
            // ‏**وثلاثُ محاولاتٍ لا أكثر**: عطلٌ مستمرٌّ يجب أن يظهر، **وإعادةٌ بلا حدٍّ
            // ‏تُحوّل انقطاعاً إلى تعليقٍ صامت**. والمهلةُ خمسُ ثوانٍ — أطولُ من ومضة،
            // ‏وأقصرُ من صبر المستخدم.
                builder.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null));
                break;
            case DatabaseProvider.Sqlite:
                builder.UseSqlite(connectionString);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }

        return new BrqoshDbContext(builder.Options);
    }

    /// <summary>
    /// مسار قاعدة SQLite الافتراضية داخل جذر البيانات الموحّد — يحترم BRQOSH_DATA_ROOT
    /// فلا تكتب الاختبارات في قاعدة المستخدم الحقيقية (CEEF-203).
    /// </summary>
    public static string DefaultSqlitePath() =>
        Path.Combine(BrqoshDataRoot.Directory(), "brqosh.db");
}
