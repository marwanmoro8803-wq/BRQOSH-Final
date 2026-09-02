using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAdvances : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// (CEEF-230 · ط2-٥) ‏سُلَفُ الموظفين وقسطُها المُستقطَع.
        ///
        /// <para>‏<b>وصياغةٌ يدويّةٌ بـ<c>IF NOT EXISTS</c></b>: المُرقِّعُ اليدويّ يُنشئ
        /// الجدولَ ويُضيف العمود، <b>والمُولَّدُ يرمي على ما هو موجود</b> فتعلق السلسلة.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE payroll_lines ADD COLUMN IF NOT EXISTS ""AdvanceDeduction"" NUMERIC(18,2) NOT NULL DEFAULT 0;");

            b.Sql(@"
                CREATE TABLE IF NOT EXISTS employee_advances (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""BranchId"" BIGINT NULL,
                    ""EmployeeId"" BIGINT NOT NULL,
                    ""AdvanceNumber"" character varying(32) NOT NULL,
                    ""RequestDate"" DATE NOT NULL,
                    ""DisbursedOn"" DATE NULL,
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""InstallmentAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""Status"" character varying(16) NOT NULL DEFAULT 'Approved',
                    ""Reason"" character varying(256) NULL,
                    ""JournalEntryId"" BIGINT NULL,
                    ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_employee_advances_CompanyId_AdvanceNumber"" ON employee_advances (""CompanyId"", ""AdvanceNumber"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_employee_advances_CompanyId_EmployeeId_Status"" ON employee_advances (""CompanyId"", ""EmployeeId"", ""Status"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP TABLE IF EXISTS employee_advances;");
            b.Sql(@"ALTER TABLE payroll_lines DROP COLUMN IF EXISTS ""AdvanceDeduction"";");
        }
    }
}
