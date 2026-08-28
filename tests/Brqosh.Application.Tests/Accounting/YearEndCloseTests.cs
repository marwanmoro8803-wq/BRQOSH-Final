using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

public class YearEndCloseTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public YearEndCloseTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    [Fact]
    public async Task YearEndClose_Closes_Pnl_Into_Retained_Earnings()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "310123456700099", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2025, 6, 15));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var result = await new YearEndCloseService(_db).CloseYearAsync(company.Id, 2025, "admin");
        Assert.Equal(2025, result.Year);
        Assert.True(result.NetIncome > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.EntryNumber));

        var closeEntry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == result.ClosingEntryId);
        Assert.Equal(JournalEntryStatus.Posted, closeEntry.Status);
        Assert.Equal(closeEntry.TotalDebit, closeEntry.TotalCredit);

        var openPeriods = await _db.FiscalPeriods.CountAsync(p =>
            p.CompanyId == company.Id && p.StartDate.Year == 2025 && p.Status == FiscalPeriodStatus.Open);
        Assert.Equal(0, openPeriods);
    }

    /// <summary>
    /// (CEEF-229 · م2-٧) <b>كان يمرّ على حارسِ غيره.</b>
    ///
    /// <para>الصيغة السابقة أقفلت سنةً <b>بلا حركة</b> على شركةٍ مُهيّأة للتوّ. وسنةٌ خالية
    /// تُخرج قائمة دخلٍ فارغة (كل صفٍّ رصيدُه صفر يُسقَط)، فيخرج الإقفال من فرع
    /// <c>Lines.Count &lt; 2</c> عائداً <c>(0, "", 0)</c> <b>بلا أن يحفظ قيداً</b>. فبقي حارس
    /// «مقفلة مسبقاً» — الذي يبحث عن قيد <c>YearEndClose:{year}</c> — <b>منزوع السلاح</b>.</para>
    ///
    /// <para>ومع ذلك كان الاستدعاء الثاني يرمي، فيمرّ الاختبار: لكنّ الرامي حارسٌ آخر تماماً —
    /// <c>EnsurePeriodOpenAsync</c>، لأن الإقفال الأول أنشأ فترة 2024-12 وأغلقها. والرسالة
    /// المُرماة «الفترة المالية 2024-12 مقفلة» لا «السنة 2024 مقفلة مسبقاً». والاختبار يفحص
    /// <b>نوع</b> الاستثناء دون نصّه، فلا يفرّق بين الحارسَين.</para>
    ///
    /// <para><b>أثرُه:</b> لو حُذف فحص «مقفلة مسبقاً» كلّه لبقي هذا الاختبار أخضر.</para>
    ///
    /// <para>الصيغة أدناه تُزيل الالتباس بثلاثٍ معاً: سنةٌ <b>ذات حركة</b> فيُحفظ قيد الإقفال
    /// ويتسلّح الحارس · <b>إعادة فتح ديسمبر</b> فيُنحّى الحارس المنافس عن الطريق · وتأكيدُ
    /// <b>نصّ</b> الرسالة كاملاً لا نوعِها.</para>
    /// </summary>
    [Fact]
    public async Task YearEndClose_Cannot_Run_Twice()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "310123456700088", BusinessProfile.General);
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2024, 6, 15));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        // الإقفال الأول يجب أن يُنتج قيداً فعلاً — وإلا عاد الاختبار يقيس العدم.
        var first = await new YearEndCloseService(_db).CloseYearAsync(company.Id, 2024, "admin");
        Assert.False(string.IsNullOrWhiteSpace(first.EntryNumber));

        // تنحيةُ الحارس المنافس: ما دامت 2024-12 مقفلة فحارس الفترة يرمي قبل أن يُختبر شيء.
        var december = await _db.FiscalPeriods
            .SingleAsync(p => p.CompanyId == company.Id && p.Code == "2024-12");
        december.Reopen();
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new YearEndCloseService(_db).CloseYearAsync(company.Id, 2024, "admin"));

        // النصّ لا النوع: ثلاث رسائل في النظام تحوي «مقفلة»، وكلمة «السنة» وحدها تميّز هذه.
        Assert.Equal("السنة 2024 مقفلة مسبقاً.", ex.Message);

        // ولا أثر ثانياً بقي — لا قيد إقفال مكرَّر.
        Assert.Equal(1, await _db.JournalEntries.CountAsync(e =>
            e.CompanyId == company.Id && e.SourceDocument == "YearEndClose:2024"
            && e.Status != JournalEntryStatus.Draft));
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
