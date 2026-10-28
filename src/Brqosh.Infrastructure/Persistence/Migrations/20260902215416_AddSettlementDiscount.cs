using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementDiscount : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// ‏خصمُ السداد المبكر على السندين.
        ///
        /// <para>‏<b>والصياغةُ يدويّةٌ بـ<c>IF NOT EXISTS</c> لا مُولَّدة</b>: المُرقِّعُ
        /// اليدويّ (<c>BrqoshSchemaUpdater</c>) يضيف العمودَين أنفسَهما على القواعد
        /// القائمة، <b>و<c>AddColumn</c> المُولَّد يرمي على عمودٍ موجود</b> — فتعلق
        /// السلسلةُ عند هذه الهجرة وتموت كلُّ هجرةٍ بعدها.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE receipt_vouchers ADD COLUMN IF NOT EXISTS ""SettlementDiscount"" NUMERIC(18,2) NOT NULL DEFAULT 0;");
            b.Sql(@"ALTER TABLE payment_vouchers ADD COLUMN IF NOT EXISTS ""SettlementDiscount"" NUMERIC(18,2) NOT NULL DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE receipt_vouchers DROP COLUMN IF EXISTS ""SettlementDiscount"";");
            b.Sql(@"ALTER TABLE payment_vouchers DROP COLUMN IF EXISTS ""SettlementDiscount"";");
        }
    }
}
