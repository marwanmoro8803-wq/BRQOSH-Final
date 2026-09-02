using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-230 · ط3-١) العقاراتُ والوحداتُ وعقودُ الإيجار.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يُنشئ الجداولَ قبل تشغيل الهجرات.</para>
    ///
    /// <para><b>ولا مفتاحَ أجنبيٍّ من العقد إلى خطة الفوترة</b>: الخطةُ تُنشأ في المعاملة
    /// نفسِها، لكنّ عقداً قديماً قد يُنهى وتُحذف خطتُه يوماً — <b>ومفتاحٌ يمنع حذفَ خطةٍ
    /// انتهت يُقيّد التنظيفَ بلا فائدة</b>. والرابطُ يبقى معرّفاً يُقرأ.</para>
    /// </summary>
    public partial class AddRealEstate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"
                CREATE TABLE IF NOT EXISTS properties (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""BranchId"" BIGINT NULL,
                    ""Code"" character varying(32) NOT NULL,
                    ""NameAr"" character varying(256) NOT NULL,
                    ""Kind"" character varying(16) NOT NULL,
                    ""City"" character varying(64) NOT NULL,
                    ""Address"" character varying(512) NOT NULL,
                    ""DeedNumber"" character varying(64) NULL,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT true,
                    ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");

            b.Sql(@"
                CREATE TABLE IF NOT EXISTS property_units (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""PropertyId"" BIGINT NOT NULL,
                    ""UnitNumber"" character varying(32) NOT NULL,
                    ""AreaSquareMeters"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""Bedrooms"" INTEGER NOT NULL DEFAULT 0,
                    ""Status"" character varying(16) NOT NULL,
                    ""ListedAnnualRent"" NUMERIC(18,2) NOT NULL DEFAULT 0
                );");

            b.Sql(@"
                CREATE TABLE IF NOT EXISTS lease_contracts (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""BranchId"" BIGINT NULL,
                    ""PropertyUnitId"" BIGINT NOT NULL,
                    ""TenantCustomerId"" BIGINT NOT NULL,
                    ""ContractNumber"" character varying(32) NOT NULL,
                    ""StartDate"" DATE NOT NULL,
                    ""EndDate"" DATE NOT NULL,
                    ""RentPerPeriod"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""Frequency"" character varying(16) NOT NULL,
                    ""SecurityDeposit"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""Status"" character varying(16) NOT NULL,
                    ""RecurringPlanId"" BIGINT NULL,
                    ""DepositReceivedOn"" DATE NULL,
                    ""DepositReturnedOn"" DATE NULL,
                    ""DepositForfeited"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""TerminatedOn"" DATE NULL,
                    ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_properties_CompanyId_Code"" ON properties (""CompanyId"", ""Code"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_properties_CompanyId_BranchId"" ON properties (""CompanyId"", ""BranchId"");");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_property_units_PropertyId_UnitNumber"" ON property_units (""PropertyId"", ""UnitNumber"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_property_units_CompanyId_Status"" ON property_units (""CompanyId"", ""Status"");");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_lease_contracts_CompanyId_ContractNumber"" ON lease_contracts (""CompanyId"", ""ContractNumber"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_lease_contracts_CompanyId_PropertyUnitId_Status"" ON lease_contracts (""CompanyId"", ""PropertyUnitId"", ""Status"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_lease_contracts_CompanyId_BranchId"" ON lease_contracts (""CompanyId"", ""BranchId"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP TABLE IF EXISTS lease_contracts;");
            b.Sql(@"DROP TABLE IF EXISTS property_units;");
            b.Sql(@"DROP TABLE IF EXISTS properties;");
        }
    }
}
