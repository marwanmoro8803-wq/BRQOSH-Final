using System.IO;
using System.Text.Json;

namespace Brqosh.Desktop.Support;

/// <summary>سطرُ مستوى خدمةٍ كما اعتمده المورّد — لا كما خمّنته الشاشة.</summary>
public sealed record SupportPlan(string Plan, string Response, string Hours);

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>commercial-readiness#7</c>) <b>قنواتُ الدعم ومستوياتُ الخدمة من ملفٍّ يعتمده المورّد —
/// وبدونه تقول الشاشةُ ذلك ولا تخترع.</b>
///
/// <para><b>ما كان:</b> شاشةُ «دعم العملاء» المشحونة تعرض هاتفاً مؤقّتاً (<c>9200XXXXXX</c>) ووسماً داخليّاً
/// (<c>G-31 … (MVP)</c>) وثلاثَ باقاتٍ مكتوبةً في الشيفرة — منها «مؤسسي · 4 ساعات · 24/7» — <b>ومسودّتا التسويق
/// تقولان غيرَها</b> (P1 ثماني ساعاتٍ أو أربعٌ في أيّام العمل، بلا 24/7). فكلُّ عميلٍ يرى وعداً لم يُعتمد.</para>
///
/// <para><b>والإصلاحُ لا يختار اتّفاقيّةً</b> — ذلك قرارُ المالك: الشاشةُ تعرض ما في <c>config/support.json</c>
/// المشحون بجوار البرنامج، وبغيابه «لم تُعتمد بعد».</para>
/// </summary>
public sealed record SupportContact(bool IsConfigured, string? Email, string? Phone,
    IReadOnlyList<SupportPlan> Plans, string? Problem)
{
    public const string RelativePath = "config/support.json";

    public static SupportContact NotConfigured(string? problem = null) =>
        new(false, null, null, Array.Empty<SupportPlan>(), problem);

    /// <summary>
    /// يقرأ النصَّ كما في الملفّ. <b>لا نصّ ⟵ غيرُ معتمَد</b>؛ <b>نصٌّ تالف ⟵ غيرُ معتمَدٍ بسببه المكتوب</b> — لا يُبتلع.
    /// </summary>
    public static SupportContact Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return NotConfigured();

        SupportFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SupportFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            return NotConfigured($"ملفُّ الدعم تالف: {ex.Message}");
        }

        var plans = (file?.Plans ?? new List<SupportFilePlan>())
            .Where(p => !string.IsNullOrWhiteSpace(p.Plan))
            .Select(p => new SupportPlan(p.Plan!.Trim(), p.Response?.Trim() ?? "", p.Hours?.Trim() ?? ""))
            .ToList();
        var email = Clean(file?.Email);
        var phone = Clean(file?.Phone);

        return email is null && phone is null && plans.Count == 0
            ? NotConfigured()
            : new SupportContact(true, email, phone, plans, null);
    }

    /// <summary>الملفُّ بجوار البرنامج — وغيابُه حالةٌ مشروعة لا خطأ.</summary>
    public static SupportContact LoadBeside(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : NotConfigured();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class SupportFile
    {
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public List<SupportFilePlan>? Plans { get; set; }
    }

    private sealed class SupportFilePlan
    {
        public string? Plan { get; set; }
        public string? Response { get; set; }
        public string? Hours { get; set; }
    }
}
