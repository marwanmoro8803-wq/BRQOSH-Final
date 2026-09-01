using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-230 · ط1-٤) ما حُرِّر من ذمّة العميل بعملة الدفاتر.
    ///
    /// <para><b>ولا تعبئةَ فيها عمداً</b>: <c>0</c> يعني «سندٌ سابقٌ لهذه الدفعة»، ويُقرأ
    /// <c>Amount</c> مكانَه في الدفتر المساعد — وهو الصوابُ لكلّ سندٍ قديم، إذ كان كلُّ
    /// سندٍ بالريال بالضرورة. <b>وتعبئةٌ بـ<c>Amount</c> تُنتج النتيجةَ نفسَها وتُخفي
    /// التمييزَ بين القديم والجديد</b>.</para>
    /// </summary>
    public partial class AddReceivableRelieved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE "receipt_vouchers" ADD COLUMN IF NOT EXISTS "ReceivableRelieved" numeric(18,2) NOT NULL DEFAULT 0;""");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE "receipt_vouchers" DROP COLUMN IF EXISTS "ReceivableRelieved";""");
    }
}
