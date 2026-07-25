using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.Security;

public class MetaWhatsAppNotificationChannelTests
{
    [Theory]
    [InlineData("+966582778405", "966582778405")]
    [InlineData("966582778405", "966582778405")]
    public void ToWhatsAppRecipient_StripsPlus(string input, string expected) =>
        Assert.Equal(expected, MetaWhatsAppNotificationChannel.ToWhatsAppRecipient(input));

    [Fact]
    public void ExtractCode_ParsesArabicMarker()
    {
        var message = "BRQOSH — رمز تأكيد إنشاء الحساب\nالرمز: 739553\nصالح لمدة 10 دقائق.";
        Assert.Equal("739553", MetaWhatsAppNotificationChannel.ExtractCode(message));
    }

    [Fact]
    public void BuildTemplatePayload_IncludesAuthButton_WhenEnabled()
    {
        var options = new WhatsAppChannelOptions
        {
            TemplateName = "brqosh_otp",
            TemplateLanguage = "ar",
            UseAuthenticationTemplate = true,
        };
        var payload = MetaWhatsAppNotificationChannel.BuildTemplatePayload("966501234567", "123456", options);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        Assert.Contains("brqosh_otp", json);
        Assert.Contains("123456", json);
        Assert.Contains("\"sub_type\":\"url\"", json);
    }

    [Fact]
    public void BuildTemplatePayload_HelloWorld_HasNoComponents()
    {
        var options = new WhatsAppChannelOptions { TemplateName = "hello_world", TemplateLanguage = "en_US" };
        var json = System.Text.Json.JsonSerializer.Serialize(
            MetaWhatsAppNotificationChannel.BuildTemplatePayload("966582778405", "123456", options));
        Assert.Contains("hello_world", json);
        Assert.DoesNotContain("components", json);
    }

    [Fact]
    public void Factory_UsesInAppChannel()
    {
        WhatsAppNotificationChannelFactory.ResetCache();
        var channel = WhatsAppNotificationChannelFactory.Resolve();
        Assert.IsType<InAppOtpNotificationChannel>(channel);
    }
}
