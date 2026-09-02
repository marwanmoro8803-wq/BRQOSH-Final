using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceClaims : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// (CEEF-230 · ط3-٢ب) ‏دفعاتُ المطالبات وقراراتُ الشركة عليها.
        ///
        /// <para>‏<b>وصياغةٌ يدويّةٌ بـ<c>IF NOT EXISTS</c></b>: المُرقِّعُ اليدويّ يُنشئ
        /// الجدولين على القواعد القائمة، <b>و<c>CreateTable</c> المُولَّد يرمي على جدولٍ
        /// موجود</b> — فتعلق السلسلةُ وتموت كلُّ هجرةٍ بعدها. وهي العادةُ المتّبَعة في
        /// هذا المستودع منذ <c>AddHospitalityMenu</c>.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"
                CREATE TABLE IF NOT EXISTS insurance_claims (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""BranchId"" BIGINT NULL,
                    ""PayerId"" BIGINT NOT NULL,
                    ""ClaimNumber"" character varying(64) NOT NULL,
                    ""PeriodStart"" DATE NOT NULL,
                    ""PeriodEnd"" DATE NOT NULL,
                    ""Status"" character varying(16) NOT NULL DEFAULT 'Draft',
                    ""SubmittedOn"" DATE NULL,
                    ""AdjudicatedOn"" DATE NULL,
                    ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");

            b.Sql(@"
                CREATE TABLE IF NOT EXISTS insurance_claim_lines (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    claim_id BIGINT NOT NULL
                        REFERENCES insurance_claims(""Id"") ON DELETE CASCADE,
                    ""VisitId"" BIGINT NOT NULL,
                    ""PayerInvoiceId"" BIGINT NOT NULL,
                    ""ClaimedAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""ApprovedAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""RejectedAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""RejectionReason"" character varying(512) NULL,
                    ""Disposition"" character varying(16) NOT NULL DEFAULT 'Undecided',
                    ""IsAdjudicated"" BOOLEAN NOT NULL DEFAULT false,
                    ""CreditNoteId"" BIGINT NULL,
                    ""RebillInvoiceId"" BIGINT NULL
                );");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_insurance_claims_CompanyId_PayerId_ClaimNumber"" ON insurance_claims (""CompanyId"", ""PayerId"", ""ClaimNumber"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_insurance_claims_CompanyId_Status"" ON insurance_claims (""CompanyId"", ""Status"");");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_insurance_claim_lines_PayerInvoiceId"" ON insurance_claim_lines (""PayerInvoiceId"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_insurance_claim_lines_VisitId"" ON insurance_claim_lines (""VisitId"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_insurance_claim_lines_claim_id"" ON insurance_claim_lines (claim_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP TABLE IF EXISTS insurance_claim_lines;");
            b.Sql(@"DROP TABLE IF EXISTS insurance_claims;");
        }
    }
}
