using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (م-٤ · ن-٢٨) عدّادُ المحاولات الخاطئة على تحدّيات الرمز.
    ///
    /// <para><b>SQL خامٌ بـ<c>IF NOT EXISTS</c> لا <c>AddColumn</c>:</b> قواعدُ العملاء
    /// القائمة يرقّعها <see cref="BrqoshSchemaUpdater"/> عند كلّ إقلاع، <b>وهذا العمودُ
    /// بالذات يُضاف هناك كذلك</b> (<c>EnsureOtpAttemptColumnAsync</c>) — فقد يسبق المُرقِّعُ
    /// الهجرةَ على قاعدةٍ بعينها. و<c>AddColumn</c> يُصدر <c>ALTER TABLE … ADD COLUMN</c>
    /// عارياً فيرتطم بـ<c>42701</c>، <b>فلا يُكتب صفُّ التاريخ وتعلق سلسلةُ الهجرات هنا
    /// فلا تُطبَّق أيُّ هجرةٍ بعدها أبداً</b>. فهذه ليست احتياطاً بل شرطَ أن تُطبَّق.</para>
    ///
    /// <para>ولا فرعَ لـSQLite: مسارُ التطوير/الاختبار يُهيَّأ بـ<c>EnsureCreated</c> ثمّ
    /// <see cref="BrqoshSchemaUpdater"/>، <b>ولا يمرّ على الهجرات إطلاقاً</b> — فأيُّ فرعٍ
    /// هنا شيفرةٌ ميّتةٌ تُوهم بتغطيةٍ لا تقع.</para>
    ///
    /// <para><b>والصفرُ افتراضٌ صادق:</b> تحدٍّ قديمٌ لم يُعدّ عليه شيءٌ <b>لم تقع عليه
    /// محاولاتٌ مسجَّلة</b> — لا أنّها وقعت وضاعت. ومهلةُ التحدّي عشرُ دقائق، فالصفوفُ
    /// السابقةُ منتهيةٌ أصلاً.</para>
    /// </summary>
    public partial class AddOtpFailedAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE otp_challenges
                    ADD COLUMN IF NOT EXISTS "FailedAttempts" integer NOT NULL DEFAULT 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE otp_challenges
                    DROP COLUMN IF EXISTS "FailedAttempts";
                """);
        }
    }
}
