using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Security;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>tax-zatca#12</c>) <b>الخانتان 8 و9 — ضريبةُ الاستيراد المدفوعةُ في الجمارك، والاحتسابُ العكسيّ.</b>
///
/// <para><b>ما كان:</b> الخانتان صفرٌ معلَن — فاتورةُ الشراء لا تعرف الاستيرادَ ولا المورّدَ غيرَ المقيم. <b>والقاعدةُ من دليل الهيئة
/// «الاستيراد والتصدير»</b>: ضريبةُ الاستيراد المدفوعةُ للهيئة في الخانة 8 (§5.3) وتُخصم بمستند الجمارك (§10.3)؛ <b>والمستلمُ في الاحتساب
/// العكسيّ «مورّدٌ لنفسه» يحتسب المخرجاتِ ويخصم المدخلاتِ المقابلةَ معاً في الخانة 9</b> (§6.3 · §10.4).</para>
///
/// <para>الأرقامُ محسوبةٌ سلفاً: خدماتُ استشارةٍ من مورّدٍ غيرِ مقيمٍ بـ10٬000 ⟵ ضريبةٌ عكسيّة 1٬500، والمورّدُ يُستحقّ له 10٬000؛
/// ومستندُ تخليصٍ بقيمةٍ جمركيّة 20٬000 ⟵ ضريبةُ استيراد 3٬000، والمخلِّصُ يُستحقّ له 3٬000 وحدَها.</para>
/// </summary>
public class ImportsAndReverseChargeFillBoxesEightAndNineTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _foreignSupplierId;
    private readonly long _brokerId;

    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 3, 31);

    public ImportsAndReverseChargeFillBoxesEightAndNineTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;

        var foreign = Supplier.Create(_companyId, "Consulting GmbH");
        var broker = Supplier.Create(_companyId, "مخلّص جمركي", "312233344400003");
        _db.Suppliers.AddRange(foreign, broker);
        _db.SaveChanges();
        _foreignSupplierId = foreign.Id;
        _brokerId = broker.Id;
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<PurchaseInvoice> ReverseChargeAsync(string reference = "INV-DE-1", decimal net = 10_000m)
    {
        var invoice = PurchaseInvoice.CreateDraft(_companyId, _foreignSupplierId, new DateOnly(2026, 2, 10), reference, true);
        invoice.SetVatTreatment(PurchaseVatTreatment.ReverseCharge);
        invoice.AddLine("خدمات استشارية", 1m, net);
        return await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<PurchaseInvoice> CustomsAsync(string reference = "CD-2026-1", decimal customsValue = 20_000m)
    {
        var invoice = PurchaseInvoice.CreateDraft(_companyId, _brokerId, new DateOnly(2026, 2, 12), reference, true);
        invoice.SetVatTreatment(PurchaseVatTreatment.ImportPaidAtCustoms);
        invoice.AddLine("ضريبة استيراد — البيان الجمركي", 1m, customsValue);
        return await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private Task<VatReturnResult> ReturnAsync() => new VatReturnQuery(_db).ExecuteAsync(_companyId, From, To);

    private static VatReturnBoxLine Box(IReadOnlyList<VatReturnBoxLine>? boxes, int box) =>
        Assert.Single(boxes!, b => b.Box == box);

    private async Task<decimal> BalanceAsync(string code) =>
        await _db.JournalEntries.Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Join(_db.Accounts.Where(a => a.CompanyId == _companyId && a.Code == code), l => l.AccountId, a => a.Id, (l, _) => l)
            .SumAsync(l => l.Debit - l.Credit);

    [Fact]
    public async Task AReverseChargeInvoice_PaysTheSupplierTheNet_AndSelfAccountsTheVatBothWays()
    {
        var invoice = await ReverseChargeAsync();

        Assert.Equal(0m, invoice.VatAmount);
        Assert.Equal(1_500m, invoice.ReverseChargeVatAmount);
        Assert.Equal(-10_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
        Assert.Equal(1_500m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatInput));
        Assert.Equal(-1_500m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
        Assert.Equal(10_000m, (await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _foreignSupplierId)).Balance);
    }

    [Fact]
    public async Task ReverseCharge_FillsBox9_AndLeavesTheNetPayableAndTheLedgerUnchanged()
    {
        await ReverseChargeAsync();

        var r = await ReturnAsync();

        var box9 = Box(r.PurchaseBoxes, VatReturnBox.ImportsUnderReverseCharge);
        Assert.Equal(10_000m, box9.Amount);
        Assert.Equal(1_500m, box9.VatAmount);
        Assert.Equal(0m, Box(r.PurchaseBoxes, VatReturnBox.StandardRatedDomesticPurchases).Amount);
        Assert.Equal(1_500m, r.ReverseChargeVat);
        Assert.Equal(1_500m, r.NetOutputVat);
        Assert.Equal(1_500m, r.NetDeductibleInputVat);
        Assert.Equal(0m, r.NetVatPayable);
        Assert.Equal(0m, r.GlVariance ?? 0m);
        Assert.Equal(r.PurchasesBaseNet, r.PurchaseBoxes!.Sum(b => b.Amount + b.Adjustments));
        Assert.Equal(r.NetDeductibleInputVat, r.Purchases.Sum(b => b.VatAmount));
    }

    [Fact]
    public async Task ACustomsDocument_OwesTheBrokerTheVatOnly_AndFillsBox8WithTheCustomsValue()
    {
        var document = await CustomsAsync();

        Assert.Equal(0m, document.Subtotal);
        Assert.Equal(20_000m, document.ImportCustomsValue);
        Assert.Equal(3_000m, document.VatAmount);
        Assert.Equal(-3_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
        Assert.Equal(3_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatInput));
        Assert.Equal(3_000m, (await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _brokerId)).Balance);

        var r = await ReturnAsync();

        var box8 = Box(r.PurchaseBoxes, VatReturnBox.ImportsPaidAtCustoms);
        Assert.Equal(20_000m, box8.Amount);
        Assert.Equal(3_000m, box8.VatAmount);
        Assert.Equal(0m, Box(r.PurchaseBoxes, VatReturnBox.StandardRatedDomesticPurchases).Amount);
        Assert.Equal(3_000m, r.NetDeductibleInputVat);
        Assert.Equal(-3_000m, r.NetVatPayable);
        Assert.Equal(0m, r.GlVariance ?? 0m);
        Assert.Equal(r.PurchasesBaseNet, r.PurchaseBoxes!.Sum(b => b.Amount + b.Adjustments));
    }

    [Fact]
    public async Task ACancellationInALaterPeriod_AdjustsBox9_AndKeepsTheNetAtZero()
    {
        var invoice = await ReverseChargeAsync();
        await new PurchaseInvoiceCancellationService(_db).CancelAsync(_companyId, invoice.Id, new DateOnly(2026, 4, 5), "admin");

        var april = await new VatReturnQuery(_db).ExecuteAsync(_companyId, new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30));

        var box9 = Box(april.PurchaseBoxes, VatReturnBox.ImportsUnderReverseCharge);
        Assert.Equal(-10_000m, box9.Adjustments);
        Assert.Equal(-1_500m, box9.VatAdjustments);
        Assert.Equal(-1_500m, april.ReverseChargeVat);
        Assert.Equal(0m, april.NetVatPayable);
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
    }

    [Fact]
    public async Task ACustomsDocument_CarryingStockOrForeignCurrency_IsRefused()
    {
        var foreignCurrency = PurchaseInvoice.CreateDraft(_companyId, _brokerId, new DateOnly(2026, 2, 12), "CD-USD", true);
        foreignCurrency.SetCurrency("USD", 3.75m);
        foreignCurrency.SetVatTreatment(PurchaseVatTreatment.ImportPaidAtCustoms);
        foreignCurrency.AddLine("ضريبة استيراد", 1m, 1_000m);

        await Assert.ThrowsAsync<AccountingException>(() => new PurchaseInvoicePostingService(_db).PostAsync(foreignCurrency));
        Assert.False(await _db.PurchaseInvoices.AnyAsync(i => i.Status == PurchaseInvoiceStatus.Posted));
    }

    [Fact]
    public void ANonDeductibleInvoice_CannotBeReverseCharged()
    {
        var invoice = PurchaseInvoice.CreateDraft(_companyId, _foreignSupplierId, new DateOnly(2026, 2, 10), "X-1", false);

        Assert.Throws<AccountingException>(() => invoice.SetVatTreatment(PurchaseVatTreatment.ReverseCharge));
    }

    [Fact]
    public async Task ADebitNote_OnAReverseChargeInvoice_IsRefused()
    {
        var invoice = await ReverseChargeAsync();
        var note = DebitNote.CreateDraft(_companyId, _foreignSupplierId, invoice.Id, new DateOnly(2026, 2, 20), "تخفيض");
        note.AddLine("تخفيض", 1m, 1_000m);

        await Assert.ThrowsAsync<AccountingException>(() => new DebitNotePostingService(_db).PostAsync(note));
    }

    [Fact]
    public async Task ADomesticInvoice_StillFillsBox7_AsBefore()
    {
        var invoice = PurchaseInvoice.CreateDraft(_companyId, _brokerId, new DateOnly(2026, 2, 15), "LOC-1", true);
        invoice.AddLine("مستلزمات", 1m, 2_000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var r = await ReturnAsync();

        Assert.Equal(PurchaseVatTreatment.Domestic, invoice.VatTreatment);
        Assert.Equal(2_000m, Box(r.PurchaseBoxes, VatReturnBox.StandardRatedDomesticPurchases).Amount);
        Assert.Equal(300m, Box(r.PurchaseBoxes, VatReturnBox.StandardRatedDomesticPurchases).VatAmount);
        Assert.Equal(0m, r.ReverseChargeVat);
    }
}
