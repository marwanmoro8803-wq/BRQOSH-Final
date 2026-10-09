using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١١٠ · order-to-cash-procure-to-pay-inventory#12) مستودعُ فاتورة المبيعات — عمودٌ يقبل الفراغ، وفارغُه المستودعُ
    /// الافتراضيّ كما كانت كلُّ فاتورةٍ قبله؛ فلا تعبئةَ ولا تغيُّرَ لفاتورةٍ قائمة.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> على سُنّة الهجرات قبلها — <c>BrqoshSchemaUpdater</c> يُضيف العمودَ عند الإقلاع كذلك،
    /// والمولَّدُ (<c>AddColumn</c>) يرمي على ما هو موجود. والنوعُ ما ولّده <c>dotnet ef</c> حرفاً: <c>bigint</c> يقبل الفراغ.</para>
    /// </summary>
    public partial class AddSalesInvoiceWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE sales_invoices ADD COLUMN IF NOT EXISTS "WarehouseId" bigint NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE sales_invoices DROP COLUMN IF EXISTS "WarehouseId";""");
        }
    }
}
