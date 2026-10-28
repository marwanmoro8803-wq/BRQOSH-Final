using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// الغرفُ والحجوزُ وحساباتُ النزلاء.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يُنشئ الجداولَ قبل تشغيل الهجرات.</para>
    ///
    /// <para><b>وقيدُ التفرّد على (الحساب، ليلةِ الغرفة) هو حارسُ الازدواج الحقيقيّ</b> —
    /// لا فحصٌ في الذاكرة يسبقه تشغيلٌ آخر لدفعة تقييد الليالي. <b>والفراغُ لا يتصادم</b>،
    /// فرسومُ المطعم والغسيل تتعدّد في اليوم الواحد بحرّية.</para>
    /// </summary>
    public partial class AddHospitalityLodging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"CREATE TABLE IF NOT EXISTS rooms (
        ""Id"" BIGSERIAL PRIMARY KEY,
        ""CompanyId"" BIGINT NOT NULL,
        ""RoomNumber"" character varying(16) NOT NULL,
        ""Kind"" character varying(16) NOT NULL,
        ""Floor"" INTEGER NOT NULL DEFAULT 0,
        ""NightlyRate"" NUMERIC(18,2) NOT NULL DEFAULT 0,
        ""Status"" character varying(16) NOT NULL
    );");

            b.Sql(@"CREATE TABLE IF NOT EXISTS reservations (
        ""Id"" BIGSERIAL PRIMARY KEY,
        ""CompanyId"" BIGINT NOT NULL,
        ""BranchId"" BIGINT NULL,
        ""RoomId"" BIGINT NOT NULL,
        ""GuestCustomerId"" BIGINT NOT NULL,
        ""ReservationNumber"" character varying(32) NOT NULL,
        ""CheckInDate"" DATE NOT NULL,
        ""CheckOutDate"" DATE NOT NULL,
        ""RatePerNight"" NUMERIC(18,2) NOT NULL DEFAULT 0,
        ""Guests"" INTEGER NOT NULL DEFAULT 1,
        ""Status"" character varying(16) NOT NULL,
        ""SettlementInvoiceId"" BIGINT NULL,
        ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
    );");

            b.Sql(@"CREATE TABLE IF NOT EXISTS guest_folios (
        ""Id"" BIGSERIAL PRIMARY KEY,
        ""CompanyId"" BIGINT NOT NULL,
        ""ReservationId"" BIGINT NOT NULL,
        ""IsSettled"" BOOLEAN NOT NULL DEFAULT false
    );");

            b.Sql(@"CREATE TABLE IF NOT EXISTS folio_charges (
        id BIGSERIAL PRIMARY KEY,
        guest_folio_id BIGINT NOT NULL
            REFERENCES guest_folios(""Id"") ON DELETE CASCADE,
        ""ChargeDate"" DATE NOT NULL,
        ""Kind"" character varying(16) NOT NULL,
        ""Description"" character varying(512) NOT NULL,
        ""Amount"" NUMERIC(18,2) NOT NULL DEFAULT 0,
        ""VatCategory"" character varying(16) NOT NULL,
        ""RoomNightDate"" DATE NULL
    );");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_rooms_CompanyId_RoomNumber"" ON rooms (""CompanyId"", ""RoomNumber"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_rooms_CompanyId_Status"" ON rooms (""CompanyId"", ""Status"");");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_reservations_CompanyId_ReservationNumber"" ON reservations (""CompanyId"", ""ReservationNumber"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_reservations_CompanyId_RoomId_Status"" ON reservations (""CompanyId"", ""RoomId"", ""Status"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_reservations_CompanyId_CheckInDate"" ON reservations (""CompanyId"", ""CheckInDate"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_reservations_CompanyId_BranchId"" ON reservations (""CompanyId"", ""BranchId"");");

            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_guest_folios_CompanyId_ReservationId_IsSettled"" ON guest_folios (""CompanyId"", ""ReservationId"", ""IsSettled"");");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_folio_charges_guest_folio_id_RoomNightDate"" ON folio_charges (guest_folio_id, ""RoomNightDate"");");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP TABLE IF EXISTS folio_charges;");
            b.Sql(@"DROP TABLE IF EXISTS guest_folios;");
            b.Sql(@"DROP TABLE IF EXISTS reservations;");
            b.Sql(@"DROP TABLE IF EXISTS rooms;");
        }
    }
}
