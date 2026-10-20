using System.Globalization;
using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Hospitality;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>sector-modules#6</c>) <b>عربونُ الحجز فاتورةُ دفعةٍ مقدّمة (386) يومَ قبضه — وتُسوّى في فاتورة المغادرة.</b>
///
/// <para><b>ما كان:</b> العربونُ نقطةُ استحقاق الضريبة في الأستاذ والإقرار منذ ح-٨١ — <b>بلا مستندٍ إلكترونيّ</b>: ضريبةٌ تُقرّ ولا
/// فاتورةَ تعرفها الهيئة، وفاتورةُ المغادرة تطالب النزيلَ بالإقامة كلِّها ولا تقول إنّ بعضَها دُفع.</para>
///
/// <para>والمرجعُ معيارُ XML (الإصدار 1.2، §9.5): فاتورةُ الدفعة 386، وفي النهائيّة سطرُ تسويةٍ مبالغُه أصفار يُشير إليها
/// (KSA-26/28/29/30) ويفصّل وعاءها وضريبتها (KSA-31/32)، و<c>PrepaidAmount</c> = مجموعُهما ينقص <c>PayableAmount</c>.</para>
///
/// <para>الأرقام: ليلتان × ٥٠٠ = ١٬٠٠٠ وضريبتُها ١٥٠؛ والعربونُ ٥٧٥ شاملاً = ٥٠٠ + ٧٥.</para>
/// </summary>
public class AReservationDepositIssuesAPrepaymentInvoiceTests : IDisposable
{
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly DateOnly Booked = new(2026, 2, 1);
    private static readonly DateOnly CheckIn = new(2026, 3, 1);
    private static readonly DateOnly CheckOut = new(2026, 3, 3);

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _guestId;
    private long _roomId;

