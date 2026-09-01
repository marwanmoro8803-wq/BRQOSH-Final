using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-230 · ط1-٥) خططُ الفوترة الدورية وأثرُ تكراراتها.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يُنشئ هذه الجداول قبل تشغيل الهجرات على
    /// قواعد العملاء، فـ<c>CreateTable</c> المولَّدة تصطدم بـ<c>42P07</c>.</para>
    ///
    /// <para><b>وقيدُ التفرّد على (PlanId, OccurrenceDate) هو جوهرُ الهجرة</b>: هو ما يمنع
    /// فوترةَ العميل مرّتين عن شهرٍ واحد — <b>وهو عطلٌ يراه العميلُ قبلنا</b>.</para>
    /// </summary>
    public partial class AddRecurringInvoicePlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""
                CREATE TABLE IF NOT EXISTS recurring_invoice_plans (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "BranchId" BIGINT NULL,
                    "CustomerId" BIGINT NOT NULL,
                    "Code" character varying(32) NOT NULL,
                    "Description" character varying(512) NOT NULL,
                    "Frequency" character varying(16) NOT NULL,
                    "Status" character varying(16) NOT NULL,
                    "StartDate" DATE NOT NULL,
                    "EndDate" DATE NULL,
                    "OccurrencesGenerated" INTEGER NOT NULL DEFAULT 0,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """);

            b.Sql("""
                CREATE TABLE IF NOT EXISTS recurring_invoice_plan_lines (
                    id BIGSERIAL PRIMARY KEY,
                    recurring_invoice_plan_id BIGINT NOT NULL
                        REFERENCES recurring_invoice_plans("Id") ON DELETE CASCADE,
                    "Description" character varying(512) NOT NULL,
                    "Quantity" NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "UnitPrice" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "ProductId" BIGINT NULL,
                    "VatCategory" character varying(16) NOT NULL DEFAULT 'Standard',
                    "TaxExemptionReasonCode" character varying(32) NULL,
                    "TaxExemptionReason" text NULL,
                    "BatchNumber" character varying(64) NULL,
                    "DiscountPercent" NUMERIC(9,4) NOT NULL DEFAULT 0,
                    "UnitCode" character varying(16) NULL
                );
                """);

            b.Sql("""
                CREATE TABLE IF NOT EXISTS recurring_invoice_runs (
                    "Id" BIGSERIAL PRIMARY KEY,
                    "CompanyId" BIGINT NOT NULL,
                    "PlanId" BIGINT NOT NULL,
                    "OccurrenceDate" DATE NOT NULL,
                    "SalesInvoiceId" BIGINT NOT NULL,
                    "GeneratedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                """);

            b.Sql("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_recurring_invoice_plans_CompanyId_Code" ON recurring_invoice_plans ("CompanyId", "Code");""");
            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_recurring_invoice_plans_CompanyId_Status" ON recurring_invoice_plans ("CompanyId", "Status");""");
            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_recurring_invoice_plans_CompanyId_BranchId" ON recurring_invoice_plans ("CompanyId", "BranchId");""");
            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_recurring_invoice_plan_lines_recurring_invoice_plan_id" ON recurring_invoice_plan_lines (recurring_invoice_plan_id);""");
            b.Sql("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_recurring_invoice_runs_PlanId_OccurrenceDate" ON recurring_invoice_runs ("PlanId", "OccurrenceDate");""");
            b.Sql("""CREATE INDEX IF NOT EXISTS "IX_recurring_invoice_runs_CompanyId_OccurrenceDate" ON recurring_invoice_runs ("CompanyId", "OccurrenceDate");""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""DROP TABLE IF EXISTS recurring_invoice_runs;""");
            b.Sql("""DROP TABLE IF EXISTS recurring_invoice_plan_lines;""");
            b.Sql("""DROP TABLE IF EXISTS recurring_invoice_plans;""");
        }
    }
}
