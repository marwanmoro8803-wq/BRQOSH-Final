using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brqosh.Infrastructure.Persistence;

public class BrqoshDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BrqoshDbContext>
{
    public BrqoshDbContext CreateDbContext(string[] args) =>
        BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=brqosh-migrations-design.db");
}
