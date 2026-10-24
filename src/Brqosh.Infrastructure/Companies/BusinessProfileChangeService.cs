using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Companies;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>ui-ux-i18n-printing#7</c> · قرارُ المالك 2026-10-18) <b>ملفُّ النشاط يغيّره المديرُ بعد الإعداد — بحارس.</b>
///
/// <para><b>ما كان:</b> الملفُّ يُختار مرّةً في المعالج و<c>UpdateBusinessProfile</c> بلا مستدعٍ؛ فمنشأةٌ أخطأت الاختيارَ (أو توسّع
/// نشاطُها) لا طريقَ لها إلّا منشأةٌ جديدة. <b>والحارس</b>: الملفُّ يحدّد الوحداتِ الظاهرة، فالانتقالُ إلى ملفٍّ يُخفي وحدةً فيها
/// بياناتٌ يُخفي دفاترَ قائمةً عن أصحابها — فيُرفض بأسماء الوحدات. والإضافةُ (ملفٌّ أوسع) تمرّ دائماً، والتغييرُ يُدقَّق.</para>
/// </summary>
public class BusinessProfileChangeService(BrqoshDbContext db)
{
    public async Task ChangeAsync(long companyId, BusinessProfile profile, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var previous = company.Profile;
        if (previous == profile)
            return;

        var lost = ModuleCatalog.ResolveModules(previous).Except(ModuleCatalog.ResolveModules(profile)).ToList();
        var withData = new List<ModuleKey>();
        foreach (var module in lost)
            if (await HasDataAsync(companyId, module, ct))
                withData.Add(module);
        if (withData.Count > 0)
            throw new ProfileHidesModulesWithDataException(profile, withData);

        company.UpdateBusinessProfile(profile);
        await db.SaveChangesAsync(ct);
        await new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(), "تعديل", "Company",
            companyId.ToString(), $"ملف النشاط: {previous} ← {profile}", null, ct);
    }

    /// <summary>هل للوحدة بياناتٌ في هذه المنشأة؟ — بجدولها الدالّ عليها؛ والوحدةُ بلا جدولٍ خاصٍّ لا تُعدّ ذاتَ بيانات.</summary>
    internal async Task<bool> HasDataAsync(long companyId, ModuleKey module, CancellationToken ct = default) => module switch
    {
        ModuleKey.Sales => await db.SalesInvoices.AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.Purchasing => await db.PurchaseInvoices.AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.Inventory => await db.StockMovements.AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.FixedAssets => await db.FixedAssets.IgnoreQueryFilters().AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.Payroll => await db.PayrollRuns.AnyAsync(x => x.CompanyId == companyId, ct)
                             || await db.Employees.IgnoreQueryFilters().AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.PointOfSale => await db.PosShifts.AnyAsync(x => x.CompanyId == companyId, ct),
        // ‏الوصفةُ تُحفظ قبل أيّ أمرٍ — فإخفاءُ الوحدة يُخفيها عن صاحبها وإن لم يُشغَّل أمرُ إنتاجٍ بعد.
        ModuleKey.Manufacturing => await db.WorkOrders.AnyAsync(x => x.CompanyId == companyId, ct)
                                   || await db.BillsOfMaterials.AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.ProjectsConstruction => await db.ConstructionProjects.AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.Crm => await db.CrmLeads.AnyAsync(x => x.CompanyId == companyId, ct)
                         || await db.CrmOpportunities.AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.RealEstate => await db.Properties.AnyAsync(x => x.CompanyId == companyId, ct)
                                || await db.LeaseContracts.AnyAsync(x => x.CompanyId == companyId, ct),
        // ‏ملفُّ المريض يُفتح قبل أوّل زيارة، وهو سجلٌّ طبيٌّ قائم.
        ModuleKey.Healthcare => await db.Visits.AnyAsync(x => x.CompanyId == companyId, ct)
                                || await db.Patients.IgnoreQueryFilters().AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.Hospitality => await db.Reservations.AnyAsync(x => x.CompanyId == companyId, ct)
                                 || await db.Rooms.AnyAsync(x => x.CompanyId == companyId, ct),
        ModuleKey.Logistics => await db.Shipments.AnyAsync(x => x.CompanyId == companyId, ct),
        _ => false,
    };
}

/// <summary>
/// (<c>ui-ux-i18n-printing#7</c>) <b>رفضُ ملفٍّ يُخفي وحداتٍ ذاتَ بيانات</b> — يحمل الملفَّ والوحداتِ نفسَها لا أسماءَها في الكود،
/// فتعرضها الشاشةُ بأسمائها المعروضة («الرواتب» لا <c>Payroll</c>). والرسالةُ الخامُ لمن يستدعي الخدمةَ بلا شاشة.
/// </summary>
public sealed class ProfileHidesModulesWithDataException(BusinessProfile profile, IReadOnlyList<ModuleKey> modules)
    : AccountingException($"لا يُغيَّر ملف النشاط إلى «{profile}»: يُخفي وحداتٍ فيها بيانات — {string.Join("، ", modules)}.")
{
    public BusinessProfile Profile { get; } = profile;
    public IReadOnlyList<ModuleKey> Modules { get; } = modules;
}
