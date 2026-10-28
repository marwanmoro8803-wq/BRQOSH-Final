using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// المرضى وشركاتُ التأمين والوثائقُ والزيارات.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يُنشئ الجداولَ قبل تشغيل الهجرات.</para>
    ///
    /// <para><b>ولا مفتاحَ أجنبيٍّ من الزيارة إلى فاتورتيها</b>: الفاتورتان تُنشآن في
    /// المعاملة نفسِها، لكنّ فاتورةً قد تُلغى يوماً بإشعارٍ ثمّ تُنظَّف — <b>ومفتاحٌ يمنع
    /// ذلك يُقيّد التنظيفَ بلا فائدة</b>. والرابطُ يبقى معرّفاً يُقرأ.</para>
    /// </summary>
    public partial class AddHealthcare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"CREATE TABLE IF NOT EXISTS patients (
        ""Id"" BIGSERIAL PRIMARY KEY,
        ""CompanyId"" BIGINT NOT NULL,
        ""BranchId"" BIGINT NULL,
        ""MedicalRecordNumber"" character varying(32) NOT NULL,
        ""NameAr"" character varying(256) NOT NULL,
        ""NationalId"" character varying(32) NOT NULL,
        ""DateOfBirth"" DATE NOT NULL,
        ""Gender"" character varying(16) NOT NULL,
        ""Phone"" character varying(32) NOT NULL,
        ""CustomerId"" BIGINT NOT NULL,
        ""IsActive"" BOOLEAN NOT NULL DEFAULT true,
        ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
    );");

            b.Sql(@"CREATE TABLE IF NOT EXISTS insurance_payers (
        ""Id"" BIGSERIAL PRIMARY KEY,
        ""CompanyId"" BIGINT NOT NULL,
        ""Code"" character varying(32) NOT NULL,
        ""NameAr"" character varying(256) NOT NULL,
        ""CustomerId"" BIGINT NOT NULL,
        ""PaymentTermDays"" INTEGER NOT NULL DEFAULT 60,
        ""IsActive"" BOOLEAN NOT NULL DEFAULT true
    );");

            b.Sql(@"CREATE TABLE IF NOT EXISTS insurance_policies (
        ""Id"" BIGSERIAL PRIMARY KEY,
        ""CompanyId"" BIGINT NOT NULL,
        ""PatientId"" BIGINT NOT NULL,
        ""PayerId"" BIGINT NOT NULL,
        ""PolicyNumber"" character varying(64) NOT NULL,
        ""MemberNumber"" character varying(64) NOT NULL,
        ""CoveragePercent"" NUMERIC(9,4) NOT NULL DEFAULT 0,
        ""CopayFixed"" NUMERIC(18,2) NOT NULL DEFAULT 0,
        ""AnnualLimit"" NUMERIC(18,2) NULL,
        ""StartDate"" DATE NOT NULL,
        ""EndDate"" DATE NOT NULL,
        ""IsActive"" BOOLEAN NOT NULL DEFAULT true
    );");

            b.Sql(@"CREATE TABLE IF NOT EXISTS visits (
        ""Id"" BIGSERIAL PRIMARY KEY,
        ""CompanyId"" BIGINT NOT NULL,
        ""BranchId"" BIGINT NULL,
        ""PatientId"" BIGINT NOT NULL,
        ""VisitNumber"" character varying(32) NOT NULL,
        ""VisitDate"" DATE NOT NULL,
        ""DoctorName"" character varying(128) NOT NULL,
        ""Diagnosis"" character varying(512) NULL,
        ""PolicyId"" BIGINT NULL,
        ""Status"" character varying(16) NOT NULL,
        ""PatientInvoiceId"" BIGINT NULL,
        ""PayerInvoiceId"" BIGINT NULL,
        ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
    );");

            b.Sql(@"CREATE TABLE IF NOT EXISTS visit_lines (
        id BIGSERIAL PRIMARY KEY,
        visit_id BIGINT NOT NULL
            REFERENCES visits(""Id"") ON DELETE CASCADE,
        ""Description"" character varying(512) NOT NULL,
        ""Quantity"" NUMERIC(18,4) NOT NULL DEFAULT 0,
        ""UnitPrice"" NUMERIC(18,2) NOT NULL DEFAULT 0,
        ""IsCovered"" BOOLEAN NOT NULL DEFAULT true,
        ""ProductId"" BIGINT NULL,
        ""VatCategory"" character varying(16) NOT NULL,
        ""TaxExemptionReasonCode"" character varying(32) NULL,
        ""TaxExemptionReason"" text NULL
    );");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_patients_CompanyId_MedicalRecordNumber"" ON patients (""CompanyId"", ""MedicalRecordNumber"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_patients_CompanyId_CustomerId"" ON patients (""CompanyId"", ""CustomerId"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_patients_CompanyId_BranchId"" ON patients (""CompanyId"", ""BranchId"");");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_insurance_payers_CompanyId_Code"" ON insurance_payers (""CompanyId"", ""Code"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_insurance_policies_CompanyId_PatientId_IsActive"" ON insurance_policies (""CompanyId"", ""PatientId"", ""IsActive"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_insurance_policies_CompanyId_PayerId"" ON insurance_policies (""CompanyId"", ""PayerId"");");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_visits_CompanyId_VisitNumber"" ON visits (""CompanyId"", ""VisitNumber"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_visits_CompanyId_PatientId_VisitDate"" ON visits (""CompanyId"", ""PatientId"", ""VisitDate"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_visits_CompanyId_PolicyId_VisitDate"" ON visits (""CompanyId"", ""PolicyId"", ""VisitDate"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_visits_CompanyId_BranchId"" ON visits (""CompanyId"", ""BranchId"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_visit_lines_visit_id"" ON visit_lines (visit_id);");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP TABLE IF EXISTS visit_lines;");
            b.Sql(@"DROP TABLE IF EXISTS visits;");
            b.Sql(@"DROP TABLE IF EXISTS insurance_policies;");
            b.Sql(@"DROP TABLE IF EXISTS insurance_payers;");
            b.Sql(@"DROP TABLE IF EXISTS patients;");
        }
    }
}
