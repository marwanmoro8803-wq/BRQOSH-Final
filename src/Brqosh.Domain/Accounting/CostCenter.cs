namespace Brqosh.Domain.Accounting;

/// <summary>مركز تكلفة — يُربط ببنود القيود لتحليل المصروفات والإيرادات.</summary>
public class CostCenter : ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }

    private CostCenter()
    {
        Code = null!;
        NameAr = null!;
    }

    private CostCenter(long companyId, string code, string nameAr)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
    }

    public static CostCenter Create(long companyId, string code, string nameAr)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود مركز التكلفة مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم مركز التكلفة مطلوب.");

        return new CostCenter(companyId, code.Trim().ToUpperInvariant(), nameAr.Trim());
    }

    public void Update(string nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم مركز التكلفة مطلوب.");
        NameAr = nameAr.Trim();
    }

    /// <summary>
    /// إيقاف مركز التكلفة — <b>حالةٌ تشغيلية لا واقعةُ حذف</b>.
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

    /// <summary>إعادة تفعيل مركز التكلفة الموقوف.</summary>
    public void Reactivate() => IsActive = true;
}
