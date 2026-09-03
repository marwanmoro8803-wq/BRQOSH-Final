using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentTracking : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// (CEEF-230 · ط3-٤ج) ‏أثرُ تتبّع الشحنة — <b>سندُ التسليم</b>.
        ///
        /// <para>‏<b>وصياغةٌ يدويّةٌ بـ<c>IF NOT EXISTS</c></b> كعادة هذا المستودع:
        /// المُرقِّعُ يُنشئ الجدولَ على القواعد القائمة، <b>و<c>CreateTable</c> المُولَّد
        /// يرمي على جدولٍ موجود</b> فتعلق السلسلة.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"CREATE TABLE IF NOT EXISTS shipment_events (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    shipment_id BIGINT NOT NULL
                        REFERENCES shipments(""Id"") ON DELETE CASCADE,
                    ""Kind"" character varying(20) NOT NULL DEFAULT 'PickedUp',
                    ""OccurredOn"" DATE NOT NULL,
                    ""Location"" character varying(128) NULL,
                    ""Note"" character varying(256) NULL,
                    ""ReceivedBy"" character varying(128) NULL
                );");
            b.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_shipment_events_shipment_id_OccurredOn"" ON shipment_events (shipment_id, ""OccurredOn"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP TABLE IF EXISTS shipment_events;");
        }
    }
}
