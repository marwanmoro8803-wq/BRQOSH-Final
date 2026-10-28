using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// عملةُ المستند وسعرُ صرفها ومبالغُها الأجنبية.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يسبقها على قواعد العملاء.</para>
    ///
    /// <para><b>والافتراضُ ريالٌ بسعرِ واحد</b>: فاتورةٌ قائمةٌ لا يتغيّر إجماليُّها ولا
    /// قيدُها ولا بصمتُها بهذه الترقية.</para>
    ///
    /// <para><b>وتعبئةُ المبالغ الأجنبية من الريال ليست اختراعاً</b>: بسعرِ صرفٍ واحدٍ
    /// هما رقمٌ واحد. <b>وتركُها صفراً يجعل الفوترةَ تُصدر مستنداً بإجماليٍّ صفر</b> —
    /// وهو الضررُ الحقيقيّ. والتعبئةُ محصورةٌ بما ليس صفراً أصلاً فلا تُكرَّر.</para>
    /// </summary>
    public partial class AddDocumentCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "sales_invoices" ADD COLUMN IF NOT EXISTS "CurrencyCode" character varying(3) NOT NULL DEFAULT 'SAR';""");
            b.Sql("""ALTER TABLE "sales_invoices" ADD COLUMN IF NOT EXISTS "ExchangeRate" numeric(18,6) NOT NULL DEFAULT 1;""");
            b.Sql("""ALTER TABLE "sales_invoices" ADD COLUMN IF NOT EXISTS "ForeignSubtotal" numeric(18,2) NOT NULL DEFAULT 0;""");
            b.Sql("""ALTER TABLE "sales_invoices" ADD COLUMN IF NOT EXISTS "ForeignVatAmount" numeric(18,2) NOT NULL DEFAULT 0;""");

            b.Sql("""
                UPDATE sales_invoices
                   SET "ForeignSubtotal" = "Subtotal", "ForeignVatAmount" = "VatAmount"
                 WHERE "ForeignSubtotal" = 0 AND "Subtotal" <> 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "sales_invoices" DROP COLUMN IF EXISTS "ForeignVatAmount";""");
            b.Sql("""ALTER TABLE "sales_invoices" DROP COLUMN IF EXISTS "ForeignSubtotal";""");
            b.Sql("""ALTER TABLE "sales_invoices" DROP COLUMN IF EXISTS "ExchangeRate";""");
            b.Sql("""ALTER TABLE "sales_invoices" DROP COLUMN IF EXISTS "CurrencyCode";""");
        }
    }
}
