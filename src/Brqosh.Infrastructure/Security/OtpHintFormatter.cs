namespace Brqosh.Infrastructure.Security;

public static class OtpHintFormatter
{
    public static string Format(OtpSendResult result) =>
        string.IsNullOrWhiteSpace(result.DevHint)
            ? "تم إنشاء رمز التحقق — صالح 10 دقائق."
            : $"رمز التحقق: {result.DevHint} — صالح 10 دقائق.";
}
