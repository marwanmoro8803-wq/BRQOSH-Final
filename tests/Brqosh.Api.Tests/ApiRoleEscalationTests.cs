using Brqosh.Api.Middleware;
using Brqosh.Application.Setup;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace Brqosh.Api.Tests;

/// <summary>
/// (CEEF-229 · م2-١) <b>الدور يضبطه الخادم، والترويسة تُضيّق ولا تُوسّع.</b>
///
/// <para><b>العطل الأثقل في مسح الصلاحيات كلّه:</b> كان الوسيط يبدأ من
/// <c>var role = UserRole.Administrator;</c> ثم يسمح لترويسة <c>X-Api-Role</c> بتغييره. أي أن
/// الدور <b>يُعلنه المستدعي</b>، و<b>إغفالُ الترويسة يمنحه أعلى امتياز</b>. فمواضع فرض
/// الصلاحيات السبعون كلُّها — التي أُنفق على مسحها هذا الجهد — كانت <b>بلا أثرٍ واحد</b> أمام
/// أيّ مستدعي API.</para>
///
/// <para>المصادقة بمفتاح مشترك كانت موجودة، لكنها تُثبت <b>مَن</b> يتصل لا <b>ماذا يحقّ له</b>؛
/// وهذا فرقٌ يُخلط كثيراً. والنتيجة أن حملة الاختبارات كلَّها كانت تقيس سطح سطح المكتب وحده،
/// وتصمت عن سطح API تماماً.</para>
///
/// <para><b>وحدُّ الإصلاح مُصرَّح به:</b> لم يُجعل الضبط شرطاً لبدء التشغيل — كما هو مفتاح API —
/// كي لا تُبرَّك ترقيةُ تركيبٍ قائم؛ بل يفشل إلى <c>Viewer</c> في الإنتاج، فتُرفض الكتابة
/// برسالةٍ تسمّي الصلاحية الناقصة. والرفض المسمَّى أوضح من خدمةٍ لا تُقلع.</para>
/// </summary>
[Collection("SequentialEnv")]
public class ApiRoleEscalationTests : IDisposable
{
    private readonly string? _savedRole = Environment.GetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, _savedRole);
        // ‏(م-٤ · ن-٣٨) ‏يُعاد إلى **الاختبار** لا إلى علامة الجهاز — فـ`ResetForTests`
        // ‏تُسقط التجاوزَ فيعود القرارُ إلى المصدر الرسميّ، وهو «إنتاج» على أجهزة النشر.
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Test);
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
    }

    private static async Task<UserRole?> RoleSeenByHandlerAsync(string? roleHeader)
    {
        UserRole? seen = null;
        RequestDelegate next = _ => { seen = SecurityContext.CurrentRole; return Task.CompletedTask; };

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/v1/customers";
        if (roleHeader is not null)
            ctx.Request.Headers["X-Api-Role"] = roleHeader;

        await new ApiSecurityContextMiddleware(next).InvokeAsync(ctx);
        return seen;
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> في الإنتاج، طلبٌ بلا ضبطٍ خادميّ وبلا ترويسة لا يصير مديراً —
    /// بل أقلَّ الأدوار امتيازاً. هذا بالضبط ما كان معكوساً.
    /// </summary>
    [Fact]
    public async Task Production_WithoutConfiguration_FailsClosedToViewer()
    {
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, null);
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

        Assert.Equal(UserRole.Viewer, await RoleSeenByHandlerAsync(roleHeader: null));
    }

    /// <summary>
    /// <b>وهذا هو منعُ التصعيد نفسه:</b> مستدعٍ مضبوطٌ كـ«مُطّلع» يطلب صراحةً دور المدير —
    /// فلا يُمنَح. لولا هذه لكان يكفي إرسال ترويسةٍ واحدة لإبطال الطبقة كلّها.
    /// </summary>
    [Fact]
    public async Task RequestedRole_CannotWidenBeyondTheConfiguredRole()
    {
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, "Viewer");
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

        Assert.Equal(UserRole.Viewer, await RoleSeenByHandlerAsync("Administrator"));
        Assert.Equal(UserRole.Viewer, await RoleSeenByHandlerAsync("Accountant"));
    }

    /// <summary>
    /// ويبقى التضييق الطوعيّ مشروعاً — فلمستدعٍ يملك امتيازاً أن يتنازل عنه لعمليةٍ بعينها،
    /// وهو استعمالٌ صحيح لمبدأ أقلّ امتياز. الإصلاح منع الاتّساع لا التضييق.
    /// </summary>
    [Fact]
    public async Task RequestedRole_MayNarrowVoluntarily()
    {
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, "Administrator");
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

        Assert.Equal(UserRole.Viewer, await RoleSeenByHandlerAsync("Viewer"));
        Assert.Equal(UserRole.Cashier, await RoleSeenByHandlerAsync("Cashier"));
    }

    /// <summary>
    /// وترويسةٌ بقيمةٍ لا تُفهم لا تُرقّي ولا تُسقط — تُتجاهَل ويبقى الضبط الخادميّ.
    /// فقيمةٌ مكتوبة خطأً يجب ألّا تُغيّر التفويض في أيّ اتجاه.
    /// </summary>
    [Fact]
    public async Task UnparseableRoleHeader_LeavesTheConfiguredRoleIntact()
    {
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, "Accountant");
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

        Assert.Equal(UserRole.Accountant, await RoleSeenByHandlerAsync("Superuser"));
        Assert.Equal(UserRole.Accountant, await RoleSeenByHandlerAsync(""));
    }

    /// <summary>
    /// وخارج الإنتاج يبقى الافتراض «مدير النظام» — فلا يتعطّل تطويرٌ ولا تكاملٌ تجريبيّ،
    /// وهو حدُّ الإصلاح المُصرَّح به لا سهوٌ فيه.
    /// </summary>
    [Fact]
    public async Task OutsideProduction_DefaultRemainsAdministrator()
    {
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, null);
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Development);

        Assert.Equal(UserRole.Administrator, await RoleSeenByHandlerAsync(roleHeader: null));
    }

    /// <summary>والضبط الخادميّ يُحترم في كل بيئة — فهو مصدر الحقيقة لا الافتراض.</summary>
    [Fact]
    public async Task ConfiguredRole_IsHonouredInEveryEnvironment()
    {
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, "Cashier");

        AppEnvironmentConfig.ForceForTests(AppEnvironment.Development);
        Assert.Equal(UserRole.Cashier, await RoleSeenByHandlerAsync(roleHeader: null));

        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
        Assert.Equal(UserRole.Cashier, await RoleSeenByHandlerAsync(roleHeader: null));
    }
}
