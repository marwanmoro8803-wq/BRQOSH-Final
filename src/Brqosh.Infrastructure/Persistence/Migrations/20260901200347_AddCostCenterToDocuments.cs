using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// مركزُ التكلفة على المستندات، وفهرسُه على سطر القيد.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يسبقها على قواعد العملاء.</para>
    ///
    /// <para><b>ولا مفتاحَ أجنبيّ</b>: مركزُ التكلفة يُوقَف حذفاً ناعماً، ومفتاحٌ أجنبيٌّ
    /// إمّا يمنع إيقافَ مركزٍ عليه حركةٌ تاريخية أو يترك المرجعَ معلّقاً بلا معنى.
    /// والتحقّقُ في زمن الترحيل هو الطبقةُ الصحيحة.</para>
    /// </summary>
    public partial class AddCostCenterToDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            foreach (var table in new[] { "purchase_invoices", "payment_vouchers" })
                b.Sql($"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "CostCenterId" bigint;""");

            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_journal_entry_lines_CostCenterId" ON journal_entry_lines ("CostCenterId");""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""DROP INDEX IF EXISTS "IX_journal_entry_lines_CostCenterId";""");

            foreach (var table in new[] { "purchase_invoices", "payment_vouchers" })
                b.Sql($"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "CostCenterId";""");
        }
    }
}
