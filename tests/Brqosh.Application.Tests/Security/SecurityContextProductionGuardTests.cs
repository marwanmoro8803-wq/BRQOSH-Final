using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// CEEF-125 — بوابة أمنية: تجاوز BRQOSH_TEST للـ RBAC يجب أن يُعطَّل قسرياً في وضع الإنتاج
/// حتى لو تسرّب المتغير إلى بيئة العميل.
/// </summary>
public class SecurityContextProductionGuardTests : IDisposable
{
    private readonly Domain.Security.UserRole? _savedRole = SecurityContext.CurrentRole;
    private readonly string? _savedUsername = SecurityContext.CurrentUsername;

    public void Dispose()
    {
        AppEnvironmentConfig.ResetForTests();
        SecurityContext.CurrentRole = _savedRole;
        SecurityContext.CurrentUsername = _savedUsername;
    }

    [Fact]
    public void Production_NoUser_PostingRefused_EvenWithTestEnvVar()
    {
        // BRQOSH_TEST=1 مضبوط بالفعل في harness الاختبارات — المتغير موجود فعلياً
        Assert.Equal("1", Environment.GetEnvironmentVariable("BRQOSH_TEST"));

        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
        SecurityContext.CurrentRole = null;

        Assert.Throws<AccountingException>(SecurityContext.EnsureCanPost);
        Assert.Throws<AccountingException>(SecurityContext.EnsureCanPostAccounting);
        Assert.Throws<AccountingException>(SecurityContext.EnsureCanCancelOrReverse);
        Assert.Throws<AccountingException>(SecurityContext.EnsureAdministrator);
    }

    [Fact]
    public void NonProduction_TestBypass_StillWorksForHarness()
    {
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Test);
        SecurityContext.CurrentRole = null;

        // لا استثناء — التجاوز الاختباري يعمل خارج الإنتاج
        SecurityContext.EnsureCanPost();
        SecurityContext.EnsureCanPostAccounting();
    }
}
