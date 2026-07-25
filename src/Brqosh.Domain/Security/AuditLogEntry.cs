using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Domain.Security;

/// <summary>
/// سجل تدقيق — يُسجَّل عند كل عملية ترحيل أو تعديل حرج.
///
/// (O-19، CEEF-212) كل إدخال يحمل تجزئة محتواه مربوطة بتجزئة سابقه — سلسلة.
/// معترض EF يمنع التعديل والحذف **داخل التطبيق فقط**؛ أي وصول مباشر للقاعدة
/// (SQL خام، أداة إدارة، نسخة مستعادة) كان يعدّل أو يحذف بلا أثر. السلسلة لا تمنع
/// العبث — تجعله **مكشوفاً**: تغيير حقل واحد أو حذف صفّ يكسر الارتباط عند أول تحقق.
/// </summary>
public class AuditLogEntry
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Username { get; private set; }
    public string Action { get; private set; }
    public string EntityType { get; private set; }
    public string EntityKey { get; private set; }
    public string Details { get; private set; }
    public string? Reason { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>ترتيب الإدخال ضمن سلسلة الشركة — يبدأ من 1، وفجوةٌ فيه دليلُ حذف.</summary>
    public long Sequence { get; private set; }

    /// <summary>تجزئة الإدخال السابق في السلسلة — فارغة للأول.</summary>
    public string PreviousHash { get; private set; } = GenesisHash;

    /// <summary>تجزئة هذا الإدخال: محتواه + تجزئة سابقه.</summary>
    public string EntryHash { get; private set; } = "";

    /// <summary>تجزئة البداية — جذر سلسلة كل شركة.</summary>
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private AuditLogEntry()
    {
        Username = null!;
        Action = null!;
        EntityType = null!;
        EntityKey = null!;
        Details = null!;
    }

    public static AuditLogEntry Record(long companyId, string username, string action,
        string entityType, string entityKey, string details = "", string? reason = null)
    {
        return new AuditLogEntry
        {
            CompanyId = companyId,
            Username = username,
            Action = action,
            EntityType = entityType,
            EntityKey = entityKey,
            Details = details,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OccurredAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>يربط الإدخال بسابقه في السلسلة — يُستدعى مرة واحدة قبل الحفظ.</summary>
    public void ChainTo(long sequence, string previousHash)
    {
        if (Sequence != 0)
            throw new InvalidOperationException("إدخال التدقيق مربوط بالسلسلة مسبقاً.");

        Sequence = sequence;
        PreviousHash = string.IsNullOrWhiteSpace(previousHash) ? GenesisHash : previousHash;
        EntryHash = ComputeHash();
    }

    /// <summary>
    /// إعادة حساب التجزئة من الحقول الحالية — أساس كشف العبث.
    /// الفاصل U+001F لا يظهر في نصوص المستخدم، فلا يمكن تزوير حدود الحقول بمحتواها.
    /// </summary>
    public string ComputeHash()
    {
        var canonical = string.Join('',
            CompanyId.ToString(),
            Sequence.ToString(),
            Username,
            Action,
            EntityType,
            EntityKey,
            Details,
            Reason ?? "",
            // صيغة مستقلة عن المزوّد: بلا وسم DateTimeKind (يُفقد عند القراءة من SQLite)
            // وبدقة الملّي ثانية (PostgreSQL timestamptz يقتطع إلى ميكروثانية، فالرقم
            // السابع يضيع). بدونها تفشل إعادة حساب التجزئة على بيانات سليمة تماماً.
            OccurredAtUtc.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
            PreviousHash);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>محتوى الإدخال يطابق تجزئته المخزّنة (لم يُعبَث بحقوله).</summary>
    public bool ContentMatchesHash() =>
        string.Equals(EntryHash, ComputeHash(), StringComparison.OrdinalIgnoreCase);
}
