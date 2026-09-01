using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-230 · ط1-٢) خصمُ السطر وخصمُ المستند.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يسبقها على قواعد العملاء، فـ<c>AddColumn</c>
    /// المولَّدة تصطدم بعمودٍ موجود. وثمانيةُ جداولٍ للنسبة الواحدة لأنّ
    /// <c>SalesInvoiceLine</c> سجلٌّ مشترك.</para>
    ///
    /// <para><b>والافتراضُ صفر</b>: فاتورةٌ قائمةٌ لا يتغيّر إجماليُّها ولا وعاؤها الضريبيّ
    /// بهذه الترقية — <b>ولو تغيّر لتغيّرت إقراراتٌ سُلّمت للهيئة</b>.</para>
    /// </summary>
    public partial class AddLineAndDocumentDiscount : Migration
    {
        private static readonly string[] LineTables =
        [
            "sales_invoice_lines", "sales_order_lines", "sales_quotation_lines",
            "credit_note_lines", "debit_note_lines",
            "purchase_invoice_lines", "purchase_order_lines", "purchase_request_lines",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            foreach (var table in LineTables)
                b.Sql($"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "DiscountPercent" numeric(9,4) NOT NULL DEFAULT 0;""");

            b.Sql("""ALTER TABLE "sales_invoices" ADD COLUMN IF NOT EXISTS "DocumentDiscountAmount" numeric(18,2) NOT NULL DEFAULT 0;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE "sales_invoices" DROP COLUMN IF EXISTS "DocumentDiscountAmount";""");

            foreach (var table in LineTables)
                b.Sql($"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "DiscountPercent";""");
        }
    }
}
