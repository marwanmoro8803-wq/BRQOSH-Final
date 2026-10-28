using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// سجلُّ الموظفين ونِسَبُ التأمينات، ومعرّفُ الموظّف على سطر الرواتب.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يُنشئ الجدولين قبل تشغيل الهجرات.</para>
    ///
    /// <para><b>و<c>EmployeeId</c> يقبل العدم عمداً</b>: كلُّ سطرِ رواتبَ قائمٍ كُتب باسمٍ
    /// حرٍّ بلا سجلّ، ولا سبيلَ إلى ربطه بموظّفٍ لم يُنشأ بعد. <b>ومحاولةُ التخمين بمطابقة
    /// الاسم تُنتج ربطاً خاطئاً لا يُكتشف</b> — والعدمُ أصدقُ من ربطٍ مظنون.</para>
    /// </summary>
    public partial class AddEmployeesAndGosi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"
                CREATE TABLE IF NOT EXISTS employees (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""BranchId"" BIGINT NULL,
                    ""Code"" character varying(32) NOT NULL,
                    ""NameAr"" character varying(256) NOT NULL,
                    ""NationalId"" character varying(10) NOT NULL,
                    ""Nationality"" character varying(16) NOT NULL,
                    ""JobTitle"" character varying(128) NOT NULL,
                    ""HireDate"" DATE NOT NULL,
                    ""TerminationDate"" DATE NULL,
                    ""Iban"" character varying(24) NULL,
                    ""BasicSalary"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""HousingAllowance"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""TransportAllowance"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""OtherAllowance"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""CostCenterId"" BIGINT NULL,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT true,
                    ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");

            b.Sql(@"
                CREATE TABLE IF NOT EXISTS gosi_policies (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""SaudiEmployeePercent"" NUMERIC(9,4) NOT NULL DEFAULT 9.75,
                    ""SaudiEmployerPercent"" NUMERIC(9,4) NOT NULL DEFAULT 11.75,
                    ""NonSaudiEmployeePercent"" NUMERIC(9,4) NOT NULL DEFAULT 0,
                    ""NonSaudiEmployerPercent"" NUMERIC(9,4) NOT NULL DEFAULT 2,
                    ""ContributoryWageCap"" NUMERIC(18,2) NOT NULL DEFAULT 45000,
                    ""ContributoryWageFloor"" NUMERIC(18,2) NOT NULL DEFAULT 0
                );");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_employees_CompanyId_Code"" ON employees (""CompanyId"", ""Code"");");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_employees_CompanyId_NationalId"" ON employees (""CompanyId"", ""NationalId"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_employees_CompanyId_IsActive"" ON employees (""CompanyId"", ""IsActive"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_employees_CompanyId_BranchId"" ON employees (""CompanyId"", ""BranchId"");");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_gosi_policies_CompanyId"" ON gosi_policies (""CompanyId"");");

            b.Sql(@"ALTER TABLE ""payroll_lines"" ADD COLUMN IF NOT EXISTS ""EmployeeId"" bigint;");
            b.Sql(@"ALTER TABLE ""payroll_lines"" ADD COLUMN IF NOT EXISTS ""OtherAllowance"" numeric(18,2) NOT NULL DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE ""payroll_lines"" DROP COLUMN IF EXISTS ""OtherAllowance"";");
            b.Sql(@"ALTER TABLE ""payroll_lines"" DROP COLUMN IF EXISTS ""EmployeeId"";");
            b.Sql(@"DROP TABLE IF EXISTS gosi_policies;");
            b.Sql(@"DROP TABLE IF EXISTS employees;");
        }
    }
}
