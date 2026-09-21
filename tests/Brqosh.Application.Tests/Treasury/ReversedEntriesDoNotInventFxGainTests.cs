using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٢٥) <b>عكسُ قيدٍ على بنكٍ أجنبيٍّ كان يخلق ربحَ صرفٍ لم يقع.</b>
///
/// <para><c>CreateReversal</c> <b>يضع الأصلَ في حالة <c>Reversed</c></b> ويُرحّل العكسيَّ
/// <c>Posted</c>. وكان حسابُ الرصيد الدفتريّ في <c>FxRevaluationService</c> يُرشِّح
/// <c>Status == Posted</c> وحدَه — <b>فيخرج الأصلُ ويبقى عكسُه</b>.</para>
///
/// <para><b>فالرصيدُ الدفتريُّ يصير مقلوبَ القيد الملغى</b>: لا صفراً ولا صحيحاً. ثمّ
/// يُقارَن بالرصيد الأجنبيّ مضروباً في السعر، <b>والفرقُ يُقيَّد ربحاً أو خسارةَ صرفٍ غيرَ
/// محقَّقة</b> — <b>مبلغٌ في قائمة الدخل عن واقعةٍ لم تقع</b>.</para>
///
/// <para><b>والعلاجُ كان في المستودع لهذا السبب بعينه</b>: <c>WhereReportable</c> تضمّ
/// <c>Posted</c> و<c>Reversed</c> معاً، وتعليقُها يذكر <c>ACC-001</c>. <b>وثمانيةُ قُرّاءٍ
/// يستعملونها — وهذا لم يكن منهم.</b></para>
/// </summary>
public class ReversedEntriesDoNotInventFxGainTests : IDisposable
{
    private const string Usd = "USD";

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _bankId;
    private long _bankGlId;

    public ReversedEntriesDoNotInventFxGainTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> قيدٌ بألفِ دولارٍ ثمّ <b>عكسُه</b>، والرصيدُ الأجنبيُّ صفرٌ
    /// فعلاً ⟵ <b>لا قيدَ إعادةِ تقييمٍ أصلاً</b>، <b>ولا ريالَ في حسابَي فروق العملة</b>.
    ///
    /// <para><b>وكان يُقيَّد فرقٌ بكامل مبلغ العكس</b> — من عدم.</para>
    /// </summary>
    [Fact]
    public async Task AReversedEntry_LeavesNothingToRevalue()
    {
        await SetupAsync();

        var original = await PostBankEntryAsync(amountSar: 3_750m);
        await new JournalReversalService(_db).ReverseAsync(
            _companyId, original.Id, new DateOnly(2026, 8, 10), "admin");

        // ‏الرصيدُ الأجنبيُّ صفرٌ — فالحسابُ فارغٌ حقّاً بعد العكس.
        var entry = await new FxRevaluationService(_db).RevalueAsync(
            _companyId, _bankId, new DateOnly(2026, 8, 31), newRate: 3.80m, "admin", foreignBalance: 0.0001m);

        Assert.Equal(0m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.UnrealizedFxGain));
        Assert.Equal(0m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.UnrealizedFxLoss));
        Assert.True(entry is null || entry.Lines.Count == 0,
            "قُيّد فرقُ صرفٍ بعد عكسٍ ترك الحسابَ فارغاً — ربحٌ من عدم.");
    }

    /// <summary>
    /// <b>والرصيدُ الدفتريُّ بعد العكس صفرٌ</b> — وهو السببُ المباشر. يُقاس بميزان
    /// المراجعة، <b>وهو يستعمل <c>WhereReportable</c> أصلاً</b>، فيُظهر الصوابَ الذي
    /// كان حسابُ التقييم يخالفه.
    /// </summary>
    [Fact]
    public async Task TheLedgerItself_ShowsZero_AfterAReversal()
    {
        await SetupAsync();

        var original = await PostBankEntryAsync(amountSar: 3_750m);
        await new JournalReversalService(_db).ReverseAsync(
            _companyId, original.Id, new DateOnly(2026, 8, 10), "admin");

        Assert.Equal(0m, await LedgerAsync(await BankCodeAsync()));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — بلا عكسٍ يعمل التقييمُ كما كان:</b> ألفُ دولارٍ قُيّدت بـ٣٫٧٥
    /// وتُقوَّم بـ٣٫٨٠ ⟵ <b>ربحُ خمسين ريالاً</b>، وهو ربحٌ حقيقيّ.
    ///
    /// <para><b>ولولاه لجاز أن يكون «الإصلاح» تعطيلاً للتقييم كلِّه.</b></para>
    /// </summary>
    [Fact]
    public async Task WithoutAnyReversal_ARealFxGainIsStillPosted()
    {
        await SetupAsync();
        await PostBankEntryAsync(amountSar: 3_750m);

        await new FxRevaluationService(_db).RevalueAsync(
            _companyId, _bankId, new DateOnly(2026, 8, 31), newRate: 3.80m, "admin", foreignBalance: 1_000m);

        Assert.Equal(-50m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.UnrealizedFxGain));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — العكسُ الجزئيّ يترك ما بقي:</b> قيدان، يُعكس أحدُهما ⟵
    /// <b>الرصيدُ الدفتريُّ هو الباقي وحدَه</b>، فلا ربحَ ولا خسارةَ عند السعر نفسِه.
    /// </summary>
    [Fact]
    public async Task AfterReversingOneOfTwoEntries_OnlyTheSurvivorCounts()
    {
        await SetupAsync();

        var first = await PostBankEntryAsync(amountSar: 3_750m);
        await PostBankEntryAsync(amountSar: 3_750m);
        await new JournalReversalService(_db).ReverseAsync(
            _companyId, first.Id, new DateOnly(2026, 8, 10), "admin");

        // ‏الباقي ٣٬٧٥٠ ريالاً = ١٬٠٠٠ دولارٍ بسعر ٣٫٧٥ — فلا فرقَ عند السعر نفسِه.
        var entry = await new FxRevaluationService(_db).RevalueAsync(
            _companyId, _bankId, new DateOnly(2026, 8, 31), newRate: 3.75m, "admin", foreignBalance: 1_000m);

        Assert.Null(entry);
        Assert.Equal(0m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.UnrealizedFxGain));
        Assert.Equal(0m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.UnrealizedFxLoss));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الصرف", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;

        // ‏حسابٌ بنكيٌّ **بدفترِ أستاذٍ خاصٍّ به** — فلا تُلوِّث حركاتُ غيره القياس.
        var bankGlId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code == "1102" && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();

        var bank = BankAccount.Create(
            _companyId, "FX-BNK", "حساب الدولار", "بنك", "123",
            "SA0000000000000000000001", bankGlId);
        bank.SetCurrency(Usd);
        _db.BankAccounts.Add(bank);
        await _db.SaveChangesAsync();

        _bankId = bank.Id;
        _bankGlId = bank.GlAccountId;

        await new FxRevaluationService(_db).EnsureFxAccountsAsync(_companyId);
    }

    /// <summary>قيدٌ يُدخل مبلغاً بالريال إلى حساب البنك — مقابلَ رأس المال.</summary>
    private async Task<JournalEntry> PostBankEntryAsync(decimal amountSar)
    {
        var bankGl = await _db.Accounts.SingleAsync(a => a.Id == _bankGlId);
        var capital = await _db.Accounts.SingleAsync(
            a => a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.RetainedEarnings);

        var entry = JournalEntry.CreateDraft(
            // ‏**بلا مستندٍ مصدر** — والحارسُ قائمٌ: قيدٌ لمستندٍ يُصحَّح من مصدره
            // ‏لا بالعكس المباشر. **والعكسُ المباشر إنّما يقع على قيدٍ يدويّ**، وهو المقيس هنا.
            _companyId, new DateOnly(2026, 8, 1), "إيداع بالدولار");
        entry.AddLine(bankGl, amountSar, 0m, "إيداع");
        entry.AddLine(capital, 0m, amountSar, "مقابل");

        return await new JournalPostingService(_db).PostAsync(entry);
    }

    private async Task<string> BankCodeAsync() =>
        (await _db.Accounts.SingleAsync(a => a.Id == _bankGlId)).Code;

    /// <summary>رصيدٌ مُوقَّع: موجبٌ مدين، سالبٌ دائن.</summary>
    private async Task<decimal> LedgerAsync(string accountCode)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
