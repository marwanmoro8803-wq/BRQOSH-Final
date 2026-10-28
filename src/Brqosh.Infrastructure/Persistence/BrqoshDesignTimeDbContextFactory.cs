using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// مصنع وقت التصميم لتوليد الهجرات. **PostgreSQL حصراً** — الهجرات مسار الإنتاج وحده.
/// : توليدها سابقاً على SQLite أنتج أعمدة INTEGER/TEXT ووسم Sqlite:Autoincrement
/// الذي يتجاهله Npgsql، فكانت قاعدة PostgreSQL جديدة تُنشأ بأعمدة Id بلا identity ويفشل أول إدراج.
/// SQLite (تطوير/اختبار) يُهيَّأ بـ EnsureCreated + SchemaUpdater ولا يمرّ على الهجرات إطلاقاً.
/// </summary>
public class BrqoshDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BrqoshDbContext>
{
    public BrqoshDbContext CreateDbContext(string[] args) =>
        BrqoshDbContextFactory.Create(
            DatabaseProvider.PostgreSql,
            "Host=localhost;Port=5433;Database=brqosh_design;Username=postgres;Password=design");
}
