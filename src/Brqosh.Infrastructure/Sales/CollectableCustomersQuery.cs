using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

/// <summary>عميلٌ يصلح لسند قبض، ورصيدُه المستحق.</summary>
public record CollectableCustomer(long Id, string NameAr, decimal Balance, bool IsActive);

/// <summary>
/// <b>مَن يُمكن تحصيلُه — لا مَن هو نشط.</b>
///
/// <para><b>العطل:</b> حوار سند القبض يبني قائمته بشرط <c>c.IsActive</c> وحده. وإيقافُ العميل
/// لا يفحص شيئاً: يقرأ ويوقف ويحفظ، بلا استعلامٍ عن رصيدٍ أو فواتير — <b>رغم أن الصفحة نفسها
/// حمّلت الأرصدة قبل سطور</b>. فبعد الإيقاف يصير العميل <b>غير قابلٍ للتحصيل</b>: لا يظهر في
/// سند القبض ولا في كشف حسابه، <b>بينما ذمّتُه باقيةٌ في حساب المدينين بالأستاذ</b>. المال
/// مسجَّلٌ ولا طريق لإقفاله من داخل البرنامج.</para>
///
/// <para><b>ولماذا لا يُمنع الإيقاف بدل ذلك:</b> لأن المنع يُغلق حالةً مشروعة تماماً — منشأةٌ
/// تتوقّف عن التعامل مع عميلٍ وتظلّ تُطالبه بدَينه. القاعدة الصحيحة ليست «النشط يُحصَّل منه»
/// بل <b>«من عليه رصيدٌ يُحصَّل منه»</b>، والإيقاف يمنع البيعَ الجديد لا التحصيل القديم.</para>
///
/// <para>وقاعدةُ الاختيار وُضعت هنا لا في شيفرة الشاشة كي <b>تُقاس</b>: الهوية التي تحرسها هي
/// «مجموع أرصدة العملاء القابلين للاختيار == رصيد المدينين في ميزان المراجعة»، ولا سبيل لقياسها
/// وهي مبثوثةٌ في مُعالِج حدثٍ داخل نافذة.</para>
/// </summary>
public class CollectableCustomersQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<CollectableCustomer>> ExecuteAsync(long companyId)
    {
        var balances = await new CustomerBalanceQuery(db).AllBalancesAsync(companyId);

        var customers = await db.Customers
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.NameAr)
            .Select(c => new { c.Id, c.NameAr, c.IsActive })
            .ToListAsync();

        return
        [
            .. customers
                .Select(c => new CollectableCustomer(
                    c.Id, c.NameAr, balances.GetValueOrDefault(c.Id), c.IsActive))
                // النشط يظهر دائماً — فقد يُدفع له مقدَّماً. والموقوف يظهر متى بقي عليه رصيد.
                .Where(c => c.IsActive || c.Balance != 0m)
        ];
    }
}
