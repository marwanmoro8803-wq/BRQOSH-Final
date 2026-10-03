using Brqosh.Application.Intelligence;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩٨ · <c>manufacturing#8</c>) <b>الإنتاجُ تحت التشغيل حسابُه — لا أصلُ عقود المقاولات؛ والمساعدُ يقول ما يقع.</b>
///
/// <para><b>ما كان:</b> <c>WorkOrderService</c> يُحمّل «تحت التشغيل» على <b>1113</b> — «أعمال تحت التنفيذ (مستخلصات غير مفوترة)»، وهو أصلُ
/// عقود المقاولات الذي يكتبه <c>ContractRevenueTrueUp</c> (ح-٨٠) — فتدخل حركةُ المصنع كشفَ حساب المقاولات. <b>والمساعدُ يقول «WIP حتى الإغلاق»
/// ودليلُ الصفحة «رصيد أوامر الإنتاج غير المغلقة»</b> — والقيدان يُرحَّلان معاً عند الإكمال فلا رصيدَ يبقى لحظةً واحدة.</para>
///
/// <para>قائمةٌ تُخرج وحدةً من ٣ خامات (٢٫٠٠) وأجرُ الوحدة ١٫٠٠ ⟵ أمرٌ بـ٥: <b>٣٠ موادّ + ٥ أجر = ٣٥</b> تمرّ بـ<b>1119</b> وتُفرَّغ منه.</para>
/// </summary>
public class ProductionWipIsItsOwnAccountTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _orderId;

    public ProductionWipIsItsOwnAccountTests()
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
    /// <b>القياسُ الحاكم: الإكمالُ يُحمّل 1119 بالتكلفة كلِّها ويُفرّغه بها — ولا يمسّ 1113.</b>
    /// </summary>
    [Fact]
    public async Task TheCompletion_LoadsAndClearsTheProductionWipAccount_NotTheConstructionOne()
    {
        await SetupAndReleaseAsync();
        await CompleteAsync();

        var (debit, credit) = await TurnoverAsync(SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess);
        Assert.Equal((35m, 35m), (debit, credit));
        Assert.Equal((0m, 0m), await TurnoverAsync(SaudiChartOfAccounts.SystemCodes.WorkInProgress));
    }

    /// <summary>
    /// <b>ومنشأةٌ قائمةٌ قبل الحساب تأخذه من القالب عند أوّل إكمال</b> — باسمه ونوعه وحسابه الأب، نظاميّاً؛ فلا يسقط أمرٌ بـ«الحساب غير موجود».
    /// </summary>
    [Fact]
    public async Task ACompanyWithoutTheAccount_GetsItFromTheTemplate()
    {
        await SetupAndReleaseAsync();
        await DropTheAccountAsync();

        await CompleteAsync();

        var account = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess);
        var template = SaudiChartOfAccounts.Template.Single(a => a.Code == SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess);
        Assert.Equal((template.NameAr, AccountType.Asset, true, false), (account.NameAr, account.Type, account.IsSystem, account.IsGroup));
        Assert.Equal((35m, 35m), await TurnoverAsync(SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess));
    }

    /// <summary>
    /// <b>وحسابٌ بالرمز نفسِه ليس أصلاً قابلاً للترحيل يُرفض باسمه</b> — رمزٌ أنشأه المالكُ لغرضٍ آخر لا يُحمَّل بإنتاج المصنع صامتاً؛ ولا قيدَ ولا حركة.
    /// </summary>
    [Fact]
    public async Task AnAccountUnderThatCodeThatIsNotAPostableAsset_IsRefusedByName()
    {
        await SetupAndReleaseAsync();
        await DropTheAccountAsync();
        var parent = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "11");
        _db.Accounts.Add(Account.Create(SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess, "مجموعة المالك", "Owner group",
            AccountType.Asset, parent.Id, isGroup: true, isSystem: false, _companyId));
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(CompleteAsync);

        Assert.Contains(SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess, ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.JournalEntries.AnyAsync(e => e.SourceDocument == $"WorkOrderComplete:{_orderId}"));
        Assert.False(await _db.StockMovements.AnyAsync(m => m.SourceDocument == $"WorkOrder:{_orderId}"));
    }

    /// <summary>
    /// <b>والمساعدُ ودليلُ الصفحة يقولان ما يقع</b> — لا «WIP حتى الإغلاق» ولا «رصيد أوامر الإنتاج»: الموادُّ تُصرف وتُكلَّف عند الإكمال.
    /// </summary>
    [Fact]
    public void TheAssistant_SaysWhatTheLedgerDoes()
    {
        var hint = ProgramWorkflowKnowledge.Match("أمر إنتاج تصنيع", 5).Single(h => h.Id == "manufacturing");
        Assert.True(ProgramPageGuideTemplates.TryGet("wip", out var guide));

        Assert.DoesNotContain("حتى الإغلاق", hint.StepsAr, StringComparison.Ordinal);
        Assert.DoesNotContain("until close", hint.StepsEn, StringComparison.Ordinal);
        Assert.DoesNotContain("رصيد أوامر الإنتاج", guide.Ar, StringComparison.Ordinal);
        Assert.DoesNotContain("balances ·", guide.En, StringComparison.Ordinal);
        Assert.Contains("عند الإكمال", guide.Ar, StringComparison.Ordinal);
        Assert.Contains("on completion", guide.En, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAndReleaseAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "300000000000077", BusinessProfile.Manufacturing);
        _companyId = company.Id;

        var finished = Product.Create(_companyId, "FG-98", "منتج تامّ", "قطعة", ProductType.Stockable, 0m);
        var raw = Product.Create(_companyId, "RM-98", "خامة", "كجم", ProductType.Stockable, 0m);
        _db.Products.AddRange(finished, raw);
        await _db.SaveChangesAsync();

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId);
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, warehouse.Id, "افتتاح الخامة");
        opening.AddLine(raw.Id, 100m, 2m);
        await new StockMovementPostingService(_db).PostAsync(opening);

        var bom = BillOfMaterials.Create(_companyId, "BOM-98", "تجميع", finished.Id, outputQuantity: 1m);
        bom.AddLine(raw.Id, 3m);
        bom.SetConversionCosts(labourCostPerUnit: 1m, overheadRatePercent: 0m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 4, 1), 5m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();
        _orderId = order.Id;
        await new WorkOrderService(_db).ReleaseAsync(_companyId, order.Id);
    }

    private Task CompleteAsync() =>
        new WorkOrderService(_db).CompleteAsync(_companyId, _orderId, 5m, "admin", new DateOnly(2026, 4, 5));

    /// <summary>منشأةٌ قائمةٌ قبل ح-٩٨ — الحسابُ ليس في دليلها (حذفٌ حقيقيٌّ لا ناعم، قبل أيّ قيدٍ عليه).</summary>
    private async Task DropTheAccountAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM accounts WHERE \"CompanyId\" = {0} AND \"Code\" = {1}",
            _companyId, SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess);
        _db.ChangeTracker.Clear();
    }

    private async Task<(decimal Debit, decimal Credit)> TurnoverAsync(string code)
    {
        var lines = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId)
            .SelectMany(e => e.Lines, (e, l) => new { l.AccountCode, l.Debit, l.Credit })
            .Where(x => x.AccountCode == code)
            .ToListAsync();
        return (lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
    }
}
