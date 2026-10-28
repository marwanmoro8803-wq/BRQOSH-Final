using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuSaleProduct : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// ‏ربطُ صنفِ القائمة بصنفِ البيع.
        ///
        /// <para>‏<b>والصياغةُ يدويّةٌ بـ<c>IF NOT EXISTS</c> لا مُولَّدة</b>: المُرقِّعُ
        /// اليدويّ يضيف العمودَ نفسَه على القواعد القائمة، <b>و<c>AddColumn</c> المُولَّد
        /// يرمي على عمودٍ موجود</b> — فتعلق السلسلةُ عند هذه الهجرة وتموت كلُّ هجرةٍ
        /// بعدها. <b>وهذا ما كشفه مسبارُ المخطط</b> قبل أن يصل إلى قاعدةِ عميل.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE menu_items ADD COLUMN IF NOT EXISTS ""SaleProductId"" BIGINT NULL;");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_menu_items_CompanyId_SaleProductId"" ON menu_items (""CompanyId"", ""SaleProductId"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP INDEX IF EXISTS ""IX_menu_items_CompanyId_SaleProductId"";");
            b.Sql(@"ALTER TABLE menu_items DROP COLUMN IF EXISTS ""SaleProductId"";");
        }
    }
}
