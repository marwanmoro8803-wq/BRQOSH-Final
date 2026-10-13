using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Setup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Setup;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٥ · <c>commercial-readiness#15</c> · <c>data-upgrade-performance#8</c>) <b>الأصنافُ ومخزونُها
/// الافتتاحيّ، وملفُّ CSV كما تُصدّره الأنظمةُ فعلاً.</b>
///
/// <para>‏<b>وكان في ح-١٣٣ خطأُ تصميمٍ منّي</b>: الميزانُ يرفض 1107 ويُحيل إلى حركة المخزون — <b>فميزانٌ حقيقيٌّ فيه مخزونٌ لا
/// يتوازن بعد حذف سطره، والمستخدمُ عالق</b>. والمخزونُ كالذمم: دفترٌ مساعدٌ يُبنى من الحركات. فسطرُه يقف في 3107 كذلك،
/// <b>وحركةُ المخزون الافتتاحيّةُ بعد الميزان تُدائن 3107 لا الأرباحَ المبقاة</b> — فالميزانُ أقام حقوقَ الملكية كاملةً، وما بعده
/// تفصيلٌ لرقمه لا ملكيّةٌ جديدة.</para>
///
/// <para>‏<b>والمُحلِّلُ كان يقسم بالفاصلة وحدَها</b>: «شركة النور، فرع جدة» اسمٌ واحدٌ في كلّ نظامٍ يُصدّر CSV — مُقتبَساً — وكان
/// يصير عمودين.</para>
/// </summary>
public class GoLiveStockAndCsvTests : IDisposable
{
    private static readonly DateOnly GoLive = new(2026, 1, 1);
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _company;

    public GoLiveStockAndCsvTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestActor.Administrator();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private const string TrialBalanceWithStock = "1101,40000,0\n1107,30000,0\n3101,0,70000";

    private const string Products = """
        Code,NameAr,Unit,Type,SalePrice
        P-1,"قماش، قطني",متر,Stockable,25.50
        P-2,خيط,بكرة,Stockable,4
        S-1,خياطة,خدمة,Service,60
        """;

    private const string OpeningStock = "ProductCode,Quantity,UnitCost\nP-1,1000,24\nP-2,1500,4";

