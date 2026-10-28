using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏ المشروعُ الذي يعمل عليه الموظّف — فتُحمَّل عليه تكلفتُه في الرواتب.
    ///
    /// <para>‏فارغٌ افتراضاً، فالموظّفون القائمون على مصروف الرواتب كما كانوا. ومُطاوِعةٌ للتكرار كأخواتها.</para>
    /// </summary>
    public partial class AddEmployeeProject : Migration
    {
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE employees ADD COLUMN IF NOT EXISTS "ProjectId" bigint NULL;""");
        }

        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE employees DROP COLUMN IF EXISTS "ProjectId";""");
        }
    }
}
