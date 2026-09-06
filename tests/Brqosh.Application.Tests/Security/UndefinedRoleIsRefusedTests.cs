using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (م-٢ · ن-٢٦) <b>دورٌ خارج التعداد كان يبلغ مسارَ الدفتر بترويسةٍ واحدة.</b>
///
/// <para><b>العطل:</b> <c>Enum.TryParse&lt;UserRole&gt;("99")</c> <b>ينجح</b> — تحليلُ التعدادات
/// في .NET يقبل السلاسل الرقميّة ويُعيد قيمةً خارج المدى. وثلاثةٌ من أربعة حرّاسٍ في
/// <see cref="SecurityContext"/> كانت <b>قوائمَ منع</b> («أليس <c>Viewer</c>؟»)، وقيمةٌ خارج
/// التعداد <b>ليست على قائمة أحد</b> — فتمرّ منها جميعاً.</para>
///
/// <para><b>والفاعلُ ليس مهاجماً افتراضياً:</b> شريكُ تكاملٍ يملك مفتاحَ API شرعاً، ودورُه
/// المضبوط خادمياً <c>Viewer</c> (وهو افتراضُ الإنتاج). فترويسةٌ واحدة تمنحه الترحيلَ
/// والإلغاءَ والعكسَ المحاسبيّ.</para>
///
/// <para><b>ولماذا هذا الاختبارُ ضروريّ:</b> السلوكُ للأدوار الأربعة المعروفة لم يتغيّر
/// حرفاً، فلا اختبارَ قائمٌ يلاحظ عودةَ العطل. <b>وقائمةُ منعٍ تُعاد كتابتُها يوماً تبدو
/// صحيحةً تماماً</b> — ولا يكشفها إلا فحصٌ يسأل عن القيمة التي لا تخطر ببال أحد.</para>
/// </summary>
public class UndefinedRoleIsRefusedTests : IDisposable
{
    private readonly UserRole? _saved = SecurityContext.CurrentRole;

    public void Dispose() => SecurityContext.CurrentRole = _saved;

    /// <summary>القيمةُ التي أثبت المِجسُّ أنّها تعبر: <c>(UserRole)99</c>.</summary>
    public static TheoryData<int> UndefinedRoleValues => new() { 99, 4, -1, int.MaxValue };

    [Theory]
    [MemberData(nameof(UndefinedRoleValues))]
    public void EveryGuard_RefusesARoleOutsideTheEnum(int raw)
    {
        SecurityContext.CurrentRole = (UserRole)raw;

        Assert.Throws<AccountingException>(SecurityContext.EnsureCanPost);
        Assert.Throws<AccountingException>(SecurityContext.EnsureCanPostAccounting);
        Assert.Throws<AccountingException>(SecurityContext.EnsureCanCancelOrReverse);
        Assert.Throws<AccountingException>(SecurityContext.EnsureAdministrator);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب:</b> الأدوارُ المعرَّفة تسلك كما كانت حرفاً.
    ///
    /// <para>بدونه قد يمرّ الاختبارُ أعلاه بحارسٍ يرفض <b>كلَّ</b> دور — أي بإصلاحٍ
    /// يُغلق الثغرةَ ويُعطّل النظام. فالمطلوبُ إثباتُ الاثنين معاً.</para>
    /// </summary>
    [Theory]
    [InlineData(UserRole.Administrator, true, true, true, true)]
    [InlineData(UserRole.Accountant, true, true, true, false)]
    [InlineData(UserRole.Cashier, true, false, false, false)]
    [InlineData(UserRole.Viewer, false, false, false, false)]
    public void DefinedRoles_KeepTheirExactPriorBehaviour(
        UserRole role, bool canPost, bool canAccounting, bool canCancel, bool isAdmin)
    {
        SecurityContext.CurrentRole = role;

        Assert.Equal(canPost, Allowed(SecurityContext.EnsureCanPost));
        Assert.Equal(canAccounting, Allowed(SecurityContext.EnsureCanPostAccounting));
        Assert.Equal(canCancel, Allowed(SecurityContext.EnsureCanCancelOrReverse));
        Assert.Equal(isAdmin, Allowed(SecurityContext.EnsureAdministrator));
    }

    /// <summary>ولا دورَ إطلاقاً يبقى مرفوضاً — منعٌ افتراضيّ.</summary>
    [Fact]
    public void NoRoleAtAll_IsStillRefused()
    {
        SecurityContext.CurrentRole = null;

        Assert.Throws<AccountingException>(SecurityContext.EnsureCanPost);
        Assert.Throws<AccountingException>(SecurityContext.EnsureCanPostAccounting);
        Assert.Throws<AccountingException>(SecurityContext.EnsureCanCancelOrReverse);
        Assert.Throws<AccountingException>(SecurityContext.EnsureAdministrator);
    }

    /// <summary>
    /// <b>والطبقةُ الثانية: التحليلُ نفسُه يرفض ما ليس دوراً معرَّفاً.</b>
    ///
    /// <para>فلا تصل القيمةُ الشاذّةُ إلى الحارس أصلاً. وطبقتان لا واحدة، لأنّ موضعَ
    /// تحليلٍ ثالثاً قد يُضاف يوماً ولا يمرّ بهذه الدالّة.</para>
    /// </summary>
    [Theory]
    [InlineData("99")]
    [InlineData("4")]
    [InlineData("-1")]
    [InlineData("2147483647")]
    public void NarrowByRequest_IgnoresANumericRoleOutsideTheEnum(string header)
    {
        var effective = ApiSecuritySettings.NarrowByRequest(UserRole.Viewer, header);

        Assert.Equal(UserRole.Viewer, effective);
        Assert.True(Enum.IsDefined(effective), $"الترويسة «{header}» أنتجت دوراً خارج التعداد: {(int)effective}.");
    }

    /// <summary>وشاهدُ ضبطٍ موجب: التضييقُ بالاسم ما زال يعمل، والتصعيدُ ما زال ممنوعاً.</summary>
    [Theory]
    [InlineData("Viewer", UserRole.Viewer)]          // ‏أقلُّ امتيازاً ⟸ يُقبل التنازل
    [InlineData("Administrator", UserRole.Accountant)] // ‏أعلى امتيازاً ⟸ يُرفض التصعيد
    [InlineData("", UserRole.Accountant)]
    [InlineData(null, UserRole.Accountant)]
    public void NarrowByRequest_StillNarrowsByNameAndNeverEscalates(string? header, UserRole expected)
    {
        Assert.Equal(expected, ApiSecuritySettings.NarrowByRequest(UserRole.Accountant, header));
    }

    private static bool Allowed(Action guard)
    {
        try { guard(); return true; }
        catch (AccountingException) { return false; }
    }
}
