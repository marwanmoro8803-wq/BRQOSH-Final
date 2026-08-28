using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (CEEF-229 · م0-٨) رصيد المورد يطرح إشعارات المدين.
///
/// <para><b>العطل:</b> <c>SupplierBalanceQuery</c> كان يحسب «فواتير − مدفوعات − شيكات − محتجزات»
/// و<b>لا يذكر <c>DebitNotes</c> إطلاقاً</b>، بينما <c>DebitNotePostingService</c> يُمدِّن حساب
/// الدائنين 2101 فعلاً. فيفترق الدفتر المساعد عن الأستاذ بقيمة <b>كل مرتجع مشتريات</b> —
/// ومؤشّر «الدائنون» في لوحة التحكم مبالغٌ فيه، وكشف حساب المورد لا يعرض المرتجع، والنتيجة
/// العملية دفعٌ زائد للمورد.</para>
///
/// <para>والحلّ كان موجوداً في المستودع نفسه على الجهة المقابلة: <c>CustomerBalanceQuery</c>
/// يطرح الإشعارات الدائنة منذ البداية. فهذا نقلُ نمطٍ قائم لا اختراع.</para>
/// </summary>
public class SupplierBalanceDebitNoteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _supplierId;
    private readonly long _productId;

    public SupplierBalanceDebitNoteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        var company = new DatabaseInitializer(_db)
            .InitializeAsync("شركة تجارية", "310123456700003", BusinessProfile.General)
            .GetAwaiter().GetResult();
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        _db.SaveChanges();

        _supplierId = supplier.Id;
        _productId = product.Id;
    }

    private async Task<PurchaseInvoice> PurchaseAsync(decimal qty, decimal unitPrice, string reference)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 1, 10), reference, true);
        invoice.AddLine("توريد", qty, unitPrice, _productId);
        return await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>
    /// السيناريو المُبلَّغ حرفياً: فاتورة 11,500 ومرتجع 2,300 ⟵ الرصيد <b>9,200</b>.
    /// قبل الإصلاح كان الدفتر المساعد يقول 11,500 والأستاذ 9,200.
    /// </summary>
    [Fact]
    public async Task PostedDebitNote_ReducesSupplierBalance()
    {
        var pi1 = await PurchaseAsync(100m, 100m, "SUP-1");   // 10,000 + 1,500 ضريبة = 11,500

        var note = DebitNote.CreateDraft(_companyId, _supplierId, pi1.Id, new DateOnly(2026, 1, 20), "مرتجع تالف");
        note.AddLine("مرتجع", 20m, 100m, _productId);        // 2,000 + 300 = 2,300
        await new DebitNotePostingService(_db).PostAsync(note);

        var balance = await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _supplierId);
        Assert.Equal(9200.00m, balance.Balance);

        var all = await new SupplierBalanceQuery(_db).AllBalancesAsync(_companyId);
        Assert.Equal(9200.00m, all[_supplierId]);

        // والهوية هـ-٢ تُثبت أن الدفترين اتّفقا — لا أن الرقم «يبدو» صحيحاً
        LedgerIdentities.Assert(await LedgerIdentities.PayablesAsync(_db, _companyId));
    }

    /// <summary>إشعار مسودة لا يُحتسب — الحالة تُحترم لا مجرد وجود الصفّ.</summary>
    [Fact]
    public async Task DraftDebitNote_IsIgnored()
    {
        var pi2 = await PurchaseAsync(50m, 100m, "SUP-2");    // 5,750

        var draft = DebitNote.CreateDraft(_companyId, _supplierId, pi2.Id, new DateOnly(2026, 1, 20), "مسودة");
        draft.AddLine("مرتجع", 10m, 100m, _productId);
        _db.DebitNotes.Add(draft);
        await _db.SaveChangesAsync();

        var balance = await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _supplierId);
        Assert.Equal(5750.00m, balance.Balance);
        LedgerIdentities.Assert(await LedgerIdentities.PayablesAsync(_db, _companyId));
    }

    /// <summary>مورد بلا إشعارات: الرصيد كما كان — الإصلاح لا يزيح أرقاماً قائمة.</summary>
    [Fact]
    public async Task SupplierWithoutDebitNotes_IsUnchanged()
    {
        await PurchaseAsync(30m, 200m, "SUP-3");             // 6,000 + 900 = 6,900

        var balance = await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _supplierId);
        Assert.Equal(6900.00m, balance.Balance);
        LedgerIdentities.Assert(await LedgerIdentities.PayablesAsync(_db, _companyId));
    }

    /// <summary>إشعاران متتاليان يُطرحان معاً، والهوية تصمد بعد كلٍّ منهما.</summary>
    [Fact]
    public async Task MultipleDebitNotes_AreAllSubtracted()
    {
        var pi4 = await PurchaseAsync(100m, 100m, "SUP-4");   // 11,500

        foreach (var qty in new[] { 10m, 5m })
        {
            var note = DebitNote.CreateDraft(
                _companyId, _supplierId, pi4.Id, new DateOnly(2026, 1, 20), $"مرتجع {qty}");
            note.AddLine("مرتجع", qty, 100m, _productId);
            await new DebitNotePostingService(_db).PostAsync(note);
            LedgerIdentities.Assert(await LedgerIdentities.PayablesAsync(_db, _companyId));
        }

        var balance = await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _supplierId);
        Assert.Equal(9775.00m, balance.Balance);            // 11,500 − 1,150 − 575
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
