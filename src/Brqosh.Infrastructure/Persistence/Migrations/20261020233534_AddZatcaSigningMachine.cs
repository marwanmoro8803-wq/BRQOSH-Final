using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · <c>tax-zatca#16</c>) الجهازُ الذي يحمل مفتاحَ الختم — فارغٌ لإعدادٍ قائم (لا يُعرف جهازُه، فلا يُسأل عنه)،
    /// ‏ومُطاوِعةٌ للتكرار كأخواتها.
    /// </summary>
    public partial class AddZatcaSigningMachine : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE zatca_settings ADD COLUMN IF NOT EXISTS "SigningMachineId" character varying(128) NOT NULL DEFAULT '';""");

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE zatca_settings DROP COLUMN IF EXISTS "SigningMachineId";""");
    }
}
