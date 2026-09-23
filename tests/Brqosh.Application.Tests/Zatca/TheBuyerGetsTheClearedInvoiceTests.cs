using System.Text.RegularExpressions;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥٣ · <c>tax-zatca#9</c>) <b>المشتري يأخذ ما اعتمدته الهيئة — لا نسختَنا.</b>
///
/// <para>‏<b>ما كان:</b> في التخليص (B2B) تُعيد الهيئةُ الفاتورةَ مختومةً، <b>ويقول الكيانُ إنّها
/// «الوثيقةُ النظاميّةُ التي تُسلَّم للمشتري»</b> — وCEEF-237 حفظها على القرص. <b>ثمّ لا يقرؤها
/// شيء</b>: المطبوعُ يحمل رمزَ QR الذي ولّدناه نحن، و«عرض XML» يفتح نسختَنا الموقَّعة.
/// <b>وفاتورةٌ ضريبيّةٌ لم تُعتمد بعدُ — أو رفضتها الهيئة — تُطبع كالمعتمدة</b>، برمزها وعنوانها،
/// فتُسلَّم للمشتري ورقةٌ لا تُعدّ فاتورةً في نظام التخليص.</para>
/// </summary>
public class TheBuyerGetsTheClearedInvoiceTests : IDisposable
{
    private const string CustomerVat = "311111111100003";

    /// <summary>‏رمزٌ «أعادته الهيئة» — بائعٌ ومبلغٌ غيرُ ما نولّده، فلا يساويه رمزُنا صدفةً.</summary>
    private static readonly string AuthorityQr = ZatcaQrGenerator.GenerateTlvBase64Phase1(
        "هيئة الزكاة والضريبة والجمارك", "300000000000003",
        new DateTime(2026, 7, 9, 9, 0, 0, DateTimeKind.Utc), 1_150m, 150m);

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;

    public TheBuyerGetsTheClearedInvoiceTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h53", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);

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
        if (Directory.Exists(_workDirectory))
            Directory.Delete(_workDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>‏<b>القياسُ الحاكم:</b> الفاتورةُ المعتمدةُ تُطبع برمز الهيئة.</summary>
    [Fact]
    public async Task AClearedInvoice_PrintsTheQrTheAuthorityReturned()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(_customerId);
        var doc = await ClearanceDocumentAsync(ZatcaDocumentType.SalesInvoice, invoice.Id);
        Assert.NotEqual(AuthorityQr, doc.QrTlvBase64);
        await ClearAsync(doc);

        var options = (await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id)).Options;

