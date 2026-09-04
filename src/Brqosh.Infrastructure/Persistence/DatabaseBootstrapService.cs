using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// تهيئة موحّدة لقاعدة البيانات عند كل تشغيل — EnsureCreated + ترقيات + بذور.
/// </summary>
public class DatabaseBootstrapService(BrqoshDbContext db)
{
    public async Task<CompanyBootstrapResult> InitializeAsync(
        string companyName,
        string vatNumber,
        BusinessProfile profile,
        string? adminUsername = null,
        string? adminPassword = null,
        string countryTemplateId = "saudi-arabia",
        CancellationToken ct = default)
    {
        var company = await new DatabaseInitializer(db)
            .InitializeAsync(companyName, vatNumber, profile, countryTemplateId);

        await new BrqoshSchemaUpdater(db).ApplyAsync(ct);

        if (!string.IsNullOrWhiteSpace(adminUsername) && !string.IsNullOrWhiteSpace(adminPassword))
            await new UserAuthService(db).CreateInitialAdminAsync(company.Id, adminUsername, adminPassword, ct);

        if (!await db.InventoryBalances.AnyAsync(b => b.CompanyId == company.Id, ct))
            await new InventoryService(db).RebuildBalancesFromMovementsAsync(company.Id, ct);

        return new CompanyBootstrapResult(company.Id, company.NameAr);
    }

    /// <summary>
    /// (CEEF-236 · م-٢٠) <b>مَن يسكن هذه القاعدة؟ — يُسأل قبل أن يُكتب فيها حرف.</b>
    ///
    /// <para><b>لماذا:</b> نافذةُ الإنقاذ (م-١٣) تقبل أيَّ سلسلة اتصالٍ يمرّ اختبارُها،
    /// <b>واختبارُ الاتصال يُثبت البلوغَ لا الهويّة</b>. ثمّ يمضي الإقلاعُ إلى
    /// <see cref="InitializeAsync"/> فـ<c>DatabaseInitializer</c> يفعل أحدَ أمرَين بلا سؤال:
    /// إن كانت القاعدةُ فارغةً <b>أنشأ فيها المخطَّطَ كلَّه وشركةً جديدة</b> باسمٍ من ملفّ
    /// الإعداد المحليّ؛ وإن كانت تحمل شركةً — أيَّ شركة — <b>تبنّاها كما هي</b>
    /// (<c>db.Companies.FirstOrDefaultAsync()</c>).</para>
    ///
    /// <para><b>فالأثرُ حالتان، كلتاهما صامتة:</b> مشغّلٌ يُصلح تركيباً فيكتب اسمَ قاعدةٍ
    /// افتراضيّاً (‏<c>brqosh</c>‎ هو الافتراضُ في الشاشة) بينما دفاترُه في <c>brqosh_prod</c>
    /// — فتُنصَّب أربعون هجرةً في قاعدةٍ أجنبيّة ويظنّ أنّ دفاترَه ضاعت؛ أو يُصيب نسخةَ
    /// اختبارٍ فيها منشأةٌ أخرى — <b>فيُرحَّل على دفترِ غيرِه</b> والترويسةُ تعرض اسمَه هو،
    /// لأنّ فحصَ الترخيص وعنوانَ النافذة يقرآن ملفَّ الإعداد المحليّ لا القاعدة.</para>
    ///
    /// <para><b>وهذا يقرأ ولا يكتب</b> — يُستدعى قبل التهيئة فيُعلَم أهي قاعدةٌ عذراء أم
    /// مسكونة، وبمن.</para>
    /// </summary>
    public async Task<DatabaseIdentity> InspectAsync(CancellationToken ct = default)
    {
        try
        {
            var company = await db.Companies
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, c.NameAr, c.VatRegistrationNumber })
                .FirstOrDefaultAsync(ct);

            return company is null
                ? new DatabaseIdentity(SchemaPresent: true, null, null, null)
                : new DatabaseIdentity(true, company.Id, company.NameAr, company.VatRegistrationNumber);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ‏جدولُ الشركات غيرُ موجود ⟵ قاعدةٌ بلا مخطَّط BRQOSH. وهذا **ليس خطأً** هنا،
            // ‏بل الجوابُ نفسُه: عذراءُ. والتمييزُ بين «فارغة» و«بلا مخطَّط» يهمّ المستدعي.
            return new DatabaseIdentity(SchemaPresent: false, null, null, null);
        }
    }
}

public record CompanyBootstrapResult(long CompanyId, string CompanyName);

/// <summary>
/// هويّةُ القاعدة كما وُجدت. <see cref="SchemaPresent"/> يعني وجودَ مخطَّط BRQOSH؛
/// و<see cref="CompanyId"/> فارغاً مع مخطَّطٍ حاضرٍ يعني قاعدةً مُهيّأةً بلا منشأة.
/// </summary>
public sealed record DatabaseIdentity(
    bool SchemaPresent,
    long? CompanyId,
    string? CompanyName,
    string? VatNumber)
{
    /// <summary>لا مخطَّطَ ولا منشأة — قاعدةٌ لم يسكنها BRQOSH قطّ.</summary>
    public bool IsVirgin => !SchemaPresent || CompanyId is null;

    /// <summary>
    /// هل تسكنها المنشأةُ المُعلَنةُ في الإعداد المحليّ؟ المقارنةُ على الاسم بعد التشذيب،
    /// وعلى الرقم الضريبيّ حين يكون مُعلَناً في الطرفين — فرقمٌ ضريبيٌّ مختلفٌ دليلٌ قاطع.
    /// </summary>
    public bool Matches(string expectedCompanyName, string? expectedVatNumber)
    {
        if (CompanyId is null) return false;
        if (!string.Equals((CompanyName ?? "").Trim(), (expectedCompanyName ?? "").Trim(), StringComparison.Ordinal))
            return false;
        if (!string.IsNullOrWhiteSpace(expectedVatNumber) && !string.IsNullOrWhiteSpace(VatNumber))
            return string.Equals(VatNumber.Trim(), expectedVatNumber.Trim(), StringComparison.Ordinal);
        return true;
    }
}
