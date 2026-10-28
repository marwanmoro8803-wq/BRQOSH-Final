using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// تكاليفُ التحويل على قائمة الموادّ — الأجرُ المباشرُ
    /// ونسبةُ تحميل التكاليف الصناعيّة.
    ///
    /// <para><b>ومعيارُ المخزون يقضي بها</b>: تكلفةُ المنتَج التامّ = الموادُّ + تكاليفُ
    /// التحويل. <b>وكانت الموادَّ وحدَها</b>، فالتامُّ يدخل المخزونَ بأقلَّ من كلفته
    /// <b>وتخرج تكلفةُ المبيعات ناقصةً في كلّ بيعةٍ لاحقة</b>.</para>
    ///
    /// <para><b>ودقّةُ الأجر <c>(18,2)</c> كدقّةِ كلّ مالٍ في هذا المخطَّط</b> — وأوّلُ
    /// صياغةٍ كتبته <c>(18,4)</c>، <b>فأسقطها <c>SchemaSqlGuard</c></b>: قائمةُ الأنواع
    /// المسموحة لا تحمله، <b>وتوسيعُ قائمةِ سماحٍ أمنيّةٍ لأجل عمودٍ واحدٍ ثمنٌ لا يُدفع</b>
    /// حين يكفي أن يتبع العمودُ اصطلاحَ إخوته. والنسبةُ <c>(9,4)</c> كنسبةِ خصم السطر.</para>
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — <c>BrqoshSchemaUpdater</c> يسبق الهجراتِ على قواعد
    /// العملاء، فـ<c>AddColumn</c> المولَّدة تصطدم بعمودٍ موجودٍ وتُسقط الترقية.</para>
    ///
    /// <para><b>والافتراضُ صفرٌ في العمودين</b>: قائمةُ موادٍّ قائمةٌ <b>لا تتغيّر تكلفةُ
    /// أمرٍ يُنتَج منها</b> بهذه الترقية — والتحميلُ قرارٌ يُضبط على كلّ قائمةٍ لا يُفترض لها.</para>
    /// </summary>
    public partial class AddBomConversionCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "bills_of_materials" ADD COLUMN IF NOT EXISTS "LabourCostPerUnit" numeric(18,2) NOT NULL DEFAULT 0;""");
            b.Sql("""ALTER TABLE "bills_of_materials" ADD COLUMN IF NOT EXISTS "OverheadRatePercent" numeric(9,4) NOT NULL DEFAULT 0;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "bills_of_materials" DROP COLUMN IF EXISTS "OverheadRatePercent";""");
            b.Sql("""ALTER TABLE "bills_of_materials" DROP COLUMN IF EXISTS "LabourCostPerUnit";""");
        }
    }
}
