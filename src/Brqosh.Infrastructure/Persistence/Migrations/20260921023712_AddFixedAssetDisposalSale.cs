using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// بيعُ الأصل الثابت على سجلّ الأصل: المشتري ورقمُ فاتورته
    /// ومقابلُه وفئتُه الضريبيّةُ وسببُها.
    ///
    /// <para><b>وبيعُ أصلٍ من أصول النشاط توريدٌ خاضعٌ نظاماً</b> — وكان الاستبعادُ يُقيّد
    /// ضريبةَ المخرجات على <c>2106</c> <b>بلا فاتورةِ ضريبةٍ ولا مستندٍ إلكترونيٍّ ولا مشترٍ
    /// معروف</b>. والضريبةُ تبلغ الإقرارَ منذ <c>CEEF-232 · ض-١</c>، <b>فالنقصُ ليس في
    /// الإفصاح بل في المستند</b>: توريدٌ خاضعٌ لا تعرفه الهيئةُ ولا يحمل رقماً.</para>
    ///
    /// <para><b>ولِمَ عمودٌ للفئة وآخرُ للمبلغ:</b> صفرُ الضريبة <b>يحتمل ثلاثةَ أحوالٍ
    /// مختلفةٍ في الإقرار</b> — تصديرٌ صفريّ، ومعفى، وخارجُ النطاق — <b>فلا تُشتقّ الفئةُ
    /// من المبلغ بعدَه</b>. والمقابلُ لم يكن محفوظاً في حقلٍ قطّ: سؤالُ المراجع «بكم بيع
    /// هذا الأصل؟» جوابُه <b>سردُ قيدٍ</b> لا رقمٌ في سجلّ الأصل.</para>
    ///
    /// <para><b>والأنواعُ أنواعُ قائمة السماح في <c>SchemaSqlGuard</c></b> — و<c>(18,2)</c>
    /// دقّةُ كلّ مالٍ في هذا المخطَّط. <b>ولا يُوسَّع إذنٌ أمنيٌّ لأجل عمود</b>؛ وذاك
    /// درسُ ح-١٣ حين اختُرعت <c>(18,4)</c> بلا سبب فأسقطت ستّاً وثلاثين اختباراً.</para>
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — <c>BrqoshSchemaUpdater</c> يسبق الهجراتِ على قواعد
    /// العملاء، فـ<c>AddColumn</c> المولَّدة تصطدم بعمودٍ موجودٍ وتُسقط الترقية.</para>
    /// </summary>
    public partial class AddFixedAssetDisposalSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "fixed_assets" ADD COLUMN IF NOT EXISTS "DisposalBuyerCustomerId" bigint NULL;""");
            b.Sql("""ALTER TABLE "fixed_assets" ADD COLUMN IF NOT EXISTS "DisposalInvoiceNumber" character varying(64) NULL;""");
            b.Sql("""ALTER TABLE "fixed_assets" ADD COLUMN IF NOT EXISTS "DisposalProceeds" numeric(18,2) NULL;""");
            b.Sql("""ALTER TABLE "fixed_assets" ADD COLUMN IF NOT EXISTS "DisposalVatCategory" character varying(16) NULL;""");
            b.Sql("""ALTER TABLE "fixed_assets" ADD COLUMN IF NOT EXISTS "DisposalVatExemptionCode" character varying(64) NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "fixed_assets" DROP COLUMN IF EXISTS "DisposalVatExemptionCode";""");
            b.Sql("""ALTER TABLE "fixed_assets" DROP COLUMN IF EXISTS "DisposalVatCategory";""");
            b.Sql("""ALTER TABLE "fixed_assets" DROP COLUMN IF EXISTS "DisposalProceeds";""");
            b.Sql("""ALTER TABLE "fixed_assets" DROP COLUMN IF EXISTS "DisposalInvoiceNumber";""");
            b.Sql("""ALTER TABLE "fixed_assets" DROP COLUMN IF EXISTS "DisposalBuyerCustomerId";""");
        }
    }
}
