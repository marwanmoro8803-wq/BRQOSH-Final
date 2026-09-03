using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Domain.RealEstate;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.RealEstate;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.RealEstate;

/// <summary>
/// (CEEF-232 · ع-١/ع-٢) <b>العقارُ المؤجَّر لا يُهلَك، والتجديدُ يقع في شهرٍ واحد.</b>
///
/// <para><b>ما كان — نصفان لحفرةٍ واحدة:</b> <c>Property</c> بلا تكلفةٍ ولا عمرٍ إنتاجيٍّ
/// ولا ربطٍ بأصل، و<c>FixedAsset</c> بلا <c>PropertyId</c> — <b>و<c>DepreciationService</c>
/// يُهلك صفوفَ الأصول وحدَها</b>. و<c>MaintenanceKind</c> ثلاثةُ أصنافٍ كلُّها مصروف،
/// و<c>MaintenanceOrderService</c> يُحمّل <c>5206</c> <b>بلا شرط</b>.</para>
///
/// <para><b>فالأثرُ مضاعف:</b> الربحُ <b>مُبالَغٌ فيه بكامل الإهلاك السنويّ</b> — يُعترف
/// بإيراد الإيجار كلِّه بلا مقابلٍ من استهلاك المبنى، <b>وتُدفع زكاةٌ ويُوزَّع على ربحٍ
/// لم يقع</b> — <b>ثمّ يُشوَّه مرّةً أخرى</b> حين يقع تجديدُ برجٍ بأربعمئة ألفٍ في شهرٍ
/// واحد. <b>وعند البيع تظهر «خسارةُ استبعاد» هي إهلاكُ سنواتٍ لم يُقيَّد.</b></para>
///
/// <para><b>وهويّةُ ع-١:</b> عقارٌ مرتبطٌ بأصلٍ <b>يُهلَك مع الدفعة كأيّ أصل</b>.
/// <b>وهويّةُ ع-٢:</b> التجديدُ الرأسماليُّ <b>يزيد تكلفةَ الأصل ولا يمسّ المصروف</b>،
/// <b>ويزيد القسطَ الشهريَّ تبعاً</b>.</para>
/// </summary>
public class PropertyCapitalizationTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _propertyId;
    private long _assetId;

    private static readonly DateOnly On = new(2026, 5, 15);

    public PropertyCapitalizationTests()
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

    /// <summary>
    /// <b>القياسُ الحاكم (ع-٢): التجديدُ يزيد الأصلَ ولا يمسّ المصروف.</b>
    ///
    /// <para>مبنًى بمليونٍ وتجديدٌ بأربعمئة ألف ⟵ <b>التكلفةُ ١٬٤٠٠٬٠٠٠</b> والمصروفُ
    /// <b>صفر</b>. <b>وكان المصروفُ أربعمئة ألفٍ في شهرٍ واحد</b> — يُظهر شهراً خاسراً
    /// بلا سببٍ تشغيليّ ويحرم السنواتِ التاليةَ من حصّتها.</para>
    /// </summary>
    [Fact]
    public async Task ACapitalImprovement_RaisesTheAssetCost_AndNeverTouchesExpense()
    {
        await SetupAsync();
        var assetAccountBefore = await BalanceAsync("1203");

        var order = await CompletedOrderAsync(MaintenanceKind.CapitalImprovement, 400_000m);
        await new MaintenanceOrderService(_db).PostAsync(_companyId, order.Id, On);

        var asset = await _db.FixedAssets.SingleAsync(a => a.Id == _assetId);
        Assert.Equal(1_400_000m, asset.AcquisitionCost);

        Assert.Equal(0m, await BalanceAsync(MaintenanceOrderService.MaintenanceExpenseCode));

        // ‏**والقيدُ على حساب الأصل نفسِه** — لا على مصروفٍ ثمّ نقلٍ يدويّ.
        //
        // ‏**والفرقُ يُقاس لا الرصيدُ المطلق**: صفُّ الأصل في هذه التهيئة يُنشأ مباشرةً
        // ‏بلا قيدِ اقتناء، **فحسابُ `1203` لا يحمل المليونَ الأصليّ**. ومقارنةُ رصيدٍ
        // ‏بمرجعٍ لم يُقيَّد تقيس التهيئةَ لا المنتج.
        Assert.Equal(assetAccountBefore + 400_000m, await BalanceAsync("1203"));
    }

    /// <summary>
    /// <b>والقسطُ الشهريُّ يزيد تبعاً — وإلّا كانت الرسملةُ نصفَ عمل.</b>
    ///
    /// <para><b>ورسملةٌ لا تُغيّر القسط تُنشئ أصلاً لا يُهلَك أبداً</b>: تظلّ الأربعمئة
    /// ألفٍ في الميزانيّة إلى الأبد، <b>فيُخفى المصروفُ بدل أن يُوزَّع</b> — وهو أسوأ
    /// من صرفه دفعةً واحدة.</para>
    /// </summary>
    [Fact]
    public async Task TheMonthlyDepreciation_RisesWithTheCapitalisedCost()
    {
        await SetupAsync();
        var before = (await _db.FixedAssets.SingleAsync(a => a.Id == _assetId)).MonthlyDepreciation;

        var order = await CompletedOrderAsync(MaintenanceKind.CapitalImprovement, 240_000m);
        await new MaintenanceOrderService(_db).PostAsync(_companyId, order.Id, On);

        var after = (await _db.FixedAssets.SingleAsync(a => a.Id == _assetId)).MonthlyDepreciation;

        // ‏٢٤٠٬٠٠٠ على ٢٤٠ شهراً = ألفٌ زيادةً في القسط.
        Assert.Equal(before + 1_000m, after);
    }

    /// <summary>
    /// <b>(ع-١) والعقارُ المرتبط يُهلَك مع الدفعة كأيّ أصل.</b>
    ///
    /// <para><b>وهذا هو نصفُ الحفرة الأوّل</b>: بلا الرابط <b>لا تُهلَك محفظةٌ عقاريّةٌ
    /// كاملةٌ بريالٍ واحد</b> — ولم يُبنَ لها محرّكٌ ثانٍ، بل أُدخلت في المحرّك القائم.</para>
    /// </summary>
    [Fact]
    public async Task ALinkedProperty_DepreciatesWithTheRun()
    {
        await SetupAsync();

        var depreciated = await new DepreciationService(_db)
            .RunMonthlyAsync(_companyId, new DateOnly(2026, 5, 31), "admin");

        Assert.True(depreciated >= 1,
            "لم يُهلَك العقارُ المرتبط — فالمحفظةُ العقاريّةُ تبقى بلا استهلاك.");

        var asset = await _db.FixedAssets.SingleAsync(a => a.Id == _assetId);
        Assert.True(asset.AccumulatedDepreciation > 0m);
    }

    /// <summary>
    /// <b>ولا تُرسمَل على عقارٍ بلا أصلٍ مرتبط — يُرفض الترحيلُ بسببٍ مكتوب.</b>
    ///
    /// <para><b>ورسملةٌ إلى العدم تُخفي المصروفَ ولا تُنشئ أصلاً</b> — فالمبلغُ يختفي
    /// من قائمة الدخل ولا يظهر في الميزانيّة. <b>والرفضُ المُعلَن خيرٌ من صرفٍ صامتٍ
    /// على خلاف ما طلبه المحاسب.</b></para>
    /// </summary>
    [Fact]
    public async Task CapitalisingOnAnUnlinkedProperty_IsRefused()
    {
        await SetupAsync(linkAsset: false);
        var order = await CompletedOrderAsync(MaintenanceKind.CapitalImprovement, 50_000m);

        await Assert.ThrowsAsync<AccountingException>(() =>
            new MaintenanceOrderService(_db).PostAsync(_companyId, order.Id, On));
    }

    /// <summary>
    /// <b>والصيانةُ العاديّةُ تبقى مصروفاً — فالتغييرُ لا يمسّ ما كان يعمل.</b>
    ///
    /// <para><b>ورسملةُ دهانٍ سنويٍّ تُضخّم الأصلَ بما ليس فيه</b> وتُؤجّل مصروفاً
    /// وقع فعلاً — وهي المخالفةُ في الاتجاه المقابل.</para>
    /// </summary>
    [Fact]
    public async Task OrdinaryMaintenance_IsStillExpensed()
    {
        await SetupAsync();
        var order = await CompletedOrderAsync(MaintenanceKind.Preventive, 5_000m);

        await new MaintenanceOrderService(_db).PostAsync(_companyId, order.Id, On);

        Assert.Equal(5_000m, await BalanceAsync(MaintenanceOrderService.MaintenanceExpenseCode));

        var asset = await _db.FixedAssets.SingleAsync(a => a.Id == _assetId);
        Assert.Equal(1_000_000m, asset.AcquisitionCost);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<MaintenanceOrder> CompletedOrderAsync(MaintenanceKind kind, decimal cost)
    {
        var order = MaintenanceOrder.Create(_companyId, _propertyId, $"MO-{Guid.NewGuid():N}"[..12],
            kind, new DateOnly(2026, 5, 1), "أعمال");
        order.AddCost("أعمال", cost);
        order.Complete(new DateOnly(2026, 5, 10));

        _db.MaintenanceOrders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task SetupAsync(bool linkAsset = true)
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة عقارات", "310123456700099", BusinessProfile.RealEstate);
        _companyId = company.Id;

        var assetAccount = _db.Accounts.Single(a => a.CompanyId == _companyId && a.Code == "1203");
        var accumAccount = _db.Accounts.Single(a => a.CompanyId == _companyId && a.Code == "1207");

        // ‏مبنًى بمليون على عشرين سنة — **٢٤٠ شهراً**، فالقسطُ يقبل القسمةَ بلا كسر.
        var asset = FixedAsset.Create(_companyId, "FA-BLD", "مبنى مؤجَّر",
            new DateOnly(2026, 1, 1), acquisitionCost: 1_000_000m, salvageValue: 0m,
            usefulLifeMonths: 240, assetAccount.Id, accumAccount.Id);
        _db.FixedAssets.Add(asset);

        var property = Property.Create(_companyId, "PR-1", "برج", PropertyKind.Building,
            "الرياض", "طريق الملك");
        _db.Properties.Add(property);
        await _db.SaveChangesAsync();

        _assetId = asset.Id;
        _propertyId = property.Id;

        if (linkAsset)
        {
            property.LinkFixedAsset(asset.Id);
            await _db.SaveChangesAsync();
        }
    }
}
