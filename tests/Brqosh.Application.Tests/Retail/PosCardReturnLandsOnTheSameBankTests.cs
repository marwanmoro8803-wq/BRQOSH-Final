using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Retail;

/// <summary>
/// (CEEF-229 · م2-١٥أ) <b>بيعةُ الشبكة تنزل على حسابٍ ومرتجعُها على آخر — فلا يُغلقان أبداً.</b>
///
/// <para><b>وهذا نصفُ إصلاحٍ من صنع يدي.</b> في م2-١٥ أُنشئ <see cref="BankGlAccountResolver"/>
/// لأنّ <c>1102</c> (النقدية لدى البنوك) <b>أبٌ قابلٌ للترحيل</b> بينما كلُّ بنكٍ يُقيَّد على
/// ابنٍ خاصّ به (110201). فهُوجر الأبُ في مبيعات نقطة البيع وفي سندَي القبض والصرف —
/// <b>ونُسي المرتجع</b>.</para>
///
/// <para><b>فالنتيجة:</b> عميلٌ يشتري بمدى فتنزل قيمتُها على <c>110201</c>، ثمّ يُرجع البضاعة
/// فيُخصم المبلغُ من <c>1102</c>. المبلغان متساويان ولا يُلغي أحدُهما الآخر — يبقى الابنُ
/// منتفخاً والأبُ سالباً إلى الأبد. والتسويةُ البنكية تقرأ الابن وحده، فتُقارَن بكشفٍ لا يحوي
/// المرتجع، ويظهر <b>فرقٌ دائمٌ لا يُغلق</b>.</para>
///
/// <para><b>ولماذا لم تكشفه هوية التدفقات النقدية:</b> لأنّها تُطابق النقد بالبادئة
/// <c>"1102"</c> — وهي تشمل الأب وابنه معاً. فالمجموع سليم، <b>والخلل في التوزيع</b>.
/// وهذا بالضبط ما يقوله التعليقُ في <c>SalesInvoicePostingService</c> — والخدمةُ التي تليه
/// تفعل ما يحذّر منه.</para>
///
/// <para><b>والقياس هنا هوية:</b> بيعةٌ ومرتجعُها الكامل يجب أن يصفّرا <b>كلَّ حسابٍ على
/// حدة</b>، لا المجموع. فالمجموعُ صفرٌ في الحالتين — وهو ما جعل العطل غير مرئيّ.</para>
/// </summary>
public class PosCardReturnLandsOnTheSameBankTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _productId;

    public PosCardReturnLandsOnTheSameBankTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("Data Source=poscardreturn;Mode=Memory;Cache=Shared");
        _connection.Open();
        _db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, "Data Source=poscardreturn;Mode=Memory;Cache=Shared");
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Service, salePrice: 100m);
        _db.Products.Add(product);

        // ‏حسابٌ بنكيّ بابنٍ خاصّ تحت 1102 — وهذه هي الحالة الطبيعية لأيّ منشأةٍ لها بنك،
        // ‏فالشاشةُ تُنشئ الابنَ تلقائياً عند إضافة الحساب البنكيّ.
        var parent = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1102");
        var child = Account.Create(
            "110201", "الراجحي — الجاري", "Al Rajhi - Current", AccountType.Asset,
            parentId: parent.Id, companyId: _companyId);
        _db.Accounts.Add(child);
        await _db.SaveChangesAsync();

        var bank = BankAccount.Create(
            _companyId, "BNK-1", "الراجحي", "الراجحي", "1234567890",
            "SA0380000000608010167519", child.Id, isDefault: true);
        _db.BankAccounts.Add(bank);
        await _db.SaveChangesAsync();

        _productId = product.Id;
    }

    /// <summary>مدينٌ ودائنٌ لكلّ حسابٍ عبر قيود الشركة كلّها.</summary>
    private async Task<Dictionary<string, decimal>> NetByAccountAsync()
    {
        var lines = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId)
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.AccountCode)
            .Select(g => new { Code = g.Key, Net = g.Sum(l => l.Debit) - g.Sum(l => l.Credit) })
            .ToListAsync();

        return lines.ToDictionary(x => x.Code, x => x.Net);
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> بيعةُ شبكةٍ ثمّ مرتجعُها الكامل — وكلُّ حسابٍ نقديّ يعود صفراً.
    /// وإن نزلت البيعةُ على الابن وخرج المرتجعُ من الأب، بقي الاثنان غيرَ صفريَّين وتساويا
    /// في المقدار وتعاكسا في الإشارة: وهذا هو الانشقاق بعينه.
    /// </summary>
    [Fact]
    public async Task ACardSale_AndItsFullReturn_LeaveEveryBankAccountAtZero()
    {
        await SetupAsync();

        var shift = PosShift.Open(_companyId, "كاشير", 0m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        var sale = await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 1m)],
            isVatRegistered: true, PosPaymentMethod.Card, "كاشير");

        var note = CreditNote.CreateDraft(
            _companyId, sale.Invoice.CustomerId, sale.Invoice.Id,
            new DateOnly(2026, 6, 2), "إرجاع كامل");
        note.AddLine("صنف", 1m, 100m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        var net = await NetByAccountAsync();

        var parent = net.GetValueOrDefault("1102");
        var child = net.GetValueOrDefault("110201");

        Assert.True(parent == 0m && child == 0m,
            $"انشقّ نقدُ البنوك: الأب 1102 = {parent} · الابن 110201 = {child}. "
            + "البيعةُ ومرتجعُها لم ينزلا على الحساب نفسه، فيبقى فرقٌ دائمٌ في التسوية البنكية "
            + $"مقدارُه {Math.Abs(child):N2} ريال لا يُغلق أبداً.");
    }

    /// <summary>
    /// <b>حارسٌ للحارس.</b> المجموعُ تحت البادئة <c>1102</c> صفرٌ في الحالتين — سليمةً كانت
    /// أو منشقّة. فلو قِيس المجموعُ وحده لمرّ العطلُ دائماً، وهذا سببُ خفائه كلَّ هذه المدّة.
    /// </summary>
    [Fact]
    public async Task ThePrefixTotal_IsZeroEitherWay_SoItProvesNothing()
    {
        await SetupAsync();

        var shift = PosShift.Open(_companyId, "كاشير", 0m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        var sale = await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 1m)],
            isVatRegistered: true, PosPaymentMethod.Card, "كاشير");

        var note = CreditNote.CreateDraft(
            _companyId, sale.Invoice.CustomerId, sale.Invoice.Id,
            new DateOnly(2026, 6, 2), "إرجاع كامل");
        note.AddLine("صنف", 1m, 100m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        var net = await NetByAccountAsync();
        var prefixTotal = net.Where(kv => kv.Key.StartsWith("1102", StringComparison.Ordinal))
                             .Sum(kv => kv.Value);

        Assert.Equal(0m, prefixTotal);
    }

    /// <summary>
    /// <b>وحارسٌ ثالث:</b> البيعةُ وحدها تنزل على <b>ابن</b> البنك لا على الأب — فلو عاد
    /// أحدٌ بالمبيعات إلى الأب لانشقّ النقد من الطرف الآخر، وصمت الاختبار الأوّل.
    /// </summary>
    [Fact]
    public async Task ACardSale_LandsOnTheBankChild_NotTheParent()
    {
        await SetupAsync();

        var shift = PosShift.Open(_companyId, "كاشير", 0m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 1m)],
            isVatRegistered: true, PosPaymentMethod.Card, "كاشير");

        var net = await NetByAccountAsync();

        Assert.True(net.GetValueOrDefault("110201") > 0m,
            "بيعةُ الشبكة لم تنزل على ابن البنك — الأب 1102 لا يمثّل بنكاً بعينه.");
        Assert.Equal(0m, net.GetValueOrDefault("1102"));
    }
}
