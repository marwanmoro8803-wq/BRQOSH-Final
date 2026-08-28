using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;

namespace Brqosh.Infrastructure.Security;

/// <summary>سياق المستخدم الحالي للتحقق من الصلاحيات على مستوى الخدمات — fail-closed.</summary>
public static class SecurityContext
{
    private static readonly AsyncLocal<UserRole?> CurrentRoleSlot = new();
    private static readonly AsyncLocal<string?> CurrentUsernameSlot = new();

    // (CEEF-229 · م2-١) حُذف `AutomatedTestBypass` — ولم يكن نقصَ اختبارٍ بل **تناقضاً مكتوباً**.
    //
    // كان متغيّر البيئة `BRQOSH_TEST=1` يُطفئ الحرّاس الأربعة كلَّها متى كان الدور معدوماً.
    // ونصَّ تعليقٌ في `TestEnvironmentInitializer` على أن الاختبارات **لا تضبطه** «لأنه يُطفئ
    // طبقة الصلاحيات ولا نريد ذلك هنا» — بينما `test.runsettings` على بُعد مجلّدين يضبطه.
    // فالكود يدّعي خاصيّةً يُبطلها ملفُ إعداد، والادّعاء بقي مكتوباً بلا أن يقيسه أحد.
    //
    // وأثرُه المقيس: نزعُ المتغيّر أسقط **73 اختبارة من 529**، كلَّها برسالة واحدة — «لا يوجد
    // مستخدم مسجّل». أي أن ثلاثة وسبعين مساراً ماليّاً كانت تُختبر بلا فاعل، وهو وضعٌ **يستحيل
    // في الإنتاج**. فالاختبارات كانت تشهد لنظامٍ غير الذي يُشحن.
    //
    // والحذف — لا الإصلاح — هو الصواب: أن يبقى في المنتج مسارٌ يُبطل الصلاحيات بمتغيّر بيئة
    // خطرٌ قائم بذاته، ولو حُرس بـ`!IsProduction`. والبديل ليس تساهلاً بل تصريحاً: كل اختبار
    // يُعلن فاعله. انظر `TestActor` في مشروع الاختبارات.
    //
    // ملاحظة: `BRQOSH_TEST` نفسه لم يُحذف — لا يزال يُعلم `AppEnvironment.Decide` أن العملية
    // عمليةُ اختبار. ما حُذف هو **أثره على الصلاحيات** وحده.

    public static UserRole? CurrentRole
    {
        get => CurrentRoleSlot.Value;
        set => CurrentRoleSlot.Value = value;
    }

    public static string? CurrentUsername
    {
        get => CurrentUsernameSlot.Value;
        set => CurrentUsernameSlot.Value = value;
    }

    public static string ResolveUsername(string? passedUsername = null)
    {
        if (!string.IsNullOrWhiteSpace(CurrentUsername))
            return CurrentUsername.Trim();
        if (!string.IsNullOrWhiteSpace(passedUsername))
            return passedUsername.Trim();
        return "system";
    }

    public static void EnsureCanPost()
    {
        if (CurrentRole is null)
            throw new AccountingException("لا يوجد مستخدم مسجّل — الترحيل مرفوض.");
        if (CurrentRole is UserRole.Viewer)
            throw new AccountingException("لا تملك صلاحية الترحيل أو الإلغاء.");
    }

    public static void EnsureCanPostAccounting()
    {
        if (CurrentRole is null)
            throw new AccountingException("لا يوجد مستخدم مسجّل — العملية المحاسبية مرفوضة.");
        if (CurrentRole is UserRole.Viewer or UserRole.Cashier)
            throw new AccountingException("لا تملك صلاحية العمليات المحاسبية المتقدمة.");
    }

    public static void EnsureCanCancelOrReverse()
    {
        if (CurrentRole is null)
            throw new AccountingException("لا يوجد مستخدم مسجّل — الإلغاء أو العكس مرفوض.");
        if (CurrentRole is UserRole.Viewer or UserRole.Cashier)
            throw new AccountingException("لا تملك صلاحية الإلغاء أو العكس.");
    }

    public static void EnsureAdministrator()
    {
        if (CurrentRole is null)
            throw new AccountingException("لا يوجد مستخدم مسجّل — العملية الإدارية مرفوضة.");
        if (CurrentRole is not UserRole.Administrator)
            throw new AccountingException("هذه العملية تتطلب صلاحية مدير النظام.");
    }
}
