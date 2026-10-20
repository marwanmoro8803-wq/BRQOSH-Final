using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.RealEstate;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.RealEstate;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.RealEstate;

/// <summary>
/// (CEEF-230 · ط3-١ب) <b>تكلفةُ الصيانة على وحدتها — والمصادرةُ التي صار لها سند.</b>
///
/// <para><b>ما كان:</b> المؤجِّرُ يُصلح ويدفع، <b>والمبلغُ مصروفُ صيانةٍ عامٌّ بلا رابطٍ
/// بوحدة</b>. ومبلغُ مصادرة التأمين <b>رقمٌ يكتبه محاسبٌ بلا شيءٍ خلفه</b> — ومصادرةٌ بلا
/// سندٍ دعوى يخسرها المؤجِّر.</para>
///
/// <para><b>وهذا الملفُّ يقيس الحلقةَ كاملةً</b>: تلفٌ ⇒ أمرُ صيانةٍ ⇒ فاتورةٌ على
/// المستأجر ⇒ مصادرةٌ في مقابلها.</para>
/// </summary>
public class MaintenancePostingTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _tenantId;
    private long _propertyId;
    private long _unitId;
    private long _leaseId;

    public MaintenancePostingTests()
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
    /// <b>القياسُ الحاكم: التكلفةُ كلُّها مصروفٌ — لا حصّةُ المالك وحدَها.</b>
    ///
    /// <para>العملُ وقع بكامله ودُفع بكامله، <b>وما يُستردّ إيرادٌ لا نقصٌ في المصروف</b>.
    /// وقيدُ حصّةِ المالك وحدَها <b>يُخفي نصفَ ما أنفقته المنشأة</b>.</para>
    /// </summary>
    [Fact]
    public async Task PostingAnOrder_ExpensesTheWholeCost_NotJustTheOwnerShare()
    {
        await SetupAsync();
        var order = await CompletedDamageOrderAsync(total: 1000m, tenantShare: 400m);

        await new MaintenanceOrderService(_db).PostAsync(
            _companyId, order.Id, new DateOnly(2026, 5, 20));

        Assert.Equal(1000m, await BalanceAsync(MaintenanceOrderService.MaintenanceExpenseCode));
    }

    /// <summary>
    /// <b>وحصّةُ المستأجر تصير فاتورةً — لا سطرَ قيد.</b>
    ///
    /// <para><b>والدفترُ المساعد يُبنى من المستندات</b>: قيدٌ يزيد <c>1104</c> يُصلح
    /// الميزانيةَ <b>ويترك كشفَ حساب المستأجر كما كان</b> — فيُطالَب بما لا يراه، ولا
    /// يظهر في أعمار ديونه.</para>
    /// </summary>
    [Fact]
    public async Task TheTenantShare_BecomesAnInvoiceOnTheirStatement()
    {
        await SetupAsync();
        var order = await CompletedDamageOrderAsync(total: 1000m, tenantShare: 400m);

        var result = await new MaintenanceOrderService(_db).PostAsync(
            _companyId, order.Id, new DateOnly(2026, 5, 20));

        Assert.NotNull(result.TenantInvoiceId);

        var owed = (await new CustomerBalanceQuery(_db)
            .ForCustomerAsync(_companyId, _tenantId)).Balance;

        // ‏٤٠٠ صافياً + ١٥٪ = ٤٦٠ — **وهو ما يراه المستأجرُ في كشفه**.
        Assert.Equal(460m, owed);
    }

    /// <summary>
    /// <b>والحلقةُ كاملة: تلفٌ ⇒ أمرٌ ⇒ فاتورةٌ ⇒ مصادرةٌ في مقابلها.</b>
    ///
    /// <para><b>وهذا هو الرابطُ الذي كان مفقوداً.</b> وبغير الأمر لا فاتورة، وبغير الفاتورة
    /// لا دَين، <b>وبغير الدَّين لا شيءَ تُصادَر في مقابله</b> — فيصير أمرُ الصيانة لازماً
    /// لا زينة.</para>
    /// </summary>
    [Fact]
    public async Task DamageOrder_MakesTheForfeitureDefensible()
    {
        await SetupAsync();
        var deposits = new SecurityDepositService(_db);
        await deposits.ReceiveAsync(_companyId, _leaseId, new DateOnly(2026, 1, 5), "admin");

        var order = await CompletedDamageOrderAsync(total: 1000m, tenantShare: 1000m);
        await new MaintenanceOrderService(_db).PostAsync(
            _companyId, order.Id, new DateOnly(2026, 5, 20));

        // ‏١٠٠٠ + ١٥٪ = ١١٥٠ ديناً، فتُصادَر ألفٌ في مقابله.
        await deposits.ReturnAsync(_companyId, _leaseId, new DateOnly(2026, 6, 30), 1_000m, "admin");

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TenantSecurityDeposits));
    }

    /// <summary>
    /// <b>ومصادرةٌ بلا دَينٍ تُرفض بالرقمين.</b>
    ///
    /// <para><b>وهي الحالةُ التي كانت تمرّ</b>: يكتب المحاسبُ ألفين ويُقفل، <b>ثمّ يطلب
    /// المستأجرُ التفصيلَ فلا يجده</b> ويُردّ التأمينُ كلُّه بحكم.</para>
    /// </summary>
    [Fact]
    public async Task ForfeitingWithNothingOwed_IsRefusedByBothNumbers()
    {
        await SetupAsync();
        var deposits = new SecurityDepositService(_db);
        await deposits.ReceiveAsync(_companyId, _leaseId, new DateOnly(2026, 1, 5), "admin");

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            deposits.ReturnAsync(_companyId, _leaseId, new DateOnly(2026, 6, 30), 2_000m, "admin"));

        Assert.Contains("2,000.00", error.Message);
        Assert.Contains("0.00", error.Message);
    }

    /// <summary>
    /// <b>ولا يُرحَّل أمرٌ لم يُنهَ.</b>
    ///
    /// <para>تكلفتُه لم تثبت، <b>وترحيلُ أمرٍ قائمٍ يُقيّد مصروفاً يزيد غداً</b> فيفترق
    /// القيدُ عن سنده.</para>
    /// </summary>
    [Fact]
    public async Task PostingAnUnfinishedOrder_IsRefused()
    {
        await SetupAsync();
        var order = MaintenanceOrder.Create(_companyId, _propertyId, "MO-OPEN",
            MaintenanceKind.Corrective, new DateOnly(2026, 5, 1), "تسريب", unitId: _unitId);
        order.AddCost("سباكة", 300m);
        _db.MaintenanceOrders.Add(order);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new MaintenanceOrderService(_db).PostAsync(_companyId, order.Id, new DateOnly(2026, 5, 20)));

        Assert.Contains("أُنهي", error.Message);
        Assert.Equal(0m, await BalanceAsync(MaintenanceOrderService.MaintenanceExpenseCode));
    }

    /// <summary>
    /// <b>ولا يُرحَّل أمرٌ مرّتين.</b>
    ///
    /// <para><b>وترحيلُه ثانيةً يُضاعف المصروفَ ويُصدر فاتورةً ثانيةً على المستأجر</b> عن
    /// إصلاحٍ واحد.</para>
    /// </summary>
    [Fact]
    public async Task PostingTwice_IsRefused()
    {
        await SetupAsync();
        var order = await CompletedDamageOrderAsync(total: 500m, tenantShare: 0m);
        var service = new MaintenanceOrderService(_db);

        await service.PostAsync(_companyId, order.Id, new DateOnly(2026, 5, 20));

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            service.PostAsync(_companyId, order.Id, new DateOnly(2026, 5, 20)));

        Assert.Contains("مُرحَّلٌ سلفاً", error.Message);
        Assert.Equal(500m, await BalanceAsync(MaintenanceOrderService.MaintenanceExpenseCode));
    }

    /// <summary>
    /// <b>وتكلفةُ الوحدات تُرتَّب بالأعلى — فالآكلةُ تُرى أوّلاً.</b>
    ///
    /// <para><b>ومحفظةٌ بأربعين وحدةً تُظهر ربحاً إجمالياً ولا تقول أيَّ وحدةٍ تخسر</b>.
    /// <b>ورقمٌ لا يُرى أوّلاً لا يُعالَج</b>.</para>
    /// </summary>
    [Fact]
    public async Task CostByUnit_SurfacesTheExpensiveUnitFirst()
    {
        await SetupAsync();

        var second = PropertyUnit.Create(_companyId, _propertyId, "U-2", 90m, listedAnnualRent: 3_000m);
        _db.PropertyUnits.Add(second);
        await _db.SaveChangesAsync();

        await CompletedOrderAsync("MO-A", _unitId, 300m);

        // ‏**وتكلفةُ المالك ليست تكلفةَ الأمر**: هذا الأمرُ تلفٌ بفعل المستأجر، **وخمسُمئةٍ
        // ‏منه عليه**. وطفرةٌ نجت فأخرجت أنّ اختباري كان بأوامرَ وقائيةٍ وحدَها — **وفيها
        // ‏الحصّتان واحدة**، فلا شيءَ يقيس أيَّهما جُمع.
        var damage = MaintenanceOrder.Create(_companyId, _propertyId, "MO-B",
            MaintenanceKind.TenantDamage, new DateOnly(2026, 5, 1), "تلف", unitId: second.Id);
        damage.AddCost("إصلاح", 2_500m);
        damage.ChargeTenant(500m, _leaseId);
        damage.Complete(new DateOnly(2026, 5, 15));
        _db.MaintenanceOrders.Add(damage);
        await _db.SaveChangesAsync();

        var rows = await new MaintenanceOrderService(_db).CostByUnitAsync(
            _companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(2, rows.Count);
        Assert.Equal(second.Id, rows[0].UnitId);
        Assert.Equal(2_000m, rows[0].OwnerCost);
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-٤٨ · <c>sector-modules#8</c>) <b>نقدُ الصيانة يخرج من حساب البنك
    /// نفسِه — لا من الأب <c>1102</c>.</b>
    ///
    /// <para>‏كلُّ حسابٍ بنكيٍّ ابنٌ تحت <c>1102</c>، <b>والأبُ يقبل الترحيلَ أيضاً</b>. فمسارٌ يُقيّد
    /// على الأب يَشقّ نقدَ البنك: الكشفُ في الابن والمالُ في الأب، <b>ويبقى في التسوية البنكيّة فرقٌ
    /// لا يُغلق</b>. وهو عينُ ما أصلحه CEEF-229 (م2-١٥) في البيع والسندات — <b>وبقيت المداخلُ
    /// القطاعيّةُ على الأب بقيمةٍ افتراضيّة</b>، وكلُّ شاشةٍ تستدعيها بلا حساب.</para>
    /// </summary>
    [Fact]
    public async Task MaintenanceCash_LeavesTheBankAccount_NotItsParent()
    {
        await SetupAsync();
        var bankGl = await OpenBankAccountAsync();
        var order = await CompletedDamageOrderAsync(total: 1000m, tenantShare: 0m);

        await new MaintenanceOrderService(_db).PostAsync(_companyId, order.Id, new DateOnly(2026, 5, 20));

        Assert.Equal(-1_000m, await ExactBalanceAsync(bankGl));
        Assert.Equal(0m, await ExactBalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
    }

    /// <summary>
    /// ‏<b>وتأمينُ المستأجر يدخل حسابَ البنك ويخرج منه</b> — استلامُه وردُّه.
    /// </summary>
    [Fact]
    public async Task DepositCash_EntersAndLeavesTheBankAccount_NotItsParent()
    {
        await SetupAsync();
        var bankGl = await OpenBankAccountAsync();
        var deposits = new SecurityDepositService(_db);

        await deposits.ReceiveAsync(_companyId, _leaseId, new DateOnly(2026, 1, 5), "admin");
        Assert.Equal(10_000m, await ExactBalanceAsync(bankGl));

        await deposits.ReturnAsync(_companyId, _leaseId, new DateOnly(2026, 6, 30), 0m, "admin");
        Assert.Equal(0m, await ExactBalanceAsync(bankGl));

        Assert.Equal(0m, await ExactBalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
    }

    /// <summary>
    /// ‏<b>ومنشأةٌ بلا حسابٍ بنكيٍّ بعدُ تُقيّد على الأب</b> — السقوطُ المقصودُ في
    /// <c>BankGlAccountResolver</c> (شاهدُ ضبط): رفضُ القيد لأنّ لا ابنَ له يُغلق حالةً مشروعة.
    /// </summary>
    [Fact]
    public async Task WithNoBankAccountYet_TheParentIsTheBank()
    {
        await SetupAsync();

        await new SecurityDepositService(_db).ReceiveAsync(_companyId, _leaseId, new DateOnly(2026, 1, 5), "admin");

        Assert.Equal(10_000m, await ExactBalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
    }

    // ── بفاتورة المورّد (sector-modules#11 · قرارُ المالك 2026-10-20) ─────────

    /// <summary>
    /// <b>الأمرُ بفاتورة المورّد</b>: الفاتورةُ أثبتت المورّدَ وضريبةَ المدخلات (١٥٠) والدَّينَ (١٬١٥٠)، والأمرُ ينقل التكلفةَ من
    /// المشتريات غير المخزنيّة (5102) إلى الصيانة (5206) — <b>ولا نقدَ يخرج</b>، وكان يُدفع نقداً بالإجماليّ بلا ضريبة.
    /// </summary>
    [Fact]
    public async Task FromTheSupplierInvoice_TheCostMovesToMaintenance_AndNoCashLeaves()
    {
        await SetupAsync();
        var invoice = await SupplierInvoiceAsync("SUP-1", 1_000m);
        var order = await CompletedDamageOrderAsync(total: 1_000m, tenantShare: 0m);

        await new MaintenanceOrderService(_db).PostAsync(_companyId, order.Id, new DateOnly(2026, 5, 20), purchaseInvoiceId: invoice.Id);

        Assert.Equal(1_000m, await ExactBalanceAsync(MaintenanceOrderService.MaintenanceExpenseCode));
        Assert.Equal(0m, await ExactBalanceAsync(MaintenanceOrderService.NonInventoryPurchasesCode));
        Assert.Equal(150m, await ExactBalanceAsync(SaudiChartOfAccounts.SystemCodes.VatInput));
        Assert.Equal(-1_150m, await ExactBalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
        Assert.Equal(0m, await ExactBalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
        Assert.Equal(invoice.Id, (await _db.MaintenanceOrders.SingleAsync(m => m.Id == order.Id)).PurchaseInvoiceId);
    }

    [Fact]
    public async Task ASupplierInvoiceWithAnotherNet_IsRefused_NamingBoth()
    {
        await SetupAsync();
        var invoice = await SupplierInvoiceAsync("SUP-2", 900m);
        var order = await CompletedDamageOrderAsync(total: 1_000m, tenantShare: 0m);

        var refused = await Assert.ThrowsAsync<AccountingException>(() => new MaintenanceOrderService(_db)
            .PostAsync(_companyId, order.Id, new DateOnly(2026, 5, 20), purchaseInvoiceId: invoice.Id));

        Assert.Contains("900.00", refused.Message, StringComparison.Ordinal);
        Assert.Contains("1,000.00", refused.Message, StringComparison.Ordinal);
        Assert.Null((await _db.MaintenanceOrders.SingleAsync(m => m.Id == order.Id)).JournalEntryId);
    }

    [Fact]
    public async Task ASupplierInvoice_BacksOneOrderOnly()
    {
        await SetupAsync();
        var invoice = await SupplierInvoiceAsync("SUP-3", 500m);
        var first = await CompletedDamageOrderAsync(total: 500m, tenantShare: 0m);
        var second = await CompletedDamageOrderAsync(total: 500m, tenantShare: 0m);
        var service = new MaintenanceOrderService(_db);

        await service.PostAsync(_companyId, first.Id, new DateOnly(2026, 5, 20), purchaseInvoiceId: invoice.Id);
        await Assert.ThrowsAsync<AccountingException>(() =>
            service.PostAsync(_companyId, second.Id, new DateOnly(2026, 5, 20), purchaseInvoiceId: invoice.Id));
    }

    /// <summary>فاتورةُ شراءٍ مرحّلةٌ لمورّدٍ مسجَّل — سطرُ خدمةٍ بلا صنف بصافي <paramref name="net"/> وضريبته ١٥٪.</summary>
    private async Task<Brqosh.Domain.Purchasing.PurchaseInvoice> SupplierInvoiceAsync(string number, decimal net)
    {
        var supplier = Brqosh.Domain.Purchasing.Supplier.Create(_companyId, $"مقاول صيانة {number}", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var invoice = Brqosh.Domain.Purchasing.PurchaseInvoice.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 5, 15), number, true);
        invoice.AddLine("أعمال صيانة", 1m, net);
        return await new Brqosh.Infrastructure.Purchasing.PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    /// <summary>
    /// ‏رصيدُ الحساب بعينه من سطور القيود — <b>لا بالبادئة</b>: فالبادئةُ <c>1102</c> تضمّ الأبَ
    /// وأبناءه معاً، <b>وهي بعينها ما أخفى الانشقاقَ عن هويّة التدفّقات النقديّة</b>.
    /// </summary>
    private async Task<decimal> ExactBalanceAsync(string code) =>
        await _db.JournalEntries.AsNoTracking()
            .WhereReportable(_companyId)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .SumAsync(l => l.Debit - l.Credit);

    private async Task<string> OpenBankAccountAsync()
    {
        var bank = await new BankAccountService(_db).CreateAsync(
            _companyId, "B1", "البنك الأول", "مصرف الرياض", "1234567890",
            "SA0380000000608010167519", isDefault: true);
        return (await _db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId)).Code;
    }

    private async Task<decimal> BalanceAsync(string prefix)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode.StartsWith(prefix))
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<MaintenanceOrder> CompletedDamageOrderAsync(decimal total, decimal tenantShare)
    {
        var order = MaintenanceOrder.Create(_companyId, _propertyId, $"MO-{Guid.NewGuid():N}"[..12],
            MaintenanceKind.TenantDamage, new DateOnly(2026, 5, 1), "تلف بفعل المستأجر",
            unitId: _unitId);
        order.AddCost("إصلاح", total);
        if (tenantShare > 0m) order.ChargeTenant(tenantShare, _leaseId);
        order.Complete(new DateOnly(2026, 5, 15));

        _db.MaintenanceOrders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    private async Task CompletedOrderAsync(string number, long unitId, decimal cost)
    {
        var order = MaintenanceOrder.Create(_companyId, _propertyId, number,
            MaintenanceKind.Preventive, new DateOnly(2026, 5, 1), "دهان", unitId: unitId);
        order.AddCost("دهان", cost);
        order.Complete(new DateOnly(2026, 5, 15));
        _db.MaintenanceOrders.Add(order);
        await _db.SaveChangesAsync();
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة عقارات", "310123456700099", BusinessProfile.RealEstate);
        _companyId = company.Id;

        var tenant = Customer.Create(_companyId, "مستأجر");
        _db.Customers.Add(tenant);
        await _db.SaveChangesAsync();
        _tenantId = tenant.Id;

        var property = Property.Create(_companyId, "P-1", "عمارة المسبار", PropertyKind.Building, "الرياض", "حي المسبار");
        _db.Properties.Add(property);
        await _db.SaveChangesAsync();
        _propertyId = property.Id;

        var unit = PropertyUnit.Create(_companyId, _propertyId, "U-1", 120m, listedAnnualRent: 4_000m);
        _db.PropertyUnits.Add(unit);
        await _db.SaveChangesAsync();
        _unitId = unit.Id;

        var lease = LeaseContract.Create(_companyId, _unitId, _tenantId, "L-1",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 4_000m,
            securityDeposit: 10_000m);
        await new LeaseService(_db).ActivateAsync(lease);
        _leaseId = lease.Id;
    }
}
