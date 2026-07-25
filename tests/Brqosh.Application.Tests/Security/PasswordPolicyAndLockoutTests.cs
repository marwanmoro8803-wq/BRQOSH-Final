using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.Security;

public class PasswordPolicyAndLockoutTests
{
    [Fact]
    public void PasswordPolicy_Requires_12_Chars_And_Complexity()
    {
        Assert.False(PasswordPolicy.IsValid("Short1!", out _));
        Assert.False(PasswordPolicy.IsValid("alllowercase1!", out _));
        Assert.False(PasswordPolicy.IsValid("ALLUPPERCASE1!", out _));
        Assert.False(PasswordPolicy.IsValid("NoDigitHere!!", out _));
        Assert.False(PasswordPolicy.IsValid("NoSpecialChar1", out _));
        Assert.True(PasswordPolicy.IsValid("ValidPass12!!", out var err));
        Assert.Null(err);
    }

    [Fact]
    public void LoginLockout_Locks_After_Five_Failures()
    {
        var companyId = 999001L;
        var user = "lockoutuser_" + Guid.NewGuid().ToString("N")[..8];

        for (var i = 0; i < LoginLockoutTracker.MaxFailedAttempts; i++)
            LoginLockoutTracker.RegisterFailure(companyId, user);

        Assert.Throws<Domain.Accounting.AccountingException>(() =>
            LoginLockoutTracker.ThrowIfLocked(companyId, user));

        LoginLockoutTracker.RegisterSuccess(companyId, user);
        LoginLockoutTracker.ThrowIfLocked(companyId, user);
    }

    [Fact]
    public void Viewer_Cannot_Call_UserManagement_List()
    {
        SecurityContext.CurrentRole = UserRole.Viewer;
        Assert.Throws<Domain.Accounting.AccountingException>(() =>
            SecurityContext.EnsureAdministrator());
    }
}
