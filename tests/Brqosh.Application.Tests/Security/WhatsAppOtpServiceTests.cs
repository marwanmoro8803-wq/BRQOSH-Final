using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

public class WhatsAppOtpServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private readonly CapturingWhatsAppChannel _channel;

    public WhatsAppOtpServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
        _channel = new CapturingWhatsAppChannel();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupUserAsync(string username, string phone = "0501234567")
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة OTP", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var user = AppUser.Create(_companyId, username, UserAuthService.HashPassword("OldPass12!!"),
            "مستخدم", UserRole.Accountant);
        user.SetMobilePhone("+966501234567");
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public void PhoneNumberNormalizer_Accepts_Saudi_Mobile()
    {
        Assert.True(PhoneNumberNormalizer.TryNormalizeSaudi("0501234567", out var e164, out _));
        Assert.Equal("+966501234567", e164);
    }

    [Fact]
    public async Task Recovery_ResetPassword_WithValidOtp()
    {
        await SetupUserAsync("recoverme");
        var service = new WhatsAppOtpService(_db, _channel);
        var send = await service.SendRecoveryCodeAsync(_companyId, "recoverme");
        Assert.Contains("***", send.MaskedPhone);
        Assert.NotNull(_channel.LastCode);

        await service.ResetPasswordAsync(_companyId, "recoverme", _channel.LastCode!, "NewPass12!!!");
        var user = await _db.AppUsers.SingleAsync(u => u.Username == "recoverme");
        Assert.True(UserAuthService.VerifyPassword("NewPass12!!!", user.PasswordHash));
    }

    [Fact]
    public async Task Registration_CreatesInactiveUser()
    {
        await SetupUserAsync("adminonly");
        var service = new WhatsAppOtpService(_db, _channel);
        await service.SendRegistrationCodeAsync(_companyId, "newuser", "0509876543");
        var user = await service.RegisterVerifiedUserAsync(
            _companyId, "newuser", "مستخدم جديد", "0509876543", "ValidPass12!!", _channel.LastCode!);

        Assert.False(user.IsActive);
        Assert.Equal(UserRole.Viewer, user.Role);
        Assert.Equal("+966509876543", user.MobilePhoneE164);
    }

    private sealed class CapturingWhatsAppChannel : IWhatsAppNotificationChannel
    {
        public string? LastCode { get; private set; }

        public WhatsAppSendResult SendOtp(string phoneE164, string messageAr)
        {
            var marker = "الرمز:";
            var idx = messageAr.IndexOf(marker, StringComparison.Ordinal);
            LastCode = idx >= 0
                ? messageAr[(idx + marker.Length)..].Trim().Split(' ', '\n', '\r')[0]
                : null;
            return new WhatsAppSendResult(true, LastCode, null);
        }
    }
}
