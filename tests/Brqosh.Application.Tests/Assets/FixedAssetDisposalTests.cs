using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (CEEF-229 · م0-١٥) استبعاد أصل ثابت قبل نهاية عمره الإنتاجيّ.
///
/// <para><b>العطل:</b> <c>EnsureReadyForDisposal()</c> كانت ترفض الاستبعاد ما دامت القيمة
/// الدفترية أعلى من قيمة الخردة — أي <b>ما دام الأصل لم يُستهلَك بالكامل</b>. وهو يمنع أكثر
/// حالات الاستبعاد وقوعاً: بيع سيارة بعد سنتين من عمرٍ عشر، أو استبعاد جهاز تلف. ورسالتُه
/// تقترح علاجاً يُفسد الدفاتر: «شغّل الإهلاك أولاً» — ستّاً وتسعين مرّة، فيُحمَّل إهلاكُ ثمان
/// سنوات على شهرٍ واحد.</para>
///
/// <para><b>ولا مبرّر محاسبيّ له:</b> القيد يوازن بالبناء — تُخرَج التكلفة كاملةً ويُقفل مجمّع
/// الإهلاك مهما بلغ، والفرق يقع على الربح أو الخسارة. وحضورُ فرع الربح في المُرحِّل بلا مسارٍ
/// يبلغه كان أوضح دليل على أن الحارس زائد.</para>
/// </summary>
public class FixedAssetDisposalTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _assetAccountId;
    private long _accumAccountId;

    public FixedAssetDisposalTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        var company = new DatabaseInitializer(_db)
            .InitializeAsync("شركة تجارية", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult();
        _companyId = company.Id;
        ManualJournals.LiftApprovalThreshold(_db);   // ‏(accounting-core#6) قيودٌ يدويّةٌ كبيرةٌ لسببٍ غيرِ الاعتماد — منشأةٌ بمستخدمٍ واحد

        _assetAccountId = _db.Accounts.Single(a => a.CompanyId == _companyId && a.Code == "1203").Id;
        _accumAccountId = _db.Accounts.Single(a => a.CompanyId == _companyId && a.Code == "1207").Id;
    }

    /// <summary>سيارة بـ120,000، عمر 120 شهراً، خردة صفر — أي 1,000 شهرياً.</summary>
    private async Task<FixedAsset> CreateVehicleAsync(int monthsDepreciated)
    {
        var asset = FixedAsset.Create(
            _companyId, "FA-1", "سيارة", new DateOnly(2024, 1, 1),
            acquisitionCost: 120_000m, salvageValue: 0m, usefulLifeMonths: 120,
            glAssetAccountId: _assetAccountId, glDepreciationAccountId: _accumAccountId);

        for (var i = 0; i < monthsDepreciated; i++)
            asset.ApplyDepreciation();

        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    private async Task<(decimal Debit, decimal Credit)> AccountTotalsAsync(long entryId, string code)
    {
        var accountId = (await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code)).Id;
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == entryId);
        return (entry.Lines.Where(l => l.AccountId == accountId).Sum(l => l.Debit),
                entry.Lines.Where(l => l.AccountId == accountId).Sum(l => l.Credit));
    }

    /// <summary>
    /// <b>القياس الأساسيّ.</b> سيارة بيعت بعد 24 شهراً من عمرٍ 120: التكلفة 120,000،
    /// مجمّع الإهلاك 24,000، القيمة الدفترية 96,000. تُباع بـ80,000 ⟵ <b>خسارة 16,000</b>.
    /// قبل الإصلاح كانت العملية <b>مستحيلة</b>.
    /// </summary>
    [Fact]
    public async Task DisposalBeforeEndOfLife_PostsLoss()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 24);
        Assert.Equal(96_000m, asset.BookValue);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 80_000m, new DateOnly(2026, 1, 15), "admin");

        Assert.True(entry.IsBalanced);
        Assert.Equal(16_000m, (await AccountTotalsAsync(entry.Id, "5215")).Debit);   // الخسارة
        Assert.Equal(120_000m, (await AccountTotalsAsync(entry.Id, "1203")).Credit); // التكلفة كاملةً
        Assert.Equal(24_000m, (await AccountTotalsAsync(entry.Id, "1207")).Debit);   // إقفال المجمّع
        // ‏(CEEF-232 · ض-٢) ‏والنقدُ صار المقابلَ **زائد ضريبتَه** — فبيعُ الأصل توريدٌ خاضع.
        // ‏والخسارةُ أعلاه لم تتغيّر بريال: **الضريبةُ أمانةٌ تُورَّد لا مصروفٌ ولا إيراد**.
        Assert.Equal(92_000m, (await AccountTotalsAsync(entry.Id, "1101")).Debit);   // ‏العائد + ١٥٪
        Assert.False(asset.IsActive);
    }

    /// <summary>
    /// وفرع الربح — الذي كان مبنيّاً بلا مسارٍ يبلغه. تُباع بـ110,000 وقيمتها الدفترية 96,000
    /// ⟵ <b>ربح 14,000</b> على حساب الإيرادات الأخرى.
    /// </summary>
    [Fact]
    public async Task DisposalAboveBookValue_PostsGain()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 24);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 110_000m, new DateOnly(2026, 1, 15), "admin");

        Assert.True(entry.IsBalanced);
        Assert.Equal(14_000m, (await AccountTotalsAsync(entry.Id, "4106")).Credit);
        Assert.Equal(0m, (await AccountTotalsAsync(entry.Id, "5215")).Debit);
    }

    /// <summary>
    /// أصلٌ لم يُهلَك إطلاقاً — الحالة الحديّة التي كان الحارس يرفضها بأشدّ صورة: مجمّع الإهلاك
    /// صفر، فالقيد سطران فقط زائد الخسارة. يجب أن يتوازن.
    /// </summary>
    [Fact]
    public async Task DisposalOfNeverDepreciatedAsset_StillBalances()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 0);
        Assert.Equal(120_000m, asset.BookValue);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 100_000m, new DateOnly(2026, 1, 15), "admin");

        Assert.True(entry.IsBalanced);
        Assert.Equal(20_000m, (await AccountTotalsAsync(entry.Id, "5215")).Debit);
        Assert.Equal(0m, (await AccountTotalsAsync(entry.Id, "1207")).Debit);
    }

    /// <summary>استبعاد بلا عائد (تلف/تبرّع): الخسارة تساوي القيمة الدفترية كاملةً.</summary>
    [Fact]
    public async Task DisposalWithoutProceeds_LossEqualsBookValue()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 60);
        Assert.Equal(60_000m, asset.BookValue);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 0m, new DateOnly(2026, 1, 15), "admin");

        Assert.True(entry.IsBalanced);
        Assert.Equal(60_000m, (await AccountTotalsAsync(entry.Id, "5215")).Debit);
        Assert.Equal(0m, (await AccountTotalsAsync(entry.Id, "1101")).Debit);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> ما بقي من الحماية يعمل. أصلٌ مستبعَد سلفاً يُرفض — فحذفُ شرط القيمة
    /// الدفترية لم يفتح الباب لاستبعادٍ مزدوج يُخرج التكلفة من الدفاتر مرّتين.
    /// </summary>
    [Fact]
    public async Task AlreadyDisposedAsset_IsStillRejected()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 12);
        await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, 50_000m, new DateOnly(2026, 1, 15), "admin");

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new FixedAssetDisposalPostingService(_db)
                .PostDisposalAsync(asset, 10_000m, new DateOnly(2026, 2, 15), "admin"));
        Assert.Contains("غير نشط", ex.Message);
    }

    /// <summary>وعائد سالب يبقى مرفوضاً — الحارس المعنيّ بالمدخلات لم يُمسّ.</summary>
    [Fact]
    public async Task NegativeProceeds_AreRejected()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 12);

        await Assert.ThrowsAsync<AccountingException>(() =>
            new FixedAssetDisposalPostingService(_db)
                .PostDisposalAsync(asset, -1m, new DateOnly(2026, 1, 15), "admin"));
    }


    /// <summary>
    /// <b>المقياس الذي لا يُغني عنه <c>IsBalanced</c>.</b> الفرق بين طرفَي القيد يساوي
    /// <c>AccumDep + BookValue − Cost</c> — وهو صفرٌ <b>متطابق</b> بحكم تعريف <c>BookValue</c>،
    /// فيمرّ ولو كانت كل القيم خاطئة. الدليل الحقيقيّ أن الأصل <b>خرج من الأستاذ</b>:
    /// حركة حسابَي التكلفة والمجمّع الصافية تعود صفراً بعد الاستبعاد.
    /// </summary>
    [Fact]
    public async Task AfterDisposal_AssetAndAccumulatedDepreciationNetToZeroInLedger()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 30);

        // قيد الاقتناء والإهلاك المتراكم يُحاكَيان بقيدٍ افتتاحيّ حتى يكون للأستاذ رصيدٌ يُختبَر
        var opening = JournalEntry.CreateDraft(_companyId, new DateOnly(2024, 1, 1), "اقتناء وإهلاك متراكم");
        opening.AddLine(await AccountAsync("1203"), 120_000m, 0m, "تكلفة الأصل");
        opening.AddLine(await AccountAsync("1207"), 0m, 30_000m, "مجمّع الإهلاك");
        opening.AddLine(await AccountAsync("3101"), 0m, 90_000m, "رأس المال");
        await new JournalPostingService(_db).PostAsync(opening);

        await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 70_000m, new DateOnly(2026, 1, 15), "admin");

        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2000, 1, 1), new DateOnly(2099, 12, 31));

        decimal Net(string code)
        {
            var r = rows.FirstOrDefault(x => x.AccountCode == code);
            return r is null ? 0m : r.TotalDebit - r.TotalCredit;
        }

        Assert.Equal(0m, Net("1203"));   // التكلفة خرجت من الدفاتر
        Assert.Equal(0m, Net("1207"));   // والمجمّع أُقفل معها
        Assert.Equal(20_000m, Net("5215"));   // 90,000 دفترية − 70,000 عائد
    }

    /// <summary>
    /// عائدٌ بثلاث منازل عشرية يُرحَّل. <c>NumericInput</c> يقبله، وكان سطر النقدية يأخذه خاماً
    /// بينما الربح/الخسارة مُقرَّب — ففرقٌ بـ0.005 يرفضه <c>Post</c> برسالة تلوم القيد لا المُدخَل.
    /// انحدارٌ يفتحه فتحُ البيع المبكّر تحديداً: ثمنُ الخردة مستدير، وثمنُ التفاوض ليس كذلك.
    /// </summary>
    [Fact]
    public async Task ProceedsWithSubHalalaPrecision_StillPosts()
    {
        var asset = await CreateVehicleAsync(monthsDepreciated: 0);

        var entry = await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 100_000.005m, new DateOnly(2026, 1, 15), "admin");

        Assert.True(entry.IsBalanced);
        // ‏(CEEF-232 · ض-٢) ‏١٠٠٬٠٠٠٫٠١ مقابلاً + ١٥٬٠٠٠٫٠٠ ضريبةً — **والتقريبُ يقع على
        // ‏الضريبة كذلك**: ١٥٠٠٠٫٠٠١٥ تُقرَّب قبل أن تُجمع، فلا يدخل القيدَ كسرُ هللة.
        Assert.Equal(115_000.01m, (await AccountTotalsAsync(entry.Id, "1101")).Debit);
        Assert.Equal(19_999.99m, (await AccountTotalsAsync(entry.Id, "5215")).Debit);
    }

    private async Task<Account> AccountAsync(string code) =>
        await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code);

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
