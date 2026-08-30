using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (CEEF-229 · م2-٢٤) لحظةُ ترحيل الفاتورة والإشعار الدائن — يعلنها رمزُ الطور الأوّل.
    ///
    /// <para><b>وقابلةٌ للعدم عمداً:</b> المستنداتُ المرحّلة قبل هذه الهجرة لا لحظةَ لها،
    /// وملؤها بقيمةٍ افتراضية اختلاقٌ يشبه العطلَ الذي جاءت تُصلحه. فتبقى فارغةً ويعود
    /// المسارُ المطبوع إلى الظهر الحتميّ مُقِرّاً بحدّه.</para>
    ///
    /// <para>وSQL خامّ مُطاوعٌ للتكرار لأنّ <c>BrqoshSchemaUpdater</c> يُضيف أعمدةً قبل
    /// الهجرات — والاصطدامُ يرمي <c>42701</c> فيبتلعه المُرقِّع وتقف السلسلةُ صامتة.</para>
    /// </summary>
    public partial class AddPostedAtUtc : Migration
    {
        private static readonly string[] Tables = ["sales_invoices", "credit_notes"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
                migrationBuilder.Sql(
                    $"""ALTER TABLE "{table}" ADD COLUMN IF NOT EXISTS "PostedAtUtc" timestamp with time zone;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
                migrationBuilder.Sql(
                    $"""ALTER TABLE "{table}" DROP COLUMN IF EXISTS "PostedAtUtc";""");
        }
    }
}
