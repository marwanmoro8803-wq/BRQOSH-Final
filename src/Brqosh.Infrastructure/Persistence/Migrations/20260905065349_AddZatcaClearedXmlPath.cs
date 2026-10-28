using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// عمودُ المستند المُخلَّص العائد من الهيئة.
    ///
    /// <para><b>SQL خامٌ بـ<c>IF NOT EXISTS</c> لا <c>AddColumn</c>:</b> قواعدُ العملاء
    /// القائمة رقّعها <see cref="BrqoshSchemaUpdater"/> عند كلّ إقلاع، وقد يكون العمود
    /// موجوداً سلفاً. و<c>AddColumn</c> يُصدر <c>ALTER TABLE … ADD COLUMN</c> عارياً فيرتطم
    /// بـ<c>42701</c>، فيُبتلع الخطأ ولا يُكتب صفُّ التاريخ، <b>فتعلق سلسلةُ الهجرات هنا
    /// فلا تُطبَّق أيُّ هجرةٍ بعدها أبداً</b>. فهذه ليست احتياطاً بل شرطَ أن تُطبَّق الهجرة.</para>
    ///
    /// <para>ولا فرعَ لـSQLite: مسارُ التطوير/الاختبار يُهيَّأ بـ<c>EnsureCreated</c> ثمّ
    /// <see cref="BrqoshSchemaUpdater"/>، <b>ولا يمرّ على الهجرات إطلاقاً</b> — فأيُّ فرعٍ
    /// هنا شيفرةٌ ميّتةٌ تُوهم بتغطيةٍ لا تقع.</para>
    /// </summary>
    public partial class AddZatcaClearedXmlPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE zatca_einvoice_documents
                    ADD COLUMN IF NOT EXISTS "ClearedXmlFilePath" text NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE zatca_einvoice_documents
                    DROP COLUMN IF EXISTS "ClearedXmlFilePath";
                """);
        }
    }
}
