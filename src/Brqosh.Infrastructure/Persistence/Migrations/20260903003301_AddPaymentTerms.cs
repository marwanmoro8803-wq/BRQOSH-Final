using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTerms : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// ‏مهلةُ السداد للعملاء والموردين.
        ///
        /// <para>‏<b>وصياغةٌ يدويّةٌ بـ<c>IF NOT EXISTS</c></b>: المُرقِّعُ يضيف العمودَ
        /// نفسَه على القواعد القائمة، <b>و<c>AddColumn</c> المُولَّد يرمي على عمودٍ
        /// موجود</b> فتعلق السلسلة — وقد أخرج مسبارُ المخطّط خرقَها من قبل.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE customers ADD COLUMN IF NOT EXISTS ""PaymentTermDays"" INTEGER NOT NULL DEFAULT 0;");
            b.Sql(@"ALTER TABLE suppliers ADD COLUMN IF NOT EXISTS ""PaymentTermDays"" INTEGER NOT NULL DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE customers DROP COLUMN IF EXISTS ""PaymentTermDays"";");
            b.Sql(@"ALTER TABLE suppliers DROP COLUMN IF EXISTS ""PaymentTermDays"";");
        }
    }
}
