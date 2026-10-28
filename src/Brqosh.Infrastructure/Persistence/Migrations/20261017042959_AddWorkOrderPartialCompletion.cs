using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏ دفعاتُ إكمال أمر الإنتاج، وما نُقل من موادّه المصروفة إلى التامّ.
    ///
    /// <para>‏صفرٌ افتراضاً — فالأمرُ المكتملُ القائمُ دفعتُه الأولى بالمفتاحين القديمين ولا دفعةَ بعدها. ومُطاوِعةٌ للتكرار كأخواتها.</para>
    /// </summary>
    public partial class AddWorkOrderPartialCompletion : Migration
    {
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "CompletionCount" integer NOT NULL DEFAULT 0;""");
            b.Sql("""ALTER TABLE work_orders ADD COLUMN IF NOT EXISTS "MaterialCostRelieved" numeric(18,2) NOT NULL DEFAULT 0;""");
        }

        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE work_orders DROP COLUMN IF EXISTS "MaterialCostRelieved";""");
            b.Sql("""ALTER TABLE work_orders DROP COLUMN IF EXISTS "CompletionCount";""");
        }
    }
}
