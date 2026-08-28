using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-229 · م2-١٤) <b>هوية قائمة التدفقات النقدية</b> — على غرار الهويات الجبرية (ح-٥).
///
/// <para><b>لماذا هويةٌ لا تأكيدُ قيمة:</b> اختبارات القائمة القائمة تؤكّد <c>Operating.Subtotal</c>
/// و<c>Financing.Subtotal</c> <b>منفردَين</b> — فكلٌّ منها يمرّ وإن سقط بندٌ كامل من القائمة،
/// إذ لا شيء يربط الأجزاء بالكلّ. والقائمة النقدية تخضع لهويّتين لا تحتملان استثناءً:</para>
///
/// <para>هـ-٦: <c>ClosingCash − OpeningCash == NetCashChange</c> — تعريفُ «صافي التغيّر».</para>
/// <para>هـ-٧: <c>Operating + Investing + Financing == NetCashChange</c> — تفسيرُ ذلك التغيّر
/// كاملاً. وكسرُها يعني أن نقداً تحرّك <b>بلا تفسير</b>، وهو أخطر ما يمكن أن تقوله قائمةٌ نقدية.</para>
///
/// <para><b>والعطلان اللذان أمسكتهما هاتان الهويتان:</b> (١) <c>SumCash</c> يطابق أكواد النقدية
/// <b>مساواةً تامّة</b> على <c>["1101","1102","1103"]</c>، بينما كل حسابٍ بنكيّ يُنشأ <b>ابناً</b>
/// بكودٍ مثل <c>110201</c> — فكلّ نقد البنوك خارج الرصيدين الافتتاحيّ والختاميّ. (٢) قائمة رأس
/// المال العامل تُعدَّد يدوياً، وينقصها الشيكات برسم الدفع والرواتب المستحقة — ودفعةٌ سابقة
/// (CEEF-228) اضطرّت لإضافة خمسة أكواد غائبة، فالعطل نمطٌ متكرّر لا حادثة.</para>
/// </summary>
public class CashFlowIdentityTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;

    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 3, 31);

    public CashFlowIdentityTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("منشأة تجارية", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
        new BrqoshSchemaUpdater(_db).ApplyAsync().GetAwaiter().GetResult();

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        _db.SaveChanges();
        _customerId = customer.Id;
    }

    private async Task<CashFlowStatementResult> StatementAsync() =>
        await new CashFlowStatementService(_db).ExecuteAsync(_companyId, From, To);

    private static void AssertIdentities(CashFlowStatementResult r)
    {
        Assert.True(r.ClosingCash - r.OpeningCash == r.NetCashChange,
            $"هـ-٦ مكسورة: الختاميّ {r.ClosingCash} − الافتتاحيّ {r.OpeningCash} " +
            $"= {r.ClosingCash - r.OpeningCash} ≠ صافي التغيّر {r.NetCashChange}.");

        var explained = r.Operating.Subtotal + r.Investing.Subtotal + r.Financing.Subtotal;
        Assert.True(explained == r.NetCashChange,
            $"هـ-٧ مكسورة: الأقسام تفسّر {explained} بينما تحرّك النقد {r.NetCashChange} — " +
            $"الفرق {r.NetCashChange - explained} ريالاً بلا تفسير.");
    }

    private async Task<long> BankAccountAsync()
    {
        var bank = await new BankAccountService(_db).CreateAsync(
            _companyId, "B1", "البنك الأول", "مصرف الرياض", "1234567890",
            "SA0380000000608010167519", isDefault: true);
        return bank.Id;
    }

    private async Task<SalesInvoice> SellAsync(decimal amount)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("خدمة", 1m, amount);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    /// <summary>
    /// <b>القياس المركزيّ — وأبسط سيناريو ممكن لمنشأة تجارية.</b> بيعةٌ آجلة ثم تحصيلٌ
    /// <b>بنكيّ</b>. النقد دخل البنك فعلاً، فيجب أن يظهر في الرصيد الختاميّ.
    /// </summary>
    [Fact]
    public async Task BankCollection_AppearsInCash_AndTheStatementBalances()
    {
        var bankId = await BankAccountAsync();
        var invoice = await SellAsync(10_000m);

        var voucher = ReceiptVoucher.CreateDraft(
            _companyId, _customerId, new DateOnly(2026, 2, 15), 11_500m,
            ReceiptMethod.Bank, "تحصيل بنكيّ", bankAccountId: bankId);
        await new ReceiptVoucherPostingService(_db).PostAsync(voucher);

        var result = await StatementAsync();

        Assert.Equal(11_500m, result.ClosingCash);
        AssertIdentities(result);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> التحصيل النقديّ يظهر أصلاً — فلو مرّ الاختبار السابق ورسب هذا
    /// لكان العيب في القياس لا في المقيس. الفارق الوحيد بينهما طريقةُ التحصيل.
    /// </summary>
    [Fact]
    public async Task CashCollection_AppearsInCash_AndTheStatementBalances()
    {
        var invoice = await SellAsync(10_000m);

        var voucher = ReceiptVoucher.CreateDraft(
            _companyId, _customerId, new DateOnly(2026, 2, 15), 11_500m,
            ReceiptMethod.Cash, "تحصيل نقديّ");
        await new ReceiptVoucherPostingService(_db).PostAsync(voucher);

        var result = await StatementAsync();

        Assert.Equal(11_500m, result.ClosingCash);
        AssertIdentities(result);
    }

    /// <summary>
    /// ومنشأةٌ لها بنكان — وهو حال أيّ منشأة تكبر قليلاً. الابن الثاني يحمل كوداً آخر،
    /// فإن كان العدّ حرفياً سقط الاثنان معاً.
    /// </summary>
    [Fact]
    public async Task TwoBankAccounts_AreBothCounted()
    {
        var first = await BankAccountAsync();
        var second = await new BankAccountService(_db).CreateAsync(
            _companyId, "B2", "البنك الثاني", "البنك الأهلي", "9876543210",
            "SA4420000001234567891234", isDefault: false);

        var invoice = await SellAsync(10_000m);
        await new ReceiptVoucherPostingService(_db).PostAsync(ReceiptVoucher.CreateDraft(
            _companyId, _customerId, new DateOnly(2026, 2, 10), 5_000m,
            ReceiptMethod.Bank, "تحصيل بنك 1", bankAccountId: first));
        await new ReceiptVoucherPostingService(_db).PostAsync(ReceiptVoucher.CreateDraft(
            _companyId, _customerId, new DateOnly(2026, 2, 20), 6_500m,
            ReceiptMethod.Bank, "تحصيل بنك 2", bankAccountId: second.Id));

        var result = await StatementAsync();

        Assert.Equal(11_500m, result.ClosingCash);
        AssertIdentities(result);
    }

    /// <summary>
    /// <b>الشيك الصادر — وهو أداة الدفع الأشيع لدى المنشآت التجارية المحلية.</b>
    ///
    /// <para>إصدارُ الشيك <b>لا يحرّك نقداً</b>: يُدين ذمم الموردين ويُدين حساب «شيكات برسم
    /// الدفع» (2103). فالنقد لم يتغيّر، ويجب أن تفسّر الأقسام صفراً. لكنّ 2103 غائبٌ عن قائمة
    /// رأس المال العامل المعدودة يدوياً — فيظهر انخفاضُ الذمم بلا ما يقابله، وتزعم القائمة
    /// تدفقاً تشغيلياً سالباً <b>لم يحدث</b>. الهوية هـ-٧ تكشفه فوراً.</para>
    /// </summary>
    [Fact]
    public async Task IssuingACheque_MovesNoCash_AndTheSectionsMustSaySo()
    {
        var bankId = await BankAccountAsync();
        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var purchase = PurchaseInvoice.CreateDraft(
            _companyId, supplier.Id, new DateOnly(2026, 2, 1), "SUP-1", vatDeductible: true);
        purchase.AddLine("بضاعة", 1m, 5_000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var cheque = Cheque.CreateIssued(
            _companyId, "CHQ-001", new DateOnly(2026, 2, 20), 5_750m,
            "سداد مورد", bankId, supplier.Id);
        await new ChequePostingService(_db).RegisterIssuedAsync(cheque);

        var result = await StatementAsync();

        Assert.Equal(0m, result.NetCashChange);   // لم يُصرف ريال بعد
        AssertIdentities(result);
    }

    /// <summary>
    /// <b>مجمّع إهلاكٍ لكل فئة أصل</b> — عرفٌ محاسبيّ شائع، ودليلُ الحسابات يسمح به.
    ///
    /// <para>كان ردُّ الإهلاك يُقرأ بمساواةٍ تامّة على <c>1207</c>، فمنشأةٌ تفصّل مجمّعاتها
    /// (<c>120701</c> للسيارات، <c>120702</c> للأثاث) يصير ردُّها صفراً — <b>فيُطرح الإهلاك
    /// كأنه نقدٌ خرج</b>، وهو أكبر بندٍ غير نقديّ في أيّ قائمة. وفي الوقت نفسه يدخل الابنُ
    /// قسمَ الاستثمار لأن استثناءه كان بمساواةٍ تامّة كذلك — فيُحتسب مرّتين بإشارتين.</para>
    /// </summary>
    [Fact]
    public async Task DepreciationSubAccount_IsAddedBackOnce_NotCountedAsInvesting()
    {
        var parent = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1207");
        var child = Account.Create("120701", "مجمع إهلاك السيارات", "Accum. dep. — vehicles",
            AccountType.Asset, parent.Id, companyId: _companyId);
        _db.Accounts.Add(child);
        await _db.SaveChangesAsync();

        var expense = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "5211");
        var entry = Domain.Accounting.JournalEntry.CreateDraft(
            _companyId, new DateOnly(2026, 2, 28), "إهلاك الشهر");
        entry.AddLine(expense, 3_000m, 0m);
        entry.AddLine(child, 0m, 3_000m);
        await new Infrastructure.Accounting.JournalPostingService(_db).PostAsync(entry);

        var result = await StatementAsync();

        // لا نقد تحرّك: الإهلاك قيدٌ غير نقديّ بالكامل.
        Assert.Equal(0m, result.NetCashChange);
        Assert.Equal(0m, result.Investing.Subtotal);
        AssertIdentities(result);
    }

    /// <summary>
    /// وفترةٌ بلا حركة تصدق فيها الهويتان بالأصفار — فحارسٌ يمرّ على العدم لا يُثبت شيئاً،
    /// وهذا يُثبت أن الاختبارات أعلاه تمرّ لأن الأرقام صحيحة لا لأن القائمة فارغة.
    /// </summary>
    [Fact]
    public async Task EmptyPeriod_SatisfiesTheIdentitiesTrivially()
    {
        var result = await StatementAsync();

        Assert.Equal(0m, result.NetCashChange);
        AssertIdentities(result);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
