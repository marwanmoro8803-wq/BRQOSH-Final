using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-229 · م1-٦ / Z-4) <b>قفلُ عدّاد ICV حيٌّ على كلّ مسار — لا على بعضها.</b>
///
/// <para><b>العطل:</b> <c>pg_advisory_xact_lock</c> مربوطٌ بالمعاملة. وإلغاءُ الفاتورة
/// يستدعي المولّدَ <b>داخل</b> معاملته، بينما إلغاءُ الإشعار الدائن والمستخلص يستدعيانه
/// <b>بعد إغلاقها بستّة أسطر</b> — فيجري القفلُ في autocommit ويُحرَّر فوراً.</para>
///
/// <para><b>ولماذا لم يُكشف:</b> القفلُ يعود مبكّراً على SQLite، <b>وكلُّ اختبارات المستودع
/// على SQLite</b>. فالمسارُ الميّت غيرُ مرئيٍّ للحزمة كلِّها. ثمّ إنّ الفهرس الفريد يلتقط
/// التزاحمَ برميةٍ صاخبة، <b>فيبدو النظامُ محميّاً وهو يرفض عملاً مشروعاً</b>.</para>
///
/// <para>ويُقاس هنا على PostgreSQL حصراً — فهو المزوّدُ الذي يملك القفل أصلاً.</para>
/// </summary>
public class IcvLockIsAliveTests
{
    /// <summary>
    /// <b>القياس المركزيّ.</b> إلغاءُ إشعارٍ دائن يُولّد مستندَ إلغاءٍ لدى الهيئة —
    /// و<b>توليدُه يجب أن يقع داخل معاملة</b>، وإلا كان القفلُ زينة.
    /// </summary>
    [PostgresFact]
    public async Task CancellingACreditNote_GeneratesInsideATransaction()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("icvlock");

        var (companyId, customerId) = await SeedWithZatcaAsync(scope.Db);

        var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة", 1m, 1_000m);
        var posted = await new SalesInvoicePostingService(scope.Db).PostAsync(invoice);

        var note = CreditNote.CreateDraft(companyId, customerId, posted.Id, new DateOnly(2026, 7, 20), "مرتجع");
        note.AddLine("خدمة", 1m, 1_000m);
        var postedNote = await new CreditNotePostingService(scope.Db).PostAsync(note);

        // ‏لا يرمي: التوليدُ داخل المعاملة فالقفلُ حيّ. وقبل الإصلاح يرمي حارسُ العدم.
        await new CreditNoteCancellationService(scope.Db)
            .CancelAsync(companyId, postedNote.Id, new DateOnly(2026, 7, 25), "مدير");

        var documents = await scope.Db.ZatcaEInvoiceDocuments
            .CountAsync(d => d.CompanyId == companyId);
        Assert.True(documents >= 3,
            $"مستنداتُ الهيئة {documents} — يُتوقّع فاتورةٌ وإشعارٌ ومستندُ إلغاء.");
    }

    /// <summary>
    /// <b>وعدّادُ ICV متتالٍ بلا ثغرة.</b> فالقفلُ الحيُّ يُسلسل، والميّتُ يُنتج تكراراً
    /// يلتقطه الفهرسُ الفريد رميةً — أي رفضَ عملٍ مشروع.
    /// </summary>
    [PostgresFact]
    public async Task TheIcvSequence_HasNoGapsAndNoDuplicates()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("icvseq");

        var (companyId, customerId) = await SeedWithZatcaAsync(scope.Db);

        for (var i = 0; i < 3; i++)
        {
            var invoice = SalesInvoice.CreateDraft(companyId, customerId, new DateOnly(2026, 7, 9));
            invoice.AddLine($"خدمة {i}", 1m, 100m);
            await new SalesInvoicePostingService(scope.Db).PostAsync(invoice);
        }

        var icvs = await scope.Db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == companyId)
            .Select(d => d.Icv)
            .ToListAsync();

        var numbers = icvs.OrderBy(n => n).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, numbers.Count).ToList(), numbers);
    }

    private static async Task<(long CompanyId, long CustomerId)> SeedWithZatcaAsync(BrqoshDbContext db)
    {
        var company = await new DatabaseInitializer(db)
            .InitializeAsync("منشأة القفل", "310123456700003", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(db).ApplyAsync();

        company.UpdateRegulatoryIdentity(
            "منشأة القفل للتجارة", "1010123456",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "1234", streetName: "طريق الملك فهد", districtName: "العليا",
                cityName: "الرياض", postalZone: "12345", countrySubentity: "منطقة الرياض",
                additionalNumber: "5678"));

        var customer = Customer.Create(company.Id, "عميل نقدي");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var workDirectory = Path.Combine(Path.GetTempPath(), $"brqosh-icv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);
        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(workDirectory);

        var settings = await db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(isEnabled: true, Brqosh.Domain.Zatca.ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
        await db.SaveChangesAsync();

        return (company.Id, customer.Id);
    }
}