    /// <summary>
    /// ‏<b>القياسُ الحاكم:</b> ميزانٌ فيه مخزون ⟵ أصناف ⟵ مخزونُها الافتتاحيّ ⟵ 3107 صفر، و1107 رصيدُ الميزان، وهويّةُ المخزون صادقة.
    /// </summary>
    [Fact]
    public async Task AStockedTrialBalance_IsDetailedByTheOpeningStock_AndTheClearingEmpties()
    {
        await StartAsync();
        Assert.Equal(3, (await Service().ImportOpeningTrialBalanceCsvAsync(_company, GoLive, TrialBalanceWithStock)).Imported);
        Assert.Equal(30_000m, await BalanceAsync(CsvDataMigrationService.OpeningClearingCode));

        Assert.Equal(3, (await Service().ImportProductsCsvAsync(_company, Products)).Imported);
        Assert.Equal(2, (await Service().ImportOpeningStockCsvAsync(_company, OpeningStock)).Imported);

        Assert.Equal(0m, await BalanceAsync(CsvDataMigrationService.OpeningClearingCode));
        Assert.Equal(30_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.Inventory));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.RetainedEarnings));
        await LedgerIdentities.AssertAllAsync(_db, _company);
    }

    /// <summary>‏<b>والأصنافُ بأنواعها وأسعارها</b>، والمكرَّرُ بالكود يُتخطّى بسببه.</summary>
    [Fact]
    public async Task Products_AreImportedWithTheirTypeAndPrice()
    {
        await StartAsync();

        var result = await Service().ImportProductsCsvAsync(_company, $"{Products}\nP-1,مكرَّر,متر,Stockable,1");

        Assert.Equal(3, result.Imported);
        Assert.Contains(result.Details, d => !d.Success && d.Error!.Contains("موجود", StringComparison.Ordinal));
        var cloth = await _db.Products.SingleAsync(p => p.CompanyId == _company && p.Code == "P-1");
        Assert.Equal("قماش، قطني", cloth.NameAr);
        Assert.Equal(25.50m, cloth.SalePrice);
        Assert.Equal(ProductType.Service, (await _db.Products.SingleAsync(p => p.Code == "S-1")).Type);
    }

    /// <summary>‏<b>والمخزونُ الافتتاحيُّ كلُّه أو لا شيء</b> — صنفٌ مجهول، أو خدمة، أو كمّيّةٌ غيرُ موجبة تُسقط الملفّ.</summary>
    [Theory]
    [InlineData("P-9,10,5", "غير موجود")]
    [InlineData("S-1,10,5", "خدمة")]
    [InlineData("P-2,0,5", "موجبة")]
    [InlineData("P-2,10,1,5", "أعمدة مطلوبة")]
    [InlineData("P-2,10,\"1,5\"", "مبلغٌ غير مقبول")]
    public async Task ABadStockLine_SinksTheWholeFile(string badLine, string reason)
    {
        await StartAsync();
        await Service().ImportOpeningTrialBalanceCsvAsync(_company, GoLive, TrialBalanceWithStock);
        await Service().ImportProductsCsvAsync(_company, Products);

        var result = await Service().ImportOpeningStockCsvAsync(_company, $"{OpeningStock}\n{badLine}");

        Assert.Equal(0, result.Imported);
        Assert.Contains(reason, Assert.Single(result.Details).Error, StringComparison.Ordinal);
        Assert.False(await _db.StockMovements.AnyAsync(m => m.CompanyId == _company));
    }

    /// <summary>‏<b>وقبل الميزان لا مخزونَ افتتاحيّ</b> — فلا تاريخَ بدءٍ يُعرف ولا رصيدَ في 3107 يُفصَّل.</summary>
    [Fact]
    public async Task BeforeTheTrialBalance_OpeningStockIsRefused()
    {
        await StartAsync();
        await Service().ImportProductsCsvAsync(_company, Products);

        var result = await Service().ImportOpeningStockCsvAsync(_company, OpeningStock);

        Assert.Equal(0, result.Imported);
        Assert.Contains("الميزانَ الافتتاحيَّ أوّلاً", Assert.Single(result.Details).Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// ‏<b>والحقلُ المُقتبَسُ حقلٌ واحد</b> — بفاصلته وبعلامة التنصيص المضاعَفة داخله — كما تُصدّره الأنظمة.
    /// </summary>
    [Theory]
    [InlineData("\"شركة النور، فرع جدة\",300000000000003", "شركة النور، فرع جدة")]
    [InlineData("\"شركة النور, فرع جدة\",300000000000003", "شركة النور, فرع جدة")]
    [InlineData("\"مؤسسة \"\"الأمانة\"\"\",", "مؤسسة \"الأمانة\"")]
    public async Task AQuotedField_IsOneField(string line, string name)
    {
        await StartAsync();

        var result = await Service().ImportCustomersCsvAsync(_company, $"NameAr,VatNumber\n{line}");

        Assert.Equal(1, result.Imported);
        Assert.True(await _db.Customers.AnyAsync(c => c.CompanyId == _company && c.NameAr == name));
    }

    /// <summary>‏<b>والقاسمُ نفسُه في الطرفين</b>: كشفُ البنك يقرأ الحقلَ المُقتبَسَ كما يقرؤه الترحيل.</summary>
    [Fact]
    public void OneSplitter_ReadsQuotedFieldsTheSameEverywhere() =>
        Assert.Equal(["a", "b, c", "d \"e\""], CsvRow.Split("a,\"b, c\",\"d \"\"e\"\"\""));

    private async Task StartAsync() =>
        _company = (await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الانتقال", "300000000000003", BusinessProfile.Retail)).Id;

    private CsvDataMigrationService Service() => new(_db);

    private async Task<decimal> BalanceAsync(string code)
    {
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(_company, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));
        var row = rows.FirstOrDefault(r => r.AccountCode == code);
        return row is null ? 0m : row.TotalDebit - row.TotalCredit;
    }
}
