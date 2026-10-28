using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// ‏(Phase 7) ‏<b>ملفُّ حماية الأجور بتخطيط SIF</b>: ترويسةٌ بوسوم المواصفة، وسطرٌ لكلّ موظّف تُحفظ فيه المتطابقة،
/// ومجموعُ الصوافي في [32A-AMT]، وما لا يُصدَّر يُسمّى.
/// </summary>
public class TheWpsSifFileFollowsTheMudadLayoutTests : IDisposable
{
    private const string RajhiIban = "SA0380000000608010167519";
    private static readonly WpsSifEstablishment Establishment = new("RJHI", "1234567890", RajhiIban, "1-2345678");

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private PayrollRun _run = null!;

    public TheWpsSifFileFollowsTheMudadLayoutTests()
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

    [Fact]
    public async Task TheFile_HasTheSpecHeader_AndEveryRowBalances()
    {
        await PostRunAsync(("أحمد", "1011111111", RajhiIban), ("سالم", "2022222222", RajhiIban));

        var result = await new WpsSifFileBuilder(_db, new FixedClock()).BuildAsync(_companyId, _run.Id, Establishment);

        Assert.Empty(result.Refusals);
        Assert.Equal(2, result.Records);
        Assert.Equal($"WPS_1-2345678_{_run.PeriodCode}.csv", result.FileName);

        var lines = result.Content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(5, lines.Length);
        Assert.Equal(WpsSifFileBuilder.HeaderTags, lines[0]);
        Assert.Equal(WpsSifFileBuilder.ContentTags, lines[2]);

        var header = lines[1].Split(',');
        Assert.Equal(10, header.Length);
        Assert.Equal(["RJHI", "1234567890", RajhiIban, "SAR", "20260628"], header[..5]);
        Assert.Equal(_run.TotalNet.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), header[5]);
        Assert.Equal($"BRQ{_run.Id}T20260628090000", header[7]);
        Assert.Equal("1-2345678", header[9]);

        var sum = 0m;
        foreach (var row in lines[3..])
        {
            var f = row.Split(',');
            Assert.Equal(14, f.Length);
            Assert.Equal(RajhiIban, f[1]);
            Assert.Equal("RJHI", f[3]);
            Assert.Equal($"SALARY {_run.PeriodCode}", f[4]);
            var net = decimal.Parse(f[0], System.Globalization.CultureInfo.InvariantCulture);
            decimal D(int i) => decimal.Parse(f[i], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(net, D(6) + D(7) + D(8) - D(9));
            Assert.True(D(9) > 0m, "GOSI employee share is a deduction");
            Assert.Matches("^[12][0-9]{9}$", f[10]);
            Assert.Equal(["", "", ""], f[11..]);
            sum += net;
        }
        Assert.Equal(sum, decimal.Parse(header[5], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AnEmployeeWithABadIban_IsNamed_AndLeftOutOfTheTotal()
    {
        await PostRunAsync(("أحمد", "1011111111", RajhiIban), ("خالد", "1033333333", "SA0380000000608010167518"),
            ("بلا آيبان", "1044444444", null));

        var result = await new WpsSifFileBuilder(_db).BuildAsync(_companyId, _run.Id, Establishment);

        Assert.Equal(1, result.Records);
        Assert.Equal(2, result.Refusals.Count);
        Assert.Contains(result.Refusals, r => r.Contains("خالد"));
        Assert.Contains(result.Refusals, r => r.Contains("بلا آيبان"));
        Assert.Equal(_run.Lines.First(l => l.EmployeeName == "أحمد").NetPay, result.TotalNet);
    }

    [Fact]
    public async Task MissingEstablishmentSettings_ProduceNoFile_AndNameEveryField()
    {
        await PostRunAsync(("أحمد", "1011111111", RajhiIban));

        var result = await new WpsSifFileBuilder(_db).BuildAsync(_companyId, _run.Id,
            WpsSifEstablishment.FromConfiguration(_ => null));

        Assert.Equal(string.Empty, result.Content);
        Assert.Equal(4, result.Refusals.Count);
        foreach (var tag in new[] { "[DEST-ID]", "[ESTB-ID]", "[BANK-ACC]", "[MOL-ESTBID]" })
            Assert.Contains(result.Refusals, r => r.StartsWith(tag, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADraftRun_IsRefused()
    {
        await SetupAsync();
        var draft = PayrollRun.CreateDraft(_companyId, 2026, 7, new DateOnly(2026, 7, 28));
        _db.PayrollRuns.Add(draft);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccountingException>(
            () => new WpsSifFileBuilder(_db).BuildAsync(_companyId, draft.Id, Establishment));
    }

    [Theory]
    [InlineData(RajhiIban, true)]
    [InlineData("SA0380000000608010167518", false)]
    [InlineData("SA03 8000 0000 6080 1016 7519", false)]
    [InlineData("GB82WEST12345698765432", false)]
    public void SaudiIbanCheck(string iban, bool valid) => Assert.Equal(valid, WpsSifFileBuilder.IsValidSaudiIban(iban));

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db).InitializeAsync("منشأة حماية الأجور", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }

    private async Task PostRunAsync(params (string Name, string NationalId, string? Iban)[] people)
    {
        await SetupAsync();
        var policy = await _db.GosiPolicies.SingleAsync(g => g.CompanyId == _companyId);
        _run = PayrollRun.CreateDraft(_companyId, 2026, 6, new DateOnly(2026, 6, 28));
        var i = 0;
        foreach (var (name, nationalId, iban) in people)
        {
            var employee = Employee.Create(_companyId, $"W-{++i}", name, nationalId,
                EmployeeNationality.Saudi, "محاسب", new DateOnly(2024, 1, 1));
            employee.SetPay(8_000m + i * 1_000m, 2_000m, 500m);
            employee.SetIban(iban);
            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();
            _run.AddEmployee(employee, policy);
        }
        await new PayrollPostingService(_db).PostAsync(_run, "admin");
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 6, 28, 9, 0, 0, TimeSpan.Zero);
    }
}
