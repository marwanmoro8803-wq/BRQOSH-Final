using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-229 · م1-١٤ / D-1) <b>ترقيمُ المستندات تحت التزاحم — أوّلُ قياسٍ من نوعه في المستودع.</b>
///
/// <para><b>العطل:</b> <c>DocumentNumberGenerator</c> يقرأ أكبرَ رقمٍ ثمّ يكتب التالي، بلا قفل.
/// وكذلك المسارُ الخاصّ بالفواتير في <c>SalesInvoicePostingService</c>. والقفلُ الوحيد في
/// النظام على أرقام <b>قيود اليومية</b> — ولا يحمي أرقامَ المستندات لأنّه يُطلب بعدها.</para>
///
/// <para><b>ولماذا لم يُكشف:</b> لا اختبارَ تزامنٍ في المستودع كلِّه. وهذا الملفُّ يستعمل
/// <see cref="ParallelWriters"/> — المِرفاسَ الذي تشترطه الخطة لخمسة بنود ولم يكن موجوداً.</para>
///
/// <para><b>ويُقاس على PostgreSQL حصراً</b>، فهو مزوّدُ الإنتاج، والقفلُ الاستشاريّ لا وجودَ
/// له في SQLite أصلاً. وقياسٌ على SQLite هنا يشهد لما لا يُشحن.</para>
/// </summary>
public class DocumentNumberConcurrencyTests
{
    private const int Writers = 6;

    /// <summary>
    /// <b>القياس المركزيّ.</b> ستّةُ كتّابٍ يرحّلون فاتورةً في اللحظة نفسها ⟵ ستّةُ أرقامٍ
    /// متمايزة. والتكرارُ الصامت هو العطل؛ ورفضُ أحدهم صراحةً سلوكٌ مقبول.
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentInvoicePostings_ProduceDistinctNumbers()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("docnum");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة التزاحم", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        var walkIn = Customer.Create(company.Id, "عميل نقدي");
        scope.Db.Customers.Add(walkIn);
        await scope.Db.SaveChangesAsync();

        // ‏ترحيلٌ تمهيديّ **متسلسل** — لينشئ الفترةَ المالية قبل التزاحم.
        //
        // ولولاه لَقاس هذا الاختبارُ سباقاً آخر: أوّلُ تشغيلٍ أسقط خمسةً من ستّة بـ
        // «23505 على IX_fiscal_periods_CompanyId_Code» — فالفترةُ تُنشأ عند الطلب،
        // وستّةُ مرحّلين يُنشئونها معاً. **عطلٌ حقيقيّ سُجّل بنداً مستقلّاً**، لكنّه يحجب
        // ما جاء هذا الاختبارُ ليقيسه. فيُدفَع خارج المقطع المقيس بترحيلٍ سابق.
        var warmUp = SalesInvoice.CreateDraft(company.Id, walkIn.Id, new DateOnly(2026, 7, 9));
        warmUp.AddLine("تمهيد", 1m, 1m);
        await new SalesInvoicePostingService(scope.Db).PostAsync(warmUp);

        var outcome = await ParallelWriters.RunAsync(Writers, async i =>
        {
            // ‏سياقٌ واتصالٌ مستقلّان — وإلا تسلسل الكتّاب ولم يقع سباقٌ أصلاً.
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            var invoice = SalesInvoice.CreateDraft(company.Id, walkIn.Id, new DateOnly(2026, 7, 9));
            invoice.AddLine($"صنف {i}", 1m, 100m);
            var posted = await new SalesInvoicePostingService(db).PostAsync(invoice);
            return posted.InvoiceNumber!;
        });

        var distinct = outcome.Successes.Distinct(StringComparer.Ordinal).Count();

        Assert.True(distinct == outcome.Successes.Count,
            "أرقامُ فواتير مكرَّرة تحت التزاحم — "
            + $"{outcome.Successes.Count} ترحيلاً أنتجت {distinct} رقماً فقط: "
            + string.Join(" · ", outcome.Successes.GroupBy(n => n)
                .Where(g => g.Count() > 1).Select(g => $"{g.Key}×{g.Count()}"))
            + $" [{outcome.Describe()}]");

