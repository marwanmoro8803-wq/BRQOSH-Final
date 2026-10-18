using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#7</c>) تجزئةُ مفتاح استعادة المدير — فارغةٌ للمستخدمين القائمين
    /// ‏حتى يُصدر المديرُ مفتاحاً. ومُطاوِعةٌ للتكرار كأخواتها.
    /// </summary>
    public partial class AddUserRecoveryKey : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "RecoveryKeyHash" character varying(128) NULL;""");

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE app_users DROP COLUMN IF EXISTS "RecoveryKeyHash";""");
    }
}
