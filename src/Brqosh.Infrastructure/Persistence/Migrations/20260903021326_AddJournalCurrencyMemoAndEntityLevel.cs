using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalCurrencyMemoAndEntityLevel : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// ‏مذكرةُ العملة على سطر القيد، وعلامةُ «على مستوى المنشأة».
        ///
        /// <para>‏<b>وصياغةٌ يدويّةٌ بـ<c>IF NOT EXISTS</c></b>: المُرقِّعُ اليدويّ
        /// (<c>BrqoshSchemaUpdater</c>) يُضيف هذه الأعمدةَ عند الإقلاع كذلك، <b>والمُولَّدُ
        /// يرمي على ما هو موجود</b> فتعلق سلسلةُ الهجرات على قواعدَ قائمة.</para>
        ///
        /// <para>‏<b>وكلُّها لاغيةٌ أو <c>false</c> افتراضاً</b> — فلا يتغيّر حرفٌ في قيدٍ
        /// مرحَّل. و<c>'SAR'</c> افتراضاً <b>كذبٌ على الماضي</b>: يجعل قيوداً رُحّلت بلا
        /// مذكرةٍ تبدو موثَّقةً بعملةٍ لم يُدخلها أحد.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE journal_entry_lines ADD COLUMN IF NOT EXISTS ""CurrencyCode"" character varying(3) NULL;");
            b.Sql(@"ALTER TABLE journal_entry_lines ADD COLUMN IF NOT EXISTS ""ExchangeRate"" NUMERIC(18,6) NULL;");
            b.Sql(@"ALTER TABLE journal_entry_lines ADD COLUMN IF NOT EXISTS ""ForeignAmount"" NUMERIC(18,2) NULL;");
            b.Sql(@"ALTER TABLE journal_entries ADD COLUMN IF NOT EXISTS ""IsEntityLevel"" BOOLEAN NOT NULL DEFAULT FALSE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE journal_entry_lines DROP COLUMN IF EXISTS ""CurrencyCode"";");
            b.Sql(@"ALTER TABLE journal_entry_lines DROP COLUMN IF EXISTS ""ExchangeRate"";");
            b.Sql(@"ALTER TABLE journal_entry_lines DROP COLUMN IF EXISTS ""ForeignAmount"";");
            b.Sql(@"ALTER TABLE journal_entries DROP COLUMN IF EXISTS ""IsEntityLevel"";");
        }
    }
}
