using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (CEEF-229) <b>أوّلُ بيعٍ في شهرٍ جديد يقتل خمسةً من ستّة.</b>
///
/// <para><b>العطل:</b> <c>EnsurePeriodOpenAsync</c> يقرأ ثمّ يُنشئ الفترةَ المالية بلا قفل.
/// فستّةُ مرحّلين في أوّل يومٍ من شهرٍ جديد يقرأون «لا فترة» ويُنشئونها ستّاً — فيربح واحدٌ
/// ويموت خمسةٌ على <c>IX_fiscal_periods_CompanyId_Code</c> برسالةِ قاعدة بيانات خامّة.</para>
///
/// <para><b>وهذا العطلُ وُجد مرّتين بالمصادفة، ولم يُقصد في أيّ منهما:</b> ظهر أوّلاً وهو
/// يحجب قياسَ ترقيم المستندات (م1-١٤)، ثمّ ثانياً وهو <b>يستر سباقَ الإقفال السنويّ</b>
/// (م1-١٥) فيجعل اختباراً معطوباً يمرّ أخضر. <b>وعطلٌ يستر غيرَه أخطرُ من عطلٍ يُرى.</b></para>
///
/// <para>ويقع في اليوم الأوّل من كلّ شهر في متجرٍ فيه صندوقان — أي اثنتَي عشرةَ مرّةً في السنة.</para>
/// </summary>
public class FiscalPeriodConcurrencyTests
{
    private const int Writers = 6;

    /// <summary>
    /// <b>القياس المركزيّ.</b> ستّةُ ترحيلاتٍ متزامنة في شهرٍ <b>لا فترةَ له بعد</b> ⟵
    /// ستّتُها تنجح، وفترةٌ واحدة تُنشأ.
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentPostings_IntoAFreshMonth_AllSucceed()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("period");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة الفترة", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        var walkIn = Customer.Create(company.Id, "عميل نقدي");
        scope.Db.Customers.Add(walkIn);
        await scope.Db.SaveChangesAsync();

        // ‏شهرٌ لم يُرحَّل فيه شيءٌ قطّ — فالفترةُ تُنشأ عند أوّل ترحيل، وهو موضعُ السباق.
        var freshMonth = new DateOnly(2026, 9, 1);
        Assert.False(await scope.Db.FiscalPeriods.AnyAsync(p =>
                p.CompanyId == company.Id && p.StartDate.Year == 2026 && p.StartDate.Month == 9),
            "الفترةُ موجودةٌ سلفاً — فالاختبارُ لا يقيس إنشاءها المتزامن.");

        var outcome = await ParallelWriters.RunAsync(Writers, async i =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            var invoice = SalesInvoice.CreateDraft(company.Id, walkIn.Id, freshMonth);
            invoice.AddLine($"صنف {i}", 1m, 100m);
            var posted = await new SalesInvoicePostingService(db).PostAsync(invoice);
            return posted.InvoiceNumber!;
        });

        Assert.True(outcome.Failures.Count == 0,
            "ترحيلٌ متزامنٌ مشروع مات على إنشاء الفترة — " + outcome.Describe());

        var periods = await scope.Db.FiscalPeriods.CountAsync(p =>
            p.CompanyId == company.Id && p.StartDate.Year == 2026 && p.StartDate.Month == 9);
        Assert.Equal(1, periods);
    }

    /// <summary>
    /// <b>والفترةُ المقفلة تبقى مقفلة تحت التزاحم.</b> فقفلٌ يُصلح السباقَ ويُرخي حارسَ
    /// الإقفال يكون قد استبدل عطلاً بأسوأَ منه: ترحيلٌ في فترةٍ أُقفلت دفاترُها.
    /// </summary>
    [PostgresFact]
    public async Task AClosedPeriod_StaysClosed_UnderConcurrency()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("periodclosed");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة مقفلة", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        var walkIn = Customer.Create(company.Id, "عميل نقدي");
        scope.Db.Customers.Add(walkIn);
        await scope.Db.SaveChangesAsync();

        var month = new DateOnly(2026, 9, 1);
        var period = Brqosh.Domain.Accounting.FiscalPeriod.Create(company.Id, 2026, 9);
        period.Close();
        scope.Db.FiscalPeriods.Add(period);
        await scope.Db.SaveChangesAsync();

        var outcome = await ParallelWriters.RunAsync(Writers, async i =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            var invoice = SalesInvoice.CreateDraft(company.Id, walkIn.Id, month);
            invoice.AddLine($"صنف {i}", 1m, 100m);
            return (await new SalesInvoicePostingService(db).PostAsync(invoice)).InvoiceNumber!;
        });

        Assert.True(outcome.Successes.Count == 0,
            $"رُحّل في فترةٍ مقفلة تحت التزاحم ({outcome.Successes.Count} مرّة). {outcome.Describe()}");
    }
}
