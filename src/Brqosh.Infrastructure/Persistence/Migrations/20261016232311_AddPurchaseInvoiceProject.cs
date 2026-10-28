using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏ المشروعُ المُحمَّلُ عليه فاتورةُ المشتريات.
    ///
    /// <para>‏عمودٌ فارغٌ افتراضاً — فالفواتيرُ القائمةُ بلا مشروعٍ كما كانت. ومُطاوِعةٌ للتكرار كأخواتها:
    /// ‏<c>BrqoshSchemaUpdater</c> يُضيف العمودَ نفسَه في تنصيباتٍ لا تمرّ بالهجرات.</para>
    /// </summary>
    public partial class AddPurchaseInvoiceProject : Migration
    {
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE purchase_invoices ADD COLUMN IF NOT EXISTS "ProjectId" bigint NULL;""");
        }

        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE purchase_invoices DROP COLUMN IF EXISTS "ProjectId";""");
        }
    }
}
