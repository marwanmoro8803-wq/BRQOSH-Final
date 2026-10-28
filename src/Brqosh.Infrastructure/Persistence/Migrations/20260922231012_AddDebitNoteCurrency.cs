using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// عملةُ الإشعار المدين — مرآةُ ح-٣٥ في المشتريات.
    ///
    /// <para><b>وكان الإشعارُ المدينُ بلا عملةٍ أصلاً</b>، وسطورُ فاتورة الشراء الأجنبيّة
    /// مُسعَّرةٌ بعملتها. وقِيس: شراءٌ بـ<c>USD 1,000</c> بسعر <c>3.75</c> (‏٤٬٣١٢٫٥٠)
    /// <b>ومرتجعٌ كاملٌ يُنقص المستحقَّ للمورّد ١٬١٥٠ فقط</b> — فتبقى الدفاترُ تقول إنّا
    /// مدينون بـ٣٬١٦٢٫٥٠ وقد أعدنا كلَّ شيء.</para>
    ///
    /// <para><b>وسعرُ الصرف يُفتتح بـ<c>1</c> لا بصفر</b>: المولِّدُ كتب
    /// <c>defaultValue: 0m</c> ثانيةً — <b>وهو عينُ ما سُجّل في ح-٣٥</b> — لأنّه يقرأ ربطَ
    /// العمود ولا يرى مُهيِّئ الصنف (<c>= 1m</c>). <b>وصفرٌ يُصيّر كلَّ إشعارٍ قائمٍ صفراً
    /// عند أوّل إعادة حساب</b>، إذ الريالُ يُشتقّ بالضرب. فكُتبت بيد.</para>
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — <c>BrqoshSchemaUpdater</c> يسبق الهجراتِ على قواعد
    /// العملاء، فـ<c>AddColumn</c> المولَّدة تصطدم بعمودٍ موجودٍ وتُسقط الترقية.</para>
    /// </summary>
    public partial class AddDebitNoteCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "debit_notes" ADD COLUMN IF NOT EXISTS "CurrencyCode" character varying(3) NOT NULL DEFAULT 'SAR';""");
            b.Sql("""ALTER TABLE "debit_notes" ADD COLUMN IF NOT EXISTS "ExchangeRate" numeric(18,6) NOT NULL DEFAULT 1;""");
            b.Sql("""ALTER TABLE "debit_notes" ADD COLUMN IF NOT EXISTS "ForeignSubtotal" numeric(18,2) NOT NULL DEFAULT 0;""");
            b.Sql("""ALTER TABLE "debit_notes" ADD COLUMN IF NOT EXISTS "ForeignVatAmount" numeric(18,2) NOT NULL DEFAULT 0;""");

            b.Sql("""UPDATE "debit_notes" SET "ForeignSubtotal" = "Subtotal", "ForeignVatAmount" = "VatAmount" WHERE "ForeignSubtotal" = 0 AND "Subtotal" <> 0;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "debit_notes" DROP COLUMN IF EXISTS "ForeignVatAmount";""");
            b.Sql("""ALTER TABLE "debit_notes" DROP COLUMN IF EXISTS "ForeignSubtotal";""");
            b.Sql("""ALTER TABLE "debit_notes" DROP COLUMN IF EXISTS "ExchangeRate";""");
            b.Sql("""ALTER TABLE "debit_notes" DROP COLUMN IF EXISTS "CurrencyCode";""");
        }
    }
}
