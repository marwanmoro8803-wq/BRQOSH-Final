using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// (CEEF-230 · ط1-٤) <b>حسابُ نظامٍ موجودٌ — ويُنشأ من القالب إن غاب.</b>
///
/// <para><b>ولماذا يُنشأ لا يُرمى:</b> حسابا فرقِ العملة المحقَّق أُضيفا إلى قالب الحسابات
/// في هذه الدفعة، <b>وكلُّ قاعدةِ عميلٍ قائمةٍ لا تحملهما</b>. فأوّلُ تحصيلٍ بعملةٍ أجنبية
/// كان سيسقط بـ«الحساب غير موجود» على قاعدةٍ سليمةٍ تماماً — <b>وهو عطلُ ترقيةٍ لا عطلُ
/// بيانات</b>، ومَن يراه لا يعرف أنّ علاجَه إضافةُ حساب.</para>
///
/// <para><b>والإنشاءُ من القالب لا من الهواء</b>: الاسمُ العربيُّ والإنجليزيُّ والنوعُ
/// والحسابُ الأب كلُّها منه — <b>فحسابٌ يُنشأ باسمٍ مخترَعٍ يخالف نظيرَه في منشأةٍ أخرى</b>،
/// وتُصبح التقاريرُ المقارنة غيرَ قابلةٍ للقراءة.</para>
///
/// <para>وهو مذهبُ <c>FxClosingTranslationService</c> نفسِه مع <c>3106</c> —
/// <b>معمَّماً بدل أن يُكرَّر لكلّ حساب</b>.</para>
/// </summary>
public static class SystemAccountProvisioning
{
    public static async Task<Account> EnsureAsync(
        BrqoshDbContext db, long companyId, string code, CancellationToken ct = default)
    {
        var existing = await db.Accounts
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);
        if (existing is not null) return existing;

        var template = SaudiChartOfAccounts.Template.FirstOrDefault(a => a.Code == code)
            ?? throw new AccountingException($"لا قالبَ للحساب النظاميّ {code}.");

        var parent = await db.Accounts.FirstOrDefaultAsync(
                a => a.CompanyId == companyId && a.Code == template.ParentCode, ct)
            ?? throw new AccountingException(
                $"الحسابُ الأب {template.ParentCode} غير موجود — فلا يُنشأ {code} تحته.");

        var account = Account.Create(template.Code, template.NameAr, template.NameEn,
            template.Type, parent.Id, isGroup: false, isSystem: true, companyId);

        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }
}
