namespace Brqosh.Domain.Crm;

public enum CrmLeadStatus
{
    New,
    Qualified,
    Converted,
    Disqualified,
}

/// <summary>عميل محتمل قبل التحويل إلى Customer — G-10 CRM.</summary>
public class CrmLead : ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Name { get; private set; }
    public string Phone { get; private set; }
    public string Email { get; private set; }
    public string Source { get; private set; }
    public CrmLeadStatus Status { get; private set; } = CrmLeadStatus.New;
    public long? CustomerId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CrmLead()
    {
        Name = null!;
        Phone = null!;
        Email = null!;
        Source = null!;
    }

    private CrmLead(long companyId, string name, string phone, string email, string source)
    {
        CompanyId = companyId;
        Name = name;
        Phone = phone;
        Email = email;
        Source = source;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static CrmLead Create(long companyId, string name, string phone = "", string email = "", string source = "مباشر")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم العميل المحتمل مطلوب.", nameof(name));
        return new CrmLead(companyId, name.Trim(), (phone ?? "").Trim(), (email ?? "").Trim(), (source ?? "مباشر").Trim());
    }

    public void MarkQualified()
    {
        if (Status is CrmLeadStatus.Converted or CrmLeadStatus.Disqualified)
            throw new InvalidOperationException("لا يمكن تأهيل عميل محتمل مُحوَّل أو مرفوض.");
        Status = CrmLeadStatus.Qualified;
    }

    public void MarkConverted(long customerId)
    {
        if (customerId <= 0)
            throw new ArgumentException("معرّف العميل مطلوب.", nameof(customerId));
        Status = CrmLeadStatus.Converted;
        CustomerId = customerId;
    }

    public void Disqualify()
    {
        if (Status == CrmLeadStatus.Converted)
            throw new InvalidOperationException("لا يمكن رفض عميل محتمل مُحوَّل.");
        Status = CrmLeadStatus.Disqualified;
    }

    /// <summary>
    /// إيقاف العميل المحتمَل — <b>حالةٌ تشغيلية لا واقعةُ حذف</b>.
    ///
    /// <para> كان يضبط <c>DeletedAtUtc</c> أيضاً، ومرشّح الاستعلام العامّ
    /// يُخفي كلَّ ما له تاريخ حذف — <b>فيختفي من قوائمه تماماً</b> لا يظهر موقوفاً. وزرُّ
    /// الشاشة مكتوبٌ عليه «إيقاف» ورسالةُ التأكيد تسأل «إيقاف …؟»، فكان الوعد إيقافاً
    /// والفعلُ حذفاً. ولا رجعة: لا <c>Reactivate</c> في الكيان أصلاً.</para>
    ///
    /// <para>أُصلح هذا في <see cref="Brqosh.Domain.Accounting.Account"/> ضمن م0-١٣، وبقيت
    /// سبعةُ كياناتٍ أخرى بالجسم القديم حرفاً بحرف — فالإصلاح طُبِّق على كيانٍ واحد ولم يُعمَّم.</para>
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>إعادة تفعيل العميل المحتمَل الموقوف.</summary>
    public void Reactivate() => IsActive = true;
}
