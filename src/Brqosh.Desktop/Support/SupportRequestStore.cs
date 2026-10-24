using System.IO;
using System.Text;
using System.Text.Json;

namespace Brqosh.Desktop.Support;

/// <summary>نوعُ الطلب الذي يرفعه المستخدم.</summary>
public enum SupportRequestKind
{
    Question = 0,
    Problem = 1,
    Complaint = 2,
    Suggestion = 3,
}

/// <summary>حالةُ الطلب على هذا الجهاز.</summary>
public enum SupportRequestStatus
{
    /// <summary>سُجّل ولم يُغلق.</summary>
    Open = 0,

    /// <summary>أغلقه المستخدم (حُلّ أو سُحب).</summary>
    Closed = 1,
}

/// <summary>طلبُ دعمٍ أو شكوى مسجَّلٌ على هذا الجهاز.</summary>
public sealed class SupportRequest
{
    public string Id { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public SupportRequestKind Kind { get; set; }
    public string Subject { get; set; } = "";
    public string Details { get; set; } = "";
    public SupportRequestStatus Status { get; set; }

    /// <summary>الإصدارُ يوم الرفع — فالمشكلةُ تُقرأ بإصدارها.</summary>
    public string AppVersion { get; set; } = "";

    public string Company { get; set; } = "";
    public string User { get; set; } = "";
}

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · الدعمُ والشكاوى) <b>سجلٌّ حقيقيٌّ بطلبات الدعم والشكاوى، على هذا الجهاز.</b>
///
/// <para><b>ما كان:</b> جدولُ «تذاكر الدعم» في الشاشة فارغٌ دائماً ونصُّه «لا تُدار من داخل البرنامج» — لا نموذجَ لرفع مشكلة أو شكوى،
/// ولا أثرَ لما يُكتب. <b>الآن</b> يكتب المستخدمُ نوعَ الطلب وموضوعَه وتفاصيله فيُسجَّل بأرقامٍ متسلسلة (<c>SR-0001</c>…) في ملفٍّ
/// في بيانات المستخدم، ويفتح بريداً جاهزاً إلى عنوان الدعم المعتمد إن وُجد (<c>config/support.json</c>).</para>
///
/// <para><b>وبصراحة:</b> البرنامجُ لا يرسل شيئاً من نفسه — لا خادمَ تذاكر عند المورّد في هذه النسخة. السجلُّ مرجعٌ للمستخدم وما
/// يُرسَل بالبريد يرسله هو. وهذا ما تقوله الشاشة.</para>
/// </summary>
public static class SupportRequestStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "support-requests.json");

    /// <summary>
    /// أطولُ جسمٍ <b>مُرمَّزاً</b> يُحمَّل في رابط بريد. الحرفُ العربيُّ الواحد يصير ستةَ أحرفٍ مرمَّزة، فالعدُّ على المرمَّز لا على النصّ؛
    /// وفوق نحو ألفين تفشل عدّةُ برامج بريدٍ في فتح الرابط أو تقصّه صامتةً.
    /// </summary>
    public const int MailBodyEncodedLimit = 1800;

    /// <summary>الطلباتُ الأحدثُ أوّلاً. الملفُّ الغائبُ أو التالفُ ⟵ قائمةٌ فارغة؛ ولا يُسقط الشاشة.</summary>
    public static IReadOnlyList<SupportRequest> Load(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (!File.Exists(path))
                return [];
            var all = JsonSerializer.Deserialize<List<SupportRequest>>(File.ReadAllText(path), Options) ?? [];
            return all.OrderByDescending(r => r.CreatedUtc).ThenByDescending(r => r.Id, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>يسجّل طلباً جديداً برقمٍ متسلسل. يرمي <see cref="ArgumentException"/> على موضوعٍ أو تفاصيلَ فارغة.</summary>
    public static SupportRequest Add(
        SupportRequestKind kind, string subject, string details,
        string appVersion, string company, string user,
        string? path = null, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("موضوع الطلب مطلوب.", nameof(subject));
        if (string.IsNullOrWhiteSpace(details))
            throw new ArgumentException("تفاصيل الطلب مطلوبة.", nameof(details));

        path ??= DefaultPath;
        var all = Load(path).ToList();
        var request = new SupportRequest
        {
            Id = NextId(all),
            CreatedUtc = nowUtc ?? DateTime.UtcNow,
            Kind = kind,
            Subject = subject.Trim(),
            Details = details.Trim(),
            Status = SupportRequestStatus.Open,
            AppVersion = appVersion,
            Company = company,
            User = user,
        };
        all.Add(request);
        Write(all, path);
        return request;
    }

    /// <summary>يُغلق الطلب. <c>false</c> إن لم يوجد.</summary>
    public static bool Close(string id, string? path = null)
    {
        path ??= DefaultPath;
        var all = Load(path).ToList();
        var request = all.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        if (request is null)
            return false;
        request.Status = SupportRequestStatus.Closed;
        Write(all, path);
        return true;
    }

    /// <summary>رقمٌ يلي أكبرَ رقمٍ مسجَّل — فلا يتكرر رقمٌ بعد إغلاق طلب.</summary>
    internal static string NextId(IEnumerable<SupportRequest> existing)
    {
        var max = 0;
        foreach (var r in existing)
            if (r.Id.StartsWith("SR-", StringComparison.Ordinal) && int.TryParse(r.Id.AsSpan(3), out var n) && n > max)
                max = n;
        return $"SR-{max + 1:D4}";
    }

    private static void Write(List<SupportRequest> all, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // ‏كتابةٌ إلى ملفٍّ مؤقّتٍ ثم نقل: انقطاعُ التيار في منتصف الكتابة لا يترك سجلَّ المستخدم نصفَ ملفّ.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(all, Options));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>نصُّ الطلب الكامل بالسياق التقني — يُنسخ أو يُلصق في بريد.</summary>
    public static string ToText(SupportRequest request, string kindLabel)
    {
        var text = new StringBuilder();
        text.AppendLine($"{request.Id} · {kindLabel}");
        text.AppendLine(request.Subject);
        text.AppendLine();
        text.AppendLine(request.Details);
        text.AppendLine();
        text.AppendLine($"BRQOSH {request.AppVersion} · {request.Company} · {request.User} · {request.CreatedUtc:yyyy-MM-dd HH:mm} UTC");
        return text.ToString();
    }

    /// <summary>
    /// رابطُ <c>mailto:</c> إلى بريد الدعم المعتمد. الجسمُ يُقصّ حتى لا يتجاوز <see cref="MailBodyEncodedLimit"/> مرمَّزاً بعلامةٍ صريحة،
    /// والنصُّ الكاملُ يبقى في السجلّ وبزرّ النسخ.
    /// </summary>
    public static string MailtoUri(string supportEmail, SupportRequest request, string kindLabel)
    {
        const string Marker = "\n… (النص كاملاً في سجل الطلبات)";
        var body = ToText(request, kindLabel);
        if (Uri.EscapeDataString(body).Length > MailBodyEncodedLimit)
        {
            var keep = body.Length;
            while (keep > 0 && Uri.EscapeDataString(body[..keep] + Marker).Length > MailBodyEncodedLimit)
                keep -= Math.Max(1, keep / 20);
            body = body[..keep] + Marker;
        }

        return $"mailto:{supportEmail.Trim()}?subject={Uri.EscapeDataString($"[{request.Id}] {request.Subject}")}"
               + $"&body={Uri.EscapeDataString(body)}";
    }
}
