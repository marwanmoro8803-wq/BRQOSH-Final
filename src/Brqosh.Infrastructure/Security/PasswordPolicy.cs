using System.Text.RegularExpressions;

namespace Brqosh.Infrastructure.Security;

/// <summary>سياسة كلمة المرور — حد أدنى 12 حرفاً مع تعقيد.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    public static bool IsValid(string password, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
        {
            error = $"كلمة المرور يجب أن تكون {MinLength} حرفاً على الأقل.";
            return false;
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower))
        {
            error = "كلمة المرور يجب أن تتضمن حرفاً كبيراً وآخر صغيراً.";
            return false;
        }

        if (!password.Any(char.IsDigit))
        {
            error = "كلمة المرور يجب أن تتضمن رقماً واحداً على الأقل.";
            return false;
        }

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
        {
            error = "كلمة المرور يجب أن تتضمن رمزاً خاصاً واحداً على الأقل.";
            return false;
        }

        if (Regex.IsMatch(password, @"admin123|password|123456", RegexOptions.IgnoreCase))
        {
            error = "كلمة المرور ضعيفة أو ممنوعة.";
            return false;
        }

        return true;
    }
}
