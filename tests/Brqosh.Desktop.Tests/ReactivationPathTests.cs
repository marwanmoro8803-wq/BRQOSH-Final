using System.IO;
using System.Reflection;
using Brqosh.Desktop.Views.Pages;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م2-٢٣) <b>كلُّ زرِّ «إيقاف» يقابله طريقُ عودة.</b>
///
/// <para><b>نصفُ إصلاحٍ لا يُحتسب.</b> فُصل الإيقافُ عن الحذف في ثمانية كيانات وأُضيفت
/// <c>Reactivate()</c> — ثم <b>لم يُستدعَ أحدُها من أيّ شاشة</b>: الصفحات تُرشّح <c>IsActive</c>
/// بلا مفتاح «أظهر الموقوف»، فالموقوف يختفي من قائمته ولا زرَّ يُعيده. بابُ العودة فُتح في
/// النموذج ولم يُوصَل بالواجهة — والمستخدم يرى ما كان يراه قبل الإصلاح تماماً.</para>
///
/// <para>الحارس يقيس <b>الاقتران</b> لا وجودَ الزرّ: أيُّ صفحةٍ تُعلن معالجاً للإيقاف يجب أن
/// تُعلن معالجاً للعودة ومفتاحاً لإظهار الموقوف. ويقرأ ذلك من <see cref="MethodInfo"/> على
/// النوع نفسه ومن أسماء الحقول المولَّدة من XAML — لا من نصّ ملفّ.</para>
/// </summary>
public class ReactivationPathTests
{
    /// <summary>الصفحات التي تُوقف بياناتٍ أساسية يستعملها التاجر يومياً.</summary>
    public static TheoryData<Type> DeactivatingPages() =>
    [
        typeof(ProductsPage),
        typeof(CustomersPage),
        typeof(SuppliersPage),
    ];

    private static bool Declares(Type page, string member) =>
        page.GetMethod(member, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is not null
        || page.GetField(member, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is not null;

    /// <summary>
    /// <b>القياس المركزيّ.</b> من يُوقف يجب أن يستطيع الإعادة — وإلا صار الإيقاف حذفاً
    /// من وجهة نظر المستخدم مهما قال النموذج.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeactivatingPages))]
    public void APageThatDeactivates_AlsoOffersReactivation(Type page)
    {
        Assert.True(Declares(page, "OnDeactivate"),
            $"«{page.Name}» لا تُوقف أصلاً — فالحالة خارج نطاق هذا الحارس.");

        Assert.True(Declares(page, "OnReactivate"),
            $"«{page.Name}» تُوقف ولا تُعيد التفعيل — فالإيقاف قرارٌ بلا رجعة من الواجهة.");
    }

    /// <summary>
    /// ولا يكفي الزرّ: الموقوف مخفيٌّ بالترشيح، فبلا مفتاح «أظهر الموقوف» لا سبيل لاختياره
    /// أصلاً — يبقى الزرُّ معطَّلاً على قائمةٍ لا تحوي إلا النشط.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeactivatingPages))]
    public void APageThatDeactivates_CanShowTheInactiveOnes(Type page)
    {
        Assert.True(Declares(page, "ShowInactiveCheck"),
            $"«{page.Name}» لا مفتاح فيها لإظهار الموقوف — فلا يظهر ليُختار ويُعاد تفعيله.");
        Assert.True(Declares(page, "OnToggleInactive"),
            $"«{page.Name}» فيها المفتاح بلا معالج.");
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> صفحةٌ لا تُوقف شيئاً لا يُطلب منها زرُّ عودة — فالاقتران شرطيّ
    /// لا فرضٌ على الجميع. ولولا هذا لكان الحارس يطلب من كل شاشةٍ ما لا يعنيها.
    /// </summary>
    [Fact]
    public void APageThatDoesNotDeactivate_IsNotRequiredToReactivate()
    {
        Assert.False(Declares(typeof(DashboardPage), "OnDeactivate"));
        Assert.False(Declares(typeof(DashboardPage), "OnReactivate"));
    }
}
