using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EntryHash",
                table: "audit_log",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviousHash",
                table: "audit_log",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "audit_log",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // ‏(CEEF-229) ترقيمُ الصفوف القائمة **قبل** الفهرس الفريد.
            //
            // ‏العمودُ أعلاه يُضاف بصفرٍ لكلّ صفٍّ قائم، والفهرسُ أدناه فريدٌ على
            // ‏(CompanyId, Sequence) — فمنشأةٌ لها سطرا تدقيقٍ اثنان يتصادمان، وتسقط
            // ‏الترقيةُ كلُّها بـ 23505. وسجلُّ التدقيق يُكتب عند كلّ ترحيلٍ وإغلاقِ فترة،
            // ‏فقاعدةُ أيّ عميلٍ عمل يوماً واحداً فيها عشراتُ الصفوف.
            //
            // ‏ولم يظهر هذا في أيّ قياسٍ سابق لأنّ كلّ ما نُرحّله **قواعدُ فارغةٌ من
            // ‏البيانات**: لا صفوفَ تتصادم. قِيس بمرحلةٍ رابعة في SchemaCoverageProbe.
            //
            // ‏**ولماذا سالبة؟** لأنّ الترقيم بالموجب يصطدم بقاعدةٍ رقّعها المُرقِّع: هناك
            // ‏تُضاف الأعمدةُ بلا فهرس، فتبقى الصفوفُ القديمة أصفاراً **بينما يبدأ ما بعدها
            // ‏من ١**. فالسالبُ يفصل الحقبتين فصلاً لا يتصادم: ما قبل السلسلة في
            // ‏[-N..-1]، والسلسلةُ نفسُها من ١ فصاعداً. والترتيبُ الطبيعيّ يضع القديمَ أوّلاً.
            //
            // ‏وتبقى EntryHash فارغةً: **هي علامةُ «ما قبل السلسلة»** لا الترتيب. والمتحقّقُ
            // ‏يقرؤها فيُعفي هذه الصفوف من فحص الارتباط، ويشترط أن تسبق أوّلَ صفٍّ مربوط
            // ‏**بمفتاحها الأوّليّ أيضاً** — فصفٌّ يُدسّ اليوم مفتاحُه أعلى، فيُكشف.
            migrationBuilder.Sql("""
                UPDATE audit_log AS a
                SET "Sequence" = -n.rn
                FROM (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "Id" DESC) AS rn
                    FROM audit_log
                    WHERE "EntryHash" = ''
                ) AS n
                WHERE a."Id" = n."Id" AND a."EntryHash" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_CompanyId_Sequence",
                table: "audit_log",
                columns: new[] { "CompanyId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_log_CompanyId_Sequence",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "EntryHash",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "PreviousHash",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "audit_log");
        }
    }
}
