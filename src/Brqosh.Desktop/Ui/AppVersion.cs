using System.Reflection;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (مراجعةُ رقم النسخة) <b>مصدرٌ واحدٌ لرقم الإصدار المعروض</b> — من <c>InformationalVersion</c> الذي يحقنه البناء
/// (<c>1.0.&lt;رقم البناء&gt;+&lt;بصمة الالتزام&gt;</c>). كان شعارُ شاشة الدخول نصّاً ثابتاً «ERP v4» لا يطابق شيئاً.
/// </summary>
public static class AppVersion
{
    /// <summary>الإصدارُ كما يُقرأ للمستخدم: <c>1.0.7</c> بلا بصمة الالتزام.</summary>
    public static string Display
    {
        get
        {
            var full = Full;
            var plus = full.IndexOf('+');
            return plus > 0 ? full[..plus] : full;
        }
    }

    /// <summary>الإصدارُ مع بصمة الالتزام — للتشخيص والدعم.</summary>
    public static string Full
    {
        get
        {
            var assembly = typeof(AppVersion).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return string.IsNullOrWhiteSpace(informational) ? assembly.GetName().Version?.ToString(3) ?? "—" : informational;
        }
    }
}
