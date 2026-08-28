using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (CEEF-229 · م2-١٥) <b>هوية شجرة البنوك</b> — الحارس الذي لا تُغني عنه هوية التدفقات.
///
/// <para><b>العطل:</b> كل حسابٍ بنكيّ يُنشئ حساب أستاذٍ <b>ابناً</b> تحت <c>1102</c>
/// (<c>110201</c>)، والمال يذهب إليه في سندات القبض والصرف والشيكات والتحويلات. لكنّ بيع
/// نقطة البيع <b>بالبطاقة</b> كان يحلّ الكود الحرفيّ <c>1102</c> — أي الأب. و<c>1102</c> في
/// القالب <c>IsGroup: false</c>، فالأب والابن يقبلان الترحيل معاً: <b>وهذا شرطُ الانشقاق
/// البنيويّ لا عرَضُه</b>.</para>
///
/// <para><b>الأثر عند تاجرٍ يبيع بالشبكة ويدفع بالتحويل — وهو حال أيّ متجر:</b> إيرادُ
/// البطاقات يتراكم على الأب ولا ينزل أبداً، والمدفوعات تُخصم من الابن حتى <b>يصير رصيدُ
/// حسابٍ بنكيّ سالباً</b> في جانب الأصول. والتسويةُ البنكية تقرأ الابن وحده فتُقارَن بكشفٍ
/// يحوي تحصيلات الشبكة، فيظهر فرقٌ دائم لا يُغلق مهما دقّق المالك.</para>
///
/// <para><b>ولماذا حارسٌ منفصل:</b> هويّتا قائمة التدفقات (هـ-٦/هـ-٧) <b>لا تكشفانه إطلاقاً</b> —
/// الأب والابن كلاهما تحت البادئة <c>1102</c>، فالمجاميع سليمة تماماً بينما التوزيع خاطئ.
/// خللٌ في التوزيع لا في الإجمالي يحتاج هويةً في مستوى التوزيع: <b>الأب لا يحمل رصيداً
/// مباشراً ما دام له ابن</b>.</para>
/// </summary>
public class BankAccountSplitTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private long _productId;

    public BankAccountSplitTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700099", BusinessProfile.Retail)
            .GetAwaiter().GetResult().Id;
        new BrqoshSchemaUpdater(_db).ApplyAsync().GetAwaiter().GetResult();
    }

    private async Task<BankAccount> BankAsync() =>
        await new BankAccountService(_db).CreateAsync(
            _companyId, "B1", "البنك الأول", "مصرف الرياض", "1234567890",
            "SA0380000000608010167519", isDefault: true);

    private async Task SeedStockAsync()
    {
        var product = Product.Create(_companyId, "P1", "منتج", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _productId = product.Id;

        var warehouse = await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId);
        var opening = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 1, 5), StockMovementType.Opening, warehouse.Id, "افتتاحيّ");
        opening.AddLine(_productId, 100m, 40m);
        await new StockMovementPostingService(_db).PostAsync(opening);
    }

    private async Task<decimal> DirectBalanceAsync(string code)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var row = rows.SingleOrDefault(r => r.AccountCode == code);
        return row is null ? 0m : row.BalanceDebit - row.BalanceCredit;
    }

    /// <summary>
    /// <b>هوية شجرة البنوك.</b> ما دام للمنشأة حسابٌ بنكيّ، فالأب <c>1102</c> لا يحمل رصيداً
    /// مباشراً — كلُّ نقد البنك على الأبناء. وكسرُها يعني أن مالاً يجلس في حسابٍ لا تراه أيّ
    /// تسويةٍ بنكية.
    /// </summary>
    [Fact]
    public async Task CardSale_LandsOnTheBankAccount_NotOnTheParent()
    {
        var bank = await BankAsync();
        await SeedStockAsync();

        var shift = PosShift.Open(_companyId, "كاشير", 0m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        TestActor.Cashier();
        await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 5m)], true, PosPaymentMethod.Card, "كاشير");

        TestActor.Administrator();
        var child = await _db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId);

        Assert.Equal(0m, await DirectBalanceAsync("1102"));
        Assert.True(await DirectBalanceAsync(child.Code) > 0m,
            "بيعُ البطاقة لم يصل حساب البنك — الأب ما زال يستقبل نقد الشبكة.");
    }

    /// <summary>
    /// <b>السيناريو الكامل الذي يُنتج رصيداً سالباً:</b> بيعٌ بالشبكة ثم دفعٌ بتحويل بنكيّ.
    /// المال دخل البنك وخرج منه، فرصيدُه يجب أن يكون الفارق موجباً — لا سالباً.
    /// </summary>
    [Fact]
    public async Task CardIncomeThenBankPayment_LeavesAPositiveBankBalance()
    {
        var bank = await BankAsync();
        await SeedStockAsync();

        var shift = PosShift.Open(_companyId, "كاشير", 0m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        TestActor.Cashier();
        var sale = await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 10m)], true, PosPaymentMethod.Card, "كاشير");

        TestActor.Administrator();
        var supplier = Domain.Purchasing.Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var payment = PaymentVoucher.CreateDraft(
            _companyId, supplier.Id, new DateOnly(2026, 2, 20), 200m,
            ReceiptMethod.Bank, "سداد بتحويل", bankAccountId: bank.Id);
        await new PaymentVoucherPostingService(_db).PostAsync(payment);

        var child = await _db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId);
        var balance = await DirectBalanceAsync(child.Code);

        Assert.Equal(0m, await DirectBalanceAsync("1102"));
        Assert.True(balance > 0m,
            $"رصيد الحساب البنكيّ {balance} — دخلَه إيرادُ الشبكة {sale.Invoice.GrandTotal} وخرج منه 200 فقط.");
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> منشأةٌ <b>بلا</b> حساب بنكيّ تُقيَّد على الحساب النظاميّ — فالسقوط
    /// مقصودٌ ومحدود، ولا يُغلق البيع بالشبكة على من لم يُنشئ بنكه بعد.
    /// </summary>
    [Fact]
    public async Task WithNoBankAccount_TheSystemAccountIsUsed()
    {
        await SeedStockAsync();

        var shift = PosShift.Open(_companyId, "كاشير", 0m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        TestActor.Cashier();
        await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 3m)], true, PosPaymentMethod.Card, "كاشير");

        TestActor.Administrator();
        Assert.True(await DirectBalanceAsync("1102") > 0m);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
