using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · <c>sector-modules#11</c>) فاتورةُ المورّد التي سندُ أمر الصيانة — فارغةٌ لكلّ أمرٍ قائم (دُفع نقداً).
    /// ‏ومُطاوِعةٌ للتكرار كأخواتها: المُرقِّعُ قد يكون أضاف العمودَ قبلها.
    /// </summary>
    public partial class AddMaintenanceOrderPurchaseInvoice : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE maintenance_orders ADD COLUMN IF NOT EXISTS "PurchaseInvoiceId" bigint NULL;""");

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE maintenance_orders DROP COLUMN IF EXISTS "PurchaseInvoiceId";""");
    }
}