    public AReservationDepositIssuesAPrepaymentInvoiceTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-prepayment", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_workDirectory, recursive: true); }
        catch (IOException) { /* ‏ملفٌّ مؤقّتٌ ممسوك — لا يعني فشلَ القياس. */ }
        catch (UnauthorizedAccessException) { /* ‏كسابقه. */ }
        GC.SuppressFinalize(this);
    }

    private async Task SetupAsync(bool zatcaEnabled)
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("فندق", "310123456700044", BusinessProfile.Hospitality);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        ZatcaReadySeller.Complete(company);

        var guest = Customer.Create(_companyId, "نزيل", "");
        _db.Customers.Add(guest);
        var room = Room.Create(_companyId, "201", RoomKind.Double, 500m, floor: 2);
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        _guestId = guest.Id;
        _roomId = room.Id;

        if (zatcaEnabled)
        {
            var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
            var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
            settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
            await _db.SaveChangesAsync();
        }
    }

    private async Task<(Reservation Reservation, JournalEntry Deposit)> BookWithDepositAsync()
    {
        var reservation = Reservation.Create(_companyId, _roomId, _guestId, "R-P1", CheckIn, CheckOut, 500m);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        var deposit = await new ReservationDepositService(_db).TakeAsync(_companyId, reservation.Id, 575m, Booked, "admin");
        return (reservation, deposit);
    }

    private static XElement Xml(ZatcaEInvoiceDocument document)
    {
        var path = !string.IsNullOrEmpty(document.SignedXmlFilePath) ? document.SignedXmlFilePath! : document.XmlFilePath;
        return XDocument.Load(path).Root!;
    }

    private static decimal Amount(XElement parent, string name) =>
        decimal.Parse(parent.Element(Cbc + name)!.Value, CultureInfo.InvariantCulture);

    [Fact]
    public async Task TakingADeposit_IssuesA386_WithItsNetAndVat()
    {
        await SetupAsync(zatcaEnabled: true);
        var (_, deposit) = await BookWithDepositAsync();

        var document = await _db.ZatcaEInvoiceDocuments.SingleAsync(d =>
            d.DocumentType == ZatcaDocumentType.Prepayment && d.SourceDocumentId == deposit.Id);
        var xml = Xml(document);
        var totals = xml.Element(Cac + "LegalMonetaryTotal")!;

        Assert.Equal($"DEP-{deposit.EntryNumber}", document.InvoiceNumber);
        Assert.Equal("386", xml.Element(Cbc + "InvoiceTypeCode")!.Value);
        Assert.Equal((500m, 575m, 575m), (Amount(totals, "TaxExclusiveAmount"), Amount(totals, "TaxInclusiveAmount"), Amount(totals, "PayableAmount")));
        Assert.Null(totals.Element(Cbc + "PrepaidAmount"));
    }

    [Fact]
    public async Task TheCheckoutInvoice_SettlesThePrepayment_ByReferenceAndAmount()
    {
        await SetupAsync(zatcaEnabled: true);
        var (reservation, deposit) = await BookWithDepositAsync();
        var prepayment = await _db.ZatcaEInvoiceDocuments.SingleAsync(d => d.DocumentType == ZatcaDocumentType.Prepayment);

        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        var invoiceId = await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, CheckOut);

        var invoice = await _db.ZatcaEInvoiceDocuments.SingleAsync(d =>
            d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == invoiceId);
        var xml = Xml(invoice);
        var totals = xml.Element(Cac + "LegalMonetaryTotal")!;

        Assert.Equal((1_150m, 575m, 575m),
            (Amount(totals, "TaxInclusiveAmount"), Amount(totals, "PrepaidAmount"), Amount(totals, "PayableAmount")));

        var adjustment = xml.Elements(Cac + "InvoiceLine").Single(l => l.Element(Cac + "DocumentReference") is not null);
        var reference = adjustment.Element(Cac + "DocumentReference")!;
        Assert.Equal(prepayment.InvoiceNumber, reference.Element(Cbc + "ID")!.Value);
        Assert.Equal("2026-02-01", reference.Element(Cbc + "IssueDate")!.Value);
        Assert.Equal("386", reference.Element(Cbc + "DocumentTypeCode")!.Value);
        Assert.Equal(0m, Amount(adjustment, "LineExtensionAmount"));

        var subtotal = adjustment.Element(Cac + "TaxTotal")!.Element(Cac + "TaxSubtotal")!;
        Assert.Equal((500m, 75m), (Amount(subtotal, "TaxableAmount"), Amount(subtotal, "TaxAmount")));
        Assert.Equal(reservation.Id, (await _db.SalesInvoices.SingleAsync(i => i.Id == invoiceId)).SettlesReservationId);
    }

    [Fact]
    public async Task RefundingOnCancellation_IssuesACreditNote_ReferencingThe386()
    {
        await SetupAsync(zatcaEnabled: true);
        var (reservation, deposit) = await BookWithDepositAsync();
        var prepayment = await _db.ZatcaEInvoiceDocuments.SingleAsync(d => d.DocumentType == ZatcaDocumentType.Prepayment);

        await new FolioService(_db).CancelAsync(_companyId, reservation.Id, new DateOnly(2026, 2, 10), DepositOutcome.Refund);

        var refund = await _db.ZatcaEInvoiceDocuments.SingleAsync(d =>
            d.DocumentType == ZatcaDocumentType.DepositRefund && d.SourceDocumentId == deposit.Id);
        var xml = Xml(refund);
        Assert.Equal("381", xml.Element(Cbc + "InvoiceTypeCode")!.Value);
        Assert.Equal(prepayment.InvoiceNumber,
            xml.Element(Cac + "BillingReference")!.Element(Cac + "InvoiceDocumentReference")!.Element(Cbc + "ID")!.Value);
        Assert.Equal(575m, Amount(xml.Element(Cac + "LegalMonetaryTotal")!, "TaxInclusiveAmount"));
    }

    [Fact]
    public async Task ForfeitingOnCancellation_IssuesNoCreditNote()
    {
        await SetupAsync(zatcaEnabled: true);
        var (reservation, _) = await BookWithDepositAsync();

        await new FolioService(_db).CancelAsync(_companyId, reservation.Id, new DateOnly(2026, 2, 10), DepositOutcome.Forfeit);

        Assert.False(await _db.ZatcaEInvoiceDocuments.AnyAsync(d => d.DocumentType == ZatcaDocumentType.DepositRefund));
    }

    /// <summary>
    /// <b>والعربونُ في الخانة 1 من الإقرار</b>: فبراير يقرّ وعاءه ٥٠٠ وضريبتَه ٧٥ «مبلغاً»، ومارسُ يقرّ الإقامةَ كلَّها (١٬٠٠٠ و١٥٠)
    /// ويطرح العربونَ «تعديلاً» (−٥٠٠ و−٧٥) — فلا يُقرّ وعاءٌ ولا ضريبةٌ مرّتين؛ وصفُّ «بلا خانة» لا يُعيده.
    /// </summary>
    [Fact]
    public async Task TheDepositBase_IsInBox1_InItsMonth_AndAdjustedOutAtCheckout()
    {
        await SetupAsync(zatcaEnabled: false);
        var (reservation, _) = await BookWithDepositAsync();
        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, CheckOut);

        var february = await new Brqosh.Infrastructure.Reporting.VatReturnQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));
        var march = await new Brqosh.Infrastructure.Reporting.VatReturnQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var feb1 = february.SalesBoxes!.Single(b => b.Box == 1);
        var mar1 = march.SalesBoxes!.Single(b => b.Box == 1);
        Assert.Equal((500m, 0m, 75m, 0m), (feb1.Amount, feb1.Adjustments, feb1.VatAmount, feb1.VatAdjustments));
        Assert.Equal((1_000m, -500m, 150m, -75m), (mar1.Amount, mar1.Adjustments, mar1.VatAmount, mar1.VatAdjustments));

        Assert.Equal(february.NetOutputVat, february.SalesBoxes!.Sum(b => b.VatAmount + b.VatAdjustments)
                                            + february.JournalOutputVat - february.JournalOutputVatInBoxes);
        Assert.Equal(75m, february.JournalOutputVatInBoxes);
        Assert.Equal(-75m, march.JournalOutputVatInBoxes);
    }

    [Fact]
    public async Task WithoutEInvoicing_ADepositIssuesNoDocument()
    {
        await SetupAsync(zatcaEnabled: false);

        await BookWithDepositAsync();

        Assert.False(await _db.ZatcaEInvoiceDocuments.AnyAsync(d => d.DocumentType == ZatcaDocumentType.Prepayment));
    }
}
