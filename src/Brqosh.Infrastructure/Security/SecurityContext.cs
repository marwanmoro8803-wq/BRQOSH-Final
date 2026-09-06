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

    // ‏(م-٢ · ن-٢٦) ‏**قوائمُ منعٍ لا سماح — وقيمةٌ خارج التعداد ليست على قائمة أحد.**
    //
    // ‏كانت الحرّاسُ الثلاثةُ تسأل «أليس Viewer؟» و«أليس Viewer أو Cashier؟». وذلك يحمي
    // ‏ما دامت القيمُ أربعاً معروفة. لكنّ `Enum.TryParse<UserRole>` **يقبل السلاسل الرقميّة**،
    // ‏فترويسةُ `X-Api-Role: 99` تُنتج `(UserRole)99` — وهي ليست `Viewer` ولا `Cashier`
    // ‏ولا `null`، **فتمرّ من الثلاثة جميعاً**.
    //
    // ‏مِجسٌّ منفَّذ بنسخٍ حرفيّةٍ من الدالّتين (‏exit=0):
    // ‏  X-Api-Role=Viewer         ⟸ 3   CanPost=False CanPostAccounting=False CanCancel=False
    // ‏  X-Api-Role=Administrator  ⟸ 3   (التسميةُ لا تُصعّد — التضييقُ يعمل)
    // ‏  X-Api-Role=99             ⟸ 99  **CanPost=True  CanPostAccounting=True  CanCancel=True**
    // ‏  X-Api-Role=2147483647     ⟸ …   **مثلُها**
    //
    // ‏والصامدُ الوحيد `EnsureAdministrator` — لأنّه وحدَه كان **فحصَ مساواة**، أي قائمةَ سماح.
    //
    // ‏فصارت الثلاثةُ قوائمَ سماحٍ صريحة. والسلوكُ للأدوار الأربعة المعروفة **لم يتغيّر
    // ‏حرفاً**؛ ما تغيّر أنّ ما ليس دوراً معروفاً يُرفض بدل أن يُقبل. وهذا هو الفرقُ بين
    // ‏«‏من نمنعه؟» و«‏من نسمح له؟»: الأولى تنسى ما لم يخطر ببالها، والثانية لا تستطيع.
    //
    // ‏(‏ومُنع المصدرُ كذلك: `ApiSecuritySettings` صار يرفض قيمةً خارج التعداد عند التحليل،
    // ‏في موضعَيه معاً. فالدفاعُ طبقتان — لأنّ المصدرَ قد يُضاف له ثالثٌ يوماً.)

    private static readonly UserRole[] CanPostRoles =
        [UserRole.Administrator, UserRole.Accountant, UserRole.Cashier];

    private static readonly UserRole[] AccountingRoles =
        [UserRole.Administrator, UserRole.Accountant];

    public static void EnsureCanPost()
    {
        if (CurrentRole is null)
            throw new AccountingException("لا يوجد مستخدم مسجّل — الترحيل مرفوض.");
        if (!CanPostRoles.Contains(CurrentRole.Value))
            throw new AccountingException("لا تملك صلاحية الترحيل أو الإلغاء.");
    }

    public static void EnsureCanPostAccounting()
    {
        if (CurrentRole is null)
            throw new AccountingException("لا يوجد مستخدم مسجّل — العملية المحاسبية مرفوضة.");
        if (!AccountingRoles.Contains(CurrentRole.Value))
            throw new AccountingException("لا تملك صلاحية العمليات المحاسبية المتقدمة.");
    }

    public static void EnsureCanCancelOrReverse()
    {
        if (CurrentRole is null)
            throw new AccountingException("لا يوجد مستخدم مسجّل — الإلغاء أو العكس مرفوض.");
        if (!AccountingRoles.Contains(CurrentRole.Value))
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
