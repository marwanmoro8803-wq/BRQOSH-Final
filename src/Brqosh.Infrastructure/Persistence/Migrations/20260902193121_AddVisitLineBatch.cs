using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitLineBatch : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// (CEEF-231 · ط3-٢ج) ‏رقمُ دفعة الدواء المصروف في الزيارة.
        ///
        /// <para>‏<b>والصياغةُ يدويّةٌ بـ<c>IF NOT EXISTS</c> لا مُولَّدة</b>: المُرقِّعُ
        /// اليدويّ (<c>BrqoshSchemaUpdater</c>) يضيف العمودَ نفسَه على القواعد القائمة،
        /// <b>و<c>AddColumn</c> المُولَّد يرمي على عمودٍ موجود</b> — فتعلق السلسلةُ عند
        /// هذه الهجرة وتموت كلُّ هجرةٍ بعدها. <b>وهو الدرسُ نفسُه الذي أخرجه مسبارُ
        /// المخطّط في <c>AddMenuSaleProduct</c>.</b></para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE visit_lines ADD COLUMN IF NOT EXISTS ""BatchNumber"" character varying(64) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE visit_lines DROP COLUMN IF EXISTS ""BatchNumber"";");
        }
    }
}
