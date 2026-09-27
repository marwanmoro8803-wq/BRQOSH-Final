using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.RealEstate;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.RealEstate;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.RealEstate;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٩ · <c>sector-modules#7</c>) <b>الإيجارُ المُفوتَرُ مقدَّماً يُكتسب على مدّته — وكان إيراداً كلُّه
/// يومَ الفاتورة.</b>
///
/// <para><b>والأرقامُ تُفرّق بين الطرق:</b> ربعٌ بـ٣٬٠٠٠ ⟵ ألفٌ لكلّ شهر (لا ٣٬٠٠٠ في يناير)؛ وعقدٌ سنويٌّ بـ١٢٬٠٠٠ يبدأ في
/// يوليو ⟵ ٦٬٠٠٠ مكتسبةٌ و٦٬٠٠٠ مقبوضةٌ مقدّماً في آخر السنة (لا ١٢٬٠٠٠ إيراداً)؛ وعقدٌ ينتهي في منتصف الربع ⟵ يُكتسب كلُّه
/// بنهايته (لا يبقى ثلثٌ معلّقاً بعدها).</para>
///
/// <para><b>والضريبةُ لا تتبع الاعتراف</b> — نقطتُها الفوترة، فتبقى ٤٥٠ في يوم الفاتورة.</para>
/// </summary>
public class RentBilledInAdvanceIsEarnedOverItsPeriodTests : IDisposable
{
    private const string Revenue = SaudiChartOfAccounts.SystemCodes.SalesRevenue;
    private const string Unearned = SaudiChartOfAccounts.SystemCodes.UnearnedRent;
    private const string VatOutput = SaudiChartOfAccounts.SystemCodes.VatOutput;

    private readonly BrqoshDbContext _db;
    private long _companyId;

    public RentBilledInAdvanceIsEarnedOverItsPeriodTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary><b>القياسُ الحاكم:</b> ربعٌ مُفوتَرٌ مقدَّماً ⟵ ألفٌ لكلّ شهر، والباقي مقبوضٌ مقدّماً حتى يُكتسب.</summary>
    [Fact]
    public async Task AQuarterBilledInAdvance_IsEarnedMonthByMonth()
    {
        await BillAsync(RecurrenceFrequency.Quarterly, 3_000m, new(2026, 1, 1), new(2026, 12, 31), asOf: new(2026, 1, 1));

        Assert.Equal(1_000m, await CreditBalanceAsync(Revenue, new(2026, 1, 31)));
        Assert.Equal(2_000m, await CreditBalanceAsync(Unearned, new(2026, 1, 31)));
        Assert.Equal(2_000m, await CreditBalanceAsync(Revenue, new(2026, 2, 28)));
        Assert.Equal(3_000m, await CreditBalanceAsync(Revenue, new(2026, 3, 31)));
        Assert.Equal(0m, await CreditBalanceAsync(Unearned, new(2026, 3, 31)));

        Assert.Equal(450m, await CreditBalanceAsync(VatOutput, new(2026, 1, 1)));
    }

    /// <summary><b>وعقدٌ سنويٌّ من يوليو</b> ⟵ نصفُه مكتسبٌ في آخر السنة ونصفُه مقبوضٌ مقدّماً — ويُكتسب كلُّه بنهاية العقد.</summary>
    [Fact]
    public async Task AnAnnualLeaseFromJuly_LeavesHalfItsRentUnearnedAtYearEnd()
    {
        await BillAsync(RecurrenceFrequency.Annual, 12_000m, new(2026, 7, 1), new(2027, 6, 30), asOf: new(2026, 7, 1));

        Assert.Equal(6_000m, await CreditBalanceAsync(Revenue, new(2026, 12, 31)));
        Assert.Equal(6_000m, await CreditBalanceAsync(Unearned, new(2026, 12, 31)));
        Assert.Equal(12_000m, await CreditBalanceAsync(Revenue, new(2027, 6, 30)));
        Assert.Equal(0m, await CreditBalanceAsync(Unearned, new(2027, 6, 30)));
    }

    /// <summary><b>وعقدٌ ينتهي في منتصف الربع</b> ⟵ يُكتسب كلُّه بنهايته، شهرين لا ثلاثة — فلا يبقى شيءٌ معلّقاً بعد العقد.</summary>
    [Fact]
    public async Task ALeaseEndingMidQuarter_IsFullyEarnedByItsEnd()
    {
        await BillAsync(RecurrenceFrequency.Quarterly, 3_000m, new(2026, 1, 1), new(2026, 2, 28), asOf: new(2026, 1, 1));

        Assert.Equal(1_500m, await CreditBalanceAsync(Revenue, new(2026, 1, 31)));
        Assert.Equal(3_000m, await CreditBalanceAsync(Revenue, new(2026, 2, 28)));
        Assert.Equal(0m, await CreditBalanceAsync(Unearned, new(2026, 2, 28)));
    }

    /// <summary><b>شاهدُ ضبط:</b> الإيجارُ الشهريُّ يُكتسب في شهره — فلا تأجيلَ ولا قيدَ زائد.</summary>
    [Fact]
    public async Task AMonthlyLease_IsEarnedWhenBilled_WithoutDeferral()
    {
        await BillAsync(RecurrenceFrequency.Monthly, 1_000m, new(2026, 1, 1), new(2026, 12, 31), asOf: new(2026, 1, 1));

        Assert.Equal(1_000m, await CreditBalanceAsync(Revenue, new(2026, 1, 31)));
        Assert.False(await _db.JournalEntries.AnyAsync(e =>
            e.SourceDocument != null && e.SourceDocument.StartsWith("RentDeferral:")));
    }

