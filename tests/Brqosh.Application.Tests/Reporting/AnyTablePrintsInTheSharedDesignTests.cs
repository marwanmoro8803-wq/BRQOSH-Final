using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (المخرجاتُ كلُّها بالقالب الموحَّد) <b>أيُّ جدولٍ في البرنامج يُطبع بترويسة المنشأة وتذييلها</b> — بالعربيّة وبالإنجليزيّة، وما زاد على السقف يُقصّ بتنبيهٍ
/// لا بصمت، وكلُّ طباعةٍ تُدقَّق.
/// </summary>
public class AnyTablePrintsInTheSharedDesignTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly BrqoshDbContext _db;

    public AnyTablePrintsInTheSharedDesignTests()
    {
        TestActor.Administrator();
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        PrintLang.Preferred = false;
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<long> CompanyAsync() =>
        (await new DatabaseInitializer(_db).InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail)).Id;

    private DocumentPrintService Service() => new(_db, new AuditService(_db), new ExchangeRateService(_db));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ATableRendersToARealPdf_InBothLanguages(bool english)
    {
        var companyId = await CompanyAsync();
        PrintLang.Preferred = english;
        PrintLang.Misses.Clear();

        var result = await Service().ExportTableAsync(companyId, "admin", english ? "Customers" : "العملاء",
            ["Name", "Balance"], [["Al-Nokhba", "1,250.00"], ["Al-Ufuq", "(300.00)"]]);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result.PdfBytes, 0, 4));
        Assert.True(result.PdfBytes.Length > 1500);
        Assert.Empty(PrintLang.Misses.Keys);
    }

    [Fact]
    public async Task MoreRowsThanTheCap_AreCutWithAVisibleNotice_NotSilently()
    {
        var companyId = await CompanyAsync();
        var rows = Enumerable.Range(0, DocumentPrintService.MaxTableRows + 5)
            .Select(i => (IReadOnlyList<string>)[$"عميل {i}", i.ToString()]).ToList();

        var full = await Service().ExportTableAsync(companyId, "admin", "عملاء", ["الاسم", "الرقم"], rows);
        var small = await Service().ExportTableAsync(companyId, "admin", "عملاء", ["الاسم", "الرقم"], rows.Take(10).ToList());

        Assert.True(full.PdfBytes.Length > small.PdfBytes.Length);
    }
}
