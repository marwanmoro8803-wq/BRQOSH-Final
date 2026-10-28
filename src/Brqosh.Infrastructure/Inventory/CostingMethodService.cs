using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>
/// <b>تبديلُ طريقة تكلفة المخزون — ممنوعٌ ما دام هناك مخزون.</b>
///
/// <para><c>Company.SetCostingMethod</c> كانت سطراً واحداً بلا حارس، وشاشةُ إعدادات المنشأة
/// تستدعيها وتحفظ. والطريقةُ تُقرأ في كلّ استلامٍ وكلّ صرف — <b>فتبديلُها يُغيّر معنى
/// البيانات القائمة بأثرٍ رجعيّ</b>.</para>
///
/// <para><b>متوسط ⟵ FIFO:</b> الطبقاتُ لا تُنشأ إلا في وضع FIFO، فالبضاعةُ القائمة بلا
/// طبقاتٍ إطلاقاً. <b>مقيسٌ: أوّلُ صرفٍ بعد التبديل يسقط</b> بـ«طبقات FIFO للصنف غير كافية —
/// المتبقي 0.0000، المطلوب 3.0000»، والبضاعةُ تبقى محبوسةً لا تخرج. عطلٌ صاخبٌ لا صامت،
/// <b>لكنّه يُقعِد المنشأة عن البيع</b>.</para>
///
/// <para><b>FIFO ⟵ متوسط ثمّ عودة:</b> الطبقاتُ تتجمّد — تبقى كما هي بينما تُصرَف البضاعةُ
/// بالمتوسط. فالعودةُ تجد طبقاتٍ تدّعي كمّيةً لم تعد موجودة، فتُستهلك بضاعةٌ بيعت مرّةً
/// أخرى بتكلفتها القديمة. <b>وهذا هو الصامت.</b></para>
///
/// <para><b>والقرار: المنعُ لا الترميم.</b> بناءُ الطبقات بأثرٍ رجعيّ يحتاج تواريخَ استلامٍ
/// وتكاليفَ لم يعد النظامُ يحتفظ بها بعد أن مضى في المتوسط — <b>فأيُّ ترميمٍ اختراعٌ لأرقامٍ
/// لم تقع</b>، وتقييمُ المخزون ليس موضعاً للاختراع.</para>
///
/// <para><b>والمنعُ مشروطٌ بالحالة الآن لا بالتاريخ:</b> منشأةٌ لا مخزونَ لها تُبدّل بحرّية،
/// ومخزونٌ صُفّي يفتح البابَ من جديد. <b>وحارسٌ يُغلق البابَ إلى الأبد بعد أوّل استلامٍ
/// يصير قيداً لا حماية.</b></para>
/// </summary>
public class CostingMethodService(BrqoshDbContext db)
{
    public async Task ChangeAsync(long companyId, InventoryCostingMethod method,
        CancellationToken ct = default)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);

        // ‏حفظُ شاشة الإعدادات يمرّ هنا في كلّ مرّة. ولو رُفض «لا تغيير» لصار المالكُ عاجزاً
        // ‏عن تبديل شعارِ منشأته لأنّ عنده بضاعة.
        if (company.CostingMethod == method)
            return;

        SecurityContext.EnsureAdministrator();

        var onHand = await db.InventoryBalances
            .Where(b => b.CompanyId == companyId && b.QuantityOnHand != 0)
            .Select(b => new { b.ProductId, b.QuantityOnHand })
            .ToListAsync(ct);

        if (onHand.Count > 0)
        {
            var total = onHand.Sum(b => b.QuantityOnHand);
            var name = await db.Products.IgnoreQueryFilters()
                .Where(p => p.Id == onHand[0].ProductId)
                .Select(p => p.NameAr)
                .SingleOrDefaultAsync(ct) ?? "";

            throw new AccountingException(
                $"لا تُبدَّل طريقةُ تكلفة المخزون وفي المستودعات بضاعةٌ قائمة — "
                + $"{onHand.Count} صنفاً بمجموع {total:0.####} وحدة (منها «{name}»). "
                + "فطريقةُ التكلفة تُفسَّر بها البياناتُ السابقة، وتبديلُها يُعيد تقييمَ ما مضى "
                + "بأرقامٍ لم تقع. المخرج: صفِّ المخزون (بيعاً أو جرداً) ثمّ بدِّل، "
                + "أو أنشئ منشأةً جديدة بالطريقة المطلوبة.");
        }

        company.SetCostingMethod(method);
        await db.SaveChangesAsync(ct);
    }
}
