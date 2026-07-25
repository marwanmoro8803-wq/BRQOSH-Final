using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// تحقق اختياري من PostgreSQL — يتخطّى بدون database.json،
/// ولا يفشل على مخطط موجود مسبقاً (EnsureCreated على DB حيّة).
/// </summary>
public class PostgreSqlStartupTests
{
    [Fact]
    public async Task Startup_with_user_postgresql_config_or_skip()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "database.json");
        if (!File.Exists(configPath))
            return;

        var json = await File.ReadAllTextAsync(configPath);
        if (!json.Contains("postgresql", StringComparison.OrdinalIgnoreCase))
            return;

        var config = System.Text.Json.JsonSerializer.Deserialize<DatabaseConfigDto>(json);
        Assert.NotNull(config);

        await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, config!.ConnectionString);

        try
        {
            await new BrqoshSchemaUpdater(db).ApplyAsync();
        }
        catch (Exception ex) when (IsIgnorableSchemaConflict(ex))
        {
            // مخطط موجود من FullScenarioAudit / تشغيل سابق — نكمل التحقق بالقراءة
        }

        // إن كانت القاعدة فارغة بالكامل جرّب التهيئة؛ إن وُجدت شركة اكتفِ بالقراءة
        var hasCompany = await db.Companies.AnyAsync();
        if (!hasCompany)
        {
            try
            {
                await new DatabaseBootstrapService(db).InitializeAsync(
                    "marwan", "123456789012345", BusinessProfile.Manufacturing,
                    adminUsername: "pgadmin", adminPassword: TestUserFactory.DefaultPassword);
            }
            catch (Exception ex) when (IsIgnorableSchemaConflict(ex))
            {
                // سباق إنشاء متزامن مع عملية أخرى
            }
        }

        Assert.True(await db.Database.CanConnectAsync());
        // على الأقل يجب أن يستطيع قراءة جدول الشركات بعد المخطط
        _ = await db.Companies.CountAsync();
    }

    private static bool IsIgnorableSchemaConflict(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || text.Contains("42P07", StringComparison.OrdinalIgnoreCase);
    }

    private record DatabaseConfigDto(string Provider, string ConnectionString);
}
