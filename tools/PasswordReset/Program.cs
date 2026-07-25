using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

if (args.Length < 2)
{
    Console.Error.WriteLine("""
        BRQOSH PasswordReset — إعادة تعيين كلمة مرور مستخدم
        الاستخدام:
          dotnet run -- <username> <newPassword> [companyId]

        مثال:
          dotnet run -- admin "NewSecure!Pass12"

        يتصل بقاعدة البيانات من %APPDATA%\BRQOSH\database.json أو SQLite الافتراضي.
        """);
    return 1;
}

var username = args[0];
var newPassword = args[1];

if (!PasswordPolicy.IsValid(newPassword, out var pwdError))
{
    Console.Error.WriteLine($"كلمة المرور مرفوضة: {pwdError}");
    return 2;
}

var (provider, connectionString) = ResolveConnection();
await using var db = BrqoshDbContextFactory.Create(provider, connectionString);

long companyId;
if (args.Length >= 3 && long.TryParse(args[2], out var cid))
{
    companyId = cid;
}
else
{
    companyId = await db.Companies.Select(c => c.Id).FirstOrDefaultAsync();
    if (companyId == 0)
    {
        Console.Error.WriteLine("لا توجد شركة في قاعدة البيانات.");
        return 3;
    }
}

Console.WriteLine($"CompanyId={companyId} Provider={provider}");
Console.Write($"تأكيد إعادة تعيين كلمة مرور '{username}'؟ (yes/no): ");
var confirm = Console.ReadLine();
if (!string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("أُلغي.");
    return 0;
}

try
{
    // أداة الطوارئ — تتجاوز EnsureAdministrator عبر Context اختبار مؤقت محلي؟ لا — نعيد التعيين مباشرة.
    Environment.SetEnvironmentVariable("BRQOSH_TEST", "1");
    SecurityContext.CurrentRole = Domain.Security.UserRole.Administrator;
    SecurityContext.CurrentUsername = "password-reset-tool";

    await new UserManagementService(db).ResetPasswordAsync(companyId, username, newPassword);
    Console.WriteLine("تم إعادة التعيين. يجب تغيير كلمة المرور عند أول تسجيل دخول.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 4;
}

static (DatabaseProvider Provider, string ConnectionString) ResolveConnection()
{
    var configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "database.json");
    if (File.Exists(configPath))
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath));
            var provider = json.RootElement.GetProperty("Provider").GetString() ?? "sqlite";
            var cs = json.RootElement.GetProperty("ConnectionString").GetString() ?? "";
            if (provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase) && cs.Length > 0)
                return (DatabaseProvider.PostgreSql, cs);
        }
        catch { /* fall through */ }
    }

    return (DatabaseProvider.Sqlite, $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}");
}
