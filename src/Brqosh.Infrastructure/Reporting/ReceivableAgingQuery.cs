using Brqosh.Domain.Construction;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>سطرُ أعمارٍ لعميلٍ واحد — والمجموعُ هو رصيدُه في الدفتر المساعد.</summary>
public record ReceivableAgingRow(
    long CustomerId,
    string CustomerNameAr,
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal UnappliedCredit)
{
    /// <summary>
    /// مجموعُ الخانات — <b>ويجب أن يساوي رصيدَ العميل في الدفتر المساعد</b>.
    ///
    /// <para>وهي هويّةُ <c>هـ-١٤</c>. <b>وتقريرُ أعمارٍ لا يُطابق كشفَ الحساب لا يُصدَّق
    /// في شيء</b> — والمحاسبُ يُطالِب العملاءَ بأرقامٍ لا يعرف من أين جاءت.</para>
    /// </summary>
    public decimal Total => Current + Days31To60 + Days61To90 + Over90 - UnappliedCredit;
}

/// <summary>
/// (CEEF-230 · ط4-٣) <b>أعمارُ الديون — تقريرٌ يُحيل إليه النظامُ نفسُه ولا وجودَ له.</b>
///
/// <para><b>ما كان:</b> «كشفُ أعمار الديون» مذكورٌ في نصَّين اثنين في المستودع —
/// أحدُهما <b>نصيحةُ المساعد المالي للمالك</b>: «راجعْ كشفَ أعمار الديون من تقارير
/// العملاء». <b>ولا تقريرَ ولا شاشةَ ولا استعلام.</b> فالنظامُ يُرسل مالكَه إلى بابٍ
/// لا وجودَ له.</para>
///
/// <para><b>وهو أوّلُ ما يُفتح في كلّ منشأةٍ كلَّ أسبوع</b>: مَن عليه، ومنذ متى.
/// <b>وبغيره تُدار المطالباتُ من الذاكرة</b> — فيُطالَب مَن سدّد، ويُنسى مَن تأخّر
/// حتّى يسقط الحقُّ بالتقادم.</para>
///
/// <para><b>والعمرُ من تاريخ المستند لا من تاريخ استحقاق</b>: لا تحمل الفاتورةُ في هذا
/// النظام تاريخَ استحقاقٍ ولا مهلةَ سداد، <b>فالحديثُ عن «متأخّر» ادّعاءٌ لا سند له</b>.
/// والاسمُ «أعمارُ الديون» صادقٌ بما يُحسب: <b>كم مضى على الدَّين</b>.</para>
///
/// <para><b>والسدادُ يُطبَّق بالتخصيص أوّلاً ثمّ بالأقدم</b>: سندٌ خُصّص على فاتورةٍ
/// بعينها يُطفئها، وما بقي يُطفئ الأقدمَ فالأقدم. <b>وتوزيعُه بالتناسب يُبقي كلَّ فاتورةٍ
/// مفتوحةً بجزء</b> — فتبدو ذممٌ قديمةٌ كلُّها متأخّرةً وهي مسدَّدة.</para>
/// </summary>
public class ReceivableAgingQuery(BrqoshDbContext db)
{
    /// <param name="Type">
    /// نوعُ المستند للتخصيص الصريح — <c>null</c> لمستندٍ <b>لا يُخصَّص عليه سدادٌ أبداً</b>
    /// (إفراجُ المحتجز)، فلا يلتقطه <see cref="ApplyExplicit"/> بالخطأ.
    /// </param>
    private sealed record OpenDocument(DateOnly Date, long DocumentId,
        PaymentAllocationDocumentType? Type, decimal Amount)
    {
        public decimal Remaining { get; set; } = Amount;
    }

