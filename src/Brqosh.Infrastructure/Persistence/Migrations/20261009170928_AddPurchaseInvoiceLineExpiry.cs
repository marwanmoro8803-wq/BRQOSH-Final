using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// انتهاءُ الدفعة المشتراة على سطر فاتورة الشراء المباشرة — عمودٌ
    /// يقبل الفراغ، وفارغُه سطرٌ بلا انتهاء كما كان كلُّ سطرٍ قبله؛ فلا تعبئةَ ولا تغيُّرَ لفاتورةٍ قائمة.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> على سُنّة الهجرات قبلها — <c>BrqoshSchemaUpdater</c> يُضيف العمودَ عند الإقلاع كذلك،
    /// والمولَّدُ (<c>AddColumn</c>) يرمي على ما هو موجود. والنوعُ ما ولّده <c>dotnet ef</c> حرفاً: <c>date</c> يقبل الفراغ.</para>
    /// </summary>
    public partial class AddPurchaseInvoiceLineExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE purchase_invoice_lines ADD COLUMN IF NOT EXISTS "ExpiryDate" date NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE purchase_invoice_lines DROP COLUMN IF EXISTS "ExpiryDate";""");
        }
    }
}
