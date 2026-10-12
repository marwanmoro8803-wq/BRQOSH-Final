using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(تقويةُ الـAPI · S-5) آخرُ خطوة TOTP قُبلت لكلّ مستخدم — فلا يُقبل الرمزُ نفسُه مرّتين.
    ///
    /// <para>‏<b>مُطاوِعةٌ للتكرار</b> كأخواتها: المُرقِّعُ (<c>BrqoshSchemaUpdater</c>) يُضيف العمودَ على القواعد التي أُنشئت قبل
    /// سجلّ الهجرات، والمولَّدُ (<c>AddColumn</c>) يرمي على ما هو موجود. والنوعُ ما ولّده <c>dotnet ef</c> حرفاً.</para>
    /// </summary>
    public partial class AddMfaLastAcceptedStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "MfaLastAcceptedStep" bigint NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE app_users DROP COLUMN IF EXISTS "MfaLastAcceptedStep";""");
        }
    }
}
