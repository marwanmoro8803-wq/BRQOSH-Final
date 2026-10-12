using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Retail;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٣ · ‏`ui-ux-i18n-printing#8`) <b>إيصالُ نقطة البيع فاتورةٌ ضريبيّةٌ مبسّطةٌ تُطبع — بحقولها.</b>
///
/// <para>‏كانت نافذةُ الإيصال تعرض الاسمَ ورقمَ الفاتورة والمبالغَ والرمز <b>وتُغلق فقط</b>: لا رقمَ تسجيل البائع ولا تاريخَ
/// ‏ولا وقتَ ولا أصناف، ولا طباعة. و«طباعة آخر إيصال» تُخرج PDF بمقاس A4 <b>لآخر فاتورة نقطة بيعٍ من أيّ وردية</b>.
/// ‏والإيصالُ هنا نموذجٌ يُبنى من الفاتورة والمنشأة ومستند الهيئة (<see cref="PosReceiptBuilder"/>) ويُقاس — والشاشةُ تطبعه.</para>
/// </summary>
public class APosReceiptCarriesTheSimplifiedInvoiceFieldsTests
{
    public APosReceiptCarriesTheSimplifiedInvoiceFieldsTests() => TestActor.Cashier();

    [Fact]
    public async Task The_receipt_carries_seller_vat_number_time_items_totals_and_qr()
    {
        await WithShopAsync(async (db, companyId, productId) =>
        {
            var shift = await OpenShiftAsync(db, companyId);
            var sale = await new PosSaleService(db).SellAsync(
                companyId, shift.Id, null, [(productId, 2m)], true, PosPaymentMethod.Cash, "admin");
            var company = await db.Companies.SingleAsync(c => c.Id == companyId);

            var receipt = PosReceiptBuilder.Build(sale.Invoice, company, sale.ZatcaDocument);

            Assert.Equal(company.NameAr, receipt.SellerName);
            Assert.Equal(company.VatRegistrationNumber, receipt.SellerVatNumber);
            Assert.Equal(sale.Invoice.InvoiceNumber, receipt.InvoiceNumber);
            Assert.NotEqual(default, receipt.IssuedAt);
            var line = Assert.Single(receipt.Lines);
            Assert.Equal(2m, line.Quantity);
            Assert.Equal(100m, line.UnitPrice);
            Assert.Equal(200m, line.LineTotal);
            Assert.Equal(200m, receipt.Subtotal);
            Assert.Equal(30m, receipt.VatAmount);
            Assert.Equal(230m, receipt.Total);
            Assert.Equal(sale.ZatcaDocument!.QrTlvBase64, receipt.PrintedQr);
        });
    }

    /// <summary>‏و«آخرُ إيصال» من الوردية المسؤولة عنه — لا آخرُ فاتورة نقطة بيعٍ في المنشأة كلِّها.</summary>
    [Fact]
    public async Task The_last_receipt_is_the_last_sale_of_the_given_shift()
    {
        await WithShopAsync(async (db, companyId, productId) =>
        {
            var mine = await OpenShiftAsync(db, companyId);
            var mySale = await new PosSaleService(db).SellAsync(
                companyId, mine.Id, null, [(productId, 1m)], true, PosPaymentMethod.Cash, "admin");

            var other = await OpenShiftAsync(db, companyId);
            await new PosSaleService(db).SellAsync(
                companyId, other.Id, null, [(productId, 3m)], true, PosPaymentMethod.Cash, "admin");

            var receipt = await PosReceiptBuilder.LastOfShiftAsync(db, companyId, mine.Id);

            Assert.NotNull(receipt);
            Assert.Equal(mySale.Invoice.InvoiceNumber, receipt!.InvoiceNumber);
            Assert.Equal(1m, receipt.Lines.Single().Quantity);
        });
    }

    /// <summary>شاهدُ ضبط: ورديةٌ بلا بيعٍ لا إيصالَ لها — لا إيصالُ ورديةٍ أخرى.</summary>
    [Fact]
    public async Task A_shift_without_sales_has_no_last_receipt()
    {
        await WithShopAsync(async (db, companyId, productId) =>
        {
            var busy = await OpenShiftAsync(db, companyId);
            await new PosSaleService(db).SellAsync(
                companyId, busy.Id, null, [(productId, 1m)], true, PosPaymentMethod.Cash, "admin");
            var idle = await OpenShiftAsync(db, companyId);

            Assert.Null(await PosReceiptBuilder.LastOfShiftAsync(db, companyId, idle.Id));
        });
    }

    private static async Task<PosShift> OpenShiftAsync(BrqoshDbContext db, long companyId)
    {
        var shift = PosShift.Open(companyId, "كاشير", 100m);
        db.PosShifts.Add(shift);
        await db.SaveChangesAsync();
        return shift;
    }

    private static async Task WithShopAsync(Func<BrqoshDbContext, long, long, Task> body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-receipt-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}");
            var company = await new DatabaseInitializer(db).InitializeAsync("متجر", "300000000000003", BusinessProfile.Retail);
            await new BrqoshSchemaUpdater(db).ApplyAsync();

            await ZatcaReadySeller.CompleteAsync(db, company.Id);
            var settings = await db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
            settings.Update(true, ZatcaEnvironment.Simulation, "",
                ZatcaReadySeller.Certificate.CertPath, ZatcaReadySeller.Certificate.KeyPath, "");
            await db.SaveChangesAsync();

            var product = Domain.Inventory.Product.Create(company.Id, "P1", "منتج", "قطعة",
                Domain.Inventory.ProductType.Stockable, 100m);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var wh = await db.Warehouses.SingleAsync(w => w.CompanyId == company.Id);
            var opening = Domain.Inventory.StockMovement.CreateDraft(
                company.Id, DateOnly.FromDateTime(DateTime.Today), Domain.Inventory.StockMovementType.Opening,
                wh.Id, "رصيد افتتاحي");
            opening.AddLine(product.Id, 50m, 40m);
            await new Infrastructure.Inventory.StockMovementPostingService(db).PostAsync(opening);

            await body(db, company.Id, product.Id);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
