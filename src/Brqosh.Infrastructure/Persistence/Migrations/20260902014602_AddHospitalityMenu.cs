using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// قائمةُ الطعام ووصفاتُها.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> — المُرقِّع يُنشئ الجداولَ قبل تشغيل الهجرات.</para>
    ///
    /// <para><b>ومكوّنٌ واحدٌ لا يتكرّر في وصفةٍ واحدة</b>: النطاقُ يجمع المكرَّر عند
    /// الإضافة، <b>وهذا القيدُ حارسُه الأخير</b> — فبيانٌ يدخل من مسارٍ آخر لا يُنشئ سطرين
    /// لصنفٍ واحد يُصرف أحدُهما ويُنسى الآخر.</para>
    /// </summary>
    public partial class AddHospitalityMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"
                CREATE TABLE IF NOT EXISTS menu_items (
                    ""Id"" BIGSERIAL PRIMARY KEY,
                    ""CompanyId"" BIGINT NOT NULL,
                    ""Code"" character varying(32) NOT NULL,
                    ""NameAr"" character varying(256) NOT NULL,
                    ""Section"" character varying(16) NOT NULL,
                    ""SalePrice"" NUMERIC(18,2) NOT NULL DEFAULT 0,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT true
                );");

            b.Sql(@"
                CREATE TABLE IF NOT EXISTS menu_item_components (
                    id BIGSERIAL PRIMARY KEY,
                    menu_item_id BIGINT NOT NULL
                        REFERENCES menu_items(""Id"") ON DELETE CASCADE,
                    ""ProductId"" BIGINT NOT NULL,
                    ""QuantityPerServing"" NUMERIC(18,4) NOT NULL DEFAULT 0
                );");

            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_menu_items_CompanyId_Code"" ON menu_items (""CompanyId"", ""Code"");");
            b.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_menu_item_components_menu_item_id_ProductId"" ON menu_item_components (menu_item_id, ""ProductId"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"DROP TABLE IF EXISTS menu_item_components;");
            b.Sql(@"DROP TABLE IF EXISTS menu_items;");
        }
    }
}
