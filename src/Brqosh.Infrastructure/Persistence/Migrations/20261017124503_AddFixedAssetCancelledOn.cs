using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٥٧ · <c>treasury-payroll-assets#10</c>) تاريخُ إلغاء اقتناء الأصل — فارغٌ للأصول القائمة.
    /// ‏ومُطاوِعةٌ للتكرار كأخواتها.
    /// </summary>
    public partial class AddFixedAssetCancelledOn : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE fixed_assets ADD COLUMN IF NOT EXISTS "CancelledOn" date NULL;""");

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE fixed_assets DROP COLUMN IF EXISTS "CancelledOn";""");
    }
}
