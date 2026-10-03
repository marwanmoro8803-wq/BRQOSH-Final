using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٩٥ · order-to-cash-procure-to-pay-inventory#13) حدُّ ائتمان العميل — عمودٌ يقبل الفراغ، وفارغُه بلا حدّ
    /// كما كان كلُّ عميلٍ قبله؛ فلا تعبئةَ ولا تغيُّرَ لعميلٍ قائم.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> على سُنّة الهجرات قبلها — <c>BrqoshSchemaUpdater</c> يُضيف العمودَ عند الإقلاع كذلك،
    /// والمولَّدُ (<c>AddColumn</c>) يرمي على ما هو موجود. والنوعُ ما ولّده <c>dotnet ef</c> حرفاً: <c>numeric(18,2)</c> يقبل الفراغ.</para>
    /// </summary>
    public partial class AddCustomerCreditLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE customers ADD COLUMN IF NOT EXISTS "CreditLimit" numeric(18,2) NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE customers DROP COLUMN IF EXISTS "CreditLimit";""");
        }
    }
}
