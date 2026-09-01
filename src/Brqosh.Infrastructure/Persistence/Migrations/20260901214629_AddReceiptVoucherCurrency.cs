using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-230 · ط1-٤) عملةُ سند القبض وسعرُ صرفها.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يسبقها. <b>والافتراضُ ريالٌ بسعرِ واحد</b>،
    /// فسندٌ قائمٌ لا يتغيّر قيدُه ولا مبلغُه.</para>
    /// </summary>
    public partial class AddReceiptVoucherCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "receipt_vouchers" ADD COLUMN IF NOT EXISTS "CurrencyCode" character varying(3) NOT NULL DEFAULT 'SAR';""");
            b.Sql("""ALTER TABLE "receipt_vouchers" ADD COLUMN IF NOT EXISTS "ExchangeRate" numeric(18,6) NOT NULL DEFAULT 1;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "receipt_vouchers" DROP COLUMN IF EXISTS "ExchangeRate";""");
            b.Sql("""ALTER TABLE "receipt_vouchers" DROP COLUMN IF EXISTS "CurrencyCode";""");
        }
    }
}
