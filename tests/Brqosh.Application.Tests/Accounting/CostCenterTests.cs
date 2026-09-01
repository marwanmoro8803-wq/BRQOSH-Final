using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (CEEF-230 · ط1-٧) <b>مراكزُ التكلفة: كانت تُملأ وتُدفن.</b>
///
/// <para><b>ما كان:</b> شاشةٌ كاملةٌ لإدخالها، و<c>CostCenterId</c> على سطر القيد وحدَه —
/// <b>بلا مفتاحٍ أجنبيّ وبلا فهرسٍ وبلا تحقّق</b>، و<b>أربعٌ وعشرون خدمةَ ترحيلٍ آلية
/// تُسقطه كلَّها</b>، <b>ولا تقريرَ واحدٍ في النظام يقرؤه</b>.</para>
///
/// <para>فالمستخدم يظنّ أنّه يُصنّف مصروفاته وهو لا يُصنّف شيئاً: القيمةُ تُكتب في القاعدة
/// <b>ولا تخرج منها أبداً</b>. وهو من صنف «وحدةٌ تُعلَن ولا تعمل» الذي أخرجه <c>ط0-٣</c>.</para>
/// </summary>
public class CostCenterTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public CostCenterTests()
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

    // ── التحقّق ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ومركزٌ لا وجودَ له يُرفض</b> — وكان يُقبل أيُّ رقم.
    ///
    /// <para>فالعمودُ بلا مفتاحٍ أجنبيّ، ولا شيء كان يسأل عمّا يُشير إليه.</para>
    /// </summary>
    [Fact]
    public async Task AnUnknownCostCenter_IsRefusedAtPosting()
    {
        await SetupAsync();
        var entry = await DraftEntryAsync(costCenterId: 999_999L);

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new JournalPostingService(_db).PostAsync(entry));

        Assert.Contains("غيرُ معروفٍ أو موقوف", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ومركزٌ موقوفٌ يُرفض كذلك.</b>
    ///
    /// <para>الترحيلُ إلى مركزٍ أُوقف تصنيفٌ على بابٍ أُغلق — <b>والتقريرُ سيعرضه فيُقرأ
    /// كأنّه قائم</b>.</para>
    /// </summary>
    [Fact]
    public async Task AStoppedCostCenter_IsRefused()
    {
        await SetupAsync();
        var center = await CenterAsync("CC-STOP", "مركزٌ موقوف");
        center.Deactivate();
        await _db.SaveChangesAsync();

        var entry = await DraftEntryAsync(costCenterId: center.Id);

        await Assert.ThrowsAsync<AccountingException>(
            () => new JournalPostingService(_db).PostAsync(entry));
    }

    /// <summary><b>والصحيحُ يمرّ</b> — فالحارسُ حارسٌ لا سدّ.</summary>
    [Fact]
    public async Task AValidCostCenter_Posts()
    {
        await SetupAsync();
        var center = await CenterAsync("CC-1", "الإدارة");

        await new JournalPostingService(_db).PostAsync(await DraftEntryAsync(center.Id));

        var line = await _db.JournalEntries.AsNoTracking()
            .SelectMany(e => e.Lines)
            .FirstAsync(l => l.CostCenterId != null);

        Assert.Equal(center.Id, line.CostCenterId);
    }

    // ── القراءة: التقريرُ الذي لم يكن ───────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: ما يُصنَّف يُقرأ.</b>
    ///
    /// <para>وكان التقريرُ معدوماً تماماً — <b>فكلُّ ما أدخله المستخدمُ في سنواتٍ لا يخرج
    /// منه رقمٌ واحد</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheStatement_ReadsBackWhatWasClassified()
    {
        await SetupAsync();
        var center = await CenterAsync("CC-2", "التسويق");
        await new JournalPostingService(_db).PostAsync(await DraftEntryAsync(center.Id, 700m));

        var rows = await new CostCenterStatementQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var row = Assert.Single(rows);
        Assert.Equal("CC-2", row.CostCenterCode);
        Assert.Equal(700m, row.Net);
    }

    /// <summary>
    /// <b>وغيرُ المصنَّف يُعرض ولا يُخفى.</b>
    ///
    /// <para>فتقريرٌ يُخفيه يجعل مجموعَه أصغرَ من قائمة الدخل بلا سببٍ ظاهر،
    /// <b>ويُقرأ نقصُه دقّةً</b>. وظهورُه هو ما يُخبر المالكَ كم من مصروفه لم يُصنَّف.</para>
    /// </summary>
    [Fact]
    public async Task UnclassifiedSpend_IsShownNotHidden()
    {
        await SetupAsync();
        var center = await CenterAsync("CC-3", "العمليات");
        await new JournalPostingService(_db).PostAsync(await DraftEntryAsync(center.Id, 300m));
        await new JournalPostingService(_db).PostAsync(await DraftEntryAsync(null, 200m));

        var rows = await new CostCenterStatementQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.CostCenterNameAr == "غير مُصنَّف" && r.Net == 200m);
        Assert.Equal(500m, rows.Sum(r => r.Net));
    }

    /// <summary><b>وميزانُ المراجعة يُرشَّح بمركزٍ بعينه.</b></summary>
    [Fact]
    public async Task TheTrialBalance_FiltersByCostCenter()
    {
        await SetupAsync();
        var center = await CenterAsync("CC-4", "الفرع");
        await new JournalPostingService(_db).PostAsync(await DraftEntryAsync(center.Id, 400m));
        await new JournalPostingService(_db).PostAsync(await DraftEntryAsync(null, 900m));

        var filtered = await new TrialBalanceQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            costCenterId: center.Id);

        Assert.Equal(400m, filtered.Sum(r => r.TotalDebit));
    }

    // ── الانتشار: المستندُ يختم قيدَه ───────────────────────────────────────

    /// <summary>
    /// <b>وفاتورةُ المشتريات تختم مركزَها على سطور المصروف — وعليها وحدَها.</b>
    ///
    /// <para>فمركزُ التكلفة مفهومُ <b>أداء</b> لا مركزٍ ماليّ: تصنيفُ ذمّةٍ دائنةٍ أو
    /// ضريبةِ مدخلاتٍ عليه لا يُجيب سؤالاً يُسأل. <b>وهذا الاختبارُ يقيس الأمرين</b>:
    /// أنّ المصروفَ خُتم، وأنّ الذمّةَ لم تُختم.</para>
    /// </summary>
    [Fact]
    public async Task APurchaseInvoice_StampsOnlyItsProfitAndLossLines()
    {
        await SetupAsync();
        var center = await CenterAsync("CC-5", "المشتريات");

        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, supplier.Id, new DateOnly(2026, 4, 1), "SUP-9", vatDeductible: true);
        invoice.AddLine("خدمة", 1m, 1000m);
        invoice.AssignCostCenter(center.Id);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var lines = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.SourceDocument!.StartsWith("PurchaseInvoice"))
            .SelectMany(e => e.Lines)
            .ToListAsync();

        Assert.All(lines.Where(l => l.AccountCode.StartsWith('5')),
            l => Assert.Equal(center.Id, l.CostCenterId));
        Assert.All(lines.Where(l => !l.AccountCode.StartsWith('5')),
            l => Assert.Null(l.CostCenterId));
    }

    /// <summary><b>وفاتورةٌ بمركزٍ لا وجودَ له تُرفض قبل أن تُرحَّل.</b></summary>
    [Fact]
    public async Task APurchaseInvoiceWithAnUnknownCenter_IsRefused()
    {
        await SetupAsync();
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, supplier.Id, new DateOnly(2026, 4, 1), "SUP-8", vatDeductible: true);
        invoice.AddLine("خدمة", 1m, 500m);
        invoice.AssignCostCenter(888_888L);

        await Assert.ThrowsAsync<AccountingException>(
            () => new PurchaseInvoicePostingService(_db).PostAsync(invoice));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<CostCenter> CenterAsync(string code, string name)
    {
        var center = CostCenter.Create(_companyId, code, name);
        _db.CostCenters.Add(center);
        await _db.SaveChangesAsync();
        return center;
    }

    private async Task<JournalEntry> DraftEntryAsync(long? costCenterId, decimal amount = 500m)
    {
        var expense = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "5201");
        var cash = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "1101");

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 4, 1), "مصروف");
        entry.AddLine(expense, amount, 0m, costCenterId: costCenterId);
        entry.AddLine(cash, 0m, amount);
        return entry;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المراكز", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }
}
