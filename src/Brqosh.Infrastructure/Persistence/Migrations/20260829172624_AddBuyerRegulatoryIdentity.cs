using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-229 · م1-١ب) <b>هويةُ المشتري النظامية على جدول العملاء.</b>
    ///
    /// <para>الفاتورة القياسية (B2B) تذهب إلى <b>المقاصّة قبل التسليم</b> وطرفُ مشتريها
    /// معرَّف، وكان يخرج باسمٍ ورقمٍ ضريبيّ وحدهما — بلا اسمٍ مسجَّل ولا عنوانٍ وطنيّ.</para>
    ///
    /// <para><b>و<c>IF NOT EXISTS</c> هنا شرطُ التطبيق لا احتياط</b> — للسبب المشروح كاملاً في
    /// <see cref="AddSellerRegulatoryIdentity"/>: <c>BrqoshSchemaUpdater</c> يضيف هذه الأعمدة
    /// عند كلّ إقلاع، فالهجرةُ العارية ترتطم بـ <c>42701</c> ويبتلعها
    /// <c>IsIgnorableSchemaConflict</c>، فلا يُكتب صفُّ التاريخ وتعلق السلسلة إلى الأبد.</para>
    /// </summary>
    public partial class AddBuyerRegulatoryIdentity : Migration
    {
        /// <summary>أعمدةُ هوية المشتري — الشكلُ نفسه بلا رقم سجلٍّ تجاريّ.</summary>
        private static readonly (string Column, string Definition)[] Columns =
        [
            ("RegistrationName", "character varying(256) NULL"),
            ("AddrBuildingNumber", "character varying(16) NOT NULL DEFAULT ''"),
            ("AddrAdditionalNumber", "character varying(16) NOT NULL DEFAULT ''"),
            ("AddrStreetName", "character varying(128) NOT NULL DEFAULT ''"),
            ("AddrDistrictName", "character varying(128) NOT NULL DEFAULT ''"),
            ("AddrCityName", "character varying(128) NOT NULL DEFAULT ''"),
            ("AddrPostalZone", "character varying(16) NOT NULL DEFAULT ''"),
            ("AddrCountrySubentity", "character varying(128) NOT NULL DEFAULT ''"),
            ("AddrCountryCode", "character varying(16) NOT NULL DEFAULT ''"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (column, definition) in Columns)
            {
                migrationBuilder.Sql(
                    $"""ALTER TABLE "customers" ADD COLUMN IF NOT EXISTS "{column}" {definition};""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (column, _) in Columns)
                migrationBuilder.Sql($"""ALTER TABLE "customers" DROP COLUMN IF EXISTS "{column}";""");
        }
    }
}
