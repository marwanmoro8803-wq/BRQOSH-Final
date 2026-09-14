using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (م-٥ · ن-٥١) <b>الفئةُ الضريبيّةُ نصٌّ على أصحاب السطر التسعة جميعاً.</b>
    ///
    /// <para>كان <c>SalesInvoiceLine</c> — نوعٌ واحدٌ يملكه تسعةُ مستندات — يُخزَّن نصّاً عند
    /// سبعةٍ و<b>عدداً صحيحاً</b> عند <c>sales_quotation_lines</c> و<c>purchase_request_lines</c>.
    /// فاستعلامٌ يُكتب لأحد الإخوة يُرمى على الآخر بـ<c>22P02</c>.</para>
    ///
    /// <para><b>ولماذا SQL خامّ لا <c>AlterColumn&lt;string&gt;()</c>:</b> المولَّدُ يُصدر
    /// <c>ALTER … TYPE character varying(16)</c> <b>بلا <c>USING</c></b>، فيكتب PostgreSQL
    /// النصَّ <c>'2'</c> لا <c>'Exempt'</c>. <b>ولا شيءَ يشتكي</b>: مُحوِّلُ EF يقرأ بـ
    /// <c>Enum.Parse</c> الذي يبتلع الرقمَ ويُعيد <c>Exempt</c> — <b>فتصير القيمةُ الواحدةُ
    /// رمزَين</b>، <c>'Exempt'</c> على قاعدةٍ رقّعها المُرقِّعُ و<c>'2'</c> على قاعدةٍ هاجرت.
    /// وهو عطلُ ن-٥١ نفسُه مُعاداً تحت مستوى أيّ بوّابةٍ تقارن الأنواع. فيُكتب
    /// <c>USING (CASE …)</c> صراحةً — وهو المطابقُ لما يفعله
    /// <c>BrqoshSchemaUpdater.EnsureVatCategoryIsTextAsync</c>.</para>
    ///
    /// <para><b>ومُطاوِعةٌ للتكرار</b> على أسلوب <c>AddVatExemptionReason</c>: المُرقِّعُ قد
    /// يكون سبقها على قاعدةٍ قائمة، فالحارسُ على <c>data_type</c> يجعلها لا شيءَ حينئذٍ بدل
    /// أن ترمي وتقف السلسلةُ صامتة.</para>
    ///
    /// <para><b>والأرقامُ مُجمَّدةٌ هنا عمداً</b> — لا مولَّدةٌ من التعداد كما في المُرقِّع.
    /// فالمُرقِّعُ يجعل قاعدةً قديمةً تُطابق النموذجَ <b>الحاليّ</b>، أمّا الهجرةُ فتصف
    /// تحويلاً وقع في <b>لحظةٍ بعينها</b>: لو كسب التعدادُ عضواً غداً وجب ألّا يتغيّر ما
    /// تفعله هذه الهجرةُ بأثرٍ رجعيّ.</para>
    /// </summary>
    public partial class AlterVatCategoryToTextOnQuotationAndRequest : Migration
    {
        private static readonly string[] IntegerLineTables =
        [
            "sales_quotation_lines", "purchase_request_lines",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in IntegerLineTables)
            {
                migrationBuilder.Sql($"""
                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_schema = 'public' AND table_name = '{table}'
                                     AND column_name = 'VatCategory' AND data_type = 'integer') THEN
                            ALTER TABLE "{table}" ALTER COLUMN "VatCategory" DROP DEFAULT;
                            ALTER TABLE "{table}" ALTER COLUMN "VatCategory" TYPE character varying(16)
                                USING (CASE "VatCategory"
                                           WHEN 0 THEN 'Standard'
                                           WHEN 1 THEN 'ZeroRated'
                                           WHEN 2 THEN 'Exempt'
                                           WHEN 3 THEN 'OutOfScope'
                                           ELSE 'Standard' END);
                            ALTER TABLE "{table}" ALTER COLUMN "VatCategory" SET DEFAULT 'Standard';
                        END IF;
                    END $$;
                    """);
            }

            // ‏(م-٥ · ن-٥١) وتوسيعُ رمز سبب الإعفاء على خطّة الفوترة الدوريّة. كان هذا المالكُ
            // ‏وحدَه يحمل `HasMaxLength(32)` فصار عمودُه `character varying(32)` وإخوتُه `text`
            // ‏— **نوعُ CLR واحدٌ ونوعُ تخزينٍ اثنان**. والتوسيعُ لا يرفض بياناً قائماً.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_schema = 'public' AND table_name = 'recurring_invoice_plan_lines'
                                 AND column_name = 'TaxExemptionReasonCode'
                                 AND data_type = 'character varying') THEN
                        ALTER TABLE "recurring_invoice_plan_lines"
                            ALTER COLUMN "TaxExemptionReasonCode" TYPE text;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in IntegerLineTables)
            {
                migrationBuilder.Sql($"""
                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_schema = 'public' AND table_name = '{table}'
                                     AND column_name = 'VatCategory'
                                     AND data_type = 'character varying') THEN
                            ALTER TABLE "{table}" ALTER COLUMN "VatCategory" DROP DEFAULT;
                            ALTER TABLE "{table}" ALTER COLUMN "VatCategory" TYPE integer
                                USING (CASE "VatCategory"
                                           WHEN 'Standard' THEN 0
                                           WHEN 'ZeroRated' THEN 1
                                           WHEN 'Exempt' THEN 2
                                           WHEN 'OutOfScope' THEN 3
                                           ELSE 0 END);
                            ALTER TABLE "{table}" ALTER COLUMN "VatCategory" SET DEFAULT 0;
                        END IF;
                    END $$;
                    """);
            }

            // ‏والتضييقُ في النزول قد يرفض بياناً أطولَ من ٣٢ — وهو الاتّجاهُ الخطر، ولذلك
            // ‏يُقتطع صراحةً بدل أن يرمي: النزولُ استرجاعٌ لا حفظُ بيانات.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_schema = 'public' AND table_name = 'recurring_invoice_plan_lines'
                                 AND column_name = 'TaxExemptionReasonCode' AND data_type = 'text') THEN
                        ALTER TABLE "recurring_invoice_plan_lines"
                            ALTER COLUMN "TaxExemptionReasonCode" TYPE character varying(32)
                            USING left("TaxExemptionReasonCode", 32);
                    END IF;
                END $$;
                """);
        }
    }
}
