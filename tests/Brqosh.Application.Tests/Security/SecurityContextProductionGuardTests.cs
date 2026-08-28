using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (CEEF-229 · م2-١) <b>جدول الحقيقة لطبقة الصلاحيات</b> — أربعة حرّاس × خمس حالات فاعل.
///
/// <para><b>ما كان هذا الملف يقيسه، وما صار يقيسه:</b> كان يُثبّت أن تجاوز RBAC بـ
/// <c>BRQOSH_TEST=1</c> «ميتٌ في الإنتاج وحيٌّ خارجه» — أي أنه كان <b>يحرس وجود التجاوز</b>.
/// وقد حُذف التجاوز من أصله في هذه الدفعة، فصار هذا الملف يُثبّت النقيض: <b>لا شيء يُبطل
/// الصلاحيات، في أيّ بيئة، بأيّ متغيّر</b>.</para>
///
/// <para><b>لماذا حُذف:</b> نصَّ تعليقٌ في <c>TestEnvironmentInitializer</c> على أن الاختبارات
/// لا تضبط <c>BRQOSH_TEST</c> «لأنه يُطفئ طبقة الصلاحيات ولا نريد ذلك هنا» — بينما
/// <c>test.runsettings</c> يضبطه. فالكود يدّعي خاصيّةً يُبطلها ملفُ إعداد. ونزعُ المتغيّر أسقط
/// <b>73 اختبارة من 529</b>، كلَّها برسالة «لا يوجد مستخدم مسجّل»: ثلاثةٌ وسبعون مساراً ماليّاً
/// كانت تُختبر بلا فاعل، وهو وضعٌ يستحيل في الإنتاج.</para>
///
/// <para>الجدول أدناه ليس فحص نصّ ولا فحص وجود: <b>يستدعي كل حارسٍ فعلاً</b> تحت كل حالة فاعل
/// ويقيس الرمي أو عدمه.</para>
/// </summary>
[Collection("SequentialEnv")]
public class SecurityContextProductionGuardTests : IDisposable
{
    private readonly UserRole? _savedRole = SecurityContext.CurrentRole;
    private readonly string? _savedUsername = SecurityContext.CurrentUsername;
    private readonly string? _savedTestVar = Environment.GetEnvironmentVariable("BRQOSH_TEST");

    public void Dispose()
    {
        AppEnvironmentConfig.ResetForTests();
        Environment.SetEnvironmentVariable("BRQOSH_TEST", _savedTestVar);
        SecurityContext.CurrentRole = _savedRole;
        SecurityContext.CurrentUsername = _savedUsername;
    }

    private static readonly (string Name, Action Guard)[] AllGuards =
    [
        ("EnsureCanPost", SecurityContext.EnsureCanPost),
        ("EnsureCanPostAccounting", SecurityContext.EnsureCanPostAccounting),
        ("EnsureCanCancelOrReverse", SecurityContext.EnsureCanCancelOrReverse),
        ("EnsureAdministrator", SecurityContext.EnsureAdministrator),
    ];

    private static void AssertRefuses(string guardName, Action guard) =>
        Assert.True(Record.Exception(guard) is AccountingException,
            $"{guardName} كان يجب أن يرفض، فلم يرفض.");

    private static void AssertAllows(string guardName, Action guard) =>
        Assert.True(Record.Exception(guard) is null,
            $"{guardName} كان يجب أن يسمح، فرفض.");

    // ── لا فاعل: يُرفض كل شيء، في كل بيئة ──────────────────────────────────

    /// <summary>
    /// <b>القياس المركزيّ.</b> بلا مستخدم مسجَّل تُرفض العمليات الأربع — في الإنتاج وفي
    /// الاختبار وفي التطوير سواءً. لا بيئة «متساهلة».
    /// </summary>
    [Theory]
    [InlineData(AppEnvironment.Production)]
    [InlineData(AppEnvironment.Test)]
    [InlineData(AppEnvironment.Development)]
    public void NoActor_IsRefusedByEveryGuard_InEveryEnvironment(AppEnvironment environment)
    {
        AppEnvironmentConfig.ForceForTests(environment);
        SecurityContext.CurrentRole = null;

        foreach (var (name, guard) in AllGuards)
            AssertRefuses(name, guard);
    }

