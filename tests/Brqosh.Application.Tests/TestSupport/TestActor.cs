using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · م2-١) <b>مَن يُجري هذا الاختبار؟</b> — سؤالٌ لم يكن يُطرح.
///
/// <para>كان <c>BRQOSH_TEST=1</c> يُطفئ طبقة الصلاحيات كاملةً متى كان الدور معدوماً، فكانت
/// <b>73 اختبارة من 529</b> ترحّل مستنداتٍ مالية <b>بلا فاعل على الإطلاق</b> — وهو وضعٌ يستحيل
/// في الإنتاج، إذ لا ترحيل بلا مستخدم مسجَّل. أي أن تلك الاختبارات كانت تشهد لنظامٍ غير الذي
/// يُشحن. حُذف التجاوز من <see cref="SecurityContext"/>، وصار على كل اختبارٍ أن يُصرّح بفاعله.</para>
///
/// <para><b>ولماذا أقلّ صلاحيةٍ كافية لا «مدير» دائماً:</b> اختيار الدور هنا ليس إجراءً شكلياً
/// بل <b>توثيقٌ تنفيذيّ</b> لما تتطلّبه العملية فعلاً. صنفٌ يُعلن <see cref="Cashier"/> يُثبت
/// بمروره أن أمين الصندوق يستطيع البيع؛ ولو أضاف أحدٌ يوماً <c>EnsureCanPostAccounting</c> إلى
/// مسار البيع لسقط ذلك الصنف فوراً وأخبرنا أن الصلاحية المطلوبة تغيّرت. ولو أعلنّا «مدير» في
/// كل مكانٍ لما أخبرنا شيءٌ بشيء — ولعاد التجاوز باسمٍ آخر.</para>
///
/// <para><b>لا يلزم تصفيرها:</b> <c>SecurityContext.CurrentRole</c> يقوم على <c>AsyncLocal</c>،
/// وxUnit يُنشئ نسخةً لكل اختبارة، فالقيمة المضبوطة في البانية تسري إلى اختبارتها وحدها ولا
/// تتسرّب إلى غيرها.</para>
/// </summary>
public static class TestActor
{
    /// <summary>مدير النظام — للعمليات الإدارية وحدها (<c>EnsureAdministrator</c>).</summary>
    public static void Administrator(string username = "admin") => Set(UserRole.Administrator, username);

    /// <summary>محاسب — يرحّل ويُلغي ويُجري العمليات المحاسبية المتقدّمة. الفاعل الافتراضيّ لأغلب المسارات.</summary>
    public static void Accountant(string username = "accountant") => Set(UserRole.Accountant, username);

    /// <summary>أمين صندوق — يبيع ويُحصّل، ولا يملك العمليات المحاسبية المتقدّمة ولا الإلغاء.</summary>
    public static void Cashier(string username = "cashier") => Set(UserRole.Cashier, username);

    /// <summary>مُطّلع — يقرأ ولا يكتب. يُستعمل لقياس <b>الرفض</b>.</summary>
    public static void Viewer(string username = "viewer") => Set(UserRole.Viewer, username);

    /// <summary>لا فاعل — يُستعمل لقياس أن العملية تُرفض بلا مستخدم مسجَّل.</summary>
    public static void None()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
    }

    private static void Set(UserRole role, string username)
    {
        SecurityContext.CurrentRole = role;
        SecurityContext.CurrentUsername = username;
    }
}
