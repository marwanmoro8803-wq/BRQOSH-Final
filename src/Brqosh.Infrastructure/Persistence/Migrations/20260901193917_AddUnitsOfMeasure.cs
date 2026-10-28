using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// وحداتُ القياس وتحويلُها.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يسبقها على قواعد العملاء.</para>
    ///
    /// <para><b>والجدولُ يُنشأ فارغاً هنا عمداً</b>: البذرُ في <c>DatabaseInitializer</c>
    /// للمنشآت الجديدة. <b>وفارغٌ يعني «بلا تحويل»</b> — وهو سلوكُ ما مضى حرفاً بحرف،
    /// فلا يتغيّر رقمٌ في قاعدةٍ قائمة حتى تُعرِّف المنشأةُ وحداتِها بنفسها.</para>
    /// </summary>
    public partial class AddUnitsOfMeasure : Migration
    {
        private static readonly string[] LineTables =
        [
            "sales_invoice_lines", "sales_order_lines", "sales_quotation_lines",
            "credit_note_lines", "debit_note_lines",
            "purchase_invoice_lines", "purchase_order_lines", "purchase_request_lines",
            "goods_receipt_lines",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""
                CREATE TABLE IF NOT EXISTS units_of_measure (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Code" character varying(16) NOT NULL,
                    "NameAr" character varying(64) NOT NULL,
                    "UnCode" character varying(8) NOT NULL,
                    "BaseCode" character varying(16) NOT NULL,
                    "Factor" NUMERIC(18,6) NOT NULL DEFAULT 1,
                    "IsActive" BOOLEAN NOT NULL DEFAULT true
                );
                """);

            b.Sql("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_units_of_measure_CompanyId_Code" ON units_of_measure ("CompanyId", "Code");""");

            foreach (var table in LineTables)
                b.Sql($"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "UnitCode" character varying(16);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            foreach (var table in LineTables)
                b.Sql($"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "UnitCode";""");

            b.Sql("""DROP TABLE IF EXISTS units_of_measure;""");
        }
    }
}
