using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٦) خصمُ المستند على الإشعار الدائن.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — على سابقة <c>AddLineAndDocumentDiscount</c>:
    /// <c>BrqoshSchemaUpdater</c> يسبق الهجراتِ على قواعد العملاء القديمة، فـ<c>AddColumn</c>
    /// المولَّدة تصطدم بعمودٍ موجودٍ أصلاً وتُسقط الترقية. <b>والسطرُ الخامُّ بـ
    /// <c>IF NOT EXISTS</c> يعبر الحالتين.</b></para>
    ///
    /// <para><b>والافتراضُ صفر</b>: إشعارٌ قائمٌ لا يتغيّر إجماليُّه ولا وعاؤه الضريبيّ بهذه
    /// الترقية — <b>ولو تغيّر لتغيّرت إقراراتٌ سُلّمت للهيئة</b>.</para>
    /// </summary>
    public partial class AddCreditNoteDocumentDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE "credit_notes" ADD COLUMN IF NOT EXISTS "DocumentDiscountAmount" numeric(18,2) NOT NULL DEFAULT 0;""");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE "credit_notes" DROP COLUMN IF EXISTS "DocumentDiscountAmount";""");
    }
}