    /// <summary>
    /// <b>وهذا هو حارس الانحدار المباشر:</b> ضبطُ <c>BRQOSH_TEST=1</c> — المتغيّر الذي كان
    /// يُبطل الطبقة كلَّها — لم يعد يفعل شيئاً في أيّ بيئة. لو أُعيد التجاوز يوماً لسقطت هذه
    /// الاختبارة وحدها، بلا حاجة إلى أن يلاحظ أحدٌ أن 73 اختبارة صارت تمرّ بلا فاعل.
    /// </summary>
    [Theory]
    [InlineData(AppEnvironment.Production)]
    [InlineData(AppEnvironment.Test)]
    [InlineData(AppEnvironment.Development)]
    public void TestEnvironmentVariable_CannotDisarmAnyGuard(AppEnvironment environment)
    {
        Environment.SetEnvironmentVariable("BRQOSH_TEST", "1");
        AppEnvironmentConfig.ForceForTests(environment);
        SecurityContext.CurrentRole = null;

        foreach (var (name, guard) in AllGuards)
            AssertRefuses(name, guard);
    }

    // ── جدول الأدوار: ما يستطيعه كلٌّ وما لا يستطيعه ────────────────────────

    /// <summary>المُطّلع يقرأ ولا يكتب — يُرفض عند الحرّاس الأربعة جميعاً.</summary>
    [Fact]
    public void Viewer_IsRefusedByEveryGuard()
    {
        SecurityContext.CurrentRole = UserRole.Viewer;

        foreach (var (name, guard) in AllGuards)
            AssertRefuses(name, guard);
    }

    /// <summary>
    /// أمين الصندوق يبيع ويُحصّل — ولا يملك العمليات المحاسبية المتقدّمة ولا الإلغاء ولا الإدارة.
    /// <b>وهذا التمييز هو ما كان التجاوز يمحوه:</b> بلا فاعل كان كل شيء مسموحاً.
    /// </summary>
    [Fact]
    public void Cashier_CanPost_ButNotAccountingNorCancelNorAdmin()
    {
        SecurityContext.CurrentRole = UserRole.Cashier;

        AssertAllows("EnsureCanPost", SecurityContext.EnsureCanPost);
        AssertRefuses("EnsureCanPostAccounting", SecurityContext.EnsureCanPostAccounting);
        AssertRefuses("EnsureCanCancelOrReverse", SecurityContext.EnsureCanCancelOrReverse);
        AssertRefuses("EnsureAdministrator", SecurityContext.EnsureAdministrator);
    }

    /// <summary>المحاسب يرحّل ويُلغي ويُجري المحاسبة المتقدّمة — ولا يُدير النظام.</summary>
    [Fact]
    public void Accountant_CanPostAndCancel_ButIsNotAdministrator()
    {
        SecurityContext.CurrentRole = UserRole.Accountant;

        AssertAllows("EnsureCanPost", SecurityContext.EnsureCanPost);
        AssertAllows("EnsureCanPostAccounting", SecurityContext.EnsureCanPostAccounting);
        AssertAllows("EnsureCanCancelOrReverse", SecurityContext.EnsureCanCancelOrReverse);
        AssertRefuses("EnsureAdministrator", SecurityContext.EnsureAdministrator);
    }

    /// <summary>ومدير النظام يمرّ من الأربعة — وإلا لتعطّل النظام على مالكه.</summary>
    [Fact]
    public void Administrator_PassesEveryGuard()
    {
        SecurityContext.CurrentRole = UserRole.Administrator;

        foreach (var (name, guard) in AllGuards)
            AssertAllows(name, guard);
    }

    // ── الرسائل تقول ما ينقص ───────────────────────────────────────────────

    /// <summary>
    /// الرسالة تُفرّق بين «لا مستخدم» و«لا صلاحية» — فالأولى خللُ جلسة والثانية قرارُ سياسة،
    /// ولا يُعالجان بالطريقة نفسها. رسالةٌ واحدة للحالتين تُرسل المستخدم في الاتجاه الخطأ.
    /// </summary>
    [Fact]
    public void RefusalMessages_DistinguishNoSessionFromNoPermission()
    {
        SecurityContext.CurrentRole = null;
        var noSession = Assert.Throws<AccountingException>(SecurityContext.EnsureCanPost);

        SecurityContext.CurrentRole = UserRole.Viewer;
        var noPermission = Assert.Throws<AccountingException>(SecurityContext.EnsureCanPost);

        Assert.Contains("لا يوجد مستخدم مسجّل", noSession.Message);
        Assert.Contains("لا تملك صلاحية", noPermission.Message);
        Assert.NotEqual(noSession.Message, noPermission.Message);
    }
}
