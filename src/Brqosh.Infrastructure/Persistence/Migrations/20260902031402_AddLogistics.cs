using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// البوالص والتحصيل نيابةً عن التجّار.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يُنشئ الجدول قبل تشغيل الهجرات.</para>
    /// </summary>
    public partial class AddLogistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"CREATE TABLE IF NOT EXISTS shipments (
        ""Id"" BIGSERIAL PRIMARY KEY,
        ""CompanyId"" BIGINT NOT NULL,
        ""BranchId"" BIGINT NULL,
        ""WaybillNumber"" character varying(32) NOT NULL,
        ""ShipperCustomerId"" BIGINT NOT NULL,
        ""ConsigneeName"" character varying(256) NOT NULL,
        ""ConsigneePhone"" character varying(32) NOT NULL,
        ""DestinationCity"" character varying(64) NOT NULL,
        ""Pieces"" INTEGER NOT NULL DEFAULT 1,
        ""WeightKg"" NUMERIC(18,3) NOT NULL DEFAULT 0,
        ""FreightCharge"" NUMERIC(18,2) NOT NULL DEFAULT 0,
        ""CodAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
        ""Status"" character varying(16) NOT NULL,
        ""ShipDate"" DATE NOT NULL,
        ""DeliveredOn"" DATE NULL,
        ""CodCollected"" BOOLEAN NOT NULL DEFAULT false,
        ""CodRemitted"" BOOLEAN NOT NULL DEFAULT false,
        ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
    );");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_shipments_CompanyId_WaybillNumber"" ON shipments (""CompanyId"", ""WaybillNumber"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_shipments_CompanyId_ShipperCustomerId_CodCollected_CodRemitted"" ON shipments (""CompanyId"", ""ShipperCustomerId"", ""CodCollected"", ""CodRemitted"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_shipments_CompanyId_Status"" ON shipments (""CompanyId"", ""Status"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_shipments_CompanyId_BranchId"" ON shipments (""CompanyId"", ""BranchId"");");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP TABLE IF EXISTS shipments;");
        }
    }
}
