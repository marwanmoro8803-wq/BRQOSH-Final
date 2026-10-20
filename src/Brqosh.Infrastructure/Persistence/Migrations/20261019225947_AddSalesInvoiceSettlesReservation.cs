using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · <c>sector-modules#6</c>) الحجزُ الذي تُصفّيه فاتورةُ المغادرة — فارغٌ لكلّ فاتورةٍ قائمة.
    /// ‏ومُطاوِعةٌ للتكرار كأخواتها: المُرقِّعُ قد يكون أضاف العمودَ قبلها.
    /// </summary>
    public partial class AddSalesInvoiceSettlesReservation : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE sales_invoices ADD COLUMN IF NOT EXISTS "SettlesReservationId" bigint NULL;""");

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE sales_invoices DROP COLUMN IF EXISTS "SettlesReservationId";""");
    }
}
