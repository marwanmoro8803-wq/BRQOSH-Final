using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyFixedAssetLink : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// (CEEF-232 · ع-١) ‏ربطُ العقار بأصلٍ ثابتٍ يُهلَك.
        ///
        /// <para>‏<b>وصياغةٌ يدويّةٌ بـ<c>IF NOT EXISTS</c></b> — على سُنّة الهجرتين قبلها:
        /// المُرقِّعُ اليدويّ يُضيف العمودَ عند الإقلاع كذلك، <b>و<c>AddColumn</c> المولَّدُ
        /// يرمي على ما هو موجود</b> فتعلق السلسلةُ على قواعدَ رُقِّعت مسبقاً.</para>
        ///
        /// <para>‏<b>ولاغٍ افتراضاً</b>: عقاراتٌ قائمةٌ <b>تبقى بلا قيمةٍ دفتريّة</b> حتى
        /// يربطها المحاسبُ بعلمه. <b>وربطٌ آليٌّ بالتخمين أسوأُ من غيابه</b> — يُنشئ إهلاكاً
        /// على تكلفةٍ لم يُقرّها أحد، <b>ويُنقص ربحَ سنواتٍ بأرقامٍ مُختلَقة</b>.</para>
        /// </summary>
        protected override void Up(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE properties ADD COLUMN IF NOT EXISTS ""FixedAssetId"" BIGINT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder b)
        {
            b.Sql(@"ALTER TABLE properties DROP COLUMN IF EXISTS ""FixedAssetId"";");
        }
    }
}