    /// <summary>
    /// <b>وإلغاءُ الفاتورة يعكس التأجيلَ وكلَّ اعتراف</b> — فلا إيرادَ ولا مقبوضَ مقدّماً بعده، <b>والتاريخُ قبله باقٍ</b>: يناير
    /// اكتسب ألفاً ولا يُمحى.
    /// </summary>
    [Fact]
    public async Task CancellingTheInvoice_UndoesTheDeferralAndEveryRecognition()
    {
        var invoiceId = await BillAsync(RecurrenceFrequency.Quarterly, 3_000m, new(2026, 1, 1), new(2026, 12, 31),
            asOf: new(2026, 1, 1));

        await new SalesInvoiceCancellationService(_db).CancelAsync(_companyId, invoiceId, new(2026, 2, 15), "admin");

        Assert.Equal(1_000m, await CreditBalanceAsync(Revenue, new(2026, 1, 31)));

        // ‏**وفي منتصف الربع يُقاس الفرق** — آخرَه يتقابل التأجيلُ والاعترافاتُ من تلقاء نفسِها فلا يُرى شيء:
        // ‏بلا سحبِها يبقى فبرايرُ −١٬٠٠٠ إيراداً و١٬٠٠٠ مقبوضاً مقدّماً عن فاتورةٍ أُلغيت.
        Assert.Equal(0m, await CreditBalanceAsync(Revenue, new(2026, 2, 28)));
        Assert.Equal(0m, await CreditBalanceAsync(Unearned, new(2026, 2, 28)));
        Assert.Equal(0m, await CreditBalanceAsync(Revenue, new(2026, 3, 31)));
        Assert.Equal(0m, await CreditBalanceAsync(Unearned, new(2026, 3, 31)));
    }

    /// <summary>
    /// <b>وإنهاءُ العقد في منتصف الربع</b> يوقف الاعتراف — يناير وفبراير مكتسبان، <b>ومارسُ يبقى مقبوضاً مقدّماً مستحقّاً للمستأجر</b>.
    /// </summary>
    [Fact]
    public async Task TerminatingMidQuarter_StopsRecognising_AndLeavesTheUnearnedRentOwed()
    {
        await BillAsync(RecurrenceFrequency.Quarterly, 3_000m, new(2026, 1, 1), new(2026, 12, 31), asOf: new(2026, 1, 1));
        var lease = await _db.LeaseContracts.SingleAsync(l => l.CompanyId == _companyId);

        await new LeaseService(_db).TerminateAsync(_companyId, lease.Id, new(2026, 2, 10));

        Assert.Equal(2_000m, await CreditBalanceAsync(Revenue, new(2026, 3, 31)));
        Assert.Equal(1_000m, await CreditBalanceAsync(Unearned, new(2026, 3, 31)));
    }

    /// <summary><b>والشرائحُ مجموعُها الإيرادُ بالهللة</b> — ألفٌ على ثلاثة ⟵ ٣٣٣٫٣٣ و٣٣٣٫٣٣ و٣٣٣٫٣٤.</summary>
    [Fact]
    public void TheSlices_SumToTheRevenue_ToTheHalala()
    {
        Assert.Equal(new[] { 333.33m, 333.33m, 333.34m }, LeaseRentRecognition.Slices(1_000m, 3));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<long> BillAsync(RecurrenceFrequency frequency, decimal rentPerPeriod,
        DateOnly start, DateOnly end, DateOnly asOf)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة عقارية", "310123456700066", BusinessProfile.RealEstate);
        _companyId = company.Id;

        var tenant = Customer.Create(_companyId, "مستأجر", "311111111100003");
        _db.Customers.Add(tenant);
        var property = Property.Create(_companyId, "P-1", "برج الإيراد", PropertyKind.Building, "الرياض");
        _db.Properties.Add(property);
        await _db.SaveChangesAsync();

        var unit = PropertyUnit.Create(_companyId, property.Id, "101",
            areaSquareMeters: 100m, bedrooms: 2, listedAnnualRent: 12_000m);
        _db.PropertyUnits.Add(unit);
        await _db.SaveChangesAsync();

        var lease = LeaseContract.Create(_companyId, unit.Id, tenant.Id, "L-2026-079", start, end, rentPerPeriod, frequency);
        await new LeaseService(_db).ActivateAsync(lease);

        var result = await new RecurringInvoiceService(_db).GenerateDueAsync(_companyId, asOf);
        Assert.Equal(1, result.InvoicesCreated);

        return await _db.RecurringInvoiceRuns.Where(r => r.CompanyId == _companyId)
            .Select(r => r.SalesInvoiceId).SingleAsync();
    }

    /// <summary>رصيدُ الحساب الدائنُ حتى التاريخ — من كلّ قيدٍ غيرِ مسودّة، فالأصلُ المعكوسُ وعكسُه يتقابلان.</summary>
    private async Task<decimal> CreditBalanceAsync(string accountCode, DateOnly asOf) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft && e.EntryDate <= asOf)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == accountCode)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Credit - l.Debit);
}
