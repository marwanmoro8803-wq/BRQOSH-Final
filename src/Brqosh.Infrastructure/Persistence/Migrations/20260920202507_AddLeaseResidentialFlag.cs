using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// أسكنيٌّ عقدُ الإيجار أم تجاريّ — والفرقُ ضريبةٌ لا وصف.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — على سابقة <c>AddLineAndDocumentDiscount</c>:
    /// <c>BrqoshSchemaUpdater</c> يسبق الهجراتِ على قواعد العملاء القديمة، فـ<c>AddColumn</c>
    /// المولَّدة تصطدم بعمودٍ موجودٍ أصلاً وتُسقط الترقية.</para>
    ///
    /// <para><b>والافتراضُ «تجاريّ» عمداً</b>: هو سلوكُ ما مضى حرفاً — كلُّ إيجارٍ كان
    /// يُفوتَر بـ١٥٪ — <b>فلا يتغيّر إقرارٌ لعقدٍ قائمٍ بهذه الترقية</b>. والتحوّلُ إلى
    /// الإعفاء قرارُ مكلَّفٍ يُتَّخذ على كلّ عقدٍ لا يُفترض له، <b>وإعفاءٌ يُمنح بالخطأ
    /// نقصٌ في إقرارٍ سُلّم للهيئة</b>.</para>
    /// </summary>
    public partial class AddLeaseResidentialFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE "lease_contracts" ADD COLUMN IF NOT EXISTS "IsResidential" boolean NOT NULL DEFAULT FALSE;""");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b) =>
            b.Sql("""ALTER TABLE "lease_contracts" DROP COLUMN IF EXISTS "IsResidential";""");
    }
}
