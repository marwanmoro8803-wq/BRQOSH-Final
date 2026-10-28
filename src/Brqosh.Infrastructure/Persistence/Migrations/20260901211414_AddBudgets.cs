using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// الموازنة وسطورُها.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يُنشئ الجدولين قبل تشغيل الهجرات.</para>
    ///
    /// <para><b>و<c>PeriodCode</c> بصيغة <c>FiscalPeriod</c> نفسِها</b> (<c>YYYY-MM</c>)
    /// فالمقارنةُ تقع على المفتاح ذاته — ونموذجُ زمنٍ ثانٍ في النظام يفترق عن الأوّل يوماً.</para>
    /// </summary>
    public partial class AddBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""
                CREATE TABLE IF NOT EXISTS budgets (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "Code" character varying(32) NOT NULL,
                    "NameAr" character varying(256) NOT NULL,
                    "Year" INTEGER NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """);

            b.Sql("""
                CREATE TABLE IF NOT EXISTS budget_lines (
                    id BIGSERIAL PRIMARY KEY,
                    budget_id BIGINT NOT NULL REFERENCES budgets("Id") ON DELETE CASCADE,
                    "AccountCode" character varying(20) NOT NULL,
                    "PeriodCode" character varying(7) NOT NULL,
                    "Amount" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "CostCenterId" BIGINT NULL
                );
                """);

            b.Sql("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_budgets_CompanyId_Code" ON budgets ("CompanyId", "Code");""");
            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_budgets_CompanyId_Year" ON budgets ("CompanyId", "Year");""");
            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_budget_lines_budget_id" ON budget_lines (budget_id);""");
            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_budget_lines_PeriodCode_AccountCode" ON budget_lines ("PeriodCode", "AccountCode");""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""DROP TABLE IF EXISTS budget_lines;""");
            b.Sql("""DROP TABLE IF EXISTS budgets;""");
        }
    }
}
