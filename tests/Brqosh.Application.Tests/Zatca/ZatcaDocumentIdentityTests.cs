using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-229 · Z-3 و Z-4) هوية مستند ZATCA وسلسلة عدّاده.
///
/// <para><b>Z-3 — ثلاثة فضاءات معرّفات في عمود واحد.</b> النوع <c>CreditNote</c> كان يُستعمل
/// لثلاثة أشياء: إشعارٌ دائن حقيقيّ (معرّفه معرّف الإشعار)، وإشعارٌ ناتج عن إلغاء فاتورة
/// (معرّفه معرّف <b>الفاتورة</b>)، وإشعارٌ ناتج عن إلغاء مستخلص. والتسلسلات الثلاثة مستقلّة
/// وتبدأ كلّها من واحد — داخل فهرسٍ فريد واحد.</para>
///
/// <para><b>فينتج عطلان:</b> ترحيل إشعار دائن سليم يُرفض لأن فاتورةً بالمعرّف نفسه أُلغيت
/// سابقاً · ومرجع الفاتورة الأصلية يُحلّ من الجدول الخطأ فيُنسب الإشعار إلى فاتورةٍ أخرى —
/// ويُقبل لدى الهيئة شكلاً ويُفسد الدفاتر جوهراً.</para>
///
/// <para><b>Z-4 — سلسلة العدّاد.</b> <c>MaxAsync + 1</c> بلا قفل وبلا فهرس فريد. سلسلة سجلّ
/// التدقيق أخذت الاثنين منذ البداية؛ سلسلة ZATCA لم تأخذ أياً منهما — وتفرّعُها لا يُصلَح
/// بأثر رجعيّ.</para>
/// </summary>
public class ZatcaDocumentIdentityTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;

    public ZatcaDocumentIdentityTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
    }

    private ZatcaEInvoiceDocument Doc(ZatcaDocumentType type, long sourceId, int icv) =>
        ZatcaEInvoiceDocument.Create(
            _companyId, type, sourceId, $"DOC-{type}-{sourceId}", Guid.NewGuid(),
            icv, previousHash: "prev", invoiceHash: "hash",
            ZatcaTransmissionMode.Reporting, qrTlv: "qr", xmlPath: "path.xml", signedXmlPath: null);

    // ── Z-3 · فضاءات المعرّفات صارت مستقلّة ─────────────────────────────────

    /// <summary>
    /// <b>القياس الأساسيّ.</b> إلغاء الفاتورة رقم 12 ثم ترحيل الإشعار الدائن رقم 12 —
    /// كلاهما يُقبل. قبل الإصلاح كان الفهرس الفريد يرفض الثاني رفضاً نهائياً.
    /// </summary>
    [Fact]
    public async Task InvoiceCancellation_AndRealCreditNote_WithTheSameId_CoExist()
    {
        _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.InvoiceCancel, 12, 1));
        await _db.SaveChangesAsync();

        _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.CreditNote, 12, 2));
        await _db.SaveChangesAsync();   // كان يرمي 23505

        Assert.Equal(2, await _db.ZatcaEInvoiceDocuments.CountAsync(d => d.SourceDocumentId == 12));
    }

    /// <summary>وإلغاء المستخلص فضاءٌ ثالث مستقلّ.</summary>
    [Fact]
    public async Task BillingCancellation_IsItsOwnIdentitySpace()
    {
        _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.CreditNote, 5, 1));
        _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.InvoiceCancel, 5, 2));
        _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.BillingCancel, 5, 3));

        await _db.SaveChangesAsync();

        Assert.Equal(3, await _db.ZatcaEInvoiceDocuments.CountAsync(d => d.SourceDocumentId == 5));
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> الفهرس ما زال يمنع الازدواج <b>داخل</b> النوع الواحد. لولا هذا
    /// لكان «افصل الأنواع» يُمرّر الاختبارات أعلاه بينما فتح باب توليد مستندين لإشعارٍ واحد.
    /// </summary>
    [Fact]
    public async Task SameTypeAndSource_IsStillRejected()
    {
        _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.CreditNote, 9, 1));
        await _db.SaveChangesAsync();

        _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.CreditNote, 9, 2));

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    // ── Z-4 · الفهرس الفريد على العدّاد ─────────────────────────────────────

    /// <summary>
    /// عدّادٌ مكرّر في الشركة نفسها يُرفض على مستوى القاعدة — وهذا ما يحدث حين يتفرّع السباق.
    /// </summary>
    [Fact]
    public async Task DuplicateIcv_InTheSameCompany_IsRejected()
    {
        _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.SalesInvoice, 1, 418));
        await _db.SaveChangesAsync();

        _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.SalesInvoice, 2, 418));

        var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.NotNull(ex);
    }

    /// <summary>والعدّاد متسلسل بلا فجوة على المسار الطبيعيّ.</summary>
    [Fact]
    public async Task SequentialIcv_IsAccepted()
    {
        for (var i = 1; i <= 5; i++)
            _db.ZatcaEInvoiceDocuments.Add(Doc(ZatcaDocumentType.SalesInvoice, i, i));

        await _db.SaveChangesAsync();

        var icvs = await _db.ZatcaEInvoiceDocuments
            .OrderBy(d => d.Icv).Select(d => d.Icv).ToListAsync();
        Assert.Equal([1, 2, 3, 4, 5], icvs);
    }

    // ── Z-3 · المرجع الأصليّ يُحلّ من الجدول الصحيح ─────────────────────────

    /// <summary>
    /// <b>العطل الصامت.</b> شركةٌ فيها فاتورة معرّفها 7 وإشعار دائن معرّفه 7 على فاتورة أخرى.
    /// إلغاء الفاتورة 7 يجب أن يُنتج مرجعاً إلى <b>الفاتورة 7</b> — لا إلى فاتورة الإشعار.
    /// </summary>
    [Fact]
    public async Task CancellingInvoice_ReferencesThatInvoice_NotACreditNoteWithTheSameId()
    {
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        // فاتورتان: الأولى ستُلغى، والثانية أصلٌ لإشعارٍ دائن يحمل المعرّف نفسه صدفةً
        var first = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 3, 1));
        first.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(first);

        var second = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 3, 2));
        second.AddLine("خدمة", 1m, 500m);
        await new SalesInvoicePostingService(_db).PostAsync(second);

        var generator = new ZatcaEInvoiceGenerator(_db);
        var resolved = await generator.ResolveBillingReferenceForTestsAsync(
            _companyId, ZatcaDocumentType.InvoiceCancel, first.Id);

        Assert.Equal(first.InvoiceNumber, resolved.Number);
        Assert.NotEqual(second.InvoiceNumber, resolved.Number);
    }

    /// <summary>وفاتورةُ شركةٍ أخرى لا تُحلّ إطلاقاً — التصفية بالشركة كانت غائبة.</summary>
    [Fact]
    public async Task InvoiceOfAnotherCompany_IsNeverResolved()
    {
        // شركة ثانية تُنشأ مباشرةً — `DatabaseInitializer.InitializeAsync` يُعيد **أوّل** شركة
        // في القاعدة مهما كان الاسم المطلوب، فلا يصلح لإنشاء ثانية.
        var other = Brqosh.Domain.Companies.Company.Create(
            "شركة أخرى", "310123456700110", BusinessProfile.General);
        _db.Companies.Add(other);
        await _db.SaveChangesAsync();

        // الترحيل يشترط فرعاً رئيسياً — والمهيّئ لم يزرعه لأنه لم يُنشئ هذه الشركة.
        _db.Branches.Add(Brqosh.Domain.Companies.Branch.Create(
            other.Id, "HQ", "الفرع الرئيسي", isHeadquarters: true));
        foreach (var seed in Brqosh.Domain.Accounting.SaudiChartOfAccounts.Template)
            _db.Accounts.Add(Brqosh.Domain.Accounting.Account.Create(
                seed.Code, seed.NameAr, seed.NameEn, seed.Type,
                parentId: null, isGroup: seed.IsGroup, isSystem: seed.IsSystem, companyId: other.Id));
        await _db.SaveChangesAsync();

        var otherCustomer = Customer.Create(other.Id, "عميل ب", "311111111100004");
        _db.Customers.Add(otherCustomer);
        await _db.SaveChangesAsync();

        var foreignInvoice = SalesInvoice.CreateDraft(other.Id, otherCustomer.Id, new DateOnly(2026, 3, 1));
        foreignInvoice.AddLine("خدمة", 1m, 9000m);
        await new SalesInvoicePostingService(_db).PostAsync(foreignInvoice);

        var resolved = await new ZatcaEInvoiceGenerator(_db).ResolveBillingReferenceForTestsAsync(
            _companyId, ZatcaDocumentType.InvoiceCancel, foreignInvoice.Id);

        Assert.Null(resolved.Number);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
