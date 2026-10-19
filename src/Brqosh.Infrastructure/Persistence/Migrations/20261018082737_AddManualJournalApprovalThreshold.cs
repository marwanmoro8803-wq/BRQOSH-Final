using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · <c>accounting-core#6</c>) حدُّ اعتماد القيد اليدويّ — **والمنشآتُ القائمةُ تأخذ افتراضَ المنتج ١٠٬٠٠٠**،
    /// ‏لا صفرَ يُخضع كلَّ قيدٍ يدويٍّ للاعتماد فجأة (والمولِّدُ كتب صفراً). ومُطاوِعةٌ للتكرار كأخواتها.
    /// </summary>
    public partial class AddManualJournalApprovalThreshold : Migration
    {
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE companies ADD COLUMN IF NOT EXISTS "ManualJournalApprovalThreshold" numeric(18,2) NOT NULL DEFAULT 10000;""");

        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE companies DROP COLUMN IF EXISTS "ManualJournalApprovalThreshold";""");
    }
}