        Assert.Equal(AuthorityQr, options.QrTlvBase64);
        Assert.Null(options.ClearanceNotice);
    }

    /// <summary>
    /// ‏<b>وفاتورةٌ تنتظر الاعتماد لا تُطبع كالمعتمدة</b>: بلا رمز، وبتنبيهٍ يقول إنّها ليست
    /// فاتورةً ضريبيّةً نافذةً بعد. <b>والطباعةُ باقية</b> — للنسخة الداخليّة ولإعادة الإرسال.
    ///
    /// <para>‏<b>وحالتُها حالةُ الإنتاج حين يتعذّر الإرسال</b>: الإعدادُ ناقصٌ فيرمي حارسُ الجاهزيّة
    /// قبل أيّ نداءٍ شبكيّ، ويُسجَّل السببُ على المستند، ويبقى «جاهزاً للاعتماد».</para>
    /// </summary>
    [Fact]
    public async Task AnInvoiceAwaitingClearance_PrintsNoQr_AndSaysItIsNotYetATaxInvoice()
    {
        await SetupAsync(ZatcaEnvironment.Production);
        var invoice = await PostInvoiceAsync(_customerId);
        var doc = await ClearanceDocumentAsync(ZatcaDocumentType.SalesInvoice, invoice.Id);
        Assert.Equal(ZatcaComplianceStatus.ReadyToClear, doc.ComplianceStatus);
        // ‏السببُ مُسلسَلٌ بـ`JsonSerializer` — والعربيّةُ فيه مُهرَّبة، فيُقرأ لا يُبحث فيه نصّاً.
        var reason = System.Text.Json.JsonDocument.Parse(doc.ZatcaResponseJson!)
            .RootElement.GetProperty("reason").GetString();
        Assert.Contains("غير جاهزة للإرسال", reason!, StringComparison.Ordinal);

        var options = (await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id)).Options;

        Assert.Null(options.QrTlvBase64);
        Assert.Contains("لم تعتمد", options.ClearanceNotice!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedInvoice_PrintsNoQr_AndSaysItWasRejected()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(_customerId);
        var doc = await ClearanceDocumentAsync(ZatcaDocumentType.SalesInvoice, invoice.Id);
        doc.MarkRejected("{\"clearanceStatus\":\"NOT_CLEARED\"}");
        await _db.SaveChangesAsync();

        var options = (await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id)).Options;

        Assert.Null(options.QrTlvBase64);
        Assert.Contains("رفضت", options.ClearanceNotice!, StringComparison.Ordinal);
    }

    /// <summary>
    /// ‏<b>والنسخةُ المعتمدةُ إن ضاعت من القرص لا يحلّ رمزُنا محلَّها صامتاً</b> — يُقال ذلك على
    /// الورقة ويُسمّى المسار.
    /// </summary>
    [Fact]
    public async Task AClearedInvoiceWhoseClearedCopyIsGone_PrintsNoQr_AndSaysWhere()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(_customerId);
        var doc = await ClearanceDocumentAsync(ZatcaDocumentType.SalesInvoice, invoice.Id);
        var clearedPath = await ClearAsync(doc);
        File.Delete(clearedPath);

        var options = (await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id)).Options;

        Assert.Null(options.QrTlvBase64);
        Assert.Contains(clearedPath, options.ClearanceNotice!, StringComparison.Ordinal);
    }

    /// <summary>‏<b>والإشعارُ الدائنُ لفاتورةٍ ضريبيّةٍ يُخلَّص كذلك</b> — فيُطبع برمز الهيئة.</summary>
    [Fact]
    public async Task AClearedCreditNote_PrintsTheAuthorityQr()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(_customerId);
        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 10), "تسوية سعر");
        note.AddLine("خدمة", 1m, 400m);
        await new CreditNotePostingService(_db).PostAsync(note);
        var doc = await ClearanceDocumentAsync(ZatcaDocumentType.CreditNote, note.Id);
        await ClearAsync(doc);

        var options = (await Print().CreditNoteRequestAsync(_companyId, note.Id)).Options;

        Assert.Equal(AuthorityQr, options.QrTlvBase64);
    }

    /// <summary>‏<b>ومستندُ الإلغاء كذلك</b> — رمزُه رمزُه هو (ح-١٩)، <b>والمعتمدُ منه رمزُ الهيئة</b>.</summary>
    [Fact]
    public async Task AClearedCancellation_PrintsTheAuthorityQr()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(_customerId);
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await new ZatcaEInvoiceGenerator(_db).TryGenerateCancellationForSalesInvoiceAsync(invoice.Id);
            await tx.CommitAsync();
        }
        var doc = await ClearanceDocumentAsync(ZatcaDocumentType.InvoiceCancel, invoice.Id);
        await ClearAsync(doc);

        var (request, _) = await Print().ZatcaCancellationRequestAsync(_companyId, doc.Id);

        Assert.Equal(AuthorityQr, request.Options.QrTlvBase64);
    }

    /// <summary>
    /// ‏<b>شاهدُ ضبط — المحاكاةُ لا تتغيّر</b>: لم يصل الهيئةَ شيءٌ فلا نسخةَ معتمدة، والسطرُ
    /// المطبوعُ يقول «محاكاة» من قبل (CEEF-229). <b>فالإصلاحُ لا يمسّ إلّا التخليصَ الحقيقيّ.</b>
    /// </summary>
    [Fact]
    public async Task ASimulatedInvoice_KeepsItsOwnQr_WithNoNotice()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(_customerId);
        var doc = await ClearanceDocumentAsync(ZatcaDocumentType.SalesInvoice, invoice.Id);
        doc.MarkSubmitted(ZatcaComplianceStatus.SimulatedLocally, "{\"simulation\":true}");
        await _db.SaveChangesAsync();

        var options = (await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id)).Options;

        Assert.Equal(doc.QrTlvBase64, options.QrTlvBase64);
        Assert.Null(options.ClearanceNotice);
    }

    /// <summary>
    /// ‏<b>شاهدُ ضبط — المبسّطةُ رمزُها رمزُنا</b>: تُبلَّغ ولا تُخلَّص، ولا تُعيد الهيئةُ فيها
    /// مستنداً. <b>وتُطبع قبل الإبلاغ</b> — فهو يقع خلال أربعٍ وعشرين ساعةً بعد تسليمها.
    /// </summary>
    [Fact]
    public async Task ASimplifiedInvoice_KeepsItsOwnQr_BeforeReporting()
    {
        await SetupAsync();
        var walkIn = Customer.Create(_companyId, "عميل نقدي");
        _db.Customers.Add(walkIn);
        await _db.SaveChangesAsync();
        var invoice = await PostInvoiceAsync(walkIn.Id);
        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == ZatcaDocumentType.SalesInvoice && d.SourceDocumentId == invoice.Id);
        Assert.Equal(ZatcaTransmissionMode.Reporting, doc.TransmissionMode);

        var options = (await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id)).Options;

        Assert.Equal(doc.QrTlvBase64, options.QrTlvBase64);
        Assert.Null(options.ClearanceNotice);
    }

    /// <summary>
    /// ‏<b>و«عرضُ XML» — وهو بابُ تسليم الملفّ (زرُّ النسخ) — يفتح المعتمدَ متى اعتُمد</b>، وكان
    /// يفتح نسختَنا الموقَّعةَ أبداً. <b>والمعتمدُ إن ضاع لا تحلّ نسختُنا محلَّه.</b>
    /// </summary>
    [Fact]
    public async Task TheXmlHandedOver_IsTheClearedCopy_OnceCleared()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync(_customerId);
        var doc = await ClearanceDocumentAsync(ZatcaDocumentType.SalesInvoice, invoice.Id);

        Assert.Equal(doc.SignedXmlFilePath ?? doc.XmlFilePath, ZatcaHandOverCopy.XmlPathFor(doc));

        var clearedPath = await ClearAsync(doc);
        Assert.Equal(clearedPath, ZatcaHandOverCopy.XmlPathFor(doc));

        File.Delete(clearedPath);
        Assert.Null(ZatcaHandOverCopy.XmlPathFor(doc));
    }

    /// <summary>‏<b>والتنبيهُ يُرسَم</b> — صفحةٌ بتنبيهٍ وبلا رمزٍ تخرج كاملةً لا ترمي.</summary>
    [Fact]
    public async Task APageWithANotice_Renders()
    {
        await SetupAsync(ZatcaEnvironment.Production);
        var invoice = await PostInvoiceAsync(_customerId);

        var request = await Print().SalesInvoiceRequestAsync(_companyId, invoice.Id);
        Assert.NotNull(request.Options.ClearanceNotice);

        var pdf = SalesInvoicePdfGenerator.Generate(request);
        Assert.True(pdf.Length > 1_000, $"PDF بطول {pdf.Length}");
    }

    /// <summary>
    /// ‏<b>والصنفُ لا الحالة:</b> لا يقرأ مستدعٍ في <c>src</c> رمزَ المستند مباشرةً إلّا
    /// <c>ZatcaHandOverCopy</c> — فطباعةٌ سادسةٌ تُكتب غداً لا تعود إلى رمزنا المحلّيّ صامتةً.
    /// <b>والمواضعُ الخمسةُ كانت: الفاتورة · الإشعار · بيعُ الأصل · الإلغاء · المستخلص</b>،
    /// وإيصالُ نقطة البيع سادسُها.
    /// </summary>
    [Fact]
    public void NoCallerReadsTheLocalQrAroundTheHandOverCopy()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var offenders = new List<string>();
        var seen = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file).Replace('\\', '/');
            if (rel.Contains("/obj/") || rel.Contains("/bin/") || rel.Contains("/Migrations/"))
                continue;
            seen.Add(rel);
            var count = DocumentQrReads(File.ReadAllText(file));
            var allowed = ReadersByDesign.TryGetValue(rel, out var declared) ? declared.Count : 0;
            if (count != allowed)
                offenders.Add($"{rel}: {count} (المُقَرّ {allowed})");
        }
        // ‏**وإقرارٌ بلا ملفّ إقرارٌ بائت** — يُسقط لا يُتجاوز.
        offenders.AddRange(ReadersByDesign.Keys.Where(k => !seen.Contains(k)).Select(k => $"{k}: مُقَرٌّ وغيرُ موجود"));

        Assert.True(seen.Count > 500, $"زار {seen.Count} ملفّاً فقط — الماسحُ أعمى؟");
        Assert.True(offenders.Count == 0,
            "قراءةٌ مباشرةٌ لرمز QR المحلّيّ خارج ZatcaHandOverCopy:\n" + string.Join("\n", offenders));
    }

    /// <summary>‏<b>والكاشفُ يرى ما يُفترض أن يرى</b> — ولا يرى نماذجَ الطباعة.</summary>
    [Theory]
    [InlineData("QrTlvBase64: zatca?.QrTlvBase64 ?? x", 1)]
    [InlineData("QrTlvBase64: doc.QrTlvBase64,", 1)]
    [InlineData("BuildQrImage(zatca.QrTlvBase64);", 1)]
    [InlineData("var qrPng = Helper.TryCreatePng(options.QrTlvBase64);", 0)]
    [InlineData("TryCreatePng(data.QrTlvBase64)", 0)]
    [InlineData("QrTlvBase64: mark.QrTlvBase64,", 0)]
    public void TheDetector_SeesDocumentReads_AndNotPrintModels(string source, int expected) =>
        Assert.Equal(expected, DocumentQrReads(source));

    // ── القراءاتُ المُقَرّة ─────────────────────────────────────────────────

    private static readonly Dictionary<string, (int Count, string Reason)> ReadersByDesign = new()
    {
        ["Brqosh.Infrastructure/Zatca/ZatcaHandOverCopy.cs"] =
            (1, "القارئُ الواحد: يختار بين رمزنا ورمز الهيئة ولا رمز"),
        ["Brqosh.Infrastructure/Persistence/BrqoshDbContext.cs"] =
            (1, "تعيينُ العمود — لا قراءة"),
        ["Brqosh.Desktop/Views/Pages/ZatcaInvoicesPage.xaml.cs"] =
            (3, "عمودُ «QR» في جدول المستندات يعرض أوّلَ ما ولّدناه — لا يُطبع ولا يُسلَّم"),
    };

    /// <summary>
    /// ‏قراءةُ <c>.QrTlvBase64</c> من أيّ مُستقبِلٍ <b>عدا نماذجِ الطباعة والبناء</b>
    /// (<c>options</c> · <c>data</c> · <c>mark</c>) — فهي تحمل ما اختير لا ما وُلِّد.
    /// </summary>
    private static int DocumentQrReads(string source) =>
        Regex.Matches(source, @"\b(\w+)\??\.QrTlvBase64\b")
            .Count(m => m.Groups[1].Value is not ("options" or "data" or "mark" or "Options"));

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private DocumentPrintService Print() =>
        new(_db, new AuditService(_db), new ExchangeRateService(_db));

    private async Task<SalesInvoice> PostInvoiceAsync(long customerId)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة", 1m, 1_000m);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<ZatcaEInvoiceDocument> ClearanceDocumentAsync(ZatcaDocumentType type, long sourceId)
    {
        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync(
            d => d.DocumentType == type && d.SourceDocumentId == sourceId);
        Assert.Equal(ZatcaTransmissionMode.Clearance, doc.TransmissionMode);
        return doc;
    }

    /// <summary>
    /// ‏يُعلن المستندَ معتمداً <b>بنسخةٍ مختومةٍ كما تُعيدها الهيئة</b>: مستندُنا نفسُه برمزٍ
    /// غيرِ رمزنا — وهو ما يُكتب على القرص في <c>SubmitCoreAsync</c> بعد التخليص.
    /// </summary>
    private async Task<string> ClearAsync(ZatcaEInvoiceDocument doc)
    {
        var ours = await File.ReadAllBytesAsync(doc.SignedXmlFilePath ?? doc.XmlFilePath);
        var clearedPath = Path.Combine(_workDirectory, $"zatca-{doc.Id}.cleared.xml");
        await File.WriteAllBytesAsync(clearedPath, ZatcaXmlSigner.ReplaceQrInSignedXml(ours, AuthorityQr));
        doc.MarkSubmitted(ZatcaComplianceStatus.Cleared, "{\"clearanceStatus\":\"CLEARED\"}", clearedPath);
        await _db.SaveChangesAsync();
        return clearedPath;
    }

    /// <param name="environment">
    /// ‏المحاكاةُ افتراضاً؛ <b>والإنتاجُ بلا عنوانٍ ولا رمز</b> — فيرمي حارسُ الجاهزيّة قبل الشبكة.
    /// </param>
    private async Task SetupAsync(ZatcaEnvironment environment = ZatcaEnvironment.Simulation)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة التخليص", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        ZatcaReadySeller.Complete(company);

        var customer = Customer.Create(_companyId, "عميل", CustomerVat, address: "الرياض");
        customer.UpdateRegulatoryIdentity(
            "مؤسسة العميل التجارية",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
                additionalNumber: "8765"));
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, environment, "", certPath, keyPath, "");
        await _db.SaveChangesAsync();
    }
}
