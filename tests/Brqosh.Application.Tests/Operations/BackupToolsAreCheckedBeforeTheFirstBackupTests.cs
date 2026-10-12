using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٥ · ‏`installer-deployment-ops#10` · ‏`data-upgrade-performance#6`) <b>أدواتُ النسخ تُفحص قبل أوّل نسخة —
/// ‏لا تُكتشف بفشلها.</b>
///
/// <para>‏النسخُ المجدولُ يشترط <c>pg_dump</c> و<c>pg_restore</c> على المحطّة، والمُنصِّبُ لا يشحنهما ولا يفحصهما — فكان أوّلُ ما يُعرف
/// ‏غيابُهما أوّلَ ليلةٍ لا تُنسخ فيها الدفاتر. <b>والأقدمُ من الخادم كالغائب</b>: <c>pg_dump 16</c> يرفض خادمَ 17 صراحةً. فالحكمُ هنا
/// ‏يُعرض في «حالة النظام» من أوّل يوم.</para>
/// </summary>
public class BackupToolsAreCheckedBeforeTheFirstBackupTests
{
    private static PgToolLocator.Tool Tool(string name, int major, int minor = 0) =>
        new($@"C:\Program Files\PostgreSQL\{major}\bin\{name}.exe", new Version(major, minor));

    [Fact]
    public void Matching_tools_are_ready()
    {
        var verdict = BackupToolReadiness.Assess(Tool("pg_dump", 17, 2), Tool("pg_restore", 17, 2), serverMajor: 17);

        Assert.True(verdict.Ready);
        Assert.Contains("17.2", verdict.Message, StringComparison.Ordinal);
    }

    /// <summary>‏والأحدثُ من الخادم صالح — PostgreSQL يقبل أداةً أحدث ويرفض الأقدم.</summary>
    [Fact]
    public void Newer_tools_than_the_server_are_ready()
    {
        Assert.True(BackupToolReadiness.Assess(Tool("pg_dump", 17), Tool("pg_restore", 17), serverMajor: 16).Ready);
    }

    [Fact]
    public void A_missing_pg_dump_is_not_ready_and_says_what_to_install()
    {
        var verdict = BackupToolReadiness.Assess(null, Tool("pg_restore", 17), serverMajor: 17);

        Assert.False(verdict.Ready);
        Assert.Contains("pg_dump", verdict.Message, StringComparison.Ordinal);
        Assert.Contains("17", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_pg_restore_is_not_ready()
    {
        var verdict = BackupToolReadiness.Assess(Tool("pg_dump", 17), null, serverMajor: 17);

        Assert.False(verdict.Ready);
        Assert.Contains("pg_restore", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tools_older_than_the_server_are_not_ready()
    {
        var verdict = BackupToolReadiness.Assess(Tool("pg_dump", 16, 14), Tool("pg_restore", 16, 14), serverMajor: 17);

        Assert.False(verdict.Ready);
        Assert.Contains("16.14", verdict.Message, StringComparison.Ordinal);
    }

    /// <summary>شاهدُ ضبط: خادمٌ لم يُعرف إصدارُه لا يُسقط أداتين موجودتين.</summary>
    [Fact]
    public void An_unknown_server_version_does_not_fail_present_tools()
    {
        Assert.True(BackupToolReadiness.Assess(Tool("pg_dump", 16), Tool("pg_restore", 16), serverMajor: null).Ready);
    }

    [Theory]
    [InlineData("170002", 17)]
    [InlineData("160014", 16)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    public void The_server_major_is_read_from_server_version_num(string setting, int? major) =>
        Assert.Equal(major, BackupToolReadiness.ServerMajor(setting));
}
