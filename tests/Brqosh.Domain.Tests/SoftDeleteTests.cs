using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;

namespace Brqosh.Domain.Tests;

/// <summary>
/// (CEEF-229 · م2-١٧) <b>هذا الملف كان يُثبّت العطل.</b>
///
/// <para>كانت الاختبارة الأولى اسمُها <c>Customer_Deactivate_SetsDeletedAtUtcAndIsActive</c>
/// وتؤكّد <c>Assert.NotNull(customer.DeletedAtUtc)</c> — أي أنها <b>تحرس</b> أن الإيقاف يحذف.
/// وزرُّ الشاشة مكتوبٌ عليه «إيقاف» ورسالةُ التأكيد تسأل «إيقاف العميل؟»، فكان الوعد إيقافاً
/// والفعلُ حذفاً، والاختبارُ يحرس الحذف.</para>
///
/// <para>وقد أُصلح هذا في <c>Account</c> ضمن م0-١٣ («الإيقاف حالةٌ تشغيلية لا واقعةٌ محاسبية»)
/// وبقيت سبعةُ كياناتٍ أخرى بالجسم القديم. فأيُّ إصلاحٍ يقول الحقيقة كان سيُسقط هذا الملف —
/// وهو ما وقع.</para>
/// </summary>
public class SoftDeleteTests
{
    /// <summary>الإيقاف يوقف ولا يحذف — فيبقى العميل ظاهراً في قوائمه موسوماً «موقوف».</summary>
    [Fact]
    public void Customer_Deactivate_SuspendsWithoutDeleting()
    {
        var customer = Customer.Create(1, "عميل");
        customer.Deactivate();

        Assert.False(customer.IsActive);
        Assert.Null(customer.DeletedAtUtc);
    }

    /// <summary>ويُعاد تفعيله — فالإيقاف ليس قراراً بلا رجعة.</summary>
    [Fact]
    public void Customer_Reactivate_RestoresIt()
    {
        var customer = Customer.Create(1, "عميل");
        customer.Deactivate();
        customer.Reactivate();

        Assert.True(customer.IsActive);
        Assert.Null(customer.DeletedAtUtc);
    }

    /// <summary>وتكرارُ الإيقاف لا يُغيّر شيئاً.</summary>
    [Fact]
    public void Customer_Deactivate_IsIdempotent()
    {
        var customer = Customer.Create(1, "عميل");
        customer.Deactivate();
        customer.Deactivate();

        Assert.False(customer.IsActive);
        Assert.Null(customer.DeletedAtUtc);
    }

    /// <summary>
    /// <b>والمستخدم يبقى استثناءً مقصوداً:</b> إيقاف حساب مستخدمٍ إجراءٌ أمنيّ يجب أن يمنع
    /// الدخول فوراً وأن يُخفي الحساب من قوائم التعيين — فالحذف الناعم هنا هو المطلوب، وله
    /// <c>Activate()</c> تُعيده. مذكورٌ صراحةً كي لا يُعمَّم إصلاح الكيانات الأخرى عليه سهواً.
    /// </summary>
    [Fact]
    public void AppUser_Deactivate_IsASoftDelete_AndActivateClearsIt()
    {
        var user = AppUser.Create(1, "u1", "hash", "مستخدم");
        user.Deactivate();
        Assert.NotNull(user.DeletedAtUtc);

        user.Activate();
        Assert.Null(user.DeletedAtUtc);
        Assert.True(user.IsActive);
    }
}
