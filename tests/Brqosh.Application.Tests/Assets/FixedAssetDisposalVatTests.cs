using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (CEEF-232 · ض-٢) <b>بيعُ الأصل الثابت توريدٌ خاضعٌ — وكان يُرحَّل بلا ريالِ ضريبة.</b>
///
/// <para><b>ما كان:</b> <c>PostDisposalAsync</c> يُقيّد العائدَ كلَّه نقداً، <b>ولا سطرَ
/// على حساب ضريبة المخرجات إطلاقاً</b>. فمنشأةٌ تبيع آلةً بمئة ألف تُحصّل خمسةَ عشرَ
/// ألفاً ضريبةً <b>لا يعرفها النظام</b> — لا في الأستاذ ولا في الإقرار.
///
/// <para><b>والحكم نظاميّ لا اجتهاديّ:</b> بيعُ أصلٍ من أصول النشاط توريدٌ خاضعٌ بالنسبة
/// الأساسية. <b>وتركُه بلا ضريبةٍ نقصٌ في الإقرار يُطالَب به مع غرامته</b> — وهو أشيعُ ما
/// يُضبط في فحص المنشآت الصغيرة، إذ تبيع سياراتِها وأجهزتَها القديمة سنوياً.</para>
///
/// <para><b>وقرارُ المعنى معلَن:</b> <c>saleProceeds</c> هو <b>المقابلُ قبل الضريبة</b> —
/// وبه يُحسب الربحُ والخسارة كما كان بالضبط (فالربحُ يُقاس على الصافي لا على المحصَّل).
/// <b>والنقدُ الداخل هو المقابلُ زائد الضريبة</b>، وهو ما يقبضه البائع فعلاً.</para>
///
/// <para><b>وهويّةُ ض-٢:</b> بعد استبعادٍ خاضع: <c>النقدُ المدين = المقابل + ضريبتُه</c>،
/// <c>والربح/الخسارة = المقابل − القيمة الدفترية</c> — <b>لا تتغيّر بالضريبة</b>.</para>
/// </summary>
public class FixedAssetDisposalVatTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _assetAccountId;
    private readonly long _accumAccountId;

    public FixedAssetDisposalVatTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;

        _assetAccountId = _db.Accounts.Single(a => a.CompanyId == _companyId && a.Code == "1203").Id;
        _accumAccountId = _db.Accounts.Single(a => a.CompanyId == _companyId && a.Code == "1207").Id;
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم (ض-٢): الاستبعادُ الخاضع يُقيّد ضريبةَ مخرجاته.</b>
    ///
    /// <para>سيارةٌ تكلفتُها ١٢٠٬٠٠٠ أُهلك منها ٢٤٬٠٠٠، تُباع بـ٨٠٬٠٠٠ قبل الضريبة.
    /// <b>والنقدُ الداخل ٩٢٬٠٠٠</b> — والخسارةُ ١٦٬٠٠٠ كما كانت، <b>فالضريبةُ ليست
    /// مصروفاً على المنشأة بل أمانةٌ تُورَّد</b>.</para>
    /// </summary>
    [Fact]
    public async Task ATaxableDisposal_PostsOutputVat()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 24);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 80_000m, new DateOnly(2026, 1, 15), "admin");

        Assert.True(entry.IsBalanced);
        Assert.Equal(12_000m, (await LineAsync(entry.Id, SaudiChartOfAccounts.SystemCodes.VatOutput)).Credit);
        Assert.Equal(92_000m, (await LineAsync(entry.Id, SaudiChartOfAccounts.SystemCodes.CashOnHand)).Debit);
        Assert.Equal(16_000m, (await LineAsync(entry.Id, "5215")).Debit);
    }

    /// <summary>
    /// <b>وتبلغُ الإقرارَ — وهو المقصدُ كلُّه.</b>
    ///
    /// <para><b>وقيدٌ صحيحٌ في الأستاذ لا يبلغ الإقرارَ لا يُبرّئ أحداً</b>: الهيئةُ تقرأ
    /// الإقرارَ لا الدفاتر. وهذا القياسُ يصل الطرفين — <c>ض-٢</c> يُنشئ الضريبة و<c>ض-١</c>
    /// يوصلها.</para>
    /// </summary>
    [Fact]
    public async Task TheDisposalVat_ReachesTheVatReturn()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 24);

        await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 80_000m, new DateOnly(2026, 1, 15), "admin");

        var result = await new VatReturnQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(12_000m, result.NetOutputVat);
    }

    /// <summary>
    /// <b>والتلفُ والتبرّعُ بلا عائدٍ لا ضريبةَ فيهما.</b>
    ///
    /// <para><b>ولا يُقاس ذلك بـ«صفرٌ في صفر»</b>: العائدُ صفرٌ فالضريبةُ صفرٌ حسابياً على
    /// أيّ حال. <b>والمقصودُ ألّا يُضاف سطرٌ صفريٌّ على حساب المخرجات</b> — فسطرٌ صفريٌّ
    /// في دفتر الأستاذ يُقرأ توريداً وقع بقيمة صفر، وهو ما لم يقع.</para>
    /// </summary>
    [Fact]
    public async Task AScrappingWithoutProceeds_HasNoVatLine()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 0);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 0m, new DateOnly(2026, 1, 15), "admin");

        Assert.True(entry.IsBalanced);
        Assert.DoesNotContain(entry.Lines,
            l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.VatOutput);
    }

    /// <summary>
    /// <b>والفئةُ غيرُ الأساسية لا تُضرَّب.</b>
    ///
    /// <para><b>والحاجةُ إليها واقعة:</b> تصديرُ معدّةٍ إلى الخارج توريدٌ <b>صفريُّ النسبة</b>
    /// لا معفى ولا خاضع — <b>وفرضُ خمسةَ عشرَ بالمئة عليه يُخرج المنشأةَ من السوق</b>، أو
    /// يجعلها تدفع من مالها ضريبةً لا تُحصّلها.</para>
    /// </summary>
    /// <remarks>
    /// (جاهزيّةُ الإطلاق · ح-١٦) <b>والمقدّمةُ أُكمِلت، والحكمُ لم يُمَسّ.</b>
    ///
    /// <para>كان هذا الاستدعاءُ يُمرّر فئةً صفريّةً <b>بلا رمزِ سبب</b> — وكان يمرّ لأنّ
    /// الاستبعادَ لم يكن يُصدر مستنداً أصلاً. <b>وبعد ح-١٦ يُصدر</b>، و<c>BR-KSA-CL-06</c>
    /// يرفض فئةً غيرَ أساسيّةٍ بلا رمزٍ من قائمة السعودية.</para>
    ///
    /// <para><b>فالمقدّمةُ كانت تعتمد على العطل</b> — تصديرٌ صفريٌّ بلا سببٍ مُعلَن — وهي
    /// حالةٌ <b>لا تصلح فاتورةً</b>. فأُكمِلت بالرمز <c>VATEX-SA-32</c> (تصدير السلع)،
    /// <b>وبقيت الدعوى كما هي حرفاً</b>: الصفريُّ لا يُقيَّد له ريالُ ضريبة.</para>
    /// </remarks>
    [Fact]
    public async Task AZeroRatedDisposal_ChargesNoVat()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 24);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 80_000m, new DateOnly(2026, 1, 15), "admin",
                vatCategory: VatCategory.ZeroRated, vatExemptionReasonCode: "VATEX-SA-32");

        Assert.True(entry.IsBalanced);
        Assert.DoesNotContain(entry.Lines,
            l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.VatOutput);
        Assert.Equal(80_000m, (await LineAsync(entry.Id, SaudiChartOfAccounts.SystemCodes.CashOnHand)).Debit);
    }

    /// <summary>
    /// <b>والربحُ يُقاس على المقابل لا على المحصَّل — وهذا أخطرُ التباسٍ في البند.</b>
    ///
    /// <para>أصلٌ قيمتُه الدفترية ٩٦٬٠٠٠ يُباع بـ١٠٠٬٠٠٠ قبل الضريبة: <b>الربحُ ٤٬٠٠٠</b>.
    /// <b>ولو حُسب على المحصَّل (١١٥٬٠٠٠) لبلغ ١٩٬٠٠٠</b> — فيُعلَن ربحٌ خمسةَ عشرَ ألفاً
    /// لم يقع، <b>وتُدفع عليه زكاةٌ ويُوزَّع</b>، وهو مالُ الهيئة في يد المنشأة.</para>
    /// </summary>
    [Fact]
    public async Task TheGain_IsMeasuredOnTheConsideration_NotOnTheCashCollected()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 24);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 100_000m, new DateOnly(2026, 1, 15), "admin");

        Assert.Equal(4_000m, (await LineAsync(entry.Id, "4106")).Credit);
        Assert.Equal(115_000m, (await LineAsync(entry.Id, SaudiChartOfAccounts.SystemCodes.CashOnHand)).Debit);
    }

    /// <summary>
    /// (CEEF-232 · ض-٤) <b>ومنشأةٌ غير مسجَّلةٍ لا تُصدر ضريبةً — ولو طُلبت منها.</b>
    ///
    /// <para><b>وهذا ما يجعل الفئةَ الافتراضيّةَ خطراً لا تيسيراً:</b> الشاشةُ الوحيدةُ
    /// لا تُمرّر فئة، <b>فتُفرض الأساسيّةُ على كلّ منشأة</b>. ومنشأةٌ بلا رقمٍ ضريبيٍّ تبيع
    /// معدّةً بمئة ألفٍ <b>تُقيّد التزاماً مُختلَقاً بخمسةَ عشرَ ألفاً تجاه الهيئة</b>
    /// وتُحصّله من المشتري بغير حقّ — <b>ثمّ يبلغه الإقرارُ بعد ض-١</b> فتُقدَّم بضريبةٍ
    /// لا تخصّها.</para>
    ///
    /// <para><b>والحكمُ من بيانات المنشأة لا من وسيطٍ يُنسى</b> — فالفئةُ المُمرَّرة هنا
    /// «أساسيّة» صراحةً <b>ولا تُغيّر شيئاً</b>: القاعدةُ فوقها.</para>
    /// </summary>
    [Fact]
    public async Task AnUnregisteredCompany_ChargesNoVat_EvenWhenStandardIsAsked()
    {
        // ‏**والمنشأةُ بلا رقمٍ ضريبيٍّ حالةٌ يقبلها النطاقُ صراحةً** (`Company.Update`
        // ‏يسمح بالفراغ) — فليست فرضاً، بل كلُّ منشأةٍ دون حدّ التسجيل.
        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        company.Update(company.NameAr, "");
        await _db.SaveChangesAsync();

        var asset = await CreateVehicleAsync(monthsDepreciated: 24);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 100_000m, new DateOnly(2026, 1, 15), "admin",
                vatCategory: VatCategory.Standard);

        Assert.True(entry.IsBalanced);
        Assert.DoesNotContain(entry.Lines,
            l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.VatOutput);

        // ‏**والمقبوضُ هو المقابلُ وحدَه** — والربحُ لم يتغيّر بريال.
        Assert.Equal(100_000m, (await LineAsync(entry.Id, SaudiChartOfAccounts.SystemCodes.CashOnHand)).Debit);
        Assert.Equal(4_000m, (await LineAsync(entry.Id, "4106")).Credit);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<FixedAsset> CreateVehicleAsync(int monthsDepreciated)
    {
        // ‏الأصلُ «مُهلَكٌ حتى شهر الاستبعاد»: الاستبعادُ يُقيّد إهلاكَ ما انقضى قبله (قرارُ المالك 2026-10-20)،
        // ‏وهذه الاختباراتُ تقيس حسابَ القيد لا ذلك الاستدراك — فتُعطى أصلاً قائماً بمجمّعه (الاستدراكُ له ملفُّه).
        var asset = FixedAsset.CreateOpening(
            _companyId, "FA-1", "سيارة", new DateOnly(2024, 1, 1),
            acquisitionCost: 120_000m, salvageValue: 0m, usefulLifeMonths: 120,
            glAssetAccountId: _assetAccountId, glDepreciationAccountId: _accumAccountId,
            accumulatedDepreciation: monthsDepreciated * 1_000m, openingDate: new DateOnly(2026, 2, 1));

        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    private async Task<(decimal Debit, decimal Credit)> LineAsync(long entryId, string code)
    {
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == entryId);
        return (entry.Lines.Where(l => l.AccountCode == code).Sum(l => l.Debit),
                entry.Lines.Where(l => l.AccountCode == code).Sum(l => l.Credit));
    }
}
