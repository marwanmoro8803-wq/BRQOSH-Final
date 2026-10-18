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

        // ‏(ح-١٥١) ‏المُرقِّعُ يُعرّف المخطّط (ويُعدّل سجلَّ التدقيق) — فلا يُشغَّل بدور التطبيق؛ والمخطّطُ مقيسٌ قبلها في EnsureSchemaAsync.
        if (await Security.LeastPrivilegeDatabaseRole.CanChangeSchemaAsync(db, ct))
            await new BrqoshSchemaUpdater(db).ApplyAsync(ct);

        string? recoveryKey = null;
        if (!string.IsNullOrWhiteSpace(adminUsername) && !string.IsNullOrWhiteSpace(adminPassword))
            recoveryKey = await new UserAuthService(db).CreateInitialAdminAsync(company.Id, adminUsername, adminPassword, ct);

        if (!await db.InventoryBalances.AnyAsync(b => b.CompanyId == company.Id, ct))
            await new InventoryService(db).RebuildBalancesFromMovementsAsync(company.Id, ct);

        return new CompanyBootstrapResult(company.Id, company.NameAr, company.Profile, recoveryKey);
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
        catch (Exception ex) when (IsMissingSchema(ex))
        {
            // ‏جدولُ الشركات غيرُ موجود ⟵ قاعدةٌ بلا مخطَّط BRQOSH. وهذا **ليس خطأً** هنا،
            // ‏بل الجوابُ نفسُه: عذراءُ. والتمييزُ بين «فارغة» و«بلا مخطَّط» يهمّ المستدعي.
            return new DatabaseIdentity(SchemaPresent: false, null, null, null);
        }
    }

    /// <summary>
    /// (CEEF-236 · م-٢٦) <b>«لا أستطيع القراءة» ليست «لا دفاترَ هنا».</b>
    ///
    /// <para>كان المُرشِّح <c>ex is not OperationCanceledException</c> — أي <b>كلُّ</b> إخفاق:
    /// صلاحيّةٌ ناقصة (<c>42501</c>) · مهلةٌ · انقطاعُ شبكةٍ بعد استنفاد إعادة المحاولة. فتُعلَن
    /// القاعدةُ «عذراء»، ويُقال للمشغّل <b>«هذه القاعدة لا تحمل دفاتر BRQOSH … راجع اسم القاعدة»</b>
    /// ودفاترُه فيها كاملة. <b>وهو ما يقع بالضبط بعد استعادةٍ بـ<c>pg_restore</c></b>: الجداولُ
    /// تُصبح مملوكةً لـ<c>postgres</c> ولا يُمنح دورُ التطبيق <c>SELECT</c>، فيُنصح صاحبُ الدفاتر
    /// المستعادةِ توّاً بأن يُغيّر اسمَ القاعدة — وهو الفعلُ الذي أُنشئ هذا الحارسُ ليمنعه.
    /// والاستثناءُ يُبتلع بلا سجلٍّ، فلا يبقى للدعم أثر.</para>
    ///
    /// <para><b>والأطرف:</b> الرسالةُ الصادقة كانت مكتوبةً سلفاً في <c>DescribeIdentityProblemAsync</c>
    /// («تعذّر فحصُ محتوى القاعدة: …») — <b>وهذا المُرشِّحُ الواسعُ هو ما جعلها غيرَ قابلةٍ للوصول</b>.
    /// فبتضييقه تصير مسلوكةً بلا سطرٍ جديد.</para>
    /// </summary>
    private static bool IsMissingSchema(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            // PostgreSQL: undefined_table · invalid_schema_name
            if (e is Npgsql.PostgresException pg && pg.SqlState is "42P01" or "3F000")
                return true;
            // SQLite: يُطابَق بالنصّ تفادياً لاعتمادٍ على تجميعة المزوّد في هذه الطبقة.
            if (e.GetType().Name == "SqliteException"
                && e.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

/// <param name="Profile">
/// (جاهزيّةُ الإطلاق · ح-٦٦ · sector-modules#10) <b>نظامُ العمل المخزَّنُ في القاعدة — لا المطلوب.</b>
/// على قاعدةٍ فيها منشأةٌ يُعيد <see cref="DatabaseInitializer"/> المنشأةَ القائمة ويتجاهل النظامَ المطلوب،
/// فالمطلوبُ اختيارُ معالجِ هذا الجهاز وحدَه. ويتبنّاه المُقلِع فتُبنى القوائمُ لنظام المنشأة.
/// </param>
/// <summary>
/// نتيجةُ التهيئة — و<paramref name="RecoveryKey"/> مفتاحُ استعادة المدير الأوّل <b>يُعرض مرّةً ثمّ لا يُعاد</b>
/// (<c>security-licensing-tenancy#7</c>)؛ عدمٌ متى لم يُنشأ مدير.
/// </summary>
public record CompanyBootstrapResult(long CompanyId, string CompanyName, BusinessProfile Profile, string? RecoveryKey = null);

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
