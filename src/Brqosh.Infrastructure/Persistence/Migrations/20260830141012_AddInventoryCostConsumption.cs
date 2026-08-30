using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-229 · م0-٣) سجلُّ استهلاك طبقات التكلفة — مفتاحُ ترميمها عند الإلغاء.
    ///
    /// <para><b>ومُطاوِعةٌ للتكرار</b> كسائر هجرات هذه الحملة: <c>BrqoshSchemaUpdater</c>
    /// يُنشئ الجداولَ قبل تشغيل الهجرات لإصلاح القواعد القديمة، فـ<c>CreateTable</c> تصطدم
    /// بجدولٍ موجود وترمي <c>42P07</c> — ويبتلعه <c>IsIgnorableSchemaConflict</c> فتقف
    /// السلسلةُ صامتةً. وهو عطلٌ كشفه <c>SchemaCoverageProbe</c> وحده في م2-١٣.</para>
    /// </summary>
    public partial class AddInventoryCostConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS inventory_cost_consumptions (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "CostLayerId" BIGINT NOT NULL,
                    "ProductId" BIGINT NOT NULL,
                    "WarehouseId" BIGINT NOT NULL,
                    "SourceDocument" character varying(128) NOT NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL,
                    "UnitCost" NUMERIC(18,4) NOT NULL,
                    "ConsumedAtUtc" timestamp with time zone NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_inventory_cost_consumptions_CompanyId_SourceDocument"
                    ON inventory_cost_consumptions ("CompanyId", "SourceDocument");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_inventory_cost_consumptions_CostLayerId"
                    ON inventory_cost_consumptions ("CostLayerId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("DROP TABLE IF EXISTS inventory_cost_consumptions;");
    }
}
