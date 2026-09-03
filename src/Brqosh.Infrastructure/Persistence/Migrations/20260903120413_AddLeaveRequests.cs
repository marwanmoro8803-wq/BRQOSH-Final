using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveRequests : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// (CEEF-233 · ز) ‏جدولُ طلبات الإجازة، وخصمُ الغياب على سطر الراتب.
        ///
        /// <para>‏<b>وصياغةٌ يدويّةٌ بـ<c>IF NOT EXISTS</c></b> — على سُنّة الهجرات قبلها:
        /// المُرقِّعُ اليدويّ (<c>BrqoshSchemaUpdater</c>) يُنشئ هذا الجدولَ ويُضيف العمودَ
        /// عند الإقلاع كذلك، <b>والمولَّدُ يرمي على ما هو موجود</b> فتعلق سلسلةُ الهجرات
        /// على قواعدَ رُقِّعت مسبقاً.</para>
        ///
        /// <para>‏<b>و<c>AbsenceDeduction</c> صفرٌ افتراضاً</b> — فدوراتُ رواتبَ سابقةٌ
        /// لم يكن فيها غيابٌ مُسجَّل <b>تبقى كما رُحّلت</b>، ولا يتغيّر قيدٌ مضى.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"
                CREATE TABLE IF NOT EXISTS leave_requests (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""BranchId"" BIGINT NULL,
                    ""EmployeeId"" BIGINT NOT NULL,
                    ""RequestNumber"" character varying(32) NOT NULL,
                    ""Type"" character varying(16) NOT NULL DEFAULT 'Annual',
                    ""FromDate"" DATE NOT NULL,
                    ""ToDate"" DATE NOT NULL,
                    ""Status"" character varying(16) NOT NULL DEFAULT 'Pending',
                    ""Reason"" character varying(256) NULL,
                    ""DecidedOn"" DATE NULL,
                    ""DecidedBy"" character varying(64) NULL,
                    ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_leave_requests_CompanyId_RequestNumber"" ON leave_requests (""CompanyId"", ""RequestNumber"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_leave_requests_CompanyId_EmployeeId_Status"" ON leave_requests (""CompanyId"", ""EmployeeId"", ""Status"");");

            b.Sql(@"ALTER TABLE payroll_lines ADD COLUMN IF NOT EXISTS ""AbsenceDeduction"" NUMERIC(18,2) NOT NULL DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE payroll_lines DROP COLUMN IF EXISTS ""AbsenceDeduction"";");
            b.Sql(@"DROP TABLE IF EXISTS leave_requests;");
        }
    }
}
