using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٢) وسمُ أنّ قيدَ تحصيل الدفع عند الاستلام وقع فعلاً.
    ///
    /// <para><b>وهو غيرُ <c>CodCollected</c></b>: ذاك يُضبط بالتسليم بلا قيد، وهذا يقول
    /// إنّ النقدَ دخل الدفترَ و<c>2114</c> دُوِّن. <b>وبغيره ثقبان</b>: تحصيلٌ يُقيَّد
    /// مرّتين، وتوريدٌ يسبق التحصيل فيُخرج التزامَ <c>2114</c> مديناً.</para>
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — <c>BrqoshSchemaUpdater</c> يسبق الهجراتِ على قواعد
    /// العملاء، فـ<c>AddColumn</c> المولَّدة تصطدم بعمودٍ موجودٍ وتُسقط الترقية.</para>
    ///
    /// <para><b>والافتراضُ <c>FALSE</c></b>: بوالصُ قائمةٌ <b>لا يُدّعى أنّ قيدَها وقع</b>.
    /// وأثرُه أنّ توريدَها يُرفض حتى يُقيَّد تحصيلُها — <b>وهو الرفضُ الصحيح</b>: لا يُعرف
    /// من البيانات وحدَها أيُّها قُيِّد، <b>وتوريدٌ بلا قيدٍ هو العطلُ نفسُه</b>.</para>
    /// </summary>
    public partial class AddCodCollectionPostedFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE "shipments" ADD COLUMN IF NOT EXISTS "CodCollectionPosted" boolean NOT NULL DEFAULT FALSE;""");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE "shipments" DROP COLUMN IF EXISTS "CodCollectionPosted";""");
    }
}
