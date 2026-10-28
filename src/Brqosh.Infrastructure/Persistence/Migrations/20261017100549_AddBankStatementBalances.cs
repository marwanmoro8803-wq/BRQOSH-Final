using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏ رصيدا الكشف البنكيّ كما يقولهما البنك.
    ///
    /// <para>‏فارغان للكشوف القائمة — لا يُختلق لها رصيدٌ لم يُستورد. ومُطاوِعةٌ للتكرار كأخواتها.</para>
    /// </summary>
    public partial class AddBankStatementBalances : Migration
    {
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE bank_statements ADD COLUMN IF NOT EXISTS "OpeningBalance" numeric(18,2) NULL;""");
            b.Sql("""ALTER TABLE bank_statements ADD COLUMN IF NOT EXISTS "ClosingBalance" numeric(18,2) NULL;""");
        }

        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE bank_statements DROP COLUMN IF EXISTS "ClosingBalance";""");
            b.Sql("""ALTER TABLE bank_statements DROP COLUMN IF EXISTS "OpeningBalance";""");
        }
    }
}
