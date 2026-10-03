using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٩٧ · manufacturing#7) مستودعا أمر الإنتاج — عمودان يقبلان الفراغ، وفارغُهما المستودعُ الافتراضيّ
    /// كما كان كلُّ أمرٍ قبلهما؛ فلا تعبئةَ ولا تغيُّرَ لأمرٍ قائم.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> على سُنّة الهجرات قبلها — <c>BrqoshSchemaUpdater</c> يُضيف العمودين عند الإقلاع كذلك،
    /// والمولَّدُ (<c>AddColumn</c>) يرمي على ما هو موجود. والنوعُ ما ولّده <c>dotnet ef</c> حرفاً: <c>bigint</c> يقبل الفراغ.</para>
    /// </summary>
    public partial class AddWorkOrderWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "MaterialsWarehouseId" bigint NULL;""");
            migrationBuilder.Sql("""ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "OutputWarehouseId" bigint NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE work_orders DROP COLUMN IF EXISTS "MaterialsWarehouseId";""");
            migrationBuilder.Sql("""ALTER TABLE work_orders DROP COLUMN IF EXISTS "OutputWarehouseId";""");
        }
    }
}
