using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠٠ · treasury-payroll-assets#12) آخرُ يومٍ أُهلك قبل الدفاتر — عمودٌ يقبل الفراغ، وفارغُه أصلٌ يُقتنى
    /// في الدفاتر كما كان كلُّ أصلٍ قبله؛ فلا تعبئةَ ولا تغيُّرَ لأصلٍ قائم.
    ///
    /// <para><b>مُطاوِعةٌ للتكرار</b> على سُنّة الهجرات قبلها — <c>BrqoshSchemaUpdater</c> يُضيف العمودَ عند الإقلاع كذلك،
    /// والمولَّدُ (<c>AddColumn</c>) يرمي على ما هو موجود. والنوعُ ما ولّده <c>dotnet ef</c> حرفاً: <c>date</c> يقبل الفراغ.</para>
    /// </summary>
    public partial class AddFixedAssetDepreciatedThrough : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE fixed_assets ADD COLUMN IF NOT EXISTS "DepreciatedThrough" date NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE fixed_assets DROP COLUMN IF EXISTS "DepreciatedThrough";""");
        }
    }
}
