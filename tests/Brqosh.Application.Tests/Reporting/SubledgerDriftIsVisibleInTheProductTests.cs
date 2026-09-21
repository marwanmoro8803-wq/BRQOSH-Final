using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٢٨) <b>انحرافُ الدفتر المساعد عن حساب مراقبته صار مرئيّاً
/// في المنتَج — وكان يُقاس في الاختبارات وحدَها.</b>
///
/// <para>هـ-١ وهـ-٢ وهـ-٣ <b>مُعرَّفةٌ في <c>LedgerIdentities</c> داخل شجرة الاختبارات</b>.
/// <b>فلا سبيلَ للمالك أن يعرف أنّ <c>2101</c> فارق مجموعَ ما على مورّديه</b> — والقيدُ
/// اليدويُّ إلى حساب مراقبةٍ يفعل ذلك بالضبط: <b>الأستاذُ يتحرّك والدفترُ المساعدُ لا
/// يستطيع أن يتبعه</b>، ولا شيءَ يشكو لأنّ القيد متوازن.</para>
///
/// <para><b>والكشفُ لا الحظر — وذلك قرارٌ لا تهاون</b>: القيدُ اليدويُّ إلى حساب مراقبة
/// <b>هو اليومَ المسارُ الوحيدُ للأرصدة الافتتاحيّة</b> (بندٌ مفتوحٌ مُسمًّى في التدقيق
/// نفسِه)، <b>فمنعُه يُغلق التأسيس</b>. فيُجعل الانحرافُ مقيساً بمقداره.</para>
/// </summary>
public class SubledgerDriftIsVisibleInTheProductTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public SubledgerDriftIsVisibleInTheProductTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> قيدٌ يدويٌّ يُدائن <c>2101</c> بلا مورِّد ⟵ <b>خطأٌ في
    /// تقرير جودة البيانات بمقدار الفرق</b>.
    ///
    /// <para><b>وهو ما كان يمرّ صامتاً إلى الأبد.</b></para>
    /// </summary>
    [Fact]
    public async Task AManualEntryToThePayablesControl_IsReportedAsDrift()
    {
        await SetupAsync();
        await ManualEntryAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable, credit: 1_500m);

        var finding = (await RunAsync()).Single(f => f.Code == "AP_SUBLEDGER_DRIFT");

        Assert.Equal("Error", finding.Severity);
        Assert.Contains("1,500.00", finding.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وكذلك الذمم المدينة</b> — قيدٌ يدويٌّ يُدين <c>1104</c> بلا عميل.
    /// </summary>
    [Fact]
    public async Task AManualEntryToTheReceivablesControl_IsReportedAsDrift()
    {
        await SetupAsync();
        await ManualEntryAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable, debit: 900m);

        var finding = (await RunAsync()).Single(f => f.Code == "AR_SUBLEDGER_DRIFT");

        Assert.Equal("Error", finding.Severity);
        Assert.Contains("900.00", finding.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والمخزون كذلك</b> — قيدٌ يدويٌّ يُدين <c>1107</c> بلا حركةٍ مخزنيّة،
    /// <b>فالقيمةُ في الميزانية بلا بضاعةٍ خلفَها</b>.
    /// </summary>
    [Fact]
    public async Task AManualEntryToTheInventoryControl_IsReportedAsDrift()
    {
        await SetupAsync();
        await ManualEntryAsync(SaudiChartOfAccounts.SystemCodes.Inventory, debit: 700m);

        var finding = (await RunAsync()).Single(f => f.Code == "INVENTORY_SUBLEDGER_DRIFT");

        Assert.Equal("Error", finding.Severity);
        Assert.Contains("700.00", finding.MessageAr, StringComparison.Ordinal);
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — دفاترُ سليمةٌ تُبلَّغ سليمةً لا تُسكَت عنها.</b>
    ///
    /// <para><b>وغيابُ السطر يُقرأ «لم يُفحص»</b>، ووجودُه بدرجة <c>Info</c> يقول
    /// «فُحص ولم ينحرف». <b>ولولا هذا لَما فرّق أحدٌ بين فحصٍ نظيفٍ وفحصٍ لم يقع.</b></para>
    /// </summary>
    [Fact]
    public async Task CleanBooks_ReportAllThreeIdentitiesAsInfo()
    {
        await SetupAsync();

        var findings = await RunAsync();

        foreach (var code in new[] { "AR_SUBLEDGER_DRIFT", "AP_SUBLEDGER_DRIFT", "INVENTORY_SUBLEDGER_DRIFT" })
            Assert.Equal("Info", findings.Single(f => f.Code == code).Severity);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — فاتورةُ مورّدٍ حقيقيّةٌ لا تُبلَّغ انحرافاً:</b> الأستاذُ
    /// والدفترُ المساعدُ يتحرّكان معاً. <b>ولولاه لجاز أن يكون الفحصُ يُنذر عن كلّ حركة.</b>
    /// </summary>
    [Fact]
    public async Task ARealSupplierInvoice_IsNotReportedAsDrift()
    {
        await SetupAsync();

        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, supplier.Id, new DateOnly(2026, 8, 3), "SUP-DQ", vatDeductible: true);
        invoice.AddLine("خدمة", 1m, 1_000m);
        await new Brqosh.Infrastructure.Purchasing.PurchaseInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal("Info", (await RunAsync()).Single(f => f.Code == "AP_SUBLEDGER_DRIFT").Severity);
    }

    // ── سلسلةُ التدقيق (ح-٢٩) ───────────────────────────────────────────────

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٢٩) <b>سلسلةُ تجزئة سجلّ التدقيق تُفحَص في المنتَج.</b>
    ///
    /// <para><c>AuditChainVerifier</c> يقول عن نفسه: «لا يمنع العبث — <b>يجعله غير قابل
    /// للإخفاء</b>». <b>ولم يكن له مستدعٍ واحدٌ في <c>src/</c></b> — في الاختبارات وحدَها.
    /// <b>فالعبثُ كان قابلاً للإخفاء تماماً</b>: لا أحدَ ينظر.</para>
    ///
    /// <para>ومسارُ المهاجم هو التعديلُ المباشر في القاعدة — <b>وهو ما تُوضع السلسلةُ
    /// لأجله أصلاً</b>، إذ يمنع معترضُ EF ما يجري داخل التطبيق وحدَه.</para>
    /// </summary>
    [Fact]
    public async Task TamperingWithTheAuditLog_IsReportedByTheProduct()
    {
        await SetupAsync();
        await new AuditService(_db).LogAsync(_companyId, "admin", "ترحيل", "T", "A-1", "تفاصيل");
        await new AuditService(_db).LogAsync(_companyId, "admin", "إلغاء", "T", "A-2", "تفاصيل");

        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE audit_log SET Details = 'محتوى مزوَّر' WHERE Sequence = 1");

        var finding = (await RunAsync()).Single(f => f.Code == "AUDIT_CHAIN");

        Assert.Equal("Error", finding.Severity);
        Assert.Contains("مكسورة", finding.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وشاهدُ ضبط — سلسلةٌ سليمةٌ تُبلَّغ سليمةً بعددها.</b>
    /// <b>وغيابُ السطر يُقرأ «لم يُفحص»</b>، فلا يُفرَّق بين سجلٍّ نظيفٍ وفحصٍ لم يقع.
    /// </summary>
    [Fact]
    public async Task AnIntactAuditChain_IsReportedAsInfo()
    {
        await SetupAsync();
        await new AuditService(_db).LogAsync(_companyId, "admin", "ترحيل", "T", "A-1", "تفاصيل");

        var finding = (await RunAsync()).Single(f => f.Code == "AUDIT_CHAIN");

        Assert.Equal("Info", finding.Severity);
        Assert.Contains("سليمة", finding.MessageAr, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الجودة", "310123456700055", BusinessProfile.General);
        _companyId = company.Id;
    }

    private async Task<IReadOnlyList<Brqosh.Application.Reporting.DataQualityFinding>> RunAsync() =>
        (await new DataQualityService(_db).RunAsync(_companyId)).Findings;

    /// <summary>قيدٌ يدويٌّ يمسّ حسابَ مراقبةٍ — مقابلَ الصندوق.</summary>
    private async Task ManualEntryAsync(string controlCode, decimal debit = 0m, decimal credit = 0m)
    {
        var control = await _db.Accounts.SingleAsync(
            a => a.CompanyId == _companyId && a.Code == controlCode);
        var cash = await _db.Accounts.SingleAsync(
            a => a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 8, 1), "تسوية يدويّة");
        if (debit > 0)
        {
            entry.AddLine(control, debit, 0m, "تسوية");
            entry.AddLine(cash, 0m, debit, "مقابل");
        }
        else
        {
            entry.AddLine(cash, credit, 0m, "مقابل");
            entry.AddLine(control, 0m, credit, "تسوية");
        }

        await new JournalPostingService(_db).PostAsync(entry);
    }
}
