using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// عملةُ المشتريات والسداد.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يُضيف الأعمدةَ قبل تشغيل الهجرات.</para>
    ///
    /// <para><b>والافتراضُ ريالٌ بسعرِ واحد</b>، فلا يتغيّر رقمٌ في فاتورةٍ قائمة.
    /// <b>والأساسُ الأجنبيّ يُملأ من الرياليّ</b>: بسعرِ واحدٍ هما رقمٌ واحد، وتركُه
    /// صفراً يجعل كلَّ فاتورةٍ قديمةٍ تبدو بلا قيمةٍ بعملتها.</para>
    /// </summary>
    public partial class AddPayableCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE purchase_invoices ADD COLUMN IF NOT EXISTS ""CurrencyCode"" character varying(3) NOT NULL DEFAULT 'SAR';");
            b.Sql(@"ALTER TABLE purchase_invoices ADD COLUMN IF NOT EXISTS ""ExchangeRate"" NUMERIC(18,6) NOT NULL DEFAULT 1;");
            b.Sql(@"ALTER TABLE purchase_invoices ADD COLUMN IF NOT EXISTS ""ForeignSubtotal"" NUMERIC(18,2) NOT NULL DEFAULT 0;");
            b.Sql(@"ALTER TABLE purchase_invoices ADD COLUMN IF NOT EXISTS ""ForeignVatAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0;");

            b.Sql(@"ALTER TABLE payment_vouchers ADD COLUMN IF NOT EXISTS ""CurrencyCode"" character varying(3) NOT NULL DEFAULT 'SAR';");
            b.Sql(@"ALTER TABLE payment_vouchers ADD COLUMN IF NOT EXISTS ""ExchangeRate"" NUMERIC(18,6) NOT NULL DEFAULT 1;");
            b.Sql(@"ALTER TABLE payment_vouchers ADD COLUMN IF NOT EXISTS ""PayableRelieved"" NUMERIC(18,2) NOT NULL DEFAULT 0;");

            b.Sql(@"
                UPDATE purchase_invoices
                   SET ""ForeignSubtotal"" = ""Subtotal"", ""ForeignVatAmount"" = ""VatAmount""
                 WHERE ""ForeignSubtotal"" = 0 AND ""Subtotal"" <> 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE purchase_invoices DROP COLUMN IF EXISTS ""ForeignVatAmount"";");
            b.Sql(@"ALTER TABLE purchase_invoices DROP COLUMN IF EXISTS ""ForeignSubtotal"";");
            b.Sql(@"ALTER TABLE purchase_invoices DROP COLUMN IF EXISTS ""ExchangeRate"";");
            b.Sql(@"ALTER TABLE purchase_invoices DROP COLUMN IF EXISTS ""CurrencyCode"";");

            b.Sql(@"ALTER TABLE payment_vouchers DROP COLUMN IF EXISTS ""PayableRelieved"";");
            b.Sql(@"ALTER TABLE payment_vouchers DROP COLUMN IF EXISTS ""ExchangeRate"";");
            b.Sql(@"ALTER TABLE payment_vouchers DROP COLUMN IF EXISTS ""CurrencyCode"";");
        }
    }
}
