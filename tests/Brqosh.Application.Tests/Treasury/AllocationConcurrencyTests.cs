using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>
/// (CEEF-229 · م1-١٦ / A-13) <b>سندان متزامنان على فاتورةٍ واحدة — والسقفُ يقرأ رصيداً بائتاً.</b>
///
/// <para><b>العطل:</b> السقفُ يقرأ المتبقيَ بـ<c>SumAsync</c> ثمّ يكتب، بلا قفلٍ ولا قيدٍ
/// في القاعدة. والمعاملةُ على <c>Read Committed</c> — فسندان يقرآن «المتبقي 1,150» ويكتبان
/// معاً، <b>فتُسدَّد الفاتورةُ بضِعف قيمتها</b> ويصير رصيدُ العميل سالباً.</para>
///
/// <para><b>ولا يمنعه فهرسٌ فريد:</b> فهرسُ <c>(CompanyId, DocumentType, DocumentId)</c>
/// غيرُ فريدٍ عمداً — فالمستندُ يُسدَّد على دفعات. فلا حارسَ في القاعدة إطلاقاً.</para>
///
/// <para><b>والنتيجةُ المقيسة: لا يقع السباقُ اليوم — لسببٍ غير الذي يبدو.</b>
/// <c>ReceiptVoucherPostingService</c> يأخذ قفلَ ترقيم المستندات (م1-١٤) عند السطر 48
/// <b>داخل معاملته</b>، والتخصيصُ عند 65 — فيبقى القفلُ محجوزاً إلى نهاية المعاملة
/// <b>فيُسلسِل كلَّ سنداتِ المنشأة في تلك السنة</b>. فالحمايةُ حقيقيّةٌ ومقيسة، لكنّها
/// <b>عرَضية</b>: مصدرُها الترقيمُ لا التخصيص.</para>
///
/// <para><b>وهذا الاختبارُ يُثبّت النتيجة لا السبب.</b> فلو نُقل الترقيمُ بعد التخصيص، أو
/// صار مفتاحُه لكلّ مستندٍ على حدة، انفتح السباقُ <b>وسقط هذا الاختبار</b> — وهو ما يجعله
/// نافعاً رغم أنّه أخضرُ اليوم. <b>ولم يُضَف قفلٌ ثانٍ:</b> حارسٌ لا تُقاس عضّتُه دعوى،
/// وقد أُزيل من هذا المستودع أكثرُ من واحدٍ من ذلك الصنف في هذه الحملة.</para>
/// </summary>
public class AllocationConcurrencyTests
{
    private const int Writers = 4;

    /// <summary>
    /// <b>القياس المركزيّ.</b> أربعةُ سنداتٍ متزامنة بـ1,150 على فاتورةٍ بـ1,150 ⟵
    /// <b>ينجح واحد</b>، والباقي يُرفض بالسقف. ورفضُهم سلوكٌ سليم؛ والعطلُ أن ينجح أكثرُ من واحد.
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentVouchers_CannotOverAllocateTheSameInvoice()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("alloc");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة التخصيص", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        var customer = Customer.Create(company.Id, "عميل");
        scope.Db.Customers.Add(customer);
        await scope.Db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 1));
        invoice.AddLine("بضاعة", 10m, 100m); // 1150
        var posted = await new SalesInvoicePostingService(scope.Db).PostAsync(invoice);

        var outcome = await ParallelWriters.RunAsync(Writers, async i =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            var voucher = ReceiptVoucher.CreateDraft(company.Id, customer.Id,
                new DateOnly(2026, 2, 5), 1_150m, ReceiptMethod.Cash, $"سداد {i}");

            await new ReceiptVoucherPostingService(db).PostAsync(voucher, [
                (PaymentAllocationDocumentType.SalesInvoice, posted.Id, posted.InvoiceNumber!, 1_150m),
            ]);
            return voucher.Id;
        });

        Assert.True(outcome.Successes.Count == 1,
            $"سُدّدت الفاتورةُ {outcome.Successes.Count} مرّة — والمبلغُ يفوق قيمتها. {outcome.Describe()}");

        // ‏والرصيدُ لا يصير سالباً — وهو ما يراه المحاسبُ في كشف الحساب.
        var allocated = await scope.Db.PaymentAllocationLines
            .Where(l => l.CompanyId == company.Id && l.DocumentId == posted.Id)
            .SumAsync(l => l.Amount);

        Assert.Equal(1_150m, allocated);
    }
}
