using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏ التسويةُ البنكيّةُ لكلّ سطرٍ لا لكلّ قيد.
    ///
    /// <para>‏السجلّاتُ القائمةُ تأخذ صفراً — تسويةُ القيد كلِّه كما كانت — فلا يتغيّر معنى سجلٍّ واحد. ومُطاوِعةٌ للتكرار كأخواتها.</para>
    /// <para>‏<b>والرجوعُ يطوي سجلّاتِ السطور إلى سجلٍّ للقيد</b> قبل أن يعود الفريدُ القديم — وإلا رفض قيداً سُوّي سطراه؛
    /// ‏فقيدٌ سُوّي بعضُ سطوره يعود مُسوًّى كلُّه، وهو معنى ما قبل الهجرة.</para>
    /// </summary>
    public partial class AddReconciledBankLine : Migration
    {
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE reconciled_bank_entries ADD COLUMN IF NOT EXISTS "JournalLineId" bigint NOT NULL DEFAULT 0;""");
            b.Sql("""DROP INDEX IF EXISTS "IX_reconciled_bank_entries_CompanyId_BankAccountId_JournalEntr~";""");
            b.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_reconciled_bank_entries_line"
                    ON reconciled_bank_entries ("CompanyId", "BankAccountId", "JournalEntryId", "JournalLineId");
                """);
        }

        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""DROP INDEX IF EXISTS "IX_reconciled_bank_entries_line";""");
            b.Sql("""
                DELETE FROM reconciled_bank_entries r
                WHERE EXISTS (
                    SELECT 1 FROM reconciled_bank_entries k
                    WHERE k."CompanyId" = r."CompanyId" AND k."BankAccountId" = r."BankAccountId"
                      AND k."JournalEntryId" = r."JournalEntryId" AND k."Id" < r."Id");
                """);
            b.Sql("""ALTER TABLE reconciled_bank_entries DROP COLUMN IF EXISTS "JournalLineId";""");
            b.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_reconciled_bank_entries_CompanyId_BankAccountId_JournalEntr~"
                    ON reconciled_bank_entries ("CompanyId", "BankAccountId", "JournalEntryId");
                """);
        }
    }
}
