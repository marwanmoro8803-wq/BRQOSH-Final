using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentFreightInvoice : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// (CEEF-232 · ل-١) ‏ربطُ فاتورةِ أجرة الشحن بالبوليصة.
        ///
        /// <para>‏<b>وصياغةٌ يدويّةٌ بـ<c>IF NOT EXISTS</c></b> — على سُنّة
        /// <c>AddJournalCurrencyMemoAndEntityLevel</c>: المُرقِّعُ اليدويّ
        /// (<c>BrqoshSchemaUpdater</c>) يُضيف هذا العمودَ عند الإقلاع كذلك،
        /// <b>و<c>AddColumn</c> المولَّدُ يرمي على ما هو موجود</b> فتعلق سلسلةُ الهجرات
        /// على قواعدَ قائمةٍ رُقِّعت مسبقاً.</para>
        ///
        /// <para>‏<b>ولاغٍ افتراضاً</b> — فشحناتٌ سُلّمت قبل اليوم <b>تبقى غيرَ مفوترة</b>
        /// وتظهر في أوّل تشغيلٍ للفوترة. <b>وقيمةٌ افتراضيّةٌ غيرُ لاغيةٍ كذبٌ على الماضي</b>:
        /// تجعل أجوراً لم تُفوتَر قطُّ تبدو مفوترة، <b>فيضيع الإيرادُ بلا أثرٍ يدلّ عليه</b>.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE shipments ADD COLUMN IF NOT EXISTS ""FreightInvoiceId"" BIGINT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE shipments DROP COLUMN IF EXISTS ""FreightInvoiceId"";");
        }
    }
}
