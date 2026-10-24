using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(أسئلةُ المالك 2026-10-24) اسمُ المدقِّق/المراجع القانونيّ للمنشأة — يُطبع في تذييل القوائم والكشوف. اختياريّ، ومُطاوِعٌ للتكرار
    /// ‏كأخواتها: المُرقِّعُ قد يكون أضاف العمودَ قبلها.
    /// </summary>
    public partial class AddCompanyAuditorName : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE companies ADD COLUMN IF NOT EXISTS "AuditorName" character varying(120) NULL;""");

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE companies DROP COLUMN IF EXISTS "AuditorName";""");
    }
}