        Assert.True(outcome.Failures.Count == 0,
            "ترحيلٌ متزامنٌ مشروع أخفق — " + outcome.Describe());
    }

    /// <summary>
    /// (CEEF-229 · م1-١٤) <b>مفتاحُ القفل ثابتٌ عبر العمليات — وإلا كان القفلُ زينةً.</b>
    ///
    /// <para><c>string.GetHashCode()</c> في .NET <b>مُعشّى لكلّ عملية تشغيل</b>: جهازان
    /// يحسبان مفتاحين مختلفين للبادئة نفسها، <b>فلا يتزاحمان على قفلٍ واحد</b> — ويبقى
    /// ظاهرُ الحماية سليماً بينما لا تحمي شيئاً. وهو عطلٌ لا يظهر على جهازٍ واحد أبداً.</para>
    ///
    /// <para>فتُثبَّت هنا قيمٌ محسوبةٌ من الدالّة الثابتة (FNV-1a). ولو استُبدلت بدالّةٍ
    /// معشّاة سقط هذا الاختبار في أوّل تشغيل — وهو ما لا يفعله أيّ قياسِ تزامنٍ على جهازٍ
    /// واحد، لأنّ العمليةَ الواحدة متّسقةٌ مع نفسها.</para>
    /// </summary>
    [Fact]
    public void TheLockKey_IsStableAcrossProcesses()
    {
        // ‏قيمٌ مُثبَّتة: أيُّ تغييرٍ في الاشتقاق يُسقط هذا الاختبار عمداً.
        Assert.Equal(DocumentSequenceLockProbe.KeyFor("INV", 2026), DocumentSequenceLockProbe.KeyFor("INV", 2026));
        Assert.NotEqual(DocumentSequenceLockProbe.KeyFor("INV", 2026), DocumentSequenceLockProbe.KeyFor("CN", 2026));
        Assert.NotEqual(DocumentSequenceLockProbe.KeyFor("INV", 2026), DocumentSequenceLockProbe.KeyFor("INV", 2027));

        // ‏والثباتُ المطلق: قيمةٌ معلومةٌ مكتوبةٌ بيدي، لا مأخوذةٌ من تشغيلٍ سابق.
        Assert.Equal(FnvReference("INV|2026"), DocumentSequenceLockProbe.KeyFor("INV", 2026));
        Assert.Equal(FnvReference("PO|2026"), DocumentSequenceLockProbe.KeyFor("PO", 2026));

        // ‏مرجعٌ مستقلٌّ عن التنفيذ — يُحسب هنا من التعريف لا يُستدعى من المصدر.
        static int FnvReference(string text)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var b in System.Text.Encoding.UTF8.GetBytes(text))
                {
                    hash ^= b;
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }

    /// <summary>
    /// <b>حارسٌ للمِرفاس نفسِه:</b> لو لم يتزاحم الكتّاب فعلاً لَما أثبت الاختبارُ شيئاً.
    /// فيُقاس أنّ عدّادَ الدخول بلغ ذروتَه أكثر من واحد — أي أنّ أكثر من كاتبٍ كان داخل
    /// المقطع في اللحظة نفسها. وبلا هذا يمرّ الاختبارُ الأوّل على نظامٍ متسلسلٍ ويكذب.
    /// </summary>
    [Fact]
    public async Task TheHarness_ActuallyOverlapsItsWriters()
    {
        var inside = 0;
        var peak = 0;

        var outcome = await ParallelWriters.RunAsync(Writers, async _ =>
        {
            var now = Interlocked.Increment(ref inside);
            InterlockedMax(ref peak, now);
            await Task.Delay(60);
            Interlocked.Decrement(ref inside);
            return now;
        });

        Assert.Empty(outcome.Failures);
        Assert.True(peak > 1,
            $"لم يتداخل كاتبان قطّ (الذروة {peak}) — فالمِرفاسُ يتسلسل، وكلُّ قياس تزامنٍ فوقه كاذب.");

        static void InterlockedMax(ref int target, int value)
        {
            int seen;
            while ((seen = Volatile.Read(ref target)) < value
                   && Interlocked.CompareExchange(ref target, value, seen) != seen) { }
        }
    }
}
