using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// مستودعُ الوردية والخطة الدورية — عمودان يقبلان الفراغ، وفارغُهما
    /// المستودعُ الافتراضيّ كما كانت كلُّ وردية وخطة؛ فلا تعبئةَ ولا تغيُّرَ لقائم.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> على سُنّة الهجرات قبلها — <c>BrqoshSchemaUpdater</c> يُضيف العمودين عند الإقلاع كذلك،
    /// والمولَّدُ (<c>AddColumn</c>) يرمي على ما هو موجود. والنوعُ ما ولّده <c>dotnet ef</c> حرفاً: <c>bigint</c> يقبل الفراغ.</para>
    /// </summary>
    public partial class AddPosShiftAndRecurringPlanWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE pos_shifts ADD COLUMN IF NOT EXISTS "WarehouseId" bigint NULL;""");
            migrationBuilder.Sql("""ALTER TABLE recurring_invoice_plans ADD COLUMN IF NOT EXISTS "WarehouseId" bigint NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE recurring_invoice_plans DROP COLUMN IF EXISTS "WarehouseId";""");
            migrationBuilder.Sql("""ALTER TABLE pos_shifts DROP COLUMN IF EXISTS "WarehouseId";""");
        }
    }
}
