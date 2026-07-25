using Brqosh.Domain.Modules;

namespace UiAudit;

/// <summary>ملف العمل الحالي لجلسة FlaUI — من BRQOSH_AUDIT_PROFILE.</summary>
internal static class ProfileAuditContext
{
  private static BusinessProfile? _current;

  public static BusinessProfile? Current => _current;

  public static void SetFromEnvironment()
  {
    var raw = Environment.GetEnvironmentVariable("BRQOSH_AUDIT_PROFILE");
    _current = !string.IsNullOrWhiteSpace(raw) &&
               Enum.TryParse<BusinessProfile>(raw.Trim(), ignoreCase: true, out var profile)
      ? profile
      : BusinessProfile.Manufacturing;
  }

  public static void Clear() => _current = null;
}
