using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c>) طريقةُ إهلاك الأصل — **والقائمُ «StraightLine»** لا سلسلةً فارغة (والمولِّدُ
    /// ‏كتبها فارغة). ومُطاوِعةٌ للتكرار كأخواتها: المُرقِّعُ قد يكون أضافها قبلها.
    /// </summary>
    public partial class AddFixedAssetDepreciationMethod : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE fixed_assets ADD COLUMN IF NOT EXISTS "DepreciationMethod" character varying(24) NOT NULL DEFAULT 'StraightLine';""");

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE fixed_assets DROP COLUMN IF EXISTS "DepreciationMethod";""");
    }
}
