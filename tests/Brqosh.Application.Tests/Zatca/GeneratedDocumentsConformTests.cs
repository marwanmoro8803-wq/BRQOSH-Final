using System.Globalization;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-229 · م2-١٣) <b>ما يصل الهيئةَ فعلاً — لا ما يبنيه الباني من مُدخلٍ مُعَدّ.</b>
///
/// <para><b>الفجوة:</b> أداتا القياس موجودتان — <see cref="UblStructureValidator"/> (ح-٣)
/// و<see cref="ZatcaSignatureVerifier"/> (ح-٤) — لكنّهما تُطبَّقان على مُخرَج
/// <c>ZatcaUblInvoiceBuilder</c> من <c>InvoiceData</c> <b>مصنوعةٍ في الاختبار</b>. أي أنّ
/// الطرفَ المقيس هو الباني، لا <see cref="ZatcaEInvoiceGenerator"/> الذي يُترجم مستنداً
/// <b>مرحّلاً حقيقياً</b> إلى تلك البنية.</para>
///
/// <para>وبين الطرفين تقع الترجمة: أيّ صنفٍ ضريبيّ، وأيّ نوعِ فاتورة، وأيّ مرجعِ فاتورةٍ
/// أصلية، وأيّ عدّاد تسلسليّ. <b>وخطأٌ هناك يُنتج مستنداً سليمَ الشكل خاطئَ المضمون</b> —
/// ويمرّ على كلّ اختبارٍ قائم، لأنّ لا اختبار يُشغّل المولّد ثمّ يقيس مُخرَجه.</para>
///
/// <para>وهذا هو نمطُ «الوصلة» نفسه الذي أُغلق في م2-٢٥/٢٦ بين الشاشة والخدمة: الطرفان
/// مقيسان والوسطُ مُستنتَج. فهنا تُشغَّل <b>كلُّ</b> مسارات التوليد التي يبلغها متجرُ
/// التجزئة، ويُقاس <b>ما استقرّ على القرص</b>.</para>
/// </summary>
public class GeneratedDocumentsConformTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;

    public GeneratedDocumentsConformTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);

        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-zatca-conform", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_workDirectory, recursive: true); } catch { /* مجلّدٌ مؤقّت */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// مساراتُ توليدٍ لا يبلغها متجرُ التجزئة — <b>ولكلٍّ سببٌ مكتوب</b>.
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredOutOfReach = new()
    {
        ["TryGenerateForProgressBillingAsync"] =
            "مستخلصُ مقاولات — ModuleCatalog لا يمنح ملفَّ التجزئة وحدةَ المقاولات، فلا يُنشأ عقدٌ ولا مستخلص.",
        ["TryGenerateCancellationForProgressBillingAsync"] =
            "إلغاءُ مستخلص — تابعٌ للسابق.",
    };

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المطابقة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        // ‏هويةُ بائعٍ كاملة — تملؤها المنشأةُ في شاشة الإعدادات قبل تفعيل الفوترة.
        // ‏وأوّلُ تشغيلٍ لهذا الاختبار تركها فارغةً فخرجت المستنداتُ ناقصةَ الطرف البائع،
        // ‏وهو نقصُ التهيئة لا عطلُ المولّد. والأثرُ الحقيقيّ سُجّل بنداً مفتوحاً:
        // ‏لا شيء في النطاق يمنع تفعيل الفوترة بمنشأةٍ ناقصة — الحارسُ في الشاشة وحدها.
        company.UpdateRegulatoryIdentity(
            "منشأة المطابقة للتجارة", "1010123456",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "1234", streetName: "طريق الملك فهد", districtName: "العليا",
                cityName: "الرياض", postalZone: "12345", countrySubentity: "منطقة الرياض",
                additionalNumber: "5678"));

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        // ‏(م1-١ب) مشترٍ مكتمل — الفاتورة القياسية تذهب إلى المقاصّة وطرفُ مشتريها معرَّف.
        customer.UpdateRegulatoryIdentity(
            "مؤسسة العميل التجارية",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
                additionalNumber: "8765"));
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        // ‏شهادةٌ حقيقية تُولَّد في زمن التشغيل — فالتوقيع يجري فعلاً ويُتحقَّق منه (ح-٤).
        var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
        await _db.SaveChangesAsync();
    }

    private async Task<SalesInvoice> PostInvoiceAsync(decimal amount = 1_000m)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة استشارية", 1m, amount);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>
    /// المستندُ الذي أنتجه <b>الترحيل نفسه</b> — لا استدعاءٌ ثانٍ للمولّد.
    ///
    /// <para>وهذا ما كشفه أوّلُ تشغيل: <c>SalesInvoicePostingService.PostAsync</c> يستدعي
    /// المولّد داخل معاملته (<c>ZatcaPostingOrchestrator</c>)، فاستدعاؤه ثانيةً يرمي
    /// «مستند ZATCA موجود مسبقاً». والقياسُ الصحيح أن يُقرأ ما أنتجه المسارُ الحقيقيّ.</para>
    /// </summary>
    private async Task<ZatcaEInvoiceDocument> DocumentFromPostingAsync(
        long sourceDocumentId, params ZatcaDocumentType[] types)
    {
        var document = await _db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == _companyId
                        && d.SourceDocumentId == sourceDocumentId
                        && types.Contains(d.DocumentType))
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();

        Assert.True(document is not null,
            $"الترحيل لم يُنتج مستند ZATCA للمصدر {sourceDocumentId} — لا شيء يصل الهيئة.");
        return document!;
    }

    /// <summary>يقرأ ما استقرّ على القرص فعلاً — لا ما بناه الاختبار في الذاكرة.</summary>
    private static byte[] ReadGenerated(ZatcaEInvoiceDocument document)
    {
        // ‏**الموقَّعُ هو ما يصل الهيئة**، والخام يبقى للأرشيف. وأوّلُ تشغيلٍ لهذا الاختبار
        // ‏قرأ `XmlFilePath` (الخام) فأبلغ «لا كتلة توقيع» — وكان الخطأ في القراءة لا في
        // ‏التوقيع. فيُفضَّل الموقَّع، ويُقرأ الخامُ فقط حين لا توقيعَ أصلاً.
        var path = string.IsNullOrWhiteSpace(document.SignedXmlFilePath)
            ? document.XmlFilePath
            : document.SignedXmlFilePath!;

        Assert.False(string.IsNullOrWhiteSpace(path), "المستند المولَّد بلا مسار ملفّ — لا شيء وصل القرص.");
        Assert.True(File.Exists(path), $"ملفّ المستند غير موجود: {path}");
        return File.ReadAllBytes(path);
    }

    private static void AssertConforms(string path, byte[] xml)
    {
        var violations = UblStructureValidator.Validate(xml);
        Assert.True(violations.Count == 0,
            $"مُخرَجُ «{path}» يخالف البنية — وهذا ما يصل الهيئة:\n  " + string.Join("\n  ", violations));
    }

    /// <summary>
    /// <b>القياس المركزيّ.</b> فاتورةُ مبيعاتٍ مرحّلة فعلاً ⟵ المولّد ⟵ الملفّ على القرص
    /// ⟵ ح-٣. وهذا هو المسارُ الذي يسلكه كلُّ بيعٍ في المتجر.
    /// </summary>
    [Fact]
    public async Task AGeneratedSalesInvoice_Conforms()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var document = await DocumentFromPostingAsync(invoice.Id, ZatcaDocumentType.SalesInvoice);

        AssertConforms(nameof(ZatcaEInvoiceGenerator.TryGenerateForSalesInvoiceAsync), ReadGenerated(document));
    }

    /// <summary>والإشعارُ الدائن — وهو النوع الذي ينكسر عليه ترتيب المتتالية عادةً.</summary>
    [Fact]
    public async Task AGeneratedCreditNote_Conforms()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var note = CreditNote.CreateDraft(
            _companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 10), "إرجاع جزئيّ");
        note.AddLine("خدمة استشارية", 1m, 400m);
        await new CreditNotePostingService(_db).PostAsync(note);

        var document = await DocumentFromPostingAsync(note.Id, ZatcaDocumentType.CreditNote);

        AssertConforms(nameof(ZatcaEInvoiceGenerator.TryGenerateForCreditNoteAsync), ReadGenerated(document));
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦) <b>وبيعُ أصلٍ ثابت — توريدٌ خاضعٌ نظاماً لم يكن له مستند.</b>
    ///
    /// <para>كان الاستبعادُ يُقيّد ضريبةَ المخرجات على <c>2106</c> <b>بلا فاتورةٍ ولا مشترٍ
    /// ولا مستندٍ إلكترونيّ</b>. <b>والمتجرُ يبلغه</b>: كلُّ منشأةٍ تبيع سيّارتَها أو
    /// جهازَها القديم — وهو أشيعُ ما يُضبط في فحص المنشآت الصغيرة.</para>
    ///
    /// <para><b>والقياسُ هنا ليس «صفٌّ وُجد»</b> بل <b>ما استقرّ على القرص</b>: بنيةُ UBL
    /// كما تصل الهيئة. <b>وبلا هذا كان يكفي أن يُنشأ صفٌّ لِيُقال «أُغلق».</b></para>
    /// </summary>
    [Fact]
    public async Task AGeneratedAssetDisposalInvoice_Conforms()
    {
        await SetupAsync();

        var assetAccount = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1203");
        var accumAccount = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1207");

        var asset = FixedAsset.Create(
            _companyId, "FA-Z1", "سيارة نقل", new DateOnly(2024, 1, 1),
            acquisitionCost: 120_000m, salvageValue: 0m, usefulLifeMonths: 120,
            glAssetAccountId: assetAccount.Id, glDepreciationAccountId: accumAccount.Id);
        for (var i = 0; i < 24; i++) asset.ApplyDepreciation();
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();

        await new FixedAssetDisposalPostingService(_db).PostDisposalAsync(
            asset, saleProceeds: 80_000m, new DateOnly(2026, 7, 11), "admin",
            buyerCustomerId: _customerId);

        var document = await DocumentFromPostingAsync(asset.Id, ZatcaDocumentType.AssetDisposal);

        AssertConforms(nameof(ZatcaEInvoiceGenerator.TryGenerateForAssetDisposalAsync), ReadGenerated(document));
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦١) <b>وإلغاءُ بيع الأصل</b> — إشعارٌ دائنٌ يصل الهيئة؛ أسقط هذا الحارسُ مسارَه يومَ أُضيف حتّى قِيس.
    /// </summary>
    [Fact]
    public async Task AGeneratedAssetDisposalCancellation_Conforms()
    {
        await SetupAsync();

        var assetAccount = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1203");
        var accumAccount = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1207");
        var asset = FixedAsset.Create(
            _companyId, "FA-Z2", "سيارة نقل", new DateOnly(2024, 1, 1),
            acquisitionCost: 120_000m, salvageValue: 0m, usefulLifeMonths: 120,
            glAssetAccountId: assetAccount.Id, glDepreciationAccountId: accumAccount.Id);
        for (var i = 0; i < 24; i++) asset.ApplyDepreciation();
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        await new FixedAssetDisposalPostingService(_db).PostDisposalAsync(
            asset, saleProceeds: 80_000m, new DateOnly(2026, 7, 11), "admin",
            buyerCustomerId: _customerId);

        await new FixedAssetDisposalReversalService(_db).ReverseDisposalAsync(
            _companyId, asset.Id, new DateOnly(2026, 7, 14), "admin");

        var document = await _db.ZatcaEInvoiceDocuments.SingleAsync(d =>
            d.CompanyId == _companyId && d.DocumentType == ZatcaDocumentType.DisposalCancel && d.SourceDocumentId == asset.Id);
        AssertConforms(nameof(ZatcaEInvoiceGenerator.TryGenerateCancellationForAssetDisposalAsync), ReadGenerated(document));
    }

    /// <summary>
    /// وإلغاءُ فاتورةٍ صدرت — مسارٌ يبلغه المتجر (فاتورةٌ أُصدرت خطأً)، ولم يكن مقيساً.
    /// </summary>
    [Fact]
    public async Task AGeneratedSalesInvoiceCancellation_Conforms()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        // ‏(م-٥ · ن-٢٥) **داخل معاملة — كما يفعل المسارُ الإنتاجيّ.**
        // ‏كان هذا الاستدعاءُ خارجَها، وكان يمرّ لأنّ حارسَ «خارج معاملة» كان متخطّىً على
        // ‏SQLite. **والمسارُ الحقيقيُّ يفتح معاملتَه** (`SalesInvoiceCancellationService`
        // ‏و`CreditNoteCancellationService`)، فالاستدعاءُ العاري كان يقيس شكلاً لا يسلكه أحد.
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var document = await new ZatcaEInvoiceGenerator(_db)
            .TryGenerateCancellationForSalesInvoiceAsync(invoice.Id);

        Assert.NotNull(document);
        AssertConforms(
            nameof(ZatcaEInvoiceGenerator.TryGenerateCancellationForSalesInvoiceAsync),
            ReadGenerated(document!));
    }

    /// <summary>
    /// وإلغاءُ إشعارٍ دائن — إشعارٌ مدين (UBL 383).
    ///
    /// <para><b>وهذا المسارُ كان مُدرَجاً في قائمة «المقيسة» ولا اختبارَ يستدعيه</b> — أي أنّ
    /// حارسَ الاكتمال كان <b>يُصدّق ادّعاءَ نفسه</b>. كشفه تدقيقٌ موازٍ راجع عملي، فأُضيف
    /// الاختبار وأُضيف معه فحصٌ يمنع تكرار ذلك: كلُّ اسمٍ في القائمة يجب أن يكون دالّةً
    /// حقيقيةً على المولّد.</para>
    /// </summary>
    [Fact]
    public async Task AGeneratedCreditNoteCancellation_Conforms()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var note = CreditNote.CreateDraft(
            _companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 10), "إرجاع خاطئ");
        note.AddLine("خدمة استشارية", 1m, 300m);
        await new CreditNotePostingService(_db).PostAsync(note);

        // ‏(م-٥ · ن-٢٥) **داخل معاملة — كما يفعل المسارُ الإنتاجيّ.**
        // ‏كان هذا الاستدعاءُ خارجَها، وكان يمرّ لأنّ حارسَ «خارج معاملة» كان متخطّىً على
        // ‏SQLite. **والمسارُ الحقيقيُّ يفتح معاملتَه** (`SalesInvoiceCancellationService`
        // ‏و`CreditNoteCancellationService`)، فالاستدعاءُ العاري كان يقيس شكلاً لا يسلكه أحد.
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var document = await new ZatcaEInvoiceGenerator(_db)
            .TryGenerateCancellationForCreditNoteAsync(note.Id);

        Assert.True(document is not null,
            "إلغاءُ الإشعار الدائن لم يُنتج مستنداً — والمسارُ مُدرَجٌ بين المقيسة.");
        AssertConforms(
            nameof(ZatcaEInvoiceGenerator.TryGenerateCancellationForCreditNoteAsync),
            ReadGenerated(document!));
    }

    /// <summary>
    /// <b>وح-٤ على مُخرَجٍ حقيقيّ:</b> التوقيعُ يُعاد التحقّق منه، وبصمتا المرجعين تُعاد
    /// حسابهما من المستند كما استقرّ. فلا يكفي وجودُ كتلة توقيعٍ — يجب أن تكون <b>صحيحة</b>.
    /// </summary>
    [Fact]
    public async Task AGeneratedSalesInvoice_CarriesAVerifiableSignature()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var document = await DocumentFromPostingAsync(invoice.Id, ZatcaDocumentType.SalesInvoice);

        var result = ZatcaSignatureVerifier.Verify(ReadGenerated(document));

        Assert.True(result.IsValid, $"توقيعُ المستند المولَّد لا يصمد للتحقّق — {result}");
    }

    /// <summary>
    /// <b>وحارسٌ لأداة التوقيع نفسها.</b> «التوقيع صحيح» لا يعني شيئاً ما لم يُعرَف أنّ
    /// الأداة تُميّز الصحيحَ من المعبوث. فيُعبَث بمبلغٍ في المستند <b>بعد</b> التوقيع —
    /// وهو ما يفعله المزوِّر — ويُتحقَّق أنّ ح-٤ يكشفه.
    ///
    /// <para>ولولا هذا الحارس لَما فرّق مرورُ الاختبار السابق بين توقيعٍ سليمٍ وأداةٍ
    /// تقول «صحيح» دائماً.</para>
    /// </summary>
    [Fact]
    public async Task TamperingAfterSigning_IsDetected()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();
        var document = await DocumentFromPostingAsync(invoice.Id, ZatcaDocumentType.SalesInvoice);

        var original = ReadGenerated(document);
        Assert.True(ZatcaSignatureVerifier.Verify(original).IsValid, "المستند الأصليّ يجب أن يكون صحيحاً.");

        // ‏تغييرُ مبلغٍ في جسد الفاتورة بعد التوقيع — أوضحُ صور التزوير.
        var text = System.Text.Encoding.UTF8.GetString(original);
        var tampered = text.Replace(">1000.00<", ">9000.00<", StringComparison.Ordinal);
        Assert.NotEqual(text, tampered);

        var result = ZatcaSignatureVerifier.Verify(System.Text.Encoding.UTF8.GetBytes(tampered));

        Assert.False(result.IsValid,
            $"العبثُ بالمبلغ بعد التوقيع لم يُكشف — فأداةُ التحقّق لا تُميّز شيئاً: {result}");
    }

    /// <summary>
    /// <b>حارسٌ للحارس.</b> لو صار المولّد يُعيد <c>null</c> دائماً — أو لا يكتب ملفّاً —
    /// لمرّت الاختبارات أعلاه على لا شيء. فيُثبت هنا أنّ ما يُقاس مستندٌ ذو مضمون.
    /// </summary>
    [Fact]
    public async Task WhatIsMeasured_IsARealDocument_NotAnEmptyFile()
    {
        await SetupAsync();
        var invoice = await PostInvoiceAsync();

        var document = await DocumentFromPostingAsync(invoice.Id, ZatcaDocumentType.SalesInvoice);

        var xml = ReadGenerated(document);
        Assert.True(xml.Length > 1_000, $"المستند {xml.Length} بايت فقط — أصغر من أن يكون فاتورة.");

        var text = System.Text.Encoding.UTF8.GetString(xml);
        Assert.Contains("<cbc:UUID>", text, StringComparison.Ordinal);
        Assert.Contains(document.InvoiceUuid.ToString(), text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// (CEEF-229 · م2-١٣أ) <b>منشأةٌ ناقصةُ الهوية لا تُصدر مستنداً — تُوقَف عند الباب.</b>
    ///
    /// <para><b>العطل:</b> <c>Company.RegulatoryGaps()</c> يعرف بالضبط ما ينقص، وبرسائل عربية
    /// جاهزة. لكنّه <b>لا يُقرأ إلا في شاشة الإعدادات عرضاً</b> وفي خدمة الإرسال <b>وفي وضع
    /// الإنتاج وحده</b>. أمّا المولّد فيفحص الرقمَ الضريبيّ لا غير. و<c>DatabaseInitializer</c>
    /// لا يملأ بقيةَ الحقول أصلاً — فالمنشأةُ الجديدة ناقصةٌ بالتعريف.</para>
    ///
    /// <para><b>ولماذا لا يكفي إصلاحُ الإعدادات لاحقاً:</b> كلُّ مستندٍ خاطئ <b>يستهلك عدّاداً
    /// تسلسلياً (ICV)</b>، و<b>بصمتُه تصير PreviousHash للمستند التالي</b>. فالسلسلةُ تتلوّث
    /// بأثرٍ رجعيّ لا يُصلَح: تُرفض المستنداتُ ويُطلب إعادةُ بنائها من أوّلها.</para>
    ///
    /// <para><b>وما يراه المالك:</b> البيعُ ينجح، والفاتورة تُطبع، والشاشةُ لا تشكو — ثمّ
    /// تُرفض كلُّ فواتيره عند الهيئة. فالرفضُ عند الباب أرحمُ من سلسلةٍ ملوَّثة.</para>
    /// </summary>
    [Fact]
    public async Task AnIncompleteEstablishment_IsRefused_BeforeItPoisonsTheChain()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة ناقصة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        // ‏المنشأة بلا اسمٍ مسجَّل ولا سجلٍّ تجاريّ ولا عنوانٍ وطنيّ — والرقم الضريبيّ وحده موجود.
        Assert.NotEmpty(company.RegulatoryGaps());

        var ex = await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(
            () => PostInvoiceAsync());

        Assert.Contains("السجل التجاري", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, await _db.ZatcaEInvoiceDocuments.CountAsync(d => d.CompanyId == _companyId));
    }

    /// <summary>
    /// (CEEF-229 · م2-١٣ب) <b>بلا شهادةٍ لا يُصدَر مستند — لا «يُصدَر غيرَ موقَّع بصمت».</b>
    ///
    /// <para><b>العطل:</b> <c>ZatcaXmlSigner.TrySignWithResult</c> يُعيد <c>null</c> بلا شكوى
    /// حين لا يجد ملفَّ شهادةٍ أو مفتاح. وعندها يسلك المولّد فرعاً بديلاً <b>صامتاً</b>:</para>
    ///
    /// <list type="number">
    /// <item>المستند يخرج <b>بلا توقيع</b>، ورمزُه QR من الطور الأوّل — بلا ختمٍ تشفيريّ.</item>
    /// <item>و<c>InvoiceHash</c> يُحسب بـ <b>خوارزميةٍ أخرى</b>: <c>Sha256Base64</c> على البايتات
    /// الخام، بدل <c>ComputeInvoiceHash</c> الذي يُقَنّن المستند <b>بعد نزع</b>
    /// <c>UBLExtensions</c> و<c>Signature</c> ومرجع QR. ولا يمكن أن يتّفق الرقمان.</item>
    /// <item>وتلك البصمةُ تصير <c>PreviousInvoiceHash</c> للمستند التالي — <b>فتنكسر السلسلة
    /// عند ذلك المفصل</b> انكساراً لا تستطيع الهيئة مطابقته.</item>
    /// <item>ثمّ ترفع خدمةُ الإرسال <c>SignedXmlFilePath ?? XmlFilePath</c> — أي <b>ترفع
    /// الملفَّ غيرَ الموقَّع</b>.</item>
    /// </list>
    ///
    /// <para><b>ولا يوجد في النظام مفهومُ «طورٍ أوّل» أصلاً</b> — لا إعدادَ ولا حقل. فالمسارُ
    /// غيرُ الموقَّع ليس وضعاً مشروعاً بل انحداراً صامتاً، ولا يملك المالكُ ما يخبره أيَّهما هو فيه.</para>
    /// </summary>
    [Fact]
    public async Task WithoutACertificate_NothingIsIssued_RatherThanIssuedUnsigned()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة بلا شهادة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        ZatcaReadySeller.Complete(company);

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        // ‏الفوترة مُفعَّلة والهويةُ مكتملة — والشهادةُ وحدها غائبة.
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(
            () => PostInvoiceAsync());

        Assert.Contains("شهادة", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, await _db.ZatcaEInvoiceDocuments.CountAsync(d => d.CompanyId == _companyId));
    }

    /// <summary>
    /// (CEEF-229 · م1-١ب) <b>ومشترٍ ناقصُ الهوية لا تُصدَر له فاتورةٌ قياسية.</b>
    ///
    /// <para><b>وهذه ثغرةٌ فتحها إصلاحي نفسه:</b> أُضيفت حقولُ هوية المشتري في م1-١ب،
    /// <b>والعملاءُ القائمون أعمدتُهم فارغة</b> — فلا يُطالبون بها إلا إن فتح المالكُ شاشتَهم
    /// وعدّلهم. فبقيت فواتيرُهم القياسية تخرج ناقصةَ الطرف المشتري كما كانت.</para>
    ///
    /// <para>والحارسُ هنا نظيرُ حارس المنشأة الناقصة (م2-١٣أ) وللسبب نفسه: المستندُ الخاطئ
    /// <b>يستهلك عدّاداً تسلسلياً</b> وتصير بصمتُه أساسَ ما بعده. فالرفضُ عند الباب أرحمُ من
    /// سلسلةٍ ملوَّثة — والرسالةُ تسمّي الحقلَ الناقص فيعرف المالكُ ما يملأ.</para>
    ///
    /// <para><b>والمبسّطة لا تُمَسّ:</b> عميلٌ بلا رقمٍ ضريبيّ يبيع له الكاشيرُ كما كان.</para>
    /// </summary>
    [Fact]
    public async Task AnIncompleteBuyer_IsRefused_ForAStandardInvoiceOnly()
    {
        await SetupAsync();

        // ‏عميلٌ ذو رقمٍ ضريبيّ بلا هويةٍ نظامية — وهو حالُ كلّ عميلٍ قائمٍ قبل هذا الإصلاح.
        var bare = Customer.Create(_companyId, "عميل قديم", "311222333400003");
        _db.Customers.Add(bare);
        await _db.SaveChangesAsync();

        var standard = SalesInvoice.CreateDraft(_companyId, bare.Id, new DateOnly(2026, 7, 9));
        standard.AddLine("خدمة", 1m, 500m);

        var ex = await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(
            () => new SalesInvoicePostingService(_db).PostAsync(standard));
        Assert.Contains("عميل قديم", ex.Message, StringComparison.Ordinal);

        // ‏الترحيلُ الفاشل يترك في مُتتبِّع هذا السياق كياناتٍ أُضيفت داخل معاملةٍ تراجعت
        // ‏(فترةٌ ماليةٌ مثلاً) — والتطبيقُ يفتح سياقاً جديداً لكلّ عملية فلا يراها. وهذا
        // ‏الاختبار وحده يُعيد استعمال السياق، فيُنظَّف المُتتبِّع كما يفعل الإغلاقُ هناك.
        _db.ChangeTracker.Clear();

        // ‏والمبسّطة تمرّ: عميلٌ نقديّ بلا رقمٍ ضريبيّ.
        var walkIn = Customer.Create(_companyId, "عميل نقدي");
        _db.Customers.Add(walkIn);
        await _db.SaveChangesAsync();

        var simplified = SalesInvoice.CreateDraft(_companyId, walkIn.Id, new DateOnly(2026, 7, 9));
        simplified.AddLine("خدمة", 1m, 500m);
        await new SalesInvoicePostingService(_db).PostAsync(simplified);

        Assert.NotNull(await DocumentFromPostingAsync(simplified.Id, ZatcaDocumentType.SalesInvoice));
    }

    /// <summary>
    /// (CEEF-229 · م1-١ب/الشقّ الثاني) <b>فاتورةٌ صفريةٌ مرحّلةٌ فعلاً تحمل سببَ إعفائها.</b>
    ///
    /// <para>وهذا هو القياسُ الذي يصل الطرفين: النطاقُ يقبل السطر، والمولّدُ يترجمه، والملفُّ
    /// على القرص يحمل <c>TaxExemptionReasonCode</c> — لا اختبارُ بانٍ من مُدخلٍ مُعَدّ.</para>
    /// </summary>
    [Fact]
    public async Task AZeroRatedSale_CarriesItsExemptionReason_AllTheWayToDisk()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("تصدير سلع", 1m, 1_000m, null,
            Brqosh.Domain.Tax.VatCategory.ZeroRated, "VATEX-SA-32");
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var document = await DocumentFromPostingAsync(posted.Id, ZatcaDocumentType.SalesInvoice);
        var xml = ReadGenerated(document);
        AssertConforms("فاتورة صفرية", xml);

        var text = System.Text.Encoding.UTF8.GetString(xml);
        Assert.Contains("VATEX-SA-32", text, StringComparison.Ordinal);
        Assert.Contains("Export of goods", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والعكسُ يُقاس أيضاً:</b> سطرٌ صفريّ بلا سبب لا يُبنى أصلاً — فالرفضُ عند بناء
    /// السطر، أبكرُ موضعٍ ممكن، قبل استهلاك أيّ عدّاد.
    /// </summary>
    [Fact]
    public void AZeroRatedLine_WithoutAReason_NeverBecomesADocument()
    {
        var invoice = SalesInvoice.CreateDraft(1, 1, new DateOnly(2026, 7, 9));

        Assert.Throws<Brqosh.Domain.Accounting.AccountingException>(
            () => invoice.AddLine("تصدير", 1m, 1_000m, null, Brqosh.Domain.Tax.VatCategory.ZeroRated));
    }

    /// <summary>
    /// (CEEF-229 · م1-١ب/الشقّ الثاني) <b>وحارسُ المولّد ليس زائداً على حارس السطر — فهو
    /// وحده ما يحمي البياناتِ التي سبقت هذه الدفعة.</b>
    ///
    /// <para>حارسُ <c>AddLine</c> يمنع بناءَ سطرٍ ناقص من اليوم فصاعداً، لكنّ قاعدةَ منشأةٍ
    /// قائمة تحوي أسطراً صفريةً ومعفاةً <b>خُزّنت بلا رمزٍ أصلاً</b> — ولا تمرّ تلك الأسطر
    /// بـ<c>AddLine</c> ثانيةً: تُقرأ من القرص كما هي. فتُحاكى الحالةُ هنا بتفريغ العمود
    /// مباشرةً بـ SQL، ثمّ يُطلب مستندُ إلغاءٍ يقرأ الأسطرَ المخزَّنة.</para>
    ///
    /// <para>ولولا هذا الاختبار لبقي حارسُ المولّد <b>ادّعاءً غيرَ مقيس</b>: كلُّ المسارات
    /// الأخرى تمرّ بحارس السطر فلا تبلغه.</para>
    /// </summary>
    [Fact]
    public async Task ALegacyLine_StoredWithoutAReason_IsCaughtByTheGenerator()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("تصدير سلع", 1m, 1_000m, null,
            Brqosh.Domain.Tax.VatCategory.ZeroRated, "VATEX-SA-32");
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        // ‏تفريغُ العمود = سطرٌ خُزّن قبل هذه الدفعة. ولا سبيلَ إلى بنائه عبر النطاق.
        await _db.Database.ExecuteSqlRawAsync(
            """UPDATE sales_invoice_lines SET "TaxExemptionReasonCode" = NULL, "TaxExemptionReason" = NULL;""");
        _db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(
            () => new ZatcaEInvoiceGenerator(_db).TryGenerateCancellationForSalesInvoiceAsync(posted.Id));

        Assert.Contains("سبب إعفاء", ex.Message, StringComparison.Ordinal);
        Assert.Contains("تصدير سلع", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// (CEEF-229 · م2-٢١) <b>مستندُ الإلغاء يحمل تاريخَ الإلغاء — لا تاريخَ ما أُلغي.</b>
    ///
    /// <para><b>وأثرُه انشقاقُ الفترة الضريبية:</b> خدمةُ الإلغاء تتحقّق أنّ فترةَ <i>الإلغاء</i>
    /// مفتوحة وتعكس القيدَ بتاريخ <i>الإلغاء</i> — وهو الصواب. ثمّ يُولَّد إشعارُ الإلغاء
    /// بتاريخ <i>الفاتورة الأصلية</i>. فإذا عبر الإلغاءُ حدَّ شهرٍ قيّدت الهيئةُ الخصمَ في
    /// الشهر الأوّل وقيّدته الدفاترُ في الثاني، ولا يُصلَح بأثرٍ رجعيّ: المستندُ بعد قبوله
    /// لدى الهيئة غيرُ قابلٍ للتعديل.</para>
    ///
    /// <para>و<c>CancelledAt</c> موجودٌ على المستندات الثلاثة ومملوءٌ لحظتَها — ولا يقرؤه
    /// المولّد. وإلغاءُ فاتورةِ آخرِ يومٍ في الشهر هو أشيعُ ما يقع في متجر.</para>
    /// </summary>
    [Fact]
    public async Task ACancellation_CarriesTheCancellationDate_NotTheOriginalDate()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 31));
        invoice.AddLine("خدمة", 1m, 1_000m);
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, posted.Id, new DateOnly(2026, 8, 3), "مدقّق");

        var document = await DocumentFromPostingAsync(posted.Id, ZatcaDocumentType.InvoiceCancel);
        var xml = System.Xml.Linq.XDocument.Load(
            new MemoryStream(ReadGenerated(document)));
        var cbc = System.Xml.Linq.XNamespace.Get(
            "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

        Assert.Equal("2026-08-03", xml.Root!.Element(cbc + "IssueDate")!.Value);
    }

    /// <summary>
    /// (CEEF-229 · م2-٢١) <b>والمستندُ لا يكذب على نفسه:</b> وقتُه المعلن هو لحظةُ توليده
    /// المسجَّلة، ورمزُ QR يقول اللحظةَ نفسها.
    ///
    /// <para>كان <c>IssueTime</c> ثابتاً <c>"12:00:00"</c> بلا إزاحة — تقرؤه الهيئةُ توقيتاً
    /// محلياً — بينما وسمُ QR الثالث يُبنى من ظهرٍ <b>بتوقيت UTC</b> فيقول <c>12:00:00Z</c>،
    /// أي الثالثةَ عصراً بالرياض. <b>فالمستندُ الواحد يعلن لحظتين تفصلهما ثلاث ساعات.</b></para>
    ///
    /// <para>والقياسُ لا يحتاج ساعةً محقونة: اللحظةُ الحقيقية <b>مسجَّلةٌ سلفاً</b> في
    /// <c>GeneratedAtUtc</c>. فيكفي أن يُطابَق المعلَنُ بالمسجَّل — وهو قياسٌ حتميّ.</para>
    /// </summary>
    [Fact]
    public async Task TheDeclaredInstant_MatchesTheRecordedOne_AndTheQrAgrees()
    {
        await SetupAsync();
        var posted = await PostInvoiceAsync();

        var document = await DocumentFromPostingAsync(posted.Id, ZatcaDocumentType.SalesInvoice);
        var xml = System.Xml.Linq.XDocument.Load(
            new MemoryStream(ReadGenerated(document)));
        var cbc = System.Xml.Linq.XNamespace.Get(
            "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

        // ‏(أ) الوقتُ المعلن هو لحظةُ التوليد المسجَّلة، بتوقيت الرياض (‏+03:00 بلا توقيتٍ صيفيّ).
        var declared = TimeOnly.Parse(
            xml.Root!.Element(cbc + "IssueTime")!.Value, CultureInfo.InvariantCulture);
        var recorded = TimeOnly.FromDateTime(
            document.GeneratedAtUtc + TimeSpan.FromHours(3));

        // ‏(CEEF-233 · ظ-١) **والمطابقةُ إلى الثانية، لا بتسامحٍ يُخفي قراءتين.**
        //
        // ‏كان التسامحُ ثانيةً واحدة **وكان الاختبارُ يسقط نحو ثلثِ التشغيلات**: المولّدُ
        // ‏يقرأ الساعةَ ويشتقّ منها `IssueTime`، **والكيانُ يقرؤها مرّةً ثانيةً** لـ
        // ‏`GeneratedAtUtc`. فمتى وقعت القراءتان على طرفَي ثانيةٍ اختلف المعلَنُ عن المسجَّل —
        // ‏**والمعلَنُ مقطوعُ الكسر فيبلغ الفارقُ ثانيتين فيتجاوز التسامح**.
        //
        // ‏**واختبارٌ يسقط ثلثَ الوقت أسوأ من غيابه**: يُعلَّم الفريقُ إهمالَ الأحمر
        // ‏فيفوته الأحمرُ الحقيقيّ. **وأُصلح المنتجُ لا التسامح** — تُمرَّر لحظةٌ واحدة،
        // ‏فصارت المطابقةُ حتميّةً كما وَعَد تعليقُ المولّد.
        Assert.Equal(declared.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            recorded.ToString("HH:mm:ss", CultureInfo.InvariantCulture));

        // ‏(ب) ورمزُ QR يقول اللحظةَ نفسها — لا لحظةً أخرى تفصلها ساعات.
        var qrStamp = ZatcaSignatureVerifier.ReadQrTimestamp(document.QrTlvBase64!);
        var declaredUtc = DateOnly.Parse(
            xml.Root.Element(cbc + "IssueDate")!.Value, CultureInfo.InvariantCulture)
            .ToDateTime(declared) - TimeSpan.FromHours(3);

        Assert.True(Math.Abs((qrStamp - declaredUtc).TotalSeconds) <= 1,
            $"رمزُ QR يقول {qrStamp:O} والمستندُ يقول {declaredUtc:O} — لحظتان مختلفتان في مستندٍ واحد.");
    }

    /// <summary>
    /// (CEEF-229 · م2-٢٢) <b>بذرةُ السلسلة — أوّلُ مستندٍ في حياة المنشأة.</b>
    ///
    /// <para>معيارُ الهيئة ينصّ حرفياً: «For the first invoice, the previous invoice hash is
    /// <c>NWZlY2ViNjZ…</c>». <b>وعبارتُه التالية هي الفخّ</b>: «the equivalent for base64
    /// encoded SHA256 of "0"» — فمن قرأها وصفاً للحساب كتب <c>Base64(SHA256("0"))</c> على
    /// البايتات الخام. والثابتُ المنصوصُ عليه يفكّ إلى <b>السلسلة السداسية عشرية</b> لتلك
    /// البصمة، لا إلى بايتاتها. فالقيمتان مختلفتان تماماً.</para>
    ///
    /// <para><b>وأثرُه لا يظهر محلياً:</b> البيئةُ الافتراضية «محاكاة» تَسِم المستندَ
    /// <c>SimulatedLocally</c> بلا أيّ فحصٍ للقيمة. فيُرفض أوّلُ مستندٍ عند التحوّل إلى
    /// الإنتاج، وبصمتُه أساسُ ما بعده — فلا يُصلحه تعديلُ سطر بل إعادةُ إصدار السلسلة كلّها.</para>
    /// </summary>
    [Fact]
    public async Task TheFirstDocument_CarriesTheAuthorityGenesisHash()
    {
        await SetupAsync();
        var posted = await PostInvoiceAsync();

        var document = await DocumentFromPostingAsync(posted.Id, ZatcaDocumentType.SalesInvoice);

        // ‏**منقولٌ حرفياً عن معيار الهيئة** (الإصدار 1.2، قاعدة بصمة الفاتورة السابقة)، لا
        // ‏مقروءاً من ثابتٍ في الشيفرة — وإلا كان القياسُ دائرياً يشهد للكود بنفسه.
        const string genesis =
            "NWZlY2ViNjZmZmM4NmYzOGQ5NTI3ODZjNmQ2OTZjNzljMmRiYzIzOWRkNGU5MWI0NjcyOWQ3M2EyN2ZiNTdlOQ==";

        Assert.Equal(genesis, document.PreviousInvoiceHash);
        Assert.Equal(genesis, ReadPih(ReadGenerated(document)));
    }

    /// <summary>
    /// (CEEF-229 · م2-٢٢) <b>والوصلةُ الثانية: بصمةُ سابقها بعينها.</b>
    ///
    /// <para>السلسلةُ مبنيّةٌ في الشيفرة، <b>ولم يكن يقيسها اختبارٌ واحد</b>. فأيّ تعديلٍ على
    /// <c>ComputeInvoiceHash</c> — أو على استثناءاته الثلاثة — يكسر السلسلةَ عند مفصلٍ لا
    /// تستطيع الهيئةُ مطابقتَه، <b>ولا يحمرّ اختبار</b>. وهذا القياسُ هو ما يمنع ذلك.</para>
    /// </summary>
    [Fact]
    public async Task TheSecondDocument_ChainsToTheFirstsHash()
    {
        await SetupAsync();
        var first = await PostInvoiceAsync(1_000m);
        var second = await PostInvoiceAsync(2_000m);

        var a = await DocumentFromPostingAsync(first.Id, ZatcaDocumentType.SalesInvoice);
        var bDoc = await DocumentFromPostingAsync(second.Id, ZatcaDocumentType.SalesInvoice);

        Assert.NotEqual(a.Id, bDoc.Id);
        Assert.Equal(2, bDoc.Icv);
        Assert.Equal(a.InvoiceHash, bDoc.PreviousInvoiceHash);

        // ‏والملفُّ على القرص يقولها أيضاً — لا الحقلُ المخزَّن وحده.
        Assert.Equal(a.InvoiceHash, ReadPih(ReadGenerated(bDoc)));
    }

    /// <summary>يقرأ وسمَ PIH من المستند نفسه — لا من الحقل الذي بُني منه.</summary>
    private static string? ReadPih(byte[] xml)
    {
        var doc = System.Xml.Linq.XDocument.Load(new MemoryStream(xml));
        var cac = System.Xml.Linq.XNamespace.Get(
            "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        var cbc = System.Xml.Linq.XNamespace.Get(
            "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

        return doc.Root!
            .Elements(cac + "AdditionalDocumentReference")
            .FirstOrDefault(r => r.Element(cbc + "ID")?.Value == "PIH")?
            .Element(cac + "Attachment")?
            .Element(cbc + "EmbeddedDocumentBinaryObject")?.Value;
    }

    /// <summary>
    /// <b>وكلُّ مسارِ توليدٍ إمّا مقيسٌ هنا وإمّا مُقَرٌّ بأنّه خارج متناول هذا العميل.</b>
    /// فلو أُضيف مسارٌ سابعٌ غداً لسقط هذا الاختبار حتى يُقاس أو يُقَرّ به بسبب.
    /// </summary>
    [Fact]
    public void EveryGenerationPath_IsEitherMeasuredOrDeclared()
    {
        var paths = typeof(ZatcaEInvoiceGenerator)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("TryGenerate", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(paths.Count >= 6, $"لم يُعثر إلا على {paths.Count} مسار توليد — الانعكاسُ لا يمسح المولّد.");

        string[] measured =
        [
            nameof(ZatcaEInvoiceGenerator.TryGenerateForSalesInvoiceAsync),
            nameof(ZatcaEInvoiceGenerator.TryGenerateForCreditNoteAsync),
            nameof(ZatcaEInvoiceGenerator.TryGenerateCancellationForSalesInvoiceAsync),
            nameof(ZatcaEInvoiceGenerator.TryGenerateCancellationForCreditNoteAsync),
            // ‏(جاهزيّةُ الإطلاق · ح-١٦) **وهذا هو «المسارُ السابع» الذي حذّر منه هذا
            // ‏الحارسُ نصّاً** — فأسقط الاختبارَ يومَ أُضيف حتّى قِيس. انظر
            // ‏<see cref="AGeneratedAssetDisposalInvoice_Conforms"/>.
            nameof(ZatcaEInvoiceGenerator.TryGenerateForAssetDisposalAsync),
            // ‏(ح-١٦١) ‏وإلغاءُ بيع الأصل — <see cref="AGeneratedAssetDisposalCancellation_Conforms"/>.
            nameof(ZatcaEInvoiceGenerator.TryGenerateCancellationForAssetDisposalAsync),
            // ‏(sector-modules#6) ‏فاتورةُ الدفعة المقدّمة (386) وإشعارُ ردّها — والمتجرُ لا يبلغهما (لا حجوزَ في التجزئة)، فيُقاسان
            // ‏في ملفّ الضيافة: <see cref="Hospitality.AReservationDepositIssuesAPrepaymentInvoiceTests.ThePrepayment_TheSettlingInvoice_AndTheRefund_Conform"/>.
            nameof(ZatcaEInvoiceGenerator.TryGenerateForPrepaymentAsync),
            nameof(ZatcaEInvoiceGenerator.TryGenerateRefundForPrepaymentAsync),
        ];

        // ‏واسمٌ في القائمتين لا يقابله مسارٌ حقيقيّ يجعل الحارسَ يُصدّق ادّعاءَ نفسه —
        // ‏وهو ما وقع فعلاً: أُدرج مسارُ إلغاء الإشعار الدائن بين «المقيسة» ولا اختبارَ
        // ‏يستدعيه، حتى كشفه تدقيقٌ موازٍ. فيُتحقَّق أنّ كلّ اسمٍ دالّةٌ قائمة.
        var phantom = measured.Concat(DeclaredOutOfReach.Keys)
            .Where(name => !paths.Contains(name))
            .ToList();

        Assert.True(phantom.Count == 0,
            "أسماءُ مساراتٍ لا وجودَ لها على المولّد — القائمةُ تصف ما ليس موجوداً: "
            + string.Join(" · ", phantom));

        var unaccounted = paths
            .Where(p => !measured.Contains(p) && !DeclaredOutOfReach.ContainsKey(p))
            .ToList();

        Assert.True(unaccounted.Count == 0,
            "مساراتُ توليدٍ لا تُقاس ولا إقرارَ لها — ما تُنتجه يصل الهيئة بلا فحص: "
            + string.Join(" · ", unaccounted));
    }
}
