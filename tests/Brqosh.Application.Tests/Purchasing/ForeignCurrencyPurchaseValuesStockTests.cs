using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٤) <b>فاتورةُ الشراء بعملةٍ أجنبيّة كانت تُقيّم المخزونَ بالمبلغ
/// الأجنبيّ نفسِه.</b>
///
/// <para><b>ما كان:</b> <c>RecalculateTotals</c> يحسب <c>ForeignSubtotal</c> من السطور
/// <b>ثمّ</b> يضربه في سعر الصرف ليُخرج <c>Subtotal</c> بالريال. لكنّ خدمةَ الترحيل كانت
/// تجمع <c>line.LineTotal</c> <b>خامّاً</b> — أي بالعملة الأجنبيّة — وتُمرّره حيث يُنتظر
/// الريال.</para>
///
/// <para><b>وقيس:</b> فاتورةُ ١٬٠٠٠ دولارٍ بسعر ٣٫٧٥ ⟵ <c>Subtotal</c> ٣٬٧٥٠ ريالاً،
/// و<c>inventorySubtotal</c> <b>١٬٠٠٠</b>. فيُمدَّن المخزونُ بألفٍ <b>ويذهب الفرقُ ٢٬٧٥٠
/// إلى مصروف المشتريات</b>. <b>والقيدُ متوازنٌ تماماً فلا يشكو ميزانُ المراجعة</b> —
/// والمخزونُ مُقيَّمٌ بربع قيمته، فيخرج كلُّ بيعٍ لاحقٍ بتكلفةٍ خاطئة وربحٍ موهوم.</para>
///
/// <para><b>ولا يقع إلّا على المستورِد</b> — وهو ليس حالةً نادرةً في السوق السعوديّ.</para>
/// </summary>
public class ForeignCurrencyPurchaseValuesStockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public ForeignCurrencyPurchaseValuesStockTests()
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

    /// <summary>
    /// <b>القياسُ الحاكم:</b> ١٬٠٠٠ دولارٍ بسعر ٣٫٧٥ ⟵ المخزونُ <b>٣٬٧٥٠ ريالاً</b>،
    /// ولا ريالَ واحدٌ يتسرّب إلى مصروف المشتريات.
    /// </summary>
    [Fact]
    public async Task ForeignCurrencyInvoice_ValuesStockInRiyals_NotInTheForeignAmount()
    {
        var (companyId, supplierId, productId) = await SetupAsync();

        await InvoiceAsync(companyId, supplierId, productId,
            quantity: 100m, unitPrice: 10m, currency: "USD", rate: 3.75m);

        Assert.Equal(3_750m, await LedgerAsync(companyId, "1107"));
        Assert.Equal(0m, await LedgerAsync(companyId, "5102"));
    }

    /// <summary>
    /// <b>والهويّةُ هـ-٣ لا تكشف هذا العطلَ — وهذا هو الدرس.</b>
    ///
    /// <para><b>مقيسٌ:</b> هذه الحالةُ <b>تمرّ خضراءَ على الشيفرة المعطوبة</b> كما تمرّ على
    /// المُصلَحة. فالرقمُ الخاطئُ نفسُه يدخل الطرفين: الأستاذُ يُمدَّن ١٬٠٠٠ والدفترُ
    /// المساعدُ يُقيَّد ١٬٠٠٠ — <b>فيتطابقان تطابقاً تامّاً وهما خطأ معاً</b>.</para>
    ///
    /// <para><b>فليست شاهداً على العطل بل حارساً ألّا يكسر الإصلاحُ ما كان سليماً</b>.
    /// والشاهدُ على العطل هو القيمةُ المطلقة (٣٬٧٥٠) لا الاتّساقُ بين طرفين. <b>وهويّةٌ
    /// بين مصدرَين يُغذّيهما رقمٌ واحدٌ لا تقيس صحّةَ ذلك الرقم.</b></para>
    /// </summary>
    [Fact]
    public async Task ForeignCurrencyInvoice_KeepsTheSubLedgerIdentity()
    {
        var (companyId, supplierId, productId) = await SetupAsync();

        await InvoiceAsync(companyId, supplierId, productId,
            quantity: 100m, unitPrice: 10m, currency: "USD", rate: 3.75m);

        await LedgerIdentities.AssertAllAsync(_db, companyId);
    }

    /// <summary>
    /// <b>وتكلفةُ الوحدة في الدفتر المساعد بالريال</b> — ٣٧٫٥٠ لا ١٠. ولولا هذا لخرج
    /// كلُّ بيعٍ لاحقٍ بتكلفةٍ ربعِ الحقيقة، <b>والربحُ موهومٌ في كلّ فاتورةِ بيع</b>.
    /// </summary>
    [Fact]
    public async Task ForeignCurrencyInvoice_StoresTheUnitCostInRiyals()
    {
        var (companyId, supplierId, productId) = await SetupAsync();

        await InvoiceAsync(companyId, supplierId, productId,
            quantity: 100m, unitPrice: 10m, currency: "USD", rate: 3.75m);

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(companyId);
        var row = rows.Single(r => r.ProductId == productId);

        Assert.Equal(100m, row.Quantity);
        Assert.Equal(3_750m, Math.Round(row.TotalValue, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// <b>شاهدُ ضبط:</b> فاتورةٌ بالريال تبقى كما كانت حرفاً — سعرُ الصرف واحدٌ فالضربُ
    /// لا يُغيّر شيئاً. ولولاه لجاز أن يكون «الإصلاح» ضرب السليمَ كما ضرب المعطوب.
    /// </summary>
    [Fact]
    public async Task RiyalInvoice_IsUnchanged()
    {
        var (companyId, supplierId, productId) = await SetupAsync();

        await InvoiceAsync(companyId, supplierId, productId,
            quantity: 100m, unitPrice: 10m, currency: null, rate: 1m);

        Assert.Equal(1_000m, await LedgerAsync(companyId, "1107"));
        Assert.Equal(0m, await LedgerAsync(companyId, "5102"));
        await LedgerIdentities.AssertAllAsync(_db, companyId);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<(long CompanyId, long SupplierId, long ProductId)> SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة مستورِدة", "310123456700055", BusinessProfile.Retail);

        var supplier = Supplier.Create(company.Id, "مورد أجنبيّ", "312222222200003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(company.Id, "P-FX", "صنف مستورَد", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return (company.Id, supplier.Id, product.Id);
    }

    private async Task InvoiceAsync(long companyId, long supplierId, long productId,
        decimal quantity, decimal unitPrice, string? currency, decimal rate)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            companyId, supplierId, new DateOnly(2026, 8, 5), "SUP-FX-1", vatDeductible: true);
        if (currency is not null)
            invoice.SetCurrency(currency, rate);
        invoice.AddLine("صنف مستورَد", quantity, unitPrice, productId);

        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>رصيدٌ مُوقَّع من ميزان المراجعة: موجبٌ مدين، سالبٌ دائن.</summary>
    private async Task<decimal> LedgerAsync(long companyId, string accountCode)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));

        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
