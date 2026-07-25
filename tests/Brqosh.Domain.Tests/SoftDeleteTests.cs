using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;

namespace Brqosh.Domain.Tests;

public class SoftDeleteTests
{
    [Fact]
    public void Customer_Deactivate_SetsDeletedAtUtcAndIsActive()
    {
        var customer = Customer.Create(1, "عميل");
        customer.Deactivate();

        Assert.NotNull(customer.DeletedAtUtc);
        Assert.False(customer.IsActive);
    }

    [Fact]
    public void Customer_Deactivate_IsIdempotent()
    {
        var customer = Customer.Create(1, "عميل");
        customer.Deactivate();
        var first = customer.DeletedAtUtc;
        customer.Deactivate();

        Assert.Equal(first, customer.DeletedAtUtc);
    }

    [Fact]
    public void AppUser_Activate_ClearsDeletedAtUtc()
    {
        var user = AppUser.Create(1, "u1", "hash", "مستخدم");
        user.Deactivate();
        Assert.NotNull(user.DeletedAtUtc);

        user.Activate();
        Assert.Null(user.DeletedAtUtc);
        Assert.True(user.IsActive);
    }
}
