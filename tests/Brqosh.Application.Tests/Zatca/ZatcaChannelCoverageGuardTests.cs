using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Healthcare;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Healthcare;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-230 · ط6-٧) <b>كلُّ بابٍ تخرج منه فاتورةُ ضريبةٍ يُخرج معها مستنداً إلكترونياً.</b>
///
/// <para><b>وهويّةُ هـ-١٦:</b> في منشأةٍ مفعَّلةِ الفوترة، لكلّ فاتورةِ مبيعاتٍ مرحَّلةٍ
/// <b>مستندٌ إلكترونيٌّ واحدٌ لا أكثرَ ولا أقلّ</b>، ولكلّ إشعارٍ دائنٍ مرحَّلٍ مثلُه.
/// <b>وكسرُها مخالفةُ الطور الثاني بعينها</b>: فاتورةٌ تُسلَّم للعميل ولا تعرفها الهيئة.</para>
///
/// <para><b>ولماذا حارسٌ لا اختبارُ قناة:</b> اختبارٌ لكلّ قناةٍ يُغلق <b>الحالات</b> التي
/// نعرفها اليوم، <b>وقناةٌ تُكتب غداً تولد بلا قياس</b>. فهذا الحارسُ يقيس شيئين: أنّ كلَّ
/// قناةٍ معروفةٍ تُخرج مستنداً، <b>وأنّ لا مستندَ مرحَّلاً في القاعدة كلِّها بلا مستندٍ
/// إلكترونيّ</b> — والثاني يعضّ قناةً لم تُسجَّل في جدول القنوات أصلاً.</para>
/// </summary>
public class ZatcaChannelCoverageGuardTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    private long _b2bCustomerId;      // مشترٍ مسجَّلٌ ضريبياً — فاتورةٌ قياسية (اعتماد)
    private long _guestCustomerId;    // نزيلٌ بلا رقمٍ ضريبيّ — فاتورةٌ مبسّطة (إبلاغ)
    private long _tenantCustomerId;
    private long _patientId;
    private long _payerId;
    private long _policyId;
    private long _productId;
    private long _roomId;

    public ZatcaChannelCoverageGuardTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── القياسُ السلوكيّ: قناةً قناة ─────────────────────────────────────────

    /// <summary>
    /// <b>كلُّ قناةِ ترحيلٍ تُخرج مستنداً إلكترونياً واحداً.</b>
    ///
    /// <para><b>وتُجمع الإخفاقاتُ كلُّها قبل السقوط</b>: حارسٌ يسقط عند أوّل قناةٍ يجعل
    /// إصلاحَ أربعٍ أربعَ دوراتٍ من التشغيل، <b>ويُخفي أنّ العطلَ صنفٌ لا حالة</b>.</para>
    /// </summary>
    [Fact]
    public async Task EveryPostingChannel_IssuesAnElectronicInvoice()
    {
        await SetupAsync();

        var failures = new List<string>();
        foreach (var channel in Channels())
        {
            var documentIds = await channel.RunAsync();

            // قناةٌ لم تُرحّل شيئاً تُخضّر الحارسَ كذباً — **فالفراغُ إخفاقُ قياسٍ لا نجاح**.
            if (documentIds.Count == 0)
            {
                failures.Add($"«{channel.Name}» لم تُرحّل مستنداً أصلاً — القياسُ نفسُه معطوب.");
                continue;
            }

            foreach (var id in documentIds)
            {
                var issued = await _db.ZatcaEInvoiceDocuments.CountAsync(d =>
                    d.CompanyId == _companyId &&
                    d.DocumentType == channel.DocumentType &&
                    d.SourceDocumentId == id);

                if (issued != 1)
                    failures.Add($"«{channel.Name}» — المستند {id}: مستنداتٌ إلكترونية = {issued} والمنتظر ١.");
            }
        }

        Assert.True(failures.Count == 0,
            "قنواتٌ تُصدر فاتورةً ضريبيةً بلا مستندٍ إلكترونيّ — وهي مخالفةُ الطور الثاني بعينها:\n  "
            + string.Join("\n  ", failures));
    }

    // ── هـ-١٦: القياسُ الشامل — لا مستندَ يفلت ───────────────────────────────

    /// <summary>
    /// <b>هـ-١٦: لا فاتورةَ مرحَّلةً بلا مستندٍ إلكترونيّ، ولا مستندَين لفاتورةٍ واحدة.</b>
    ///
    /// <para><b>والاتّجاهان كلاهما عطل:</b> النقصُ فاتورةٌ لا تعرفها الهيئة، <b>والزيادةُ
    /// فاتورةٌ واحدةٌ تُبلَّغ مرّتين برقمَي عدّادٍ مختلفين</b> — فتنكسر سلسلةُ البصمات.</para>
    ///
    /// <para>وهذا هو ما يعضّ <b>قناةً لم يسمع بها هذا الملفّ</b>: لا يُعدّد القنوات، بل
    /// يُعدّد ما استقرّ في القاعدة بعد أن جرت كلُّها.</para>
    /// </summary>
    [Fact]
    public async Task NoPostedTaxDocument_EscapesWithoutAnElectronicInvoice()
    {
        await SetupAsync();
        foreach (var channel in Channels())
            await channel.RunAsync();

        await AssertOneToOneAsync(
            "فاتورة مبيعات", ZatcaDocumentType.SalesInvoice,
            await _db.SalesInvoices.AsNoTracking()
                .Where(i => i.CompanyId == _companyId && i.Status == SalesInvoiceStatus.Posted)
                .Select(i => i.Id).ToListAsync());

        await AssertOneToOneAsync(
            "إشعار دائن", ZatcaDocumentType.CreditNote,
            await _db.CreditNotes.AsNoTracking()
                .Where(n => n.CompanyId == _companyId && n.Status == CreditNoteStatus.Posted)
                .Select(n => n.Id).ToListAsync());
    }

    /// <summary>
    /// <b>وسلسلةُ العدّاد والبصمة تصمد وقد صار التوليدُ في المسار المشترك.</b>
    ///
    /// <para><b>ولماذا هنا بالذات:</b> فوترةُ الزيارة تُخرج <b>مستندين في معاملةٍ واحدة</b>
    /// — حصّةُ المريض ومطالبةُ الشركة. وهو ما لم يكن يقع قبل هذا التغيير. فلو قرأ الثاني
    /// العدّادَ قبل أن يستقرّ الأوّل <b>لخرج رقمان متساويان ببصمةٍ سابقةٍ واحدة</b>،
    /// <b>وتتفرّع السلسلةُ فترفض الهيئةُ الثاني ولا يُصلَح إلّا بإعادة إصدار ما بعده</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheCounterChain_SurvivesTwoDocumentsInOneTransaction()
    {
        await SetupAsync();
        foreach (var channel in Channels())
            await channel.RunAsync();

        var docs = await _db.ZatcaEInvoiceDocuments.AsNoTracking()
            .Where(d => d.CompanyId == _companyId)
            .OrderBy(d => d.Icv)
            .Select(d => new { d.Icv, d.InvoiceHash, d.PreviousInvoiceHash })
            .ToListAsync();

        // سلسلةٌ من مستندٍ واحدٍ أو صفرٍ تمرّ بلا أن تقيس شيئاً — **فالفراغُ ليس نجاحاً**.
        Assert.True(docs.Count > 1, $"مستنداتٌ إلكترونية = {docs.Count} — لا سلسلةَ تُقاس.");

        Assert.Equal(Enumerable.Range(1, docs.Count), docs.Select(d => d.Icv));

        for (var i = 1; i < docs.Count; i++)
            Assert.True(docs[i].PreviousInvoiceHash == docs[i - 1].InvoiceHash,
                $"السلسلةُ منقطعةٌ عند العدّاد {docs[i].Icv} — بصمتُه السابقة لا تُطابق بصمةَ {docs[i - 1].Icv}.");
    }

    private async Task AssertOneToOneAsync(
        string label, ZatcaDocumentType type, IReadOnlyList<long> postedIds)
    {
        Assert.True(postedIds.Count > 0, $"لا {label} مرحَّلةً في القياس — الحارسُ يقيس فراغاً.");

        var issuedFor = await _db.ZatcaEInvoiceDocuments.AsNoTracking()
            .Where(d => d.CompanyId == _companyId && d.DocumentType == type)
            .Select(d => d.SourceDocumentId).ToListAsync();

        var orphans = postedIds.Except(issuedFor).OrderBy(id => id).ToList();
        Assert.True(orphans.Count == 0,
            $"هـ-١٦ مكسورة — {label} مرحَّلةٌ بلا مستندٍ إلكترونيّ: {string.Join("، ", orphans)}");

        var duplicated = issuedFor.GroupBy(id => id).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}×{g.Count()}").ToList();
        Assert.True(duplicated.Count == 0,
            $"هـ-١٦ مكسورة — {label} لها أكثرُ من مستندٍ إلكترونيّ: {string.Join("، ", duplicated)}");
    }

    // ── الحارسُ البنيويّ: موضعُ التوليد واحد ─────────────────────────────────

    /// <summary>
    /// <b>موضعُ التوليد واحدٌ لكلّ نوع — والمولّدُ يرمي على المكرَّر.</b>
    ///
    /// <para>فـ<c>TryGenerateFor…</c> ترمي «مستند ZATCA موجود مسبقاً» متى وُجد مستند —
    /// <b>وهي ليست عديمةَ الأثر عند التكرار</b>. فموضعان يستدعيانها في مسارٍ واحد
    /// <b>يُسقطان ترحيلاً سليماً</b>. وبموضعٍ واحدٍ لا يبقى لرميتها معنىً إلّا الحقيقيّ:
    /// أنّ هذا المستند وُلِّد له من قبل.</para>
    ///
    /// <para><b>ويُعَدُّ النداءُ لا الملفّ</b>: طفرةٌ أعادت التوليدَ إلى <c>PostAsync</c>
    /// <b>مرّت من عدٍّ بالملفّات</b> — إذ الموضعان في ملفٍّ واحد. وهي الطفرةُ التي كشفت
    /// عيباً في الحارس نفسِه قبل أن يكشف عيباً في الشيفرة.</para>
    /// </summary>
    [Fact]
    public void TheGenerationSite_IsExactlyOnePerDocumentType()
    {
        var expected = new (string Method, string File)[]
        {
            ("TryGenerateForSalesInvoiceAsync", Path.Combine("Sales", "SalesInvoicePostingService.cs")),
            ("TryGenerateForCreditNoteAsync", Path.Combine("Sales", "CreditNotePostingService.cs")),
        };

        var root = Path.Combine(FindRepositoryRoot(), "src", "Brqosh.Infrastructure");
        var generator = Path.Combine(root, "Zatca", "ZatcaEInvoiceGenerator.cs");

        foreach (var (method, expectedFile) in expected)
        {
            var callSites = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !string.Equals(f, generator, StringComparison.OrdinalIgnoreCase))
                .SelectMany(f => File.ReadAllLines(f)
                    .Select((line, i) => (File: f, Number: i + 1, Text: line))
                    .Where(l => l.Text.Contains(method + "(", StringComparison.Ordinal)))
                .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Number}")
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToList();

            Assert.True(callSites.Count == 1,
                $"«{method}» يُستدعى من {callSites.Count} موضعٍ والمنتظر واحد:\n  "
                + string.Join("\n  ", callSites)
                + "\nوالتوليدُ موضعُه المسارُ المشترك وحدَه — وإلّا نسيته قناةٌ أو كرّرته أخرى.");

            Assert.StartsWith(expectedFile + ":", callSites[0], StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>ولا تُنشأ فاتورةُ مبيعاتٍ في طبقة البنية إلّا حيث تُرحَّل بخدمة الترحيل.</b>
    ///
    /// <para>فهذا ما يمنع قناةً جديدةً من <b>الالتفاف على المسار المشترك</b>: مَن يبني
    /// <c>SalesInvoice</c> عليه أن يمرّ بـ<c>SalesInvoicePostingService</c> — وهناك يقع
    /// التوليد. <b>وحصرُ الإنشاء يمنع العطل، والقياسُ السلوكيُّ فوقه يُثبته.</b></para>
    /// </summary>
    [Fact]
    public void NoInfrastructureService_BuildsASalesInvoice_OutsideThePostingPath()
    {
        var root = Path.Combine(FindRepositoryRoot(), "src", "Brqosh.Infrastructure");

        var strays = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(f => (Path: f, Source: File.ReadAllText(f)))
            .Where(x => x.Source.Contains("SalesInvoice.CreateDraft(", StringComparison.Ordinal))
            .Where(x => !x.Source.Contains("SalesInvoicePostingService", StringComparison.Ordinal))
            .Select(x => Path.GetRelativePath(root, x.Path))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        Assert.True(strays.Count == 0,
            "ملفّاتٌ تبني فاتورةَ مبيعاتٍ ولا تمرّ بخدمة الترحيل — فتفوتها الفوترةُ الإلكترونية:\n  "
            + string.Join("\n  ", strays));
    }

    // ── جدولُ القنوات ────────────────────────────────────────────────────────

    private sealed record Channel(
        string Name, ZatcaDocumentType DocumentType, Func<Task<IReadOnlyList<long>>> RunAsync);

    private Channel[] Channels() =>
    [
        new("فاتورةُ مبيعاتٍ مباشرة", ZatcaDocumentType.SalesInvoice, DirectSaleAsync),
        new("أمرُ بيعٍ ⟵ فاتورة", ZatcaDocumentType.SalesInvoice, SalesOrderAsync),
        new("نقطةُ البيع", ZatcaDocumentType.SalesInvoice, PosSaleAsync),
        new("فوترةُ زيارةٍ طبّية (مريضٌ + تأمين)", ZatcaDocumentType.SalesInvoice, VisitBillingAsync),
        new("تصفيةُ حساب نزيل", ZatcaDocumentType.SalesInvoice, FolioCheckOutAsync),
        new("الفوترةُ الدورية", ZatcaDocumentType.SalesInvoice, RecurringAsync),
        new("إشعارٌ دائن", ZatcaDocumentType.CreditNote, CreditNoteAsync),
    ];

    private async Task<IReadOnlyList<long>> DirectSaleAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _b2bCustomerId, new DateOnly(2026, 5, 4));
        invoice.AddLine("خدمةٌ استشارية", 1m, 4_000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return [invoice.Id];
    }

    private async Task<IReadOnlyList<long>> SalesOrderAsync()
    {
        var order = SalesOrder.CreateDraft(_companyId, _b2bCustomerId, new DateOnly(2026, 5, 5));
        order.AddLine("توريدُ عدّة", 2m, 750m);
        var service = new SalesOrderService(_db);
        await service.ConfirmAsync(order);

        var invoice = await service.CreateInvoiceFromOrderAsync(_companyId, order.Id);
        return [invoice.Id];
    }

    private async Task<IReadOnlyList<long>> PosSaleAsync()
    {
        var shift = PosShift.Open(_companyId, "كاشير", 100m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();

        var result = await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 2m)], isVatRegistered: true,
            PosPaymentMethod.Cash, "admin");

        return [result.Invoice.Id];
    }

    private async Task<IReadOnlyList<long>> VisitBillingAsync()
    {
        var visit = Visit.Create(_companyId, _patientId, "V-1", new DateOnly(2026, 5, 6),
            "د. أحمد", _policyId);
        visit.AddLine("كشفٌ وعلاج", 1m, 1_000m, isCovered: true);
        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();

        var result = await new VisitBillingService(_db).BillAsync(_companyId, visit.Id);

        // **والزيارةُ تُخرج ذمّتين** — فاتورةُ المريض وفاتورةُ الشركة، وكلتاهما فاتورةُ ضريبة.
        return [.. new[] { result.PatientInvoiceId, result.PayerInvoiceId }
            .Where(id => id is not null).Select(id => id!.Value)];
    }

    private async Task<IReadOnlyList<long>> FolioCheckOutAsync()
    {
        var reservation = Reservation.Create(_companyId, _roomId, _guestCustomerId, "R-1",
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3), 500m);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();

        var folio = new FolioService(_db);
        await folio.CheckInAsync(_companyId, reservation.Id);
        var invoiceId = await folio.CheckOutAsync(_companyId, reservation.Id, new DateOnly(2026, 5, 3));
        return [invoiceId];
    }

    private async Task<IReadOnlyList<long>> RecurringAsync()
    {
        var plan = RecurringInvoicePlan.Create(_companyId, _tenantCustomerId, "RP-1", "إيجارٌ شهريّ",
            RecurrenceFrequency.Monthly, new DateOnly(2026, 5, 1));
        plan.AddLine("إيجار", 1m, 3_000m);
        _db.RecurringInvoicePlans.Add(plan);
        await _db.SaveChangesAsync();

        var result = await new RecurringInvoiceService(_db)
            .GenerateDueAsync(_companyId, new DateOnly(2026, 5, 2));

        Assert.True(result.InvoicesCreated > 0,
            "الفوترةُ الدورية لم تُولّد شيئاً. وتعذّر:\n  " + string.Join("\n  ", result.Refusals));

        return await _db.RecurringInvoiceRuns.AsNoTracking()
            .Where(r => r.CompanyId == _companyId && r.PlanId == plan.Id)
            .Select(r => r.SalesInvoiceId).ToListAsync();
    }

    private async Task<IReadOnlyList<long>> CreditNoteAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _b2bCustomerId, new DateOnly(2026, 5, 7));
        invoice.AddLine("بضاعةٌ تُرتجَع", 4m, 250m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var note = CreditNote.CreateDraft(_companyId, _b2bCustomerId, invoice.Id,
            new DateOnly(2026, 5, 8), "مرتجعُ بضاعة");
        note.AddLine("بضاعةٌ تُرتجَع", 1m, 250m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        return [note.Id];
    }

    // ── تهيئة ────────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأةُ الحارس", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        // (CEEF-229 · م2-١٣أ) هويةُ بائعٍ مكتملة — بلا هذا تُصدر منشأةٌ ناقصةٌ مستنداتٍ ترفضها الهيئة.
        await ZatcaReadySeller.CompleteAsync(_db, _companyId);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(true, ZatcaEnvironment.Simulation, "",
            ZatcaReadySeller.Certificate.CertPath, ZatcaReadySeller.Certificate.KeyPath, "");
        await _db.SaveChangesAsync();

        // **والمشترون على صنفَيهما**: مسجَّلٌ ضريبياً تُصدَر له فاتورةٌ قياسية تُعتمَد،
        // وغيرُ مسجَّلٍ تُصدَر له مبسّطةٌ تُبلَّغ — والمساران يمرّان بالحارس معاً.
        var b2b = Customer.Create(_companyId, "شركةُ المشتري", "311111111100003").WithStandardIdentity();
        var guest = Customer.Create(_companyId, "نزيل", "");
        var tenant = Customer.Create(_companyId, "مستأجر", "");
        var patientCustomer = Customer.Create(_companyId, "مريض", "");
        var payerCustomer = Customer.Create(_companyId, "بوبا العربية", "311111111100011").WithStandardIdentity();
        _db.Customers.AddRange(b2b, guest, tenant, patientCustomer, payerCustomer);
        await _db.SaveChangesAsync();

        _b2bCustomerId = b2b.Id;
        _guestCustomerId = guest.Id;
        _tenantCustomerId = tenant.Id;

        var patient = Patient.Create(_companyId, "MRN-1", "مريض", "1088888888",
            new DateOnly(1990, 1, 1), PatientGender.Male, patientCustomer.Id);
        var payer = InsurancePayer.Create(_companyId, "BUPA", "بوبا العربية", payerCustomer.Id);
        _db.Patients.Add(patient);
        _db.InsurancePayers.Add(payer);
        await _db.SaveChangesAsync();
        _patientId = patient.Id;
        _payerId = payer.Id;

        var policy = InsurancePolicy.Create(_companyId, _patientId, _payerId, "POL-1", "M-1",
            coveragePercent: 80m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        _db.InsurancePolicies.Add(policy);

        var room = Room.Create(_companyId, "201", RoomKind.Double, 500m, floor: 2);
        _db.Rooms.Add(room);

        var product = Product.Create(_companyId, "P1", "منتج", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        _policyId = policy.Id;
        _roomId = room.Id;
        _productId = product.Id;

        var warehouse = await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId);
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, warehouse.Id, "رصيدٌ افتتاحيّ");
        opening.AddLine(_productId, 100m, 40m);
        await new StockMovementPostingService(_db).PostAsync(opening);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
