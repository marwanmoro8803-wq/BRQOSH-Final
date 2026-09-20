using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.RealEstate;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.RealEstate;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.RealEstate;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٠) <b>الإيجارُ السكنيُّ كان يُفوتَر بـ١٥٪ — وهو معفًى.</b>
///
/// <para><b>ما كان:</b> <c>LeaseService</c> يبني سطرَ خطّة الفوترة بلا فئةٍ ضريبيّة،
/// و<c>RecurringInvoicePlan.AddLine</c> يفترض «أساسيّة». <b>فكلُّ إيجارٍ خرج بـ١٥٪</b> —
/// ومنه السكنيُّ المعفى بالمادة 30 من اللائحة التنفيذية.</para>
///
/// <para><b>والأثرُ مالٌ وإقرار:</b> مالكُ عماراتٍ سكنيّةٍ <b>يُحصّل من سكّانه ضريبةً لا
/// تُستحقّ</b> ثمّ يُقرّ بها للهيئة — أو يدفعها من ربحه إن لم يُحصّلها. <b>ولا يشكو
/// الميزانُ</b>: القيدُ متوازنٌ في الحالتين.</para>
///
/// <para><b>والحكمُ على استعمال الوحدة لا على نوع المبنى</b> — عمارةٌ واحدةٌ فيها محلٌّ
/// تجاريٌّ وشققٌ سكنيّة، <b>فلا يُشتقّ من <c>PropertyKind</c></b> ويُسأل عنه في العقد.</para>
/// </summary>
public class ResidentialRentIsExemptTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public ResidentialRentIsExemptTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> عقدٌ سكنيٌّ ⟵ سطرُ الفوترة <b>معفًى</b>، ومعه رمزُ
    /// الإعفاء النظاميّ.
    /// </summary>
    [Fact]
    public async Task AResidentialLease_IsBilledExempt()
    {
        var line = await ActivateAndReadLineAsync(isResidential: true);

        Assert.Equal(VatCategory.Exempt, line.VatCategory);
        Assert.Equal("VATEX-SA-30", line.TaxExemptionReasonCode);
    }

    /// <summary>
    /// <b>ولا ضريبةَ في المبلغ:</b> الفئةُ وحدَها لا تكفي دليلاً — <b>فتُقاس الضريبةُ
    /// المحسوبة</b>، إذ فئةٌ معفاةٌ بنسبةٍ خاطئةٍ تُنتج المبلغَ نفسَه الخاطئ.
    /// </summary>
    [Fact]
    public async Task AResidentialLease_ChargesNoVat()
    {
        var line = await ActivateAndReadLineAsync(isResidential: true);

        var (_, vat) = Brqosh.Domain.Accounting.LineDocumentTotalsCalculator.Compute([line]);

        Assert.Equal(0m, vat);
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل:</b> العقدُ التجاريُّ خاضعٌ بـ١٥٪ كما كان حرفاً — فالإصلاحُ
    /// لم يُعفِ ما يُستحقّ عليه. <b>ولولاه لجاز أن يُعفى كلُّ إيجارٍ ويمرّ القياسُ الأوّل.</b>
    /// </summary>
    [Fact]
    public async Task ACommercialLease_RemainsStandardRated()
    {
        var line = await ActivateAndReadLineAsync(isResidential: false);

        Assert.Equal(VatCategory.Standard, line.VatCategory);
        Assert.Null(line.TaxExemptionReasonCode);

        var (_, vat) = Brqosh.Domain.Accounting.LineDocumentTotalsCalculator.Compute([line]);
        Assert.Equal(1_500m, vat);   // ‏١٥٪ من ١٠٬٠٠٠
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — الافتراض:</b> عقدٌ يُنشأ بلا ذكرٍ للاستعمال يبقى تجاريّاً،
    /// <b>فلا يتغيّر إقرارٌ لعقدٍ قائمٍ بهذه الترقية</b>. والإعفاءُ قرارٌ يُتَّخذ لا يُفترض.
    /// </summary>
    [Fact]
    public void TheDefault_IsCommercial_SoExistingContractsAreUnchanged()
    {
        var lease = LeaseContract.Create(
            companyId: 1, propertyUnitId: 1, tenantCustomerId: 1, "L-1",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 10_000m);

        Assert.False(lease.IsResidential);
    }

    /// <summary>
    /// <b>والرمزُ مُسمًّى لا منسوخ</b> — فرمزٌ حرفيٌّ في الخدمة يفترق يومَ يتغيّر،
    /// والافتراقُ لا يُرى: تخرج الفاتورةُ برمزٍ لا تعرفه الهيئةُ فتُردّ.
    /// </summary>
    [Fact]
    public void TheExemptionCode_IsTheOneTheAuthorityKnows()
    {
        var reason = VatExemptionReason.Find(VatExemptionReason.ResidentialRentCode);

        Assert.NotNull(reason);
        Assert.Equal(VatCategory.Exempt, reason!.Category);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<SalesInvoiceLine> ActivateAndReadLineAsync(bool isResidential)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة عقارية", "310123456700055", BusinessProfile.RealEstate);
        _companyId = company.Id;

        var tenant = Customer.Create(_companyId, "مستأجر", "311111111100003");
        _db.Customers.Add(tenant);

        var property = Property.Create(
            _companyId, "P-1", "عمارة النخيل", PropertyKind.Building, "الرياض");
        _db.Properties.Add(property);
        await _db.SaveChangesAsync();

        var unit = PropertyUnit.Create(_companyId, property.Id, "A-1",
            areaSquareMeters: 120m, bedrooms: 3, listedAnnualRent: 120_000m);
        _db.PropertyUnits.Add(unit);
        await _db.SaveChangesAsync();

        var lease = LeaseContract.Create(
            _companyId, unit.Id, tenant.Id, "L-2026-001",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 10_000m,
            isResidential: isResidential);

        await new LeaseService(_db).ActivateAsync(lease);

        var plan = await _db.RecurringInvoicePlans
            .Include(p => p.Lines)
            .SingleAsync(p => p.CompanyId == _companyId);

        return plan.Lines.Single();
    }
}
