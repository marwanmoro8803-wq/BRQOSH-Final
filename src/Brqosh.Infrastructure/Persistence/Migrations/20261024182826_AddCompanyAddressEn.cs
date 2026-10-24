using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏عنوانُ المنشأة بالإنجليزيّة لترويسة المطبوعات الإنجليزيّة — اختياريّ، ومُطاوِعٌ للتكرار كأخواتها (المُرقِّعُ قد يسبقها).
    /// </summary>
    public partial class AddCompanyAddressEn : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE companies ADD COLUMN IF NOT EXISTS "AddressEn" character varying(512) NULL;""");

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE companies DROP COLUMN IF EXISTS "AddressEn";""");
    }
}
