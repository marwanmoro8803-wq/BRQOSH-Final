using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Retail;

public class PosZatcaTests
{
    public PosZatcaTests() => TestActor.Cashier();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز

    [Fact]
    public async Task Pos_sale_generates_and_submits_zatca_in_simulation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-pos-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}");
            var company = await new DatabaseInitializer(db).InitializeAsync("متجر", "300000000000003", BusinessProfile.Retail);
            await new BrqoshSchemaUpdater(db).ApplyAsync();

            var settings = await db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
            settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
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

            var shift = PosShift.Open(company.Id, "كاشير", 100m);
            db.PosShifts.Add(shift);
            await db.SaveChangesAsync();

            var result = await new PosSaleService(db).SellAsync(
                company.Id, shift.Id, null, [(product.Id, 2m)], true, PosPaymentMethod.Cash, "admin");

            Assert.NotNull(result.Invoice.InvoiceNumber);
            Assert.NotNull(result.ZatcaDocument);
            // (CEEF-229 · م0-١٠) كان يؤكّد Reported — أي «مُبلَّغ للهيئة» — على مستند لم يغادر الجهاز.
            // الاختبار كان يُثبّت العطل: أيّ إصلاح يقول الحقيقة كان سيُسقطه.
            Assert.Equal(ZatcaComplianceStatus.SimulatedLocally, result.ZatcaDocument!.ComplianceStatus);
            Assert.False(result.ZatcaDocument.ComplianceStatus.IsAcknowledgedByAuthority());
            Assert.False(string.IsNullOrEmpty(result.ZatcaDocument.QrTlvBase64));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
