using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏ معاملةُ ضريبة فاتورة الشراء — **والقائمةُ «Domestic»** لا سلسلةً فارغةً (والمولِّدُ كتبها
    /// ‏فارغة، فيقرأ EF قيمةً ليست في التعداد)، والمبلغان صفرٌ. ومُطاوِعةٌ للتكرار كأخواتها: المُرقِّعُ قد يكون أضافها قبلها.
    /// </summary>
    public partial class AddPurchaseVatTreatment : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""
                ALTER TABLE purchase_invoices ADD COLUMN IF NOT EXISTS "VatTreatment" character varying(24) NOT NULL DEFAULT 'Domestic';
                ALTER TABLE purchase_invoices ADD COLUMN IF NOT EXISTS "ReverseChargeVatAmount" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE purchase_invoices ADD COLUMN IF NOT EXISTS "ImportCustomsValue" numeric(18,2) NOT NULL DEFAULT 0;
                """);

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""
                ALTER TABLE purchase_invoices DROP COLUMN IF EXISTS "VatTreatment";
                ALTER TABLE purchase_invoices DROP COLUMN IF EXISTS "ReverseChargeVatAmount";
                ALTER TABLE purchase_invoices DROP COLUMN IF EXISTS "ImportCustomsValue";
                """);
    }
}
