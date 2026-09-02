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
/// (CEEF-230 · ط3-٢ب) <b>دورةُ المطالبات — والمالُ الذي يضيع في العيادات يضيع هنا.</b>
///
/// <para><b>ما كان:</b> الزيارةُ تُفوتَر فاتورتين (<c>ط3-٢</c>)، <b>وفاتورةُ الشركة ذمّةٌ
/// تبقى بكامل قيمتها إلى الأبد</b>. عيادةٌ تُطالب بمئة ألفٍ شهرياً تُعتمد لها اثنان
/// وثمانون، <b>وثمانيةَ عشرَ تُرفض وتبقى في الذمم</b> — فبعد سنةٍ <b>مئتان وستّةَ عشرَ
/// ألفاً وهميّة</b> تُقرأ سيولةً وليست شيئاً.</para>
///
/// <para><b>وهويّةُ هـ-١٥:</b> <c>المطالَبُ = المعتمَدُ + المرفوض</c> في كلّ سطر.</para>
/// </summary>
public class InsuranceClaimTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _payerId;
    private long _payerCustomerId;
    private long _patientId;
    private long _patientCustomerId;
    private long _policyId;
    private int _visitSeq;

    public InsuranceClaimTests()
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

    // ── هـ-١٥: المطالَبُ = المعتمَدُ + المرفوض ───────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: بقيّةٌ صامتةٌ بين المعتمد والمرفوض تُرفض.</b>
    ///
    /// <para>شركةٌ تعتمد ٧٠٠ وترفض ٢٠٠ من مطالبةِ ١٠٠٠: <b>مئةٌ لا حالةَ لها</b> — لا في
    /// الذمم ولا في المشطوب. <b>وهي تبقى في الميزانية بلا من يسأل عنها</b>، وهذا هو
    /// المالُ الذي يضيع.</para>
    /// </summary>
    [Fact]
    public async Task ApprovedPlusRejected_MustExhaustTheClaimedAmount()
    {
        var claim = await SubmittedClaimAsync(1000m);
        var invoiceId = claim.Lines[0].PayerInvoiceId;

        var error = Assert.Throws<AccountingException>(() =>
            claim.Adjudicate(invoiceId, approvedAmount: 700m, rejectedAmount: 200m, "خدمة غير مغطاة"));

        Assert.Contains("مالٌ بلا حالة", error.Message);
    }

    /// <summary>
    /// <b>ومجموعٌ يتجاوز المطالَبَ يُرفض كذلك.</b>
    ///
    /// <para>واعتمادٌ بأكثرَ ممّا طُولب به <b>يُعفي دَيناً لا وجودَ له</b> — فيصير رصيدُ
    /// الشركة سالباً، وتظهر في كشوف الأعمار دائناً وهي مدين.</para>
    /// </summary>
    [Fact]
    public async Task ApprovedPlusRejected_MayNotExceedTheClaimedAmount()
    {
        var claim = await SubmittedClaimAsync(1000m);
        var invoiceId = claim.Lines[0].PayerInvoiceId;

        var error = Assert.Throws<AccountingException>(() =>
            claim.Adjudicate(invoiceId, approvedAmount: 900m, rejectedAmount: 200m, "خدمة غير مغطاة"));

        Assert.Contains("مالٌ بلا حالة", error.Message);
    }

    /// <summary>
    /// <b>ورفضٌ بلا سببٍ يُرفض.</b>
    ///
    /// <para>السببُ هو ما يُقرَّر عليه بين إعادة التحميل والشطب، <b>وهو ما يُطعَن به</b>.
    /// ورفضٌ بلا سببٍ يُشطب حتماً لأنّ أحداً لا يستطيع مراجعتَه — <b>فيُتعلَّم من الرفض
    /// لا شيء، ويُعاد الخطأُ نفسُه السنةَ القادمة</b>.</para>
    /// </summary>
    [Fact]
    public async Task RejectionWithoutAReason_IsRefused()
    {
        var claim = await SubmittedClaimAsync(1000m);
        var invoiceId = claim.Lines[0].PayerInvoiceId;

        var error = Assert.Throws<AccountingException>(() =>
            claim.Adjudicate(invoiceId, approvedAmount: 800m, rejectedAmount: 200m, "   "));

        Assert.Contains("الرفضُ بلا سببٍ لا يُقبل", error.Message);
    }

    // ── خروجُ المرفوض من الذمّة ──────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: المرفوضُ يخرج من ذمّة الشركة — بمستند.</b>
    ///
    /// <para>وهذا هو الباب كلُّه: <b>قبل هذه الدفعةِ كانت الألفُ تبقى ألفاً بعد رفض
    /// مئتين</b>. والقياسُ على <b>كشف الحساب</b> لا على القيد — فالدفترُ المساعد يُبنى
    /// من المستندات، <b>وقيدٌ يدويٌّ كان يُصلح الميزانيةَ ويترك الكشفَ كما كان</b>.</para>
    /// </summary>
    [Fact]
    public async Task RejectedAmount_LeavesThePayerSubsidiaryLedger()
    {
        var claim = await SubmittedClaimAsync(1000m);
        var invoiceId = claim.Lines[0].PayerInvoiceId;

        var before = await PayerBalanceAsync();
        Assert.Equal(1000m, before);

        claim.Adjudicate(invoiceId, 800m, 200m, "خدمة غير مغطاة");
        claim.CloseAdjudication(new DateOnly(2026, 4, 1));
        claim.DisposeRejection(invoiceId, RejectionDisposition.WriteOff);
        await _db.SaveChangesAsync();

        await new ClaimSettlementService(_db)
            .SettleAsync(_companyId, claim.Id, new DateOnly(2026, 4, 1));

        Assert.Equal(800m, await PayerBalanceAsync());
    }

    /// <summary>
    /// <b>وإعادةُ التحميل تنقل المدينَ ولا تُنقص الإيراد.</b>
    ///
    /// <para>الشركةُ لم تكن مسؤولةً عن هذا المبلغ — <b>والمريضُ كان</b>. فيخرج من ذمّتها
    /// ويدخل ذمّتَه، <b>ومجموعُ المدينين ثابت</b>. وشطبُه بدل تحميلِه يُهدي المريضَ مالاً
    /// يلزمه.</para>
    /// </summary>
    [Fact]
    public async Task RebilledRejection_MovesTheDebtToThePatient()
    {
        var claim = await SubmittedClaimAsync(1000m);
        var invoiceId = claim.Lines[0].PayerInvoiceId;

        var patientBefore = await PatientBalanceAsync();

        claim.Adjudicate(invoiceId, 800m, 200m, "وثيقة منتهية");
        claim.CloseAdjudication(new DateOnly(2026, 4, 1));
        claim.DisposeRejection(invoiceId, RejectionDisposition.RebillPatient);
        await _db.SaveChangesAsync();

        var result = await new ClaimSettlementService(_db)
            .SettleAsync(_companyId, claim.Id, new DateOnly(2026, 4, 1));

        Assert.Equal(800m, await PayerBalanceAsync());
        Assert.Equal(patientBefore + 200m, await PatientBalanceAsync());
        Assert.Equal(200m, result.RebilledToPatients);
        Assert.Equal(0m, result.WrittenOff);
    }

    /// <summary>
    /// <b>والشطبُ يُنقص الإيرادَ ولا يُحمَّل على أحد.</b>
    ///
    /// <para>وهو الصوابُ حين يكون الرفضُ خطأَ المزوّد — ترميزاً أو توثيقاً ناقصاً:
    /// <b>المريضُ لا ذنبَ له في خطأِ العيادة</b>. وتحميلُه إيّاه يُخرج فاتورةً لا يقبلها.</para>
    /// </summary>
    [Fact]
    public async Task WrittenOffRejection_ChargesNoOneAndReducesRevenue()
    {
        var claim = await SubmittedClaimAsync(1000m);
        var invoiceId = claim.Lines[0].PayerInvoiceId;

        var patientBefore = await PatientBalanceAsync();
        var revenueBefore = await BalanceAsync("41");

        claim.Adjudicate(invoiceId, 800m, 200m, "ترميز خاطئ");
        claim.CloseAdjudication(new DateOnly(2026, 4, 1));
        claim.DisposeRejection(invoiceId, RejectionDisposition.WriteOff);
        await _db.SaveChangesAsync();

        var result = await new ClaimSettlementService(_db)
            .SettleAsync(_companyId, claim.Id, new DateOnly(2026, 4, 1));

        Assert.Equal(patientBefore, await PatientBalanceAsync());
        Assert.Equal(200m, result.WrittenOff);
        Assert.Equal(0m, result.RebilledToPatients);

        // ‏**والإيرادُ ينقص بصافي المرفوض** — والمردوداتُ حسابٌ مقابلٌ للإيراد.
        Assert.True(await BalanceAsync("41") > revenueBefore,
            "رصيدُ الإيراد المدين لم يزد بالمردود — والشطبُ لم يُنقص الإيراد.");
    }

    // ── الحرّاس ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ومرفوضٌ بلا قرارٍ يمنع الإقفال.</b>
    ///
    /// <para>إقفالُها عليه <b>يترك المبلغَ في ذمّة الشركة وقد رفضته</b> — وهو عينُ الذمّة
    /// الوهمية التي بُني هذا الباب لإغلاقها. <b>فلا يُغلق العطلُ بابَه ثمّ يتركه مفتوحاً
    /// من طرفٍ آخر.</b></para>
    /// </summary>
    [Fact]
    public async Task ClosingWithAnUndecidedRejection_IsRefusedByAmount()
    {
        var claim = await SubmittedClaimAsync(1000m);
        var invoiceId = claim.Lines[0].PayerInvoiceId;

        claim.Adjudicate(invoiceId, 800m, 200m, "خدمة غير مغطاة");
        claim.CloseAdjudication(new DateOnly(2026, 4, 1));

        var error = Assert.Throws<AccountingException>(claim.Close);
        Assert.Contains("200.00", error.Message);
    }

    /// <summary>
    /// <b>وسطرٌ لم تفصل فيه الشركة يمنع الفصل.</b>
    ///
    /// <para>فمعالجةُ نصفِ الدفعة <b>تُقفلها ويبقى نصفُها معلّقاً بلا سجلّ</b> — فلا تُطالَب
    /// الشركةُ به ثانيةً ولا يُشطب.</para>
    /// </summary>
    [Fact]
    public async Task ClosingAdjudicationWithAPendingLine_IsRefusedByCount()
    {
        var claim = await SubmittedClaimAsync(600m, 400m);
        claim.Adjudicate(claim.Lines[0].PayerInvoiceId, 600m, 0m, null);

        var error = Assert.Throws<AccountingException>(() =>
            claim.CloseAdjudication(new DateOnly(2026, 4, 1)));

        Assert.Contains("1 سطراً بلا قرارٍ من الشركة", error.Message);
    }

    /// <summary>
    /// <b>وفاتورةٌ واحدةٌ لا تُطالَب مرّتين في الدفعة الواحدة.</b>
    ///
    /// <para>والشركةُ ترفض المكرّرَ وتُسجّله على المزوّد. <b>وعندنا يُصدَر إشعاران دائنان
    /// لدينٍ واحد</b> فيصير رصيدُها سالباً لا يقابله شيء.</para>
    /// </summary>
    [Fact]
    public async Task TheSameInvoiceTwiceInOneClaim_IsRefused()
    {
        var claim = await SubmittedClaimAsync(1000m);
        var line = claim.Lines[0];

        var draft = InsuranceClaim.Create(_companyId, _payerId, "CLM-DUP",
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        draft.AddVisit(line.VisitId, line.PayerInvoiceId, 1000m);

        var error = Assert.Throws<AccountingException>(() =>
            draft.AddVisit(line.VisitId, line.PayerInvoiceId, 1000m));

        Assert.Contains("مضمومةٌ سلفاً", error.Message);
    }

    /// <summary>
    /// <b>ولا تُعدَّل دفعةٌ قُدِّمت.</b>
    ///
    /// <para>فالمُقدَّمُ عند الشركة بمبلغه، <b>وإضافةُ زيارةٍ بعده تجعل دفترَنا يخالف
    /// مطالبتَنا</b> — ولا يُطابَق أبداً.</para>
    /// </summary>
    [Fact]
    public async Task AddingToASubmittedClaim_IsRefused()
    {
        var claim = await SubmittedClaimAsync(1000m);

        var error = Assert.Throws<AccountingException>(() =>
            claim.AddVisit(999L, 999L, 100m));

        Assert.Contains("قُدِّمت", error.Message);
    }

    /// <summary>
    /// <b>ودفعةٌ فارغةٌ لا تُقدَّم.</b>
    /// </summary>
    [Fact]
    public async Task SubmittingAnEmptyClaim_IsRefused()
    {
        await SetupAsync();
        var claim = InsuranceClaim.Create(_companyId, _payerId, "CLM-EMPTY",
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var error = Assert.Throws<AccountingException>(() =>
            claim.Submit(new DateOnly(2026, 4, 1)));

        Assert.Contains("بلا زيارات", error.Message);
    }

    /// <summary>
    /// <b>ولا تُعالَج مطالبةٌ لم يُفصَل فيها.</b>
    ///
    /// <para>ومعالجتُها تُصدر إشعاراتٍ عن سطورٍ لم تقل الشركةُ فيها شيئاً — <b>فتُعفى من
    /// دَينٍ لم ترفضه</b>.</para>
    /// </summary>
    [Fact]
    public async Task SettlingAnUnadjudicatedClaim_IsRefused()
    {
        var claim = await SubmittedClaimAsync(1000m);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new ClaimSettlementService(_db)
                .SettleAsync(_companyId, claim.Id, new DateOnly(2026, 4, 1)));

        Assert.Contains("فُصل في كلّ سطورها", error.Message);
    }

    // ── البناءُ من المستندات ─────────────────────────────────────────────────

    /// <summary>
    /// <b>والدفعةُ تُبنى ممّا فُوتر فعلاً — لا بإدخالٍ يدويّ.</b>
    ///
    /// <para>فالمبلغُ المطالَبُ به هو إجمالي فاتورة المطالبة نفسِها. <b>ورقمٌ يُكتب بيدٍ
    /// يخالفها يوماً</b>، فتُطالَب الشركةُ بغير ما في دفترنا ولا يُطابَق أبداً.</para>
    /// </summary>
    [Fact]
    public async Task BuildAsync_TakesTheClaimedAmountFromThePostedInvoice()
    {
        await SetupAsync();
        await BilledVisitAsync(1000m);
        await BilledVisitAsync(450m);

        var claim = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, _payerId, "CLM-01", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(2, claim.Lines.Count);
        Assert.Equal(1450m, claim.ClaimedTotal);
    }

    /// <summary>
    /// <b>وما طُولب به سلفاً لا يُضمّ ثانيةً.</b>
    ///
    /// <para>ودفعتان تحملان الفاتورةَ نفسَها <b>تُصدران إشعارين دائنين لدينٍ واحد</b>.</para>
    /// </summary>
    [Fact]
    public async Task BuildAsync_SkipsInvoicesAlreadyClaimed()
    {
        await SetupAsync();
        await BilledVisitAsync(1000m);

        var first = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, _payerId, "CLM-01", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        first.Submit(new DateOnly(2026, 4, 1));
        _db.InsuranceClaims.Add(first);
        await _db.SaveChangesAsync();

        await BilledVisitAsync(300m);

        var second = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, _payerId, "CLM-02", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Single(second.Lines);
        Assert.Equal(300m, second.ClaimedTotal);
    }

    /// <summary>
    /// <b>والمطالَبُ به بالإجمالي لا بالصافي — والضريبةُ جزءٌ ممّا يُطالَب.</b>
    ///
    /// <para><b>وطفرةٌ نجت فأخرجت هذا</b>: كلُّ زياراتي كانت <b>صفريةَ النسبة</b>، فالصافي
    /// والإجمالي واحد — <b>واختبارٌ لا يفرّق بينهما لا يقيس أيَّهما أُخذ</b>. والعيادةُ
    /// تبيع المُعالَجاتِ التجميلية بالنسبة الأساسية، <b>فتُطالَب الشركةُ بأقلَّ من الفاتورة
    /// بمقدار الضريبة كلَّ مرّة</b> — والفرقُ يبقى في ذمّتها إلى الأبد.</para>
    /// </summary>
    [Fact]
    public async Task ClaimedAmount_IsTheInvoiceGross_IncludingVat()
    {
        await SetupAsync();
        await BilledVisitAsync(1000m, zeroRated: false);

        var claim = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, _payerId, "CLM-VAT", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        // ‏١٠٠٠ + ١٥٪ = ١١٥٠ — **وهو ما تدين به الشركةُ فعلاً**.
        Assert.Equal(1150m, claim.ClaimedTotal);
        Assert.Equal(1150m, await PayerBalanceAsync());
    }

    /// <summary>
    /// <b>وفاتورةُ شركةٍ أخرى لا تدخل دفعةَ هذه.</b>
    ///
    /// <para><b>وطفرةٌ نجت فأخرجت هذا كذلك</b>: كلُّ اختباراتي بشركةٍ واحدة، <b>فلا شيءَ
    /// يقيس أنّ التمييزَ بالعميل يعمل</b>. وضمُّ فاتورةِ شركةٍ إلى دفعةِ أخرى <b>يُطالِب
    /// بها مَن لا يعرفها</b>، فتُرفض الدفعةُ كلُّها عند الشركة — وتُسجَّل مخالفةً على
    /// المزوّد.</para>
    /// </summary>
    [Fact]
    public async Task BuildAsync_ExcludesAnotherPayersInvoices()
    {
        await SetupAsync();
        await BilledVisitAsync(1000m);

        var (otherPayerId, _) = await SecondPayerVisitAsync(500m);

        var claim = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, _payerId, "CLM-01", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Single(claim.Lines);
        Assert.Equal(1000m, claim.ClaimedTotal);

        // ‏**والأخرى تُبنى بمبلغها هي** — فالتمييزُ يعمل في الاتّجاهين لا في واحد.
        var other = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, otherPayerId, "CLM-02", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Single(other.Lines);
        Assert.Equal(500m, other.ClaimedTotal);
    }

    /// <summary>شركةٌ ثانيةٌ بمريضِها ووثيقتِها وزيارةٍ مفوترة.</summary>
    private async Task<(long PayerId, long CustomerId)> SecondPayerVisitAsync(decimal amount)
    {
        var otherCustomer = Customer.Create(_companyId, "شركة تأمين ثانية");
        var otherPatientCustomer = Customer.Create(_companyId, "مريض ثانٍ");
        _db.Customers.AddRange(otherCustomer, otherPatientCustomer);
        await _db.SaveChangesAsync();

        var otherPayer = InsurancePayer.Create(_companyId, "PAY2", "شركة تأمين ثانية", otherCustomer.Id);
        var otherPatient = Patient.Create(_companyId, "P-002", "مريض ثانٍ", "1087654321",
            new DateOnly(1985, 2, 2), PatientGender.Female, otherPatientCustomer.Id);
        _db.InsurancePayers.Add(otherPayer);
        _db.Patients.Add(otherPatient);
        await _db.SaveChangesAsync();

        var otherPolicy = InsurancePolicy.Create(_companyId, otherPatient.Id, otherPayer.Id,
            "POL-2", "M-2", coveragePercent: 100m,
            startDate: new DateOnly(2026, 1, 1), endDate: new DateOnly(2026, 12, 31));
        _db.InsurancePolicies.Add(otherPolicy);
        await _db.SaveChangesAsync();

        var visit = Visit.Create(_companyId, otherPatient.Id, $"V-{++_visitSeq:000}",
            new DateOnly(2026, 3, 12), "د. المسبار", otherPolicy.Id);
        visit.AddLine("خدمة طبية", 1m, amount, isCovered: true,
            vatCategory: VatCategory.ZeroRated,
            taxExemptionReasonCode: "VATEX-SA-HEA",
            taxExemptionReason: "خدمات صحية صفرية النسبة");
        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();

        await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);
        return (otherPayer.Id, otherCustomer.Id);
    }

    /// <summary>
    /// <b>والمرفوضُ يخرج بالفلس — لا يبقى منه شيءٌ في الذمّة.</b>
    ///
    /// <para><b>ومسبارُ القطاعات أخرج هذا</b>: القسمةُ على <c>١٫١٥</c> ثمّ التقريبُ تُنتج
    /// إجمالياً يفترق بفلس، <b>والفلسُ يبقى في ذمّة الشركة وقد رفضته</b> — وهو عينُ الذمّة
    /// الوهمية التي بُني هذا الباب لإغلاقها، بمقدارٍ أصغر لا بطبيعةٍ أخرى. <b>وفلسٌ في كلّ
    /// رفضٍ في عيادةٍ ترفض مئتَي مطالبةٍ شهرياً ذمّةٌ لا تُسوَّى أبداً.</b></para>
    ///
    /// <para>‏١٠٠٠ صافياً ⇒ ١١٥٠ إجمالاً · يُرفض ٣٤٥ ⇒ يبقى <b>٨٠٥ بالضبط</b>.</para>
    /// </summary>
    [Fact]
    public async Task RejectedAmountOnAStandardRatedClaim_LeavesToTheHalala()
    {
        await SetupAsync();
        await BilledVisitAsync(1000m, zeroRated: false);

        var claim = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, _payerId, "CLM-VAT2", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        claim.Submit(new DateOnly(2026, 4, 1));
        _db.InsuranceClaims.Add(claim);
        await _db.SaveChangesAsync();

        var invoiceId = claim.Lines[0].PayerInvoiceId;
        claim.Adjudicate(invoiceId, 805m, 345m, "خدمة غير مغطاة");
        claim.CloseAdjudication(new DateOnly(2026, 4, 1));
        claim.DisposeRejection(invoiceId, RejectionDisposition.WriteOff);
        await _db.SaveChangesAsync();

        await new ClaimSettlementService(_db)
            .SettleAsync(_companyId, claim.Id, new DateOnly(2026, 4, 1));

        Assert.Equal(805m, await PayerBalanceAsync());
    }

    /// <summary>
    /// <b>والضريبةُ المعكوسة بنسبة الوعاء من الإجمالي — لا من الصافي.</b>
    ///
    /// <para>زيارةٌ بخدمةٍ صفريةٍ وإجراءٍ أساسيّ: <b>وعاءان</b>. والمرفوضُ يُوزَّع عليهما،
    /// <b>وتوزيعُه بالصافي يُعطي الوعاءَ الأساسيَّ حصّةً أقلَّ ممّا يخصّه</b> — فيُعكَس من
    /// ضريبة المخرجات أقلُّ ممّا حُصِّل. <b>والفرقُ يبقى للهيئة لا للمنشأة</b>: ضريبةٌ
    /// دُفعت على مبلغٍ لم يُقبض.</para>
    ///
    /// <para>‏١٠٠٠ أساسيٌّ (١١٥٠ إجمالاً) + ٥٠٠ صفريّ = ١٦٥٠ · يُرفض ٣٣٠ ⇒ حصّةُ الأساسيّ
    /// <c>٣٣٠ × ١١٥٠/١٦٥٠ = ٢٣٠</c>، وضريبتُه <b>٣٠ بالضبط</b> — وهي خُمسُ ضريبة الفاتورة،
    /// كما أنّ ٢٣٠ خُمسُ ١١٥٠.</para>
    /// </summary>
    [Fact]
    public async Task RejectionAcrossTwoTaxBuckets_ReversesVatByGrossShare()
    {
        await SetupAsync();
        await MixedVisitAsync(standardNet: 1000m, zeroRatedNet: 500m);

        var claim = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, _payerId, "CLM-MIX", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        Assert.Equal(1650m, claim.ClaimedTotal);

        claim.Submit(new DateOnly(2026, 4, 1));
        _db.InsuranceClaims.Add(claim);
        await _db.SaveChangesAsync();

        var invoiceId = claim.Lines[0].PayerInvoiceId;
        claim.Adjudicate(invoiceId, 1320m, 330m, "إجراءٌ غير معتمَد");
        claim.CloseAdjudication(new DateOnly(2026, 4, 1));
        claim.DisposeRejection(invoiceId, RejectionDisposition.WriteOff);
        await _db.SaveChangesAsync();

        var result = await new ClaimSettlementService(_db)
            .SettleAsync(_companyId, claim.Id, new DateOnly(2026, 4, 1));

        var note = await _db.CreditNotes.SingleAsync(n => n.Id == result.CreditNoteIds[0]);
        Assert.Equal(300m, note.Subtotal);    // ‏٢٠٠ أساسيّ + ١٠٠ صفريّ
        Assert.Equal(30m, note.VatAmount);    // ‏وضريبةُ الأساسيّ وحدَه
        Assert.Equal(1320m, await PayerBalanceAsync());
    }

    /// <summary>
    /// <b>ومبلغٌ لا يُمثَّل بصافٍ وضريبةٍ يُرفض بصوت.</b>
    ///
    /// <para><c>345.04</c> عند <c>١٥٪</c> <b>لا صافيَ له</b>: أيُّ صافٍ إمّا يُنتج
    /// <c>345.03</c> أو <c>345.05</c>. <b>وقبولُه يُصدر إشعاراً بغير ما رفضته الشركة</b>،
    /// فيبقى فلسٌ في ذمّتها إلى الأبد. <b>والرفضُ يقول للمحاسب ما يفعل</b>: عدِّلْه بفلس.</para>
    /// </summary>
    [Fact]
    public async Task AnUnrepresentableRejectedAmount_IsRefusedByName()
    {
        await SetupAsync();
        await BilledVisitAsync(1000m, zeroRated: false);

        var claim = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, _payerId, "CLM-ODD", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        claim.Submit(new DateOnly(2026, 4, 1));
        _db.InsuranceClaims.Add(claim);
        await _db.SaveChangesAsync();

        var invoiceId = claim.Lines[0].PayerInvoiceId;
        claim.Adjudicate(invoiceId, 804.96m, 345.04m, "خدمة غير مغطاة");
        claim.CloseAdjudication(new DateOnly(2026, 4, 1));
        claim.DisposeRejection(invoiceId, RejectionDisposition.WriteOff);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new ClaimSettlementService(_db)
                .SettleAsync(_companyId, claim.Id, new DateOnly(2026, 4, 1)));

        Assert.Contains("لا يُمثَّل بصافٍ وضريبةٍ", error.Message);
    }

    /// <summary>زيارةٌ بوعاءين: إجراءٌ أساسيُّ النسبة وخدمةٌ صفرية — وكلاهما مُغطّى.</summary>
    private async Task MixedVisitAsync(decimal standardNet, decimal zeroRatedNet)
    {
        var visit = Visit.Create(_companyId, _patientId, $"V-{++_visitSeq:000}",
            new DateOnly(2026, 3, 10), "د. المسبار", _policyId);

        visit.AddLine("إجراء تجميلي", 1m, standardNet, isCovered: true);
        visit.AddLine("خدمة طبية", 1m, zeroRatedNet, isCovered: true,
            vatCategory: VatCategory.ZeroRated,
            taxExemptionReasonCode: "VATEX-SA-HEA",
            taxExemptionReason: "خدمات صحية صفرية النسبة");

        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();
        await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> PayerBalanceAsync() =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _payerCustomerId)).Balance;

    private async Task<decimal> PatientBalanceAsync() =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _patientCustomerId)).Balance;

    private async Task<decimal> BalanceAsync(string prefix)
    {
        var trial = await new Infrastructure.Reporting.TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode.StartsWith(prefix))
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    /// <summary>دفعةٌ مُقدَّمةٌ بمبالغَ مُطالَبٍ بها — واحدةٌ لكلّ مبلغ.</summary>
    private async Task<InsuranceClaim> SubmittedClaimAsync(params decimal[] amounts)
    {
        await SetupAsync();
        foreach (var amount in amounts) await BilledVisitAsync(amount);

        var claim = await new ClaimSettlementService(_db).BuildAsync(
            _companyId, _payerId, "CLM-01", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        claim.Submit(new DateOnly(2026, 4, 1));
        _db.InsuranceClaims.Add(claim);
        await _db.SaveChangesAsync();
        return claim;
    }

    /// <summary>
    /// زيارةٌ مُغطّاةٌ بالكامل — <b>فحصّةُ الشركة كلُّ المبلغ</b>، ليُقاس المرفوضُ وحدَه.
    /// </summary>
    private async Task BilledVisitAsync(decimal amount, bool zeroRated = true)
    {
        var visit = Visit.Create(_companyId, _patientId, $"V-{++_visitSeq:000}",
            new DateOnly(2026, 3, 10), "د. المسبار", _policyId);

        if (!zeroRated)
        {
            // ‏**والمُعالَجاتُ التجميلية بالنسبة الأساسية** — ليست كلُّ خدمةٍ في العيادة صفرية.
            visit.AddLine("إجراء تجميلي", 1m, amount, isCovered: true);
            _db.Visits.Add(visit);
            await _db.SaveChangesAsync();
            await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);
            return;
        }

        visit.AddLine("خدمة طبية", 1m, amount, isCovered: true,
            // ‏**والخدمةُ الصحية صفريةُ النسبة لا معفاة** — والرمزُ نفسُه يميّزهما،
            // ‏وحارسُ الفاتورة كشف خطئي حين خلطتُ بينهما.
            vatCategory: VatCategory.ZeroRated,
            taxExemptionReasonCode: "VATEX-SA-HEA",
            taxExemptionReason: "خدمات صحية صفرية النسبة");

        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();

        await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("عيادة", "310123456700066", BusinessProfile.Healthcare);
        _companyId = company.Id;

        var payerCustomer = Customer.Create(_companyId, "شركة تأمين");
        _db.Customers.Add(payerCustomer);
        var patientCustomer = Customer.Create(_companyId, "مريض");
        _db.Customers.Add(patientCustomer);
        await _db.SaveChangesAsync();
        _payerCustomerId = payerCustomer.Id;
        _patientCustomerId = patientCustomer.Id;

        var payer = InsurancePayer.Create(_companyId, "PAY1", "شركة تأمين", payerCustomer.Id);
        _db.InsurancePayers.Add(payer);
        var patient = Patient.Create(_companyId, "P-001", "مريض المسبار", "1012345678",
            new DateOnly(1990, 5, 1), PatientGender.Male, patientCustomer.Id);
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        _payerId = payer.Id;
        _patientId = patient.Id;

        // ‏تغطيةٌ كاملةٌ بلا تحمّلٍ ولا سقف — فالمقيسُ هنا الرفضُ لا القسمة.
        var policy = InsurancePolicy.Create(_companyId, patient.Id, payer.Id, "POL-1", "M-1",
            coveragePercent: 100m,
            startDate: new DateOnly(2026, 1, 1), endDate: new DateOnly(2026, 12, 31),
            copayFixed: 0m, annualLimit: null);
        _db.InsurancePolicies.Add(policy);
        await _db.SaveChangesAsync();
        _policyId = policy.Id;
    }
}
