using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٣٥) عملةُ الإشعار الدائن — نظيرُ أعمدة الفاتورة.
    ///
    /// <para><b>وكان الإشعارُ بلا عملةٍ أصلاً</b>، وسطورُ الفاتورة الأجنبيّة مُسعَّرةٌ
    /// بعملتها — <b>فنسخُ سعرِ السطر يجعل الدولارَ ريالاً</b>. وقِيس: فاتورةُ
    /// <c>USD 1,000</c> بسعر <c>3.75</c> (‏٤٬٣١٢٫٥٠ ريالاً) <b>ومرتجعٌ كاملٌ يردّ ١٬١٥٠</b>
    /// — فيبقى على العميل ٣٬١٦٢٫٥٠ وقد ردّ كلَّ شيء.</para>
    ///
    /// <para><b>وسعرُ الصرف يُفتتح بـ<c>1</c> لا بصفر</b> — <b>وهذا ليس تجميلاً</b>:
    /// <c>RecalculateTotals</c> تشتقّ الريالَ بالضرب في السعر، <b>فصفرٌ يُصيّر كلَّ
    /// إشعارٍ قائمٍ صفراً عند أوّل مسّ</b>. والمولِّدُ كتب <c>defaultValue: 0m</c> لأنّه
    /// لا يرى مُهيِّئ الحقل في الصنف (<c>= 1m</c>)، <b>فصُحّح بيد</b>.</para>
    ///
    /// <para><b>وتعبئةُ الأجنبيّ من الريال لكلّ إشعارٍ قائم</b> — بسعرِ واحدٍ هما رقمٌ
    /// واحد، <b>وتركُ الأجنبيّ صفراً يُفقد مبلغاً قائماً</b> عند أوّل إعادة حساب.
    /// <b>وتعبئةٌ بقيمةٍ مساويةٍ لا تُغيّر معنى.</b></para>
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — <c>BrqoshSchemaUpdater</c> يسبق الهجراتِ على
    /// قواعد العملاء، فـ<c>AddColumn</c> المولَّدة تصطدم بعمودٍ موجودٍ وتُسقط الترقية.</para>
    /// </summary>
    public partial class AddCreditNoteCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "credit_notes" ADD COLUMN IF NOT EXISTS "CurrencyCode" character varying(3) NOT NULL DEFAULT 'SAR';""");
            b.Sql("""ALTER TABLE "credit_notes" ADD COLUMN IF NOT EXISTS "ExchangeRate" numeric(18,6) NOT NULL DEFAULT 1;""");
            b.Sql("""ALTER TABLE "credit_notes" ADD COLUMN IF NOT EXISTS "ForeignSubtotal" numeric(18,2) NOT NULL DEFAULT 0;""");
            b.Sql("""ALTER TABLE "credit_notes" ADD COLUMN IF NOT EXISTS "ForeignVatAmount" numeric(18,2) NOT NULL DEFAULT 0;""");

            b.Sql("""UPDATE "credit_notes" SET "ForeignSubtotal" = "Subtotal", "ForeignVatAmount" = "VatAmount" WHERE "ForeignSubtotal" = 0 AND "Subtotal" <> 0;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "credit_notes" DROP COLUMN IF EXISTS "ForeignVatAmount";""");
            b.Sql("""ALTER TABLE "credit_notes" DROP COLUMN IF EXISTS "ForeignSubtotal";""");
            b.Sql("""ALTER TABLE "credit_notes" DROP COLUMN IF EXISTS "ExchangeRate";""");
            b.Sql("""ALTER TABLE "credit_notes" DROP COLUMN IF EXISTS "CurrencyCode";""");
        }
    }
}
