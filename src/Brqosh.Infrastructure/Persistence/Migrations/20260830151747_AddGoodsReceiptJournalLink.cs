using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ربطُ إذن الاستلام بقيد استحقاقه — مرساةُ عكسه عند الإلغاء.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار:</b> <c>BrqoshSchemaUpdater</c> يُضيف العمودَ قبل تشغيل
    /// الهجرات لإصلاح القواعد القائمة، فـ<c>AddColumn</c> تصطدم بعمودٍ موجود وترمي
    /// <c>42701</c> — ويبتلعه <c>IsIgnorableSchemaConflict</c> فتقف السلسلةُ صامتةً.</para>
    /// </summary>
    public partial class AddGoodsReceiptJournalLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """ALTER TABLE "goods_receipts" ADD COLUMN IF NOT EXISTS "JournalEntryId" bigint;""");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """ALTER TABLE "goods_receipts" DROP COLUMN IF EXISTS "JournalEntryId";""");
    }
}
