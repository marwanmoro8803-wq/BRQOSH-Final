using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.TestSupport;

public static class TestUserFactory
{
    public const string DefaultPassword = "TestPass99!!";

    public static async Task<AppUser> CreateAdminAsync(BrqoshDbContext db, long companyId,
        string username = "testadmin", string? password = null)
    {
        var pwd = password ?? DefaultPassword;
        var user = AppUser.Create(companyId, username, UserAuthService.HashPassword(pwd),
            "مدير اختبار", UserRole.Administrator);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
