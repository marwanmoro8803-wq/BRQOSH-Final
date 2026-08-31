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
/// (CEEF-229) <b>سلامةُ التخصيص كانت تتّكئ على قفل الترقيم — اتّكاءً غيرَ مُعلن.</b>
///
/// <para>في <c>م1-١٦</c> قِيس أنّ سندَين متزامنَين لا يتجاوزان الفاتورة، <b>ووُجد أنّ السببَ
/// عارض</b>: <c>ReceiptVoucherPostingService</c> يأخذ قفلَ ترقيم المستندات داخل معاملته،
/// فيبقى محجوزاً إلى نهايتها <b>فيُسلسِل كلَّ سنداتِ المنشأة في تلك السنة</b>. والتخصيصُ
/// نفسُه بلا حارسٍ في القاعدة.</para>
///
/// <para><b>ولم يُضَف قفلٌ حينها — عن قصد</b>: «حارسٌ لا تُقاس عضّتُه دعوى»، ولم يكن ممكناً
/// قياسُ عضّةِ قفلٍ يستره قفلٌ آخر.</para>
///
/// <para><b>وهذا الملفُّ يبني القياسَ المفقود</b>: يستدعي <c>PaymentAllocationService</c>
/// <b>مباشرةً</b>، بلا مسار السند — فينكشف السباقُ عارياً بلا سترِ قفل الترقيم. وهو مسارٌ
/// حقيقيّ لا مصطنَع: الخدمةُ عامّةٌ ويستدعيها سطحُ API وشاشتان.</para>
///
/// <para><b>ولا يظهر على SQLite</b>: الكتبةُ يتسلسلون بحكم المحرّك — درسُ Z-4. فيُقاس على
/// PostgreSQL حصراً.</para>
/// </summary>
public class AllocationLockTests
{
    private const int Writers = 4;

    /// <summary>
    /// <b>القياس المركزيّ.</b> أربعةُ تخصيصاتٍ متزامنة بـ1,150 على فاتورةٍ بـ1,150 ⟵
    /// <b>مجموعُ ما خُصّص لا يتجاوز الفاتورة</b>، مهما كان عددُ من نجح.
    ///
    /// <para><b>والقياسُ على المجموع لا على عدد النجاحات</b>: هو ما يراه المالكُ في تقرير
    /// الذمم، وهو ما يصدق أو يكذب. وفاتورةٌ مسدَّدةٌ بأكثر من قيمتها تُنتج رصيدَ عميلٍ سالباً
    /// لا مقابلَ له في الأستاذ.</para>
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentAllocations_CannotExceedTheInvoice()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("alloclock");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة التخصيص", "310123456700044", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        var customer = Customer.Create(company.Id, "عميل");
        scope.Db.Customers.Add(customer);
        await scope.Db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 1));
        invoice.AddLine("بضاعة", 10m, 100m);          // 1,150 شاملَ الضريبة
        var posted = await new SalesInvoicePostingService(scope.Db).PostAsync(invoice);

        // ‏سنداتٌ مرحّلةٌ سلفاً — فالتخصيصُ وحدَه هو ما يتزاحم، لا الترقيم.
        var voucherIds = new List<long>();
        for (var i = 0; i < Writers; i++)
        {
            var voucher = ReceiptVoucher.CreateDraft(company.Id, customer.Id,
                new DateOnly(2026, 2, 5), 1_150m, ReceiptMethod.Cash, $"سداد {i}");
            await new ReceiptVoucherPostingService(scope.Db).PostAsync(voucher, []);
            voucherIds.Add(voucher.Id);
        }

        var outcome = await ParallelWriters.RunAsync(Writers, async i =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            await new PaymentAllocationService(db).ApplyReceiptAllocationsAsync(
                company.Id, voucherIds[i], new DateOnly(2026, 2, 5),
                [(PaymentAllocationDocumentType.SalesInvoice, posted.Id, posted.InvoiceNumber!, 1_150m)]);
            await db.SaveChangesAsync();
            return i;
        });

        await using var reader = scope.OpenIndependentContext();
        var allocated = await reader.PaymentAllocationLines
            .Where(l => l.CompanyId == company.Id
                        && l.DocumentType == PaymentAllocationDocumentType.SalesInvoice
                        && l.DocumentId == posted.Id)
            .SumAsync(l => l.Amount);

        Assert.True(allocated <= 1_150m,
            $"خُصّص {allocated:N2} على فاتورةٍ بـ1,150 — فالفاتورةُ مسدَّدةٌ بأكثر من قيمتها "
            + $"ورصيدُ العميل صار سالباً بلا مقابلٍ في الأستاذ. {outcome.Describe()}");

        Assert.True(outcome.Successes.Count >= 1,
            $"سقط كلُّ التخصيصات — فالمسارُ متعذّرٌ عند التزاحم لا محميّ. {outcome.Describe()}");
    }
}
