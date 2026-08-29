using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-229 · م1-١ب الشقّ الثاني) رمزُ سبب الإعفاء ونصُّه على سطور المستندات.
    ///
    /// <para><b>ولماذا SQL خامّ لا <c>AddColumn</c>:</b> <c>BrqoshSchemaUpdater</c> يُضيف أعمدةً
    /// قبل تشغيل الهجرات لإصلاح القواعد القديمة، فتصطدم الهجرةُ بعمودٍ موجودٍ وترمي
    /// <c>42701</c> — ويبتلعه <c>IsIgnorableSchemaConflict</c> فتقف السلسلةُ صامتةً. وهو عطلٌ
    /// كشفه <c>SchemaCoverageProbe</c> وحده في م2-١٣. فتُكتب الهجرةُ **مُطاوِعة للتكرار**.</para>
    /// </summary>
    public partial class AddVatExemptionReason : Migration
    {
        private static readonly string[] LineTables =
        [
            "sales_invoice_lines", "credit_note_lines", "debit_note_lines",
            "purchase_invoice_lines", "purchase_order_lines", "purchase_request_lines",
            "sales_order_lines", "sales_quotation_lines",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in LineTables)
            {
                migrationBuilder.Sql(
                    $"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "TaxExemptionReasonCode" text;""");
                migrationBuilder.Sql(
                    $"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "TaxExemptionReason" text;""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in LineTables)
            {
                migrationBuilder.Sql(
                    $"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "TaxExemptionReasonCode";""");
                migrationBuilder.Sql(
                    $"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "TaxExemptionReason";""");
            }
        }
    }
}
