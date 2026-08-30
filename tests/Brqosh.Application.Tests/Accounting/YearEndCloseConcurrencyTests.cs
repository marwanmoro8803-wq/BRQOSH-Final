using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (CEEF-229 · م1-١٥ / B-4) <b>إقفالان سنويّان متزامنان يُضاعفان الأرباح المبقاة — بلا أثرٍ مرئيّ.</b>
///
/// <para><b>العطل:</b> الحارسُ الوحيد قراءةٌ عاديّة (<c>AnyAsync</c> على
/// <c>YearEndClose:{year}</c>)، والإدراجُ يقع بعدها بنحو خمسةٍ وتسعين سطراً. ولا قفلَ بينهما،
/// والمعاملةُ تعمل على <c>Read Committed</c> — فيقرأ المُقفلان «غيرُ مقفلة» ويكتبان قيدَي إقفال.</para>
///
/// <para><b>وأخطرُ ما فيه أنّه لا يُرى:</b> قائمةُ الدخل تستثني قيودَ الإقفال
/// (<c>ExcludeYearEndClosing</c>)، فالتضاعفُ لا يظهر فيها إطلاقاً — <b>ويستقرّ صامتاً في
/// حقوق الملكية بالميزانية</b>.</para>
///
/// <para><b>والمسارُ حقيقيّ لا نظريّ:</b> زرُّ الإقفال في شاشة الفترات <c>async void</c> ولا
/// يُعطَّل أثناء الانتظار، فنقرتان تُطلقان عمليتين متداخلتين؛ وطرفان في متجرٍ واحد على خادم
/// PostgreSQL مشترك يتسابقان كذلك.</para>
/// </summary>
public class YearEndCloseConcurrencyTests
{
    private const int Writers = 6;

    /// <summary>
    /// <b>القياس المركزيّ.</b> ستّةُ إقفالاتٍ متزامنة ⟵ <b>قيدُ إقفالٍ واحد</b>، وحقوقُ
    /// الملكية تحمل صافي الدخل مرّةً واحدة.
    ///
    /// <para>ورفضُ الخمسة الباقين سلوكٌ سليم — بل هو المطلوب. والعطلُ أن ينجح أكثرُ من واحد.</para>
    /// </summary>
    [PostgresFact]
    public async Task ConcurrentYearEndCloses_ProduceExactlyOneClosingEntry()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("yearclose");

        var companyId = await SeedAProfitableYearAsync(scope.Db);
        var netIncome = await NetIncomeAsync(scope.Db, companyId);
        Assert.True(netIncome > 0m, "السنةُ بلا ربح — فالقياسُ لا يفرّق بين مرّةٍ ومرّتين.");

        // ‏تُهيَّأ فتراتُ السنة **متسلسلةً** قبل التزاحم.
        //
        // ولولاه لَقاس هذا الاختبارُ عطلاً آخر: أوّلُ تشغيلٍ أظهر «نجح 1 وأخفق 5»، لكنّ ثلاثةً
        // ‏من الخمسة ماتوا على `IX_fiscal_periods_CompanyId_Code` — سباقِ إنشاء الفترة الذي
        // ‏كشفه م1-١٤ — فلم يبلغ حارسَ الإقفال إلا اثنان. **فالنافذةُ كانت مستورةً بعطلٍ آخر**،
        // ‏والاختبارُ يمرّ لسببٍ لا علاقة له بما جاء يقيسه.
        await PrewarmPeriodsAsync(scope.Db, companyId);

        var outcome = await ParallelWriters.RunAsync(Writers, async _ =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();
            return await new YearEndCloseService(db).CloseYearAsync(companyId, 2026, "مدير");
        });

        var closings = await scope.Db.JournalEntries
            .IgnoreQueryFilters()
            .Where(e => e.CompanyId == companyId && e.SourceDocument == "YearEndClose:2026")
            .CountAsync();

        Assert.True(closings == 1,
            $"عددُ قيود الإقفال {closings} لا واحد — والأرباحُ المبقاة تضاعفت بصمت. "
            + outcome.Describe());

        Assert.True(outcome.Successes.Count == 1,
            $"نجح أكثرُ من إقفالٍ واحد. {outcome.Describe()}");

        // ‏وحقوقُ الملكية تحمل صافي الدخل مرّةً واحدة — وهو ما لا تكشفه قائمةُ الدخل إطلاقاً.
        var retained = await LedgerAsync(scope.Db, companyId, SaudiChartOfAccounts.SystemCodes.RetainedEarnings);
        Assert.Equal(-netIncome, retained);
    }

    /// <summary>
    /// <b>وسنةٌ بلا حركة لا تُقفَل مرّتين كذلك.</b> فالمسارُ القصير كان يُغلق الفترات
    /// <b>بلا كتابة قيدٍ أصلاً</b> — فلا يبقى في الدفتر ما يدلّ على أنّها أُقفلت، ويبقى
    /// الحارسُ غيرَ مسلَّح لتلك السنة إلى الأبد.
    /// </summary>
    [PostgresFact]
    public async Task AnEmptyYear_IsAlsoClosedOnlyOnce()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("yearclosempty");

        var company = await new DatabaseInitializer(scope.Db)
            .InitializeAsync("منشأة ساكنة", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(scope.Db).ApplyAsync();

        await PrewarmPeriodsAsync(scope.Db, company.Id);

        var outcome = await ParallelWriters.RunAsync(Writers, async _ =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();
            return await new YearEndCloseService(db).CloseYearAsync(company.Id, 2026, "مدير");
        });

        Assert.True(outcome.Successes.Count == 1,
            $"سنةٌ ساكنة أُقفلت {outcome.Successes.Count} مرّة. {outcome.Describe()}");
    }

    /// <summary>فتراتُ 2026 الاثنتا عشرة تُنشأ متسلسلةً — لئلا يُقاس سباقُ إنشائها بدل الإقفال.</summary>
    private static async Task PrewarmPeriodsAsync(BrqoshDbContext db, long companyId)
    {
        var posting = new JournalPostingService(db);
        for (var month = 1; month <= 12; month++)
            await posting.EnsurePeriodOpenAsync(companyId, new DateOnly(2026, month, 1));
    }

    // ── التهيئة ──────────────────────────────────────────────────────────────

    private static async Task<long> SeedAProfitableYearAsync(BrqoshDbContext db)
    {
        var company = await new DatabaseInitializer(db)
            .InitializeAsync("منشأة الإقفال", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(db).ApplyAsync();

        var customer = Customer.Create(company.Id, "عميل نقدي");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 6, 1));
        invoice.AddLine("خدمة", 1m, 10_000m);
        await new SalesInvoicePostingService(db).PostAsync(invoice);

        return company.Id;
    }

    private static async Task<decimal> NetIncomeAsync(BrqoshDbContext db, long companyId)
    {
        var statement = await new IncomeStatementQuery(db)
            .ExecuteAsync(companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return statement.NetIncome;
    }

    private static async Task<decimal> LedgerAsync(BrqoshDbContext db, long companyId, string accountCode)
    {
        var rows = await new TrialBalanceQuery(db)
            .ExecuteAsync(companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));
        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
