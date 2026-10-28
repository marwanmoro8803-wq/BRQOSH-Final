using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// <b>حسابُ الأستاذ الذي يمثّل نقدَ البنك — من مكانٍ واحد.</b>
///
/// <para><b>العطل: انشقاق نقد البنوك بين أبٍ وابن.</b> كل حسابٍ بنكيّ يُنشئ حساب أستاذٍ
/// <b>ابناً</b> تحت <c>1102</c> بكودٍ مثل <c>110201</c>، والمال يذهب إليه في سندات القبض
/// والصرف والشيكات والتحويلات وفروق كشف الحساب. لكنّ ثلاثة مساراتٍ كانت تحلّ الحساب
/// <b>بالكود الحرفيّ <c>1102</c></b> — أي الأب — وهي: بيعُ نقطة البيع بالبطاقة، وسندُ القبض
/// البنكيّ بلا اختيار بنك، وسندُ الصرف البنكيّ بلا اختيار بنك.</para>
///
/// <para>و<c>1102</c> في القالب <c>IsGroup: false</c>، فالأب والابن <b>يقبلان الترحيل معاً</b> —
/// وهذا شرطُ الانشقاق البنيويّ لا عرَضُه.</para>
///
/// <para><b>الأثر عند تاجرٍ يبيع بالشبكة ويدفع بالتحويل:</b> إيرادُ البطاقات يتراكم على الأب
/// ولا ينزل أبداً، والمدفوعات تُخصم من الابن حتى <b>يصير رصيدُ حسابٍ بنكيّ سالباً</b> في جانب
/// الأصول. والتسوية البنكية تقرأ الابن وحده، فتُقارَن بكشفٍ يحوي تحصيلات الشبكة — ويظهر فرقٌ
/// دائم لا يُغلق مهما دقّق المالك.</para>
///
/// <para><b>ولا تمسكه هوية قائمة التدفقات</b> (هـ-٦/هـ-٧): الأب والابن كلاهما تحت البادئة
/// <c>1102</c>، فالمجاميع تبقى سليمة تماماً بينما التوزيع خاطئ. حارسُه هوية شجرة البنوك:
/// <b>مجموع أرصدة أبناء 1102 يساوي رصيد نقد البنوك كلَّه</b> — أي أن الأب لا يحمل رصيداً مباشراً.</para>
/// </summary>
public static class BankGlAccountResolver
{
    /// <summary>
    /// حساب الأستاذ لنقد البنك: البنك المحدَّد، وإلا البنك الافتراضيّ، وإلا الحساب النظاميّ.
    ///
    /// <para><b>والسقوط إلى <c>1102</c> مقصودٌ ومحدود:</b> منشأةٌ لم تُنشئ حساباً بنكياً بعد
    /// لا يوجد لها ابنٌ تُقيَّد عليه، ورفضُ البيع بالشبكة لهذا السبب يُغلق حالةَ استخدامٍ
    /// مشروعة. ومتى أُنشئ أوّل حساب بنكيّ صار هو الوجهة، فلا ينشقّ النقد بعدها.</para>
    /// </summary>
    public static async Task<Account> ResolveAsync(
        BrqoshDbContext db, long companyId, long? bankAccountId = null, CancellationToken ct = default)
    {
        if (bankAccountId is long id)
        {
            var chosen = await db.BankAccounts
                .SingleAsync(b => b.CompanyId == companyId && b.Id == id && b.IsActive, ct);
            return await db.Accounts.SingleAsync(a => a.Id == chosen.GlAccountId, ct);
        }

        var fallback = await db.BankAccounts
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (fallback is not null)
            return await db.Accounts.SingleAsync(a => a.Id == fallback.GlAccountId, ct);

        return await db.Accounts.SingleAsync(
            a => a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashAtBanks, ct);
    }

    /// <summary>
    /// ‏ <b>نقدُ البنك لمسارٍ يقبل كوداً صريحاً</b>
    /// — الصريحُ كما هو، <b>وغيابُه يُحلّ من هنا لا من قيمةٍ افتراضيّة</b>.
    ///
    /// <para>‏وكانت المداخلُ القطاعيّةُ (الصيانة · التأمين · توريدُ الدفع عند الاستلام) تأخذ
    /// <c>string cashAccountCode = CashAtBanks</c> — <b>الأبَ <c>1102</c> بعينه</b>. والشاشاتُ لا
    /// تمرّر حساباً، <b>فوقع نقدُها كلُّه على الأب والكشفُ في الابن</b>: الانشقاقُ الذي أُغلق هنا
    /// في م2-١٥ للبيع والسندات، <b>عائداً من بابٍ آخرَ بقيمةٍ افتراضيّةٍ حسنةِ الشكل</b>.</para>
    /// </summary>
    public static async Task<Account> ResolveCodeAsync(
        BrqoshDbContext db, long companyId, string? explicitCode, CancellationToken ct = default) =>
        explicitCode is null
            ? await ResolveAsync(db, companyId, ct: ct)
            : await SystemAccountProvisioning.EnsureAsync(db, companyId, explicitCode, ct);
}
