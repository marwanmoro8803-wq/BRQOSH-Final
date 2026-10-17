using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٤٧ · <c>manufacturing#4</c>) الصرفُ الفعليّ لأمر الإنتاج والتالفُ عند إكماله.
    ///
    /// <para>‏صفرٌ افتراضاً — فالأمرُ القائمُ بلا صرفٍ ويُكمَل بالوصفة كما كان. ومُطاوِعةٌ للتكرار كأخواتها.</para>
    /// </summary>
    public partial class AddWorkOrderActualIssues : Migration
    {
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "IssuedMaterialCost" numeric(18,2) NOT NULL DEFAULT 0;""");
            b.Sql("""ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "IssueCount" integer NOT NULL DEFAULT 0;""");
            b.Sql("""ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "ScrapQuantity" numeric(18,4) NOT NULL DEFAULT 0;""");
        }

        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE work_orders DROP COLUMN IF EXISTS "ScrapQuantity";""");
            b.Sql("""ALTER TABLE work_orders DROP COLUMN IF EXISTS "IssueCount";""");
            b.Sql("""ALTER TABLE work_orders DROP COLUMN IF EXISTS "IssuedMaterialCost";""");
        }
    }
}
