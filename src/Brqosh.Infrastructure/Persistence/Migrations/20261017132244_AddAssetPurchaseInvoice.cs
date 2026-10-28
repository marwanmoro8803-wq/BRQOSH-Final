using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏ شراءُ الأصل من مورّدٍ بفاتورته: الفاتورةُ تُسمّي أصلَها،
    /// ‏والأصلُ ينتظر فاتورتَه. ‏فارغٌ وخطأٌ للقائم — فلا يتغيّر معنى فاتورةٍ أو أصلٍ واحد. ‏ومُطاوِعةٌ للتكرار كأخواتها.
    /// </summary>
    public partial class AddAssetPurchaseInvoice : Migration
    {
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE purchase_invoices ADD COLUMN IF NOT EXISTS "FixedAssetId" bigint NULL;""");
            b.Sql("""ALTER TABLE fixed_assets ADD COLUMN IF NOT EXISTS "AwaitingInvoice" boolean NOT NULL DEFAULT false;""");
        }

        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE fixed_assets DROP COLUMN IF EXISTS "AwaitingInvoice";""");
            b.Sql("""ALTER TABLE purchase_invoices DROP COLUMN IF EXISTS "FixedAssetId";""");
        }
    }
}
