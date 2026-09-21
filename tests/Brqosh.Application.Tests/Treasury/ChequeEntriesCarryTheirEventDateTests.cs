using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٢) <b>قيودُ دورة الشيك كانت تحمل تاريخَ الشيك لا تاريخَ حدثها —
/// فكان الإيداعُ ينفذ من حارس الفترة المقفلة.</b>
///
/// <para><b>والخدمةُ تفحص الفترةَ بتاريخٍ لا يبلغ الدفترَ أبداً:</b>
/// <c>DepositAsync(…, depositDate)</c> تستدعي <c>EnsurePeriodOpenAsync</c> بـ<c>depositDate</c>،
/// ثمّ تبني القيدَ بـ<c>ChequeDate</c>. <b>وهذا الموضعُ لا يمرّ بـ<c>JournalPostingService.PostAsync</c></b>
/// — يكتب في <c>JournalEntries</c> رأساً — <b>فلا يبلغه <c>PostingOrchestrator</c> الذي يفحص
/// بـ<c>entry.EntryDate</c></b>.</para>
///
/// <para><b>وقِيس قبل الإصلاح</b>: شيكٌ بتاريخ <c>2026-04-05</c>، ثمّ أُقفلت فترةُ أبريل،
/// ثمّ أُودِع في <c>2026-07-10</c> ⟵ <b><c>ACCEPTED</c>، والقيدُ دخل <c>2026-04-05</c> وحالتُها
/// <c>Closed</c></b>. <b>فليس العطلُ تاريخاً خاطئاً فحسب، بل نفاذٌ من الضمان الذي بُني
/// لأجله ح-١.</b></para>
///
/// <para><b>وعلاجان لا واحد</b>: التاريخُ يُمرَّر (فيصحّ ما يُكتب)، <b>والحارسُ يُسأل عن
/// تاريخ القيد نفسِه عند كتابته</b> (فلا يعود الصنفُ إن افترق تاريخان ثانيةً).</para>
/// </summary>
public class ChequeEntriesCarryTheirEventDateTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _supplierId;
    private long _bankId;

    public ChequeEntriesCarryTheirEventDateTests()
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

    // ── النفاذُ من الحارس ───────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم — ولا يُقاس على رسالةٍ بل على الدفتر:</b> فترةُ الشيك مقفلة،
    /// والإيداعُ في فترةٍ مفتوحة ⟵ <b>لا قيدَ واحدٌ يقع في الفترة المقفلة</b>.
    /// </summary>
    [Fact]
    public async Task WithTheChequeMonthClosed_NoEntryLandsInIt()
    {
        await SetupAsync();
        var cheque = await ReceiveChequeAsync(11_500m);
        await ClosePeriodAsync(new DateOnly(2026, 4, 5));

        await new ChequePostingService(_db)
            .DepositAsync(_companyId, cheque.Id, new DateOnly(2026, 7, 10));

        Assert.Empty(await SettlementDatesAsync(inMonthOf: new DateOnly(2026, 4, 1)));
    }

    /// <summary>
    /// <b>والقيدُ يقع في الشهر الذي وقع فيه الحدثُ فعلاً</b> — وهو ما تبحث عنه مطابقةُ
    /// كشف البنك.
    /// </summary>
    [Fact]
    public async Task ADeposit_PostsOnTheDayItHappened()
    {
        await SetupAsync();
        var cheque = await ReceiveChequeAsync(11_500m);

        await new ChequePostingService(_db)
            .DepositAsync(_companyId, cheque.Id, new DateOnly(2026, 7, 10));

        Assert.Equal([new DateOnly(2026, 7, 10)], await SettlementDatesAsync());
    }

    /// <summary>
    /// <b>وحارسُ الفترة صار حيّاً على هذا المسار:</b> إيداعٌ <i>في</i> فترةٍ مقفلة يُرفض.
    ///
    /// <para><b>ولولا هذا لَقِيس «لا قيدَ في أبريل» ومرّ</b> — إذ الامتناعُ عن الكتابة في
    /// شهرٍ لا يُثبت أنّ أحداً ينظر. <b>فيُقاس المنعُ نفسُه.</b></para>
    /// </summary>
    [Fact]
    public async Task ADepositIntoAClosedPeriod_IsRefused()
    {
        await SetupAsync();
        var cheque = await ReceiveChequeAsync(11_500m);
        await ClosePeriodAsync(new DateOnly(2026, 7, 10));

        await Assert.ThrowsAnyAsync<Exception>(() => new ChequePostingService(_db)
            .DepositAsync(_companyId, cheque.Id, new DateOnly(2026, 7, 10)));
    }

    // ── بقيّةُ الدورة ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>والتحصيلُ والارتدادُ والإلغاءُ كذلك</b> — <b>فالعطلُ كان في خمسة مواضعَ لا
    /// في الإيداع وحدَه</b>، ولا يُغلق صنفٌ بإصلاح واحدٍ منه.
    /// </summary>
    [Fact]
    public async Task ClearingAndBouncing_EachPostOnTheirOwnDay()
    {
        await SetupAsync();

        var collected = await ReceiveChequeAsync(11_500m, "CHQ-A");
        await new ChequePostingService(_db)
            .ClearAsync(_companyId, collected.Id, new DateOnly(2026, 8, 3));
        Assert.Equal([new DateOnly(2026, 8, 3)], await SettlementDatesAsync("CHQ-A"));

        var bounced = await ReceiveChequeAsync(2_300m, "CHQ-B");
        await new ChequePostingService(_db)
            .BounceReceivedAsync(_companyId, bounced.Id, new DateOnly(2026, 9, 4));
        Assert.Equal([new DateOnly(2026, 9, 4)], await SettlementDatesAsync("CHQ-B"));
    }

    /// <summary>
    /// <b>والشيكُ الصادرُ يُلغى بتاريخ إلغائه.</b>
    /// </summary>
    [Fact]
    public async Task CancellingAnIssuedCheque_PostsOnTheDayItWasCancelled()
    {
        await SetupAsync();
        var cheque = Cheque.CreateIssued(_companyId, "CHQ-OUT", new DateOnly(2026, 4, 6),
            4_000m, "شيك مورّد", _bankId, _supplierId, dueDate: new DateOnly(2026, 4, 30));
        await new ChequePostingService(_db).RegisterIssuedAsync(cheque);

        await new ChequePostingService(_db)
            .CancelIssuedAsync(_companyId, cheque.Id, new DateOnly(2026, 10, 5));

        Assert.Equal([new DateOnly(2026, 10, 5)], await SettlementDatesAsync("CHQ-OUT"));
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — هويّةُ هـ-٢٣ لم تنكسر:</b> رصيدُ العميل بعد ارتداد الشيك
    /// يعود إلى ما كان قبل استلامه بالضبط. <b>فالإصلاحُ مسّ التاريخَ لا المبالغ.</b>
    /// </summary>
    [Fact]
    public async Task TheBounceIdentity_StillHolds()
    {
        await SetupAsync();
        await InvoiceAsync(10_000m);
        var before = await BalanceAsync();

        var cheque = await ReceiveChequeAsync(11_500m);
        Assert.Equal(0m, await BalanceAsync());

        await new ChequePostingService(_db)
            .BounceReceivedAsync(_companyId, cheque.Id, new DateOnly(2026, 9, 20));

        Assert.Equal(before, await BalanceAsync());
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — التسجيلُ لم يتغيّر:</b> قيدُ المحفظة يبقى على تاريخ الشيك،
    /// <b>وهو المسارُ الذي كان متّسقاً أصلاً</b> (الحارسُ والقيدُ على تاريخٍ واحد).
    ///
    /// <para><b>ولولاه لجاز أن يكون الإصلاحُ قد ساق المسارات سوقاً واحداً</b> — وتاريخُ
    /// استلامِ الشيك حقلٌ لا وجودَ له، <b>فتغييرُه اختراعُ بيانات</b>.</para>
    /// </summary>
    [Fact]
    public async Task RegisteringACheque_StillPostsAtTheChequeDate()
    {
        await SetupAsync();
        var cheque = await ReceiveChequeAsync(11_500m);

        var entry = await _db.JournalEntries
            .SingleAsync(e => e.Id == cheque.PortfolioJournalEntryId);

        Assert.Equal(new DateOnly(2026, 4, 5), entry.EntryDate);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<List<DateOnly>> SettlementDatesAsync(
        string chequeNumber = "CHQ-IN", DateOnly? inMonthOf = null)
    {
        var query = _db.JournalEntries.Where(e =>
            e.CompanyId == _companyId && e.SourceDocument == $"ChequeSettlement:{chequeNumber}");

        if (inMonthOf is DateOnly month)
            query = query.Where(e => e.EntryDate.Year == month.Year && e.EntryDate.Month == month.Month);

        return await query.OrderBy(e => e.EntryDate).Select(e => e.EntryDate).ToListAsync();
    }

    private async Task ClosePeriodAsync(DateOnly inside)
    {
        // ‏**الفترةُ لا تُنشأ إلّا عند أوّل ترحيلٍ فيها** — وهو عينُ ما أصلحَه ح-١
        // ‏(الشهرُ الساكنُ بلا صفّ). **فتُستحضَر قبل إقفالها**، وإلّا سقط
        // ‏الاختبارُ على تهيئته لا على دعواه.
        await new JournalPostingService(_db).EnsurePeriodOpenAsync(_companyId, inside);

        var period = await _db.FiscalPeriods.SingleAsync(
            p => p.CompanyId == _companyId && p.Code == FiscalPeriod.CodeFor(inside));
        await new FiscalPeriodService(_db).CloseAsync(_companyId, period.Id, "admin");
    }

    private async Task<decimal> BalanceAsync() =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance;

    private async Task InvoiceAsync(decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 4, 1));
        invoice.AddLine("بيع", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<Cheque> ReceiveChequeAsync(decimal amount, string number = "CHQ-IN")
    {
        var cheque = Cheque.CreateReceived(_companyId, number, new DateOnly(2026, 4, 5),
            amount, "شيك عميل", _bankId, _customerId, dueDate: new DateOnly(2026, 4, 30));
        await new ChequePostingService(_db).RegisterReceivedAsync(cheque);
        return cheque;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الشيكات", "310123456700134", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل");
        _db.Customers.Add(customer);
        var supplier = Brqosh.Domain.Purchasing.Supplier.Create(_companyId, "مورّد", "312233344400003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _supplierId = supplier.Id;

        var gl = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId
            && a.Code == SaudiChartOfAccounts.SystemCodes.CashAtBanks);
        var bank = BankAccount.Create(_companyId, "BK-1", "بنك", "بنك",
            "0001", "SA0000000000000000000002", gl.Id, isDefault: true);
        _db.BankAccounts.Add(bank);
        await _db.SaveChangesAsync();
        _bankId = bank.Id;
    }
}
