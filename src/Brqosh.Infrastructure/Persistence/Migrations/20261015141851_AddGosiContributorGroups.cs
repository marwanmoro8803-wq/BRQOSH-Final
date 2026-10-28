using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brqosh.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ‏ فئتا المشتركين في نظام التأمينات الجديد (٢٠٢٤) وجدولُ نسبته.
    ///
    /// <para>‏<b>والقيمُ الافتراضيّةُ للصفوف القائمة هي افتراضاتُ النطاق لا أصفار</b>: المولَّدُ (<c>AddColumn</c>) كان يضع ٠٪ و٠٠٠١-٠١-٠١
    /// وصفرَ زيادات — فموظّفُ النظام الجديد في كلّ منشأةٍ قائمةٍ كان سيُشترك بلا شيء. ومُطاوِعةٌ للتكرار كأخواتها.</para>
    /// </summary>
    public partial class AddGosiContributorGroups : Migration
    {
        protected override void Up(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE employees ADD COLUMN IF NOT EXISTS "GosiRegime" character varying(16) NOT NULL DEFAULT 'Legacy';""");
            b.Sql("""ALTER TABLE gosi_policies ADD COLUMN IF NOT EXISTS "NewLawEmployeePercent" numeric(9,4) NOT NULL DEFAULT 9.75;""");
            b.Sql("""ALTER TABLE gosi_policies ADD COLUMN IF NOT EXISTS "NewLawEmployerPercent" numeric(9,4) NOT NULL DEFAULT 11.75;""");
            b.Sql("""ALTER TABLE gosi_policies ADD COLUMN IF NOT EXISTS "NewLawAnnualStepPercent" numeric(9,4) NOT NULL DEFAULT 0.5;""");
            b.Sql("""ALTER TABLE gosi_policies ADD COLUMN IF NOT EXISTS "NewLawFirstStep" date NOT NULL DEFAULT DATE '2025-07-01';""");
            b.Sql("""ALTER TABLE gosi_policies ADD COLUMN IF NOT EXISTS "NewLawSteps" integer NOT NULL DEFAULT 4;""");
        }

        protected override void Down(MigrationBuilder b)
        {
            b.Sql("""ALTER TABLE gosi_policies DROP COLUMN IF EXISTS "NewLawSteps";""");
            b.Sql("""ALTER TABLE gosi_policies DROP COLUMN IF EXISTS "NewLawFirstStep";""");
            b.Sql("""ALTER TABLE gosi_policies DROP COLUMN IF EXISTS "NewLawAnnualStepPercent";""");
            b.Sql("""ALTER TABLE gosi_policies DROP COLUMN IF EXISTS "NewLawEmployerPercent";""");
            b.Sql("""ALTER TABLE gosi_policies DROP COLUMN IF EXISTS "NewLawEmployeePercent";""");
            b.Sql("""ALTER TABLE employees DROP COLUMN IF EXISTS "GosiRegime";""");
        }
    }
}