    public async Task<IReadOnlyList<ReceivableAgingRow>> ExecuteAsync(
        long companyId, DateOnly asOf, CancellationToken ct = default)
    {
        // ‏**والمرجعُ مؤرَّخٌ كالخانات**: رصيدُ «الآن» يُقابَل بخاناتِ «٣٠ يونيو» فيُخرج
        // ‏فرقاً يُنسب إلى التقرير وهو في المرجع. **وأخرجه طفرةٌ نجت**: ترشيحُ المستندات
        // ‏بالتاريخ كان قائماً بلا مرجعٍ يُقابله، فلم يكن لنزعه أثرٌ يُقاس.
        var balances = await new CustomerBalanceQuery(db).AllBalancesAsync(companyId, asOf);

        var names = await db.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.CompanyId == companyId)
            .ToDictionaryAsync(c => c.Id, c => c.NameAr, ct);

        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Posted
                        && i.PosShiftId == null && i.InvoiceDate <= asOf)
            .Select(i => new { i.Id, i.CustomerId, i.InvoiceDate, i.Subtotal, i.VatAmount })
            .ToListAsync(ct);

        var billings = await db.ProgressBillings.AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.Status == ProgressBillingStatus.Posted)
            .ToListAsync(ct);

        var allocations = await db.PaymentAllocationLines.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.AllocationDate <= asOf)
            .ToListAsync(ct);

        var releases = await CustomerBalanceQuery
            .ReleasedRetentionDocumentsAsync(db, companyId, asOf);

        var rows = new List<ReceivableAgingRow>();

        foreach (var (customerId, balance) in balances)
        {
            if (balance == 0m) continue;

            var open = new List<OpenDocument>();

            open.AddRange(invoices.Where(i => i.CustomerId == customerId)
                .Select(i => new OpenDocument(i.InvoiceDate, i.Id,
                    PaymentAllocationDocumentType.SalesInvoice, i.Subtotal + i.VatAmount)));

            // (CEEF-232 · ج-٣) ‏**والمستخلصُ يدخل بذمّتِه لا بإجماليّه.**
            //
            // ‏فالرصيدُ أعلاه صار يستبعد المحتجز (ج-١)، **والمستنداتُ هنا كانت تجمع
            // ‏`GrandTotal`** — والائتمانُ يُشتقّ من الفرق. **فالمحتجزُ يصير سداداً وهمياً
            // ‏يُطفئ الأقدمَ فالأقدم**: مقاولٌ له مستخلصان بمليونٍ كلٌّ واحتجازُ عشرة بالمئة
            // ‏يرى خانةَ «أكثر من ٩٠ يوماً» أقلَّ بمئة ألف والخانةَ الجارية أكثرَ بها.
            // ‏**والمجموعُ يبقى صحيحاً** فتمرّ هويّةُ هـ-١٤ — **والعطلُ في التوزيع وحدَه**،
            // ‏وهو بالضبط ما يُقرأ منه التقرير: مَن تأخّر، ومنذ متى.
            open.AddRange(billings.Where(b => b.CustomerId == customerId && b.BillingDate <= asOf)
                .Select(b => new OpenDocument(b.BillingDate, b.Id,
                    PaymentAllocationDocumentType.ProgressBilling, b.NetBillableAmount + b.VatAmount)));

            // ‏**والمُفرَج عنه يدخل بتاريخ إفراجه** — فهو دَينٌ عمرُه من ذلك اليوم، وقد كان
            // ‏قبله ضماناً لا يُطالَب به. **وإدخالُه بتاريخ المستخلص يجعله متأخّراً يومَ وُلد.**
            open.AddRange(releases.Where(r => r.CustomerId == customerId)
                .Select(r => new OpenDocument(r.Date, r.EntryId, null, r.Amount)));

            open = [.. open.OrderBy(d => d.Date).ThenBy(d => d.DocumentId)];

            var invoiced = open.Sum(d => d.Amount);

            // ‏**والائتمانُ كلُّه من الفرق**: كلُّ ما ليس مستنداً مدينـاً — إشعاراتٌ
            // ‏وسنداتٌ وشيكات — يُشتقّ من رصيد الدفتر المساعد نفسِه. **فالتقريرُ يُطابقه
            // ‏بالبناء لا بالمصادفة**، ولا يحتاج أن يُعيد جمعَ ما جمعه.
            var credit = invoiced - balance;

            ApplyExplicit(open, allocations);

            var unapplied = ApplyOldestFirst(open, RemainingCredit(credit, open, invoiced));

            var buckets = Bucket(open, asOf);
            rows.Add(new ReceivableAgingRow(customerId,
                names.TryGetValue(customerId, out var name) ? name : $"#{customerId}",
                buckets.Current, buckets.Days31To60, buckets.Days61To90, buckets.Over90,
                unapplied));
        }

        return [.. rows.OrderByDescending(r => r.Total)];
    }

    /// <summary>
    /// يُطفئ ما خُصّص صراحةً على مستنده.
    ///
    /// <para><b>والتخصيصُ الصريح يسبق الأقدم</b>: عميلٌ يدفع فاتورةَ الشهر الجاري ويترك
    /// قديمةً متنازَعاً عليها — <b>وإطفاءُ الأقدم يُظهر النزاعَ مسدَّداً والجاريةَ متأخّرة</b>،
    /// وهو عكسُ الحقيقة تماماً.</para>
    /// </summary>
    private static void ApplyExplicit(List<OpenDocument> open,
        List<PaymentAllocationLine> allocations)
    {
        foreach (var document in open)
        {
            var applied = allocations
                .Where(a => a.DocumentType == document.Type && a.DocumentId == document.DocumentId)
                .Sum(a => a.Amount);

            if (applied <= 0m) continue;
            document.Remaining = Math.Max(0m, document.Remaining - applied);
        }
    }

    /// <summary>ما بقي من الائتمان بعد ما خُصّص صراحةً — ولا يقلّ عن صفر.</summary>
    private static decimal RemainingCredit(decimal credit, List<OpenDocument> open, decimal invoiced)
    {
        var explicitlyApplied = invoiced - open.Sum(d => d.Remaining);
        return Math.Max(0m, credit - explicitlyApplied);
    }

    /// <summary>يُطفئ الأقدمَ فالأقدم — <b>ويُعيد ما فاض عن كلّ المستندات</b>.</summary>
    private static decimal ApplyOldestFirst(List<OpenDocument> open, decimal credit)
    {
        foreach (var document in open)
        {
            if (credit <= 0m) break;
            var applied = Math.Min(document.Remaining, credit);
            document.Remaining -= applied;
            credit -= applied;
        }

        // ‏**والفائضُ يُعرض ولا يُبتلع**: عميلٌ دفع مقدَّماً أو أُشعر دائناً بأكثر ممّا
        // ‏عليه له رصيدٌ **دائن**. <b>وإخفاؤه يجعل مجموعَ التقرير يخالف كشفَ الحساب.</b>
        return credit;
    }

    private static (decimal Current, decimal Days31To60, decimal Days61To90, decimal Over90)
        Bucket(List<OpenDocument> open, DateOnly asOf)
    {
        decimal current = 0m, d31 = 0m, d61 = 0m, over = 0m;

        foreach (var document in open.Where(d => d.Remaining > 0m))
        {
            var age = asOf.DayNumber - document.Date.DayNumber;

            if (age <= 30) current += document.Remaining;
            else if (age <= 60) d31 += document.Remaining;
            else if (age <= 90) d61 += document.Remaining;
            else over += document.Remaining;
        }

        return (Round(current), Round(d31), Round(d61), Round(over));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
