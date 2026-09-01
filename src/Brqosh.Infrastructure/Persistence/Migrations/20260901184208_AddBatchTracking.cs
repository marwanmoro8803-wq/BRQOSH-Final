using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-230 · ط1-٣) تتبّعُ الدفعة والصلاحية والرقم التسلسليّ.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار — ولذلك كُتب DDL بيدٍ بدل ما ولّده الأداة.</b>
    /// <c>BrqoshSchemaUpdater</c> يُنشئ الجدولَ والأعمدةَ <b>قبل</b> تشغيل الهجرات لإصلاح
    /// القواعد القائمة، فـ<c>AddColumn</c> المولَّدة تصطدم بعمودٍ موجود وترمي <c>42701</c>،
    /// ويبتلعه <c>IsIgnorableSchemaConflict</c> فتقف السلسلةُ صامتةً. وهو درسُ
    /// <c>AddGoodsReceiptJournalLink</c> بعينه.</para>
    ///
    /// <para><b>وثمانيةُ جداولٍ للعمود الواحد:</b> <c>SalesInvoiceLine</c> سجلٌّ مشتركٌ بين
    /// ثمانية مستندات. ونسيتُ منها أربعةً فسقط الإقلاعُ بـ<c>42703</c> — وسمّاها مسبارُ
    /// المخطّط في ثانية.</para>
    ///
    /// <para><b>ولا تعبئةَ فيها ولا قيدَ يُفرض على بياناتٍ قائمة</b>: كلُّ عمودٍ يقبل العدم
    /// أو له افتراض. فتمرّ على قاعدةٍ فيها ملايينُ الصفوف — وهو ما تقيسه المرحلةُ السادسة.</para>
    /// </summary>
    public partial class AddBatchTracking : Migration
    {
        private static readonly string[] LineTables =
        [
            "sales_invoice_lines", "sales_order_lines", "sales_quotation_lines",
            "credit_note_lines", "debit_note_lines",
            "purchase_invoice_lines", "purchase_order_lines", "purchase_request_lines",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""
                CREATE TABLE IF NOT EXISTS stock_batches (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "WarehouseId" BIGINT NOT NULL,
                    "ProductId" BIGINT NOT NULL,
                    "BatchNumber" character varying(64) NOT NULL,
                    "ExpiryDate" DATE NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """);

            b.Sql("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_stock_batches_CompanyId_WarehouseId_ProductId_BatchNumber" ON stock_batches ("CompanyId", "WarehouseId", "ProductId", "BatchNumber");""");
            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_stock_batches_CompanyId_ProductId_ExpiryDate" ON stock_batches ("CompanyId", "ProductId", "ExpiryDate");""");
            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_stock_batches_CompanyId_BranchId" ON stock_batches ("CompanyId", "BranchId");""");

            b.Sql("""ALTER TABLE "products" ADD COLUMN IF NOT EXISTS "Tracking" character varying(16) NOT NULL DEFAULT 'None';""");

            foreach (var table in LineTables)
                b.Sql($"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "BatchNumber" character varying(64);""");

            foreach (var table in new[] { "goods_receipt_lines", "stock_movement_lines" })
            {
                b.Sql($"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "BatchNumber" character varying(64);""");
                b.Sql($"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "ExpiryDate" date;""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            foreach (var table in new[] { "goods_receipt_lines", "stock_movement_lines" })
            {
                b.Sql($"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "ExpiryDate";""");
                b.Sql($"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "BatchNumber";""");
            }

            foreach (var table in LineTables)
                b.Sql($"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "BatchNumber";""");

            b.Sql("""ALTER TABLE "products" DROP COLUMN IF EXISTS "Tracking";""");
            b.Sql("""DROP TABLE IF EXISTS stock_batches;""");
        }
    }
}
