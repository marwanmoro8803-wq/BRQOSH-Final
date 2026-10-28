using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <b>إعادةُ سلسلة الهجرات إلى مطابقة النموذج.</b>
    ///
    /// <para>تخلّفت اللقطة عن النموذج منذ ٢٥ يوليو، فعاش كلّ تعديلٍ لاحقٍ في
    /// <see cref="BrqoshSchemaUpdater"/> — DDL يدويّ يُشغَّل عند كلّ إقلاع — بدل هجرة.
    /// وهذه الهجرة تُدرك ما فات: أعمدةَ هوية البائع النظامية (Z-10)، وعمودَ المبيعات النقدية
    /// في ورديّات نقطة البيع، وقيدَ تفرّد العدّاد التسلسليّ لفواتير هيئة الزكاة (ICV).</para>
    ///
    /// <para><b>ولماذا SQL خامٌ بدل AddColumn:</b> الأعمدة أدناه <b>موجودةٌ سلفاً</b> في كلّ
    /// قاعدةٍ رقّعها المُرقِّع — وهي كلّ قاعدةٍ قائمة. و<c>migrationBuilder.AddColumn</c>
    /// يُصدر <c>ALTER TABLE … ADD COLUMN</c> عارياً فيرتطم بـ <c>42701 column already
    /// exists</c>، ثمّ <b>يبتلعه</b> <see cref="DatabaseSchemaService.IsIgnorableSchemaConflict"/>
    /// فلا يُكتب صفُّ التاريخ، وتُعاد المحاولة عند كلّ إقلاعٍ إلى الأبد بلا شكوى — وتبقى
    /// السلسلة عالقةً عند هذه الهجرة فلا تُطبَّق أيّ هجرةٍ بعدها أبداً. فـ<c>IF NOT EXISTS</c>
    /// هنا ليست احتياطاً بل <b>شرطُ أن تُطبَّق الهجرة أصلاً</b>.</para>
    ///
    /// <para><b>حدُّ المعرفة:</b> إنشاءُ فهرس ICV الفريد على قاعدةٍ تحوي عدّادَين متكرّرَين
    /// يرمي <c>23505</c> ولا يبتلعه أحد، فيتوقّف الإقلاع برسالةٍ إنجليزية. وهذا هو التصرّف
    /// الصحيح — ازدواجُ العدّاد يُبطل سلسلة الفواتير أمام الهيئة ولا يجوز تجاوزه بصمت — لكنّ
    /// الرسالة غير مفهومة. ولم أُعالجها هنا لأنّ المُرقِّع يفرض القيد نفسه عند كلّ إقلاعٍ منذ
    /// شهور، فوجودُ تكرارٍ يعني أنّ ترقيعه فشل صامتاً وهو عطلٌ آخر يُسجَّل على حدة.</para>
    /// </summary>
    public partial class AddSellerRegulatoryIdentity : Migration
    {
        /// <summary>أعمدةٌ يجب أن توجد — والقاعدة القائمة تحوي أكثرها سلفاً.</summary>
        private static readonly (string Table, string Column, string Definition)[] Columns =
        [
            ("pos_shifts", "CashSales", "numeric(18,2) NOT NULL DEFAULT 0"),
            ("companies", "RegistrationName", "character varying(256) NULL"),
            ("companies", "CommercialRegistrationNumber", "character varying(64) NULL"),
            ("companies", "AddrBuildingNumber", "character varying(16) NOT NULL DEFAULT ''"),
            ("companies", "AddrAdditionalNumber", "character varying(16) NOT NULL DEFAULT ''"),
            ("companies", "AddrStreetName", "character varying(128) NOT NULL DEFAULT ''"),
            ("companies", "AddrDistrictName", "character varying(128) NOT NULL DEFAULT ''"),
            ("companies", "AddrCityName", "character varying(128) NOT NULL DEFAULT ''"),
            ("companies", "AddrPostalZone", "character varying(16) NOT NULL DEFAULT ''"),
            ("companies", "AddrCountrySubentity", "character varying(128) NOT NULL DEFAULT ''"),
            ("companies", "AddrCountryCode", "character varying(16) NOT NULL DEFAULT ''"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column, definition) in Columns)
            {
                migrationBuilder.Sql(
                    $"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "{column}" {definition};""");
            }

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_zatca_einvoice_documents_CompanyId_Icv"
                    ON "zatca_einvoice_documents" ("CompanyId", "Icv");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_zatca_einvoice_documents_CompanyId_Icv";""");

            foreach (var (table, column, _) in Columns)
            {
                migrationBuilder.Sql(
                    $"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "{column}";""");
            }
        }
    }
}
