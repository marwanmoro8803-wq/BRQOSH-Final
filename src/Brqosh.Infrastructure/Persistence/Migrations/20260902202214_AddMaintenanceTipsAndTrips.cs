using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceTipsAndTrips : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// ‏أوامرُ الصيانة (ط3-١ب) وأوعيةُ الإكراميات (ط3-٣د) ورحلاتُ التوصيل (ط3-٤ب).
        ///
        /// <para>‏<b>وصياغةٌ يدويّةٌ بـ<c>IF NOT EXISTS</c></b>: المُرقِّعُ اليدويّ يُنشئ
        /// الجداولَ على القواعد القائمة، <b>و<c>CreateTable</c> المُولَّد يرمي على جدولٍ
        /// موجود</b> — فتعلق السلسلةُ وتموت كلُّ هجرةٍ بعدها. وهي عادةُ هذا المستودع
        /// منذ <c>AddHospitalityMenu</c>، <b>وقد أخرج مسبارُ المخطّط خرقَها مرّتين</b>.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"CREATE TABLE IF NOT EXISTS maintenance_orders (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""BranchId"" BIGINT NULL,
                    ""PropertyId"" BIGINT NOT NULL,
                    ""UnitId"" BIGINT NULL,
                    ""OrderNumber"" character varying(64) NOT NULL,
                    ""Kind"" character varying(16) NOT NULL DEFAULT 'Preventive',
                    ""RequestDate"" DATE NOT NULL,
                    ""CompletedOn"" DATE NULL,
                    ""Description"" character varying(512) NOT NULL,
                    ""VendorName"" character varying(256) NULL,
                    ""Status"" character varying(16) NOT NULL DEFAULT 'Open',
                    ""LeaseContractId"" BIGINT NULL,
                    ""TenantChargeAmount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""JournalEntryId"" BIGINT NULL,
                    ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");
            b.Sql(@"CREATE TABLE IF NOT EXISTS maintenance_cost_lines (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    order_id BIGINT NOT NULL
                        REFERENCES maintenance_orders(""Id"") ON DELETE CASCADE,
                    ""Description"" character varying(512) NOT NULL,
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0
                );");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_maintenance_orders_CompanyId_OrderNumber"" ON maintenance_orders (""CompanyId"", ""OrderNumber"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_maintenance_orders_CompanyId_PropertyId_UnitId"" ON maintenance_orders (""CompanyId"", ""PropertyId"", ""UnitId"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_maintenance_orders_CompanyId_Status"" ON maintenance_orders (""CompanyId"", ""Status"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_maintenance_cost_lines_order_id"" ON maintenance_cost_lines (order_id);");
            b.Sql(@"CREATE TABLE IF NOT EXISTS tip_pools (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""BranchId"" BIGINT NULL,
                    ""ShiftId"" BIGINT NULL,
                    ""PoolDate"" DATE NOT NULL,
                    ""Status"" character varying(16) NOT NULL DEFAULT 'Open',
                    ""JournalEntryId"" BIGINT NULL,
                    ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");
            b.Sql(@"CREATE TABLE IF NOT EXISTS tip_collections (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    pool_id BIGINT NOT NULL REFERENCES tip_pools(""Id"") ON DELETE CASCADE,
                    ""Source"" character varying(8) NOT NULL DEFAULT 'Cash',
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""Note"" character varying(256) NULL
                );");
            b.Sql(@"CREATE TABLE IF NOT EXISTS tip_distributions (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    pool_id BIGINT NOT NULL REFERENCES tip_pools(""Id"") ON DELETE CASCADE,
                    ""EmployeeId"" BIGINT NOT NULL,
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0
                );");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_tip_pools_CompanyId_PoolDate"" ON tip_pools (""CompanyId"", ""PoolDate"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_tip_pools_CompanyId_Status"" ON tip_pools (""CompanyId"", ""Status"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_tip_collections_pool_id"" ON tip_collections (pool_id);");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_tip_distributions_pool_id_EmployeeId"" ON tip_distributions (pool_id, ""EmployeeId"");");
            b.Sql(@"CREATE TABLE IF NOT EXISTS delivery_trips (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""BranchId"" BIGINT NULL,
                    ""TripNumber"" character varying(64) NOT NULL,
                    ""RouteName"" character varying(256) NOT NULL,
                    ""VehiclePlate"" character varying(32) NOT NULL,
                    ""DriverEmployeeId"" BIGINT NULL,
                    ""DepartureDate"" DATE NOT NULL,
                    ""ReturnDate"" DATE NULL,
                    ""Status"" character varying(16) NOT NULL DEFAULT 'Planned',
                    ""DriverFloat"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""ReturnedCash"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""JournalEntryId"" BIGINT NULL,
                    ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );");
            b.Sql(@"CREATE TABLE IF NOT EXISTS trip_shipments (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    trip_id BIGINT NOT NULL REFERENCES delivery_trips(""Id"") ON DELETE CASCADE,
                    ""ShipmentId"" BIGINT NOT NULL
                );");
            b.Sql(@"CREATE TABLE IF NOT EXISTS trip_expenses (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    trip_id BIGINT NOT NULL REFERENCES delivery_trips(""Id"") ON DELETE CASCADE,
                    ""Kind"" character varying(20) NOT NULL DEFAULT 'Other',
                    ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""FromDriverFloat"" BOOLEAN NOT NULL DEFAULT false,
                    ""Note"" character varying(256) NULL
                );");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_delivery_trips_CompanyId_TripNumber"" ON delivery_trips (""CompanyId"", ""TripNumber"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_delivery_trips_CompanyId_RouteName_DepartureDate"" ON delivery_trips (""CompanyId"", ""RouteName"", ""DepartureDate"");");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_trip_shipments_ShipmentId"" ON trip_shipments (""ShipmentId"");");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_trip_shipments_trip_id"" ON trip_shipments (trip_id);");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_trip_expenses_trip_id"" ON trip_expenses (trip_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP TABLE IF EXISTS trip_expenses;");
            b.Sql(@"DROP TABLE IF EXISTS trip_shipments;");
            b.Sql(@"DROP TABLE IF EXISTS delivery_trips;");
            b.Sql(@"DROP TABLE IF EXISTS tip_distributions;");
            b.Sql(@"DROP TABLE IF EXISTS tip_collections;");
            b.Sql(@"DROP TABLE IF EXISTS tip_pools;");
            b.Sql(@"DROP TABLE IF EXISTS maintenance_cost_lines;");
            b.Sql(@"DROP TABLE IF EXISTS maintenance_orders;");
        }
    }
}
