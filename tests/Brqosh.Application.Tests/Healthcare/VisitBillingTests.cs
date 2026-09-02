using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Healthcare;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Healthcare;

/// <summary>
/// (CEEF-230 · ط3-٢) <b>الصحّة — والقسمةُ بين المريض وشركة التأمين.</b>
///
/// <para><b>ما كان:</b> ملفُّ «الصحّة» في شاشة التأسيس <b>ولا مريضَ في النظام ولا زيارةَ
/// ولا وثيقةَ تأمين</b>. فالعيادةُ تكتب فاتورةً واحدةً باسم المريض بالمبلغ كلِّه —
/// <b>والحقيقةُ أنّ أكثرَها ذمّةٌ على شركة التأمين</b> تُحصَّل بعد ستّين يوماً بمطالبة.
/// فتخرج أعمارُ الديون كلُّها خطأً، ويُطالَب المريضُ بما لا يلزمه.</para>
///
/// <para><b>وهويّةُ هـ-١٠:</b> فاتورةُ المريض + فاتورةُ الشركة = إجماليُّ الزيارة، هللةً
/// بهللة. <b>وكسرُها يترك ذمّةً معلّقةً لا يُغلقها سداد.</b></para>
/// </summary>
public class VisitBillingTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _patientId;
    private long _payerId;

    public VisitBillingTests()
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

    // ── الصيغة ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>الترتيبُ معنى: التحمّلُ الثابت أوّلاً ثمّ النسبةُ على الباقي.</b>
    ///
    /// <para>فاتورةُ 1,000 وتحمّلٌ 50 وتغطيةُ 80٪ ⟵ الشركةُ <b>760</b> والمريضُ 240.
    /// <b>وعكسُ الترتيب</b> — نسبةٌ على الكلّ ثمّ تحمّل — يُعطي الشركةَ 800، <b>وفرقُ
    /// أربعين ريالاً في كلّ زيارة</b> تُطالَب به الشركةُ فترفضه.</para>
    /// </summary>
    [Fact]
    public void TheFixedCopay_IsDeductedBeforeThePercentage()
    {
        var split = CoverageCalculator.Split(
            coveredTotal: 1_000m, uncoveredTotal: 0m,
            coveragePercent: 80m, copayFixed: 50m, remainingLimit: null);

        Assert.Equal(760m, split.PayerShare);
        Assert.Equal(240m, split.PatientShare);
        Assert.Equal(CoverageBasis.CoveragePercent, split.Basis);
    }

    /// <summary>
    /// <b>وما لا تشمله الوثيقةُ على المريض كلُّه</b> — مهما بلغت النسبة.
    /// </summary>
    [Fact]
    public void UncoveredServices_AreEntirelyThePatients()
    {
        var split = CoverageCalculator.Split(
            coveredTotal: 500m, uncoveredTotal: 300m,
            coveragePercent: 100m, copayFixed: 0m, remainingLimit: null);

        Assert.Equal(500m, split.PayerShare);
        Assert.Equal(300m, split.PatientShare);
    }

    /// <summary>
    /// <b>والسقفُ يحدّ المطالبة — وما جاوزه على المريض.</b>
    ///
    /// <para><b>وهذا أخطرُ ما في الباب</b>: عيادةٌ تتجاهل السقفَ تُثبت ذمّةً على شركةٍ
    /// استُنفد سقفُها، <b>وتكتشف الرفضَ بعد أشهر</b> حين يكون المريضُ قد غادر. والسببُ
    /// يُكتب مع القسمة فلا يُبحث عنه لاحقاً.</para>
    /// </summary>
    [Fact]
    public void TheAnnualLimit_CapsTheClaimAndTheRestFallsToThePatient()
    {
        var split = CoverageCalculator.Split(
            coveredTotal: 1_000m, uncoveredTotal: 0m,
            coveragePercent: 100m, copayFixed: 0m, remainingLimit: 300m);

        Assert.Equal(300m, split.PayerShare);
        Assert.Equal(700m, split.PatientShare);
        Assert.Equal(CoverageBasis.AnnualLimit, split.Basis);
    }

    /// <summary>
    /// <b>والتحمّلُ لا يتجاوز المشمول.</b>
    ///
    /// <para>زيارةٌ بثلاثين ريالاً وتحمّلٌ خمسون: الباقي سالبٌ لولا الحدّ، <b>فتُطالَب
    /// الشركةُ بنصيبٍ من عددٍ سالب — أي تدفع للعيادة عن زيارةٍ دفع المريضُ كلَّها</b>.</para>
    /// </summary>
    [Fact]
    public void ACopayLargerThanTheBill_DoesNotMakeThePayerOweMoney()
    {
        var split = CoverageCalculator.Split(
            coveredTotal: 30m, uncoveredTotal: 0m,
            coveragePercent: 80m, copayFixed: 50m, remainingLimit: null);

        Assert.Equal(0m, split.PayerShare);
        Assert.Equal(30m, split.PatientShare);
    }

    /// <summary>
    /// <b>والمجموعُ محفوظٌ هللةً بهللة — مهما كانت النسبة.</b>
    ///
    /// <para>وهي هويّةُ <c>هـ-١٠</c>. <b>وحسابُ الطرفين كلٍّ على حدةٍ ثمّ تقريبُ كلٍّ منهما
    /// يُنتج هللةً ضائعةً أو زائدة</b> — فتبقى ذمّةٌ معلّقةٌ لا يُغلقها سداد.</para>
    /// </summary>
    [Theory]
    [InlineData(333.33, 33.33)]
    [InlineData(0.01, 99)]
    [InlineData(1234.56, 77.77)]
    [InlineData(99.99, 1)]
    public void ThePatientAndPayerShares_AlwaysSumToTheVisitTotal(double total, double percent)
    {
        var covered = (decimal)total;
        var split = CoverageCalculator.Split(covered, 0m, (decimal)percent, 0m, null);

        Assert.Equal(covered, split.PatientShare + split.PayerShare);
    }

    /// <summary>
    /// <b>وسقفُ الوثيقة سنةُ وثيقةٍ لا سنةٌ ميلادية.</b>
    ///
    /// <para>وثيقةٌ تبدأ في ١ يوليو 2025 تُعيد سقفَها في ١ يوليو 2026. <b>وحسابُه ميلادياً
    /// يُصفّره في ١ يناير</b> — فتُصرف خدماتُ ستّة أشهرٍ فوق السقف وتُرفض مطالباتُها جملةً.</para>
    /// </summary>
    [Fact]
    public void ThePolicyYear_AnchorsToTheStartDateNotJanuary()
    {
        var policy = InsurancePolicy.Create(1L, 1L, 1L, "POL-1", "M-1", 80m,
            new DateOnly(2025, 7, 1), new DateOnly(2028, 6, 30));

        Assert.Equal(new DateOnly(2025, 7, 1), policy.PolicyYearStart(new DateOnly(2025, 12, 31)));
        Assert.Equal(new DateOnly(2025, 7, 1), policy.PolicyYearStart(new DateOnly(2026, 1, 1)));
        Assert.Equal(new DateOnly(2026, 7, 1), policy.PolicyYearStart(new DateOnly(2026, 7, 1)));
        Assert.Equal(new DateOnly(2026, 7, 1), policy.PolicyYearStart(new DateOnly(2027, 6, 30)));
    }

    /// <summary>
    /// <b>والفئةُ الأخيرة تأخذ الباقي — وإلا زادت المطالبةُ هللةً على نصيبها.</b>
    ///
    /// <para><b>وهذا القياسُ أخرجته طفرةٌ نجت</b>: كان الاختبارُ الوحيدُ للتوزيع يستعمل
    /// 400 و600 بنصيب 500 — <b>وهي تنقسم بلا كسر</b>، فمرَّ نزعُ قاعدة الباقي خضراءَ.
    /// وثلاثُ فئاتٍ بمبالغَ لا تنقسم تُفرّق: بنصيبِ 50 على 33.33 و33.33 و33.34 يُعطي
    /// التقريبُ المستقلّ <b>50.01</b> — <b>وهللةٌ تُطالَب بها الشركةُ زيادةً على حصّتها،
    /// ومجموعُ الفاتورتين يجاوز الزيارة</b>.</para>
    /// </summary>
    [Fact]
    public void TheLastTaxGroup_TakesTheRoundingRemainder()
    {
        var groups = new List<(TaxGroup, decimal)>
        {
            (new TaxGroup(VatCategory.Standard, null, null), 33.33m),
            (new TaxGroup(VatCategory.ZeroRated, "VATEX-SA-HEA", "أدوية"), 33.33m),
            (new TaxGroup(VatCategory.Exempt, "VATEX-SA-29", "خدمة معفاة"), 33.34m),
        };

        var allocated = CoverageCalculator.Allocate(groups, 50m);

        Assert.Equal(50m, allocated.Sum(a => a.Amount));
        Assert.Equal(16.66m, allocated[^1].Amount);
    }

    // ── الفوترة ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: زيارةٌ واحدةٌ تُنتج ذمّتين — على المريض وعلى الشركة.</b>
    ///
    /// <para><b>وهو البند كلُّه.</b> فاتورةٌ واحدةٌ باسم المريض بالمبلغ كلِّه تجعل أعمارَ
    /// الديون خطأً، <b>وتُطالِب مريضاً بثمانمئةٍ يدفعها تأمينُه</b> — ويبقى حقُّ العيادة
    /// عند الشركة خارج المتابعة حتّى يسقط.</para>
    /// </summary>
    [Fact]
    public async Task OneVisit_ProducesTwoReceivables()
    {
        await SetupAsync();
        var policy = await PolicyAsync(coveragePercent: 80m, copay: 50m);
        var visit = await VisitAsync("V-1", policy.Id, ("كشف", 1_000m, true));

        var result = await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);

        Assert.Equal(760m, result.PayerShare);
        Assert.Equal(240m, result.PatientShare);
        Assert.NotNull(result.PatientInvoiceId);
        Assert.NotNull(result.PayerInvoiceId);

        // ‏**والذمّةُ بالضريبة**: 240 و760 قبلها، و276 و874 بعدها — والقياسُ على الذمّة
        // ‏لا على الأساس، فهي ما يُطالَب به فعلاً.
        Assert.Equal(276m, await BalanceAsync(PatientCustomerId()));
        Assert.Equal(874m, await BalanceAsync(PayerCustomerId()));
    }

    /// <summary>
    /// <b>وهويّةُ هـ-١٠ في الدفاتر: مجموعُ الفاتورتين = إجماليُّ الزيارة.</b>
    /// </summary>
    [Fact]
    public async Task TheTwoInvoices_SumToTheVisitTotal()
    {
        await SetupAsync();
        var policy = await PolicyAsync(coveragePercent: 77.5m, copay: 33.33m);
        var visit = await VisitAsync("V-2", policy.Id,
            ("كشف", 333.33m, true), ("تحاليل", 199.99m, true), ("تجميل", 120.01m, false));

        var result = await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);

        var invoiced = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.CompanyId == _companyId).SumAsync(i => i.Subtotal);

        Assert.Equal(653.33m, result.Total);
        Assert.Equal(result.Total, invoiced);
        Assert.Equal(result.Total, result.PatientShare + result.PayerShare);
    }

    /// <summary>
    /// <b>والمعالجةُ الضريبية تُحفظ فئةً بفئة.</b>
    ///
    /// <para>دواءٌ صفريُّ النسبة وكشفٌ خاضع في زيارةٍ واحدة: <b>ضمُّهما في سطرٍ واحدٍ على
    /// المطالبة يُخضع الدواءَ أو يُعفي الكشف</b> — وكلاهما إقرارٌ خاطئ للهيئة. والقياسُ
    /// على الضريبة المحسوبة لا على وجود السطور.</para>
    /// </summary>
    [Fact]
    public async Task TheTaxTreatment_SurvivesTheSplit()
    {
        await SetupAsync();
        var policy = await PolicyAsync(coveragePercent: 50m, copay: 0m);
        var visit = Visit.Create(_companyId, _patientId, "V-3", new DateOnly(2026, 3, 1),
            "د. أحمد", policy.Id);
        visit.AddLine("كشف", 1m, 400m, isCovered: true);
        visit.AddLine("دواء", 1m, 600m, isCovered: true, vatCategory: VatCategory.ZeroRated,
            taxExemptionReasonCode: "VATEX-SA-HEA", taxExemptionReason: "أدوية وسلع طبية");
        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();

        await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);

        // ‏نصفُ الخاضع 200 على كلّ طرف ⟵ ضريبةُ كلٍّ 30. **والصفريُّ لا يُضاف.**
        var invoices = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.CompanyId == _companyId).ToListAsync();

        Assert.Equal(2, invoices.Count);
        Assert.All(invoices, i => Assert.Equal(30m, i.VatAmount));
        Assert.Equal(1_000m, invoices.Sum(i => i.Subtotal));

        // ‏**وسببُ الإعفاء يعبر إلى الفاتورتين معاً** — ومطالبةٌ بسطرٍ صفريٍّ بلا سببٍ
        // ‏يرفضها النظامُ نفسُه، فالحارسُ قائمٌ في `SalesInvoice` قبل هذا الباب.
        var zeroLines = invoices.SelectMany(i => i.Lines)
            .Where(l => l.VatCategory == VatCategory.ZeroRated).ToList();
        Assert.Equal(2, zeroLines.Count);
        Assert.All(zeroLines, l => Assert.Equal("VATEX-SA-HEA", l.TaxExemptionReasonCode));
    }

    /// <summary>
    /// <b>وزيارةٌ بلا وثيقةٍ سارية تصير على الحساب الخاصّ — ولا تُرفض.</b>
    ///
    /// <para>فالمريضُ عُولج فعلاً، <b>ورفضُ الفوترة يترك الخدمةَ بلا إيراد</b>. والصوابُ
    /// أن تظهر في ذمّته فيراجعها المحاسب.</para>
    /// </summary>
    [Fact]
    public async Task AnExpiredPolicy_BillsThePatientInFull()
    {
        await SetupAsync();
        var expired = InsurancePolicy.Create(_companyId, _patientId, _payerId, "POL-X", "M-X",
            80m, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
        _db.InsurancePolicies.Add(expired);
        await _db.SaveChangesAsync();

        var visit = await VisitAsync("V-4", expired.Id, ("كشف", 500m, true));
        var result = await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);

        Assert.Equal(500m, result.PatientShare);
        Assert.Equal(0m, result.PayerShare);
        Assert.Null(result.PayerInvoiceId);
        Assert.Equal(CoverageBasis.SelfPay, result.Basis);
    }

    /// <summary>
    /// <b>والسقفُ يُشتقّ من الزيارات المفوترة — لا من عدّادٍ مخزَّن.</b>
    ///
    /// <para>سقفٌ 1,000: زيارتان بـ600 لكلٍّ ⟵ الأولى تُطالَب بـ600، <b>والثانية بـ400
    /// وحدها والباقي على المريض</b>. وعدّادٌ في الوثيقة كان سيَنحرف عند أوّل إشعارِ دائن.</para>
    /// </summary>
    [Fact]
    public async Task TheSecondVisit_SeesTheLimitConsumedByTheFirst()
    {
        await SetupAsync();
        var policy = await PolicyAsync(coveragePercent: 100m, copay: 0m, annualLimit: 1_000m);
        var service = new VisitBillingService(_db);

        var first = await service.BillAsync(_companyId,
            (await VisitAsync("V-5", policy.Id, ("كشف", 600m, true))).Id);
        var second = await service.BillAsync(_companyId,
            (await VisitAsync("V-6", policy.Id, ("كشف", 600m, true))).Id);

        Assert.Equal(600m, first.PayerShare);
        Assert.Equal(400m, second.PayerShare);
        Assert.Equal(200m, second.PatientShare);
        Assert.Equal(CoverageBasis.AnnualLimit, second.Basis);
    }

    /// <summary>
    /// <b>ولا تُفوتَر زيارةٌ مرّتين.</b>
    ///
    /// <para>ومرورٌ ثانٍ يُنشئ فاتورتين للمريض ومطالبتين للشركة — <b>والشركةُ ترفض المكرّرة
    /// وتُسجّلها على المزوّد</b>، وتكرارُها يُخرجه من الشبكة.</para>
    /// </summary>
    [Fact]
    public async Task AVisit_IsNotBilledTwice()
    {
        await SetupAsync();
        var policy = await PolicyAsync(coveragePercent: 80m, copay: 0m);
        var visit = await VisitAsync("V-7", policy.Id, ("كشف", 500m, true));
        var service = new VisitBillingService(_db);

        await service.BillAsync(_companyId, visit.Id);
        await Assert.ThrowsAsync<AccountingException>(() => service.BillAsync(_companyId, visit.Id));

        Assert.Equal(2, await _db.SalesInvoices.CountAsync(i => i.CompanyId == _companyId));

        // ‏**وحارسان يمنعانه، وأيُّهما وحده يكفي — وقِيس ذلك.** فحصُ الخدمة يمنع العملَ
        // ‏الضائع، و`Visit.MarkBilled` يمنع الحالةَ أيّاً كان مَن استدعاه، والمعاملةُ
        // ‏تُرجع ما أُنشئ قبله. **ونزعُ أحدهما وحدَه لا يُغيّر نتيجةً؛ ونزعُهما معاً
        // ‏يُسقط هذا القياس** — فالسلوكُ مُثبَت، ولا يُدّعى أنّ أحدهما بعينه هو العامل.
    }

    /// <summary>
    /// <b>ووثيقةُ مريضٍ آخر لا تُغطّي هذا.</b>
    ///
    /// <para>والخطأُ يقع حين يُنسخ رقمُ وثيقةٍ من زيارةٍ سابقة — <b>فتُطالَب الشركةُ باسمِ
    /// عضوٍ لم يُعالَج</b>، وهو في عرف شركات التأمين احتيالٌ لا خطأ.</para>
    /// </summary>
    [Fact]
    public async Task APolicyBelongingToAnotherPatient_IsRefused()
    {
        await SetupAsync();
        var other = Customer.Create(_companyId, "مريض آخر", "");
        _db.Customers.Add(other);
        await _db.SaveChangesAsync();

        var otherPatient = Patient.Create(_companyId, "MRN-2", "مريض آخر", "1099999999",
            new DateOnly(1985, 5, 5), PatientGender.Female, other.Id);
        _db.Patients.Add(otherPatient);
        await _db.SaveChangesAsync();

        var foreignPolicy = InsurancePolicy.Create(_companyId, otherPatient.Id, _payerId,
            "POL-2", "M-2", 80m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        _db.InsurancePolicies.Add(foreignPolicy);
        await _db.SaveChangesAsync();

        var visit = await VisitAsync("V-8", foreignPolicy.Id, ("كشف", 500m, true));

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new VisitBillingService(_db).BillAsync(_companyId, visit.Id));

        Assert.Contains("ليست لهذا المريض", error.Message, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private long PatientCustomerId() => _db.Patients.AsNoTracking()
        .Single(p => p.Id == _patientId).CustomerId;

    private long PayerCustomerId() => _db.InsurancePayers.AsNoTracking()
        .Single(p => p.Id == _payerId).CustomerId;

    private async Task<decimal> BalanceAsync(long customerId) =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, customerId)).Balance;

    private async Task<InsurancePolicy> PolicyAsync(decimal coveragePercent, decimal copay,
        decimal? annualLimit = null)
    {
        var policy = InsurancePolicy.Create(_companyId, _patientId, _payerId, "POL-1", "M-1",
            coveragePercent, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            copayFixed: copay, annualLimit: annualLimit);
        _db.InsurancePolicies.Add(policy);
        await _db.SaveChangesAsync();
        return policy;
    }

    private async Task<Visit> VisitAsync(string number, long? policyId,
        params (string Description, decimal Amount, bool Covered)[] lines)
    {
        var visit = Visit.Create(_companyId, _patientId, number, new DateOnly(2026, 3, 1),
            "د. أحمد", policyId);
        foreach (var (description, amount, covered) in lines)
            visit.AddLine(description, 1m, amount, covered);

        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();
        return visit;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مجمّع عيادات", "310123456700088", BusinessProfile.Healthcare);
        _companyId = company.Id;

        var patientCustomer = Customer.Create(_companyId, "مريض", "");
        var payerCustomer = Customer.Create(_companyId, "شركة تأمين", "311111111100003");
        _db.Customers.AddRange(patientCustomer, payerCustomer);
        await _db.SaveChangesAsync();

        var patient = Patient.Create(_companyId, "MRN-1", "مريض", "1088888888",
            new DateOnly(1990, 1, 1), PatientGender.Male, patientCustomer.Id);
        var payer = InsurancePayer.Create(_companyId, "BUPA", "بوبا العربية", payerCustomer.Id);
        _db.Patients.Add(patient);
        _db.InsurancePayers.Add(payer);
        await _db.SaveChangesAsync();

        _patientId = patient.Id;
        _payerId = payer.Id;
    }
}
