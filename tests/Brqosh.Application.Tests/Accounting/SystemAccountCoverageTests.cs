using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (CEEF-229 · م2-١٢) <b>الاسمُ بلا جسد — حسابٌ في الدليل لا يكتب فيه أحد.</b>
///
/// <para>دليلُ الحسابات هو أوّلُ ما يفتحه المحاسب، وكلُّ سطرٍ فيه <b>وعد</b>. والحسابات
/// الموسومة <c>IsSystem</c> تحمل وعداً أقوى: تعليقُ القالب نفسه يقول إنها «محمية من الحذف
/// <b>لأنها مربوطة بالترحيل الآلي</b>». فحسابٌ نظاميّ لا يكتب فيه أيُّ مسارِ ترحيل هو
/// <b>اسمٌ بلا جسد</b>: يراه المالك في الدليل وفي ميزان المراجعة، ويبقى صفراً إلى الأبد،
/// ولا شيء في النظام يخبره أنّ الميزة غير موجودة.</para>
///
/// <para><b>وهذه هوية تغطية لا اختبارُ ميزة:</b> تُنفَّذ دورةُ أعمالٍ عريضة، ثمّ يُجمع
/// <b>كلُّ كودِ حسابٍ استقبل سطراً فعلاً</b> من الأستاذ، ويُقابَل بقائمة الحسابات النظامية.
/// وما لم يُغطَّ يجب أن يكون في <see cref="DeclaredUnreached"/> <b>بسببٍ مكتوب</b> —
/// فيتحوّل الموتُ الصامت إلى إقرارٍ صريح.</para>
///
/// <para><b>والقياس من الأستاذ لا من الكود:</b> وجودُ ثابتٍ باسم الحساب في
/// <c>SystemCodes</c> لا يعني أنّ أحداً يكتب فيه — والبحثُ النصّيّ يُثبت وجود الاسم لا
/// جريان المال. فلا يُحتسب الحساب مغطّى إلا إذا حمل سطرَ قيدٍ بعد تنفيذ عمليةٍ حقيقية.</para>
/// </summary>
public class SystemAccountCoverageTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public SystemAccountCoverageTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("Data Source=sysacct;Mode=Memory;Cache=Shared");
        _connection.Open();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=sysacct;Mode=Memory;Cache=Shared");
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// حساباتٌ نظاميةٌ لا تصلها الدورةُ أدناه — <b>ولكلٍّ سببٌ مكتوب</b>. وإضافةُ سطرٍ هنا
    /// قرارٌ يُوقَّع، لا تهرّبٌ من الاختبار: إمّا الميزة مُنفَّذةٌ خلف بابٍ لا يفتحه هذا العميل،
    /// وإمّا هي <b>غير منفَّذة</b> — وحينها الاعترافُ في مكانه أفضل من حسابٍ يزيّن الدليل.
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredUnreached = new()
    {
        // ── (أ) لها كاتبٌ حقيقيّ خلف وحدةٍ لا يفتحها ملفُّ التجزئة ───────────
        // ‏هذه ليست ميّتة: كودُها موجودٌ ويعمل، لكنّ ModuleCatalog لا يمنح Retail
        // ‏وحدتَي المقاولات والتصنيع. وأثرُها على مالكنا أنّها تزيّن دليلَه بأسماءٍ
        // ‏لا تخصّ عمله — وذاك بندُ عرضٍ لا بندُ صحّة.
        ["1113"] = "أعمال تحت التنفيذ — يكتبها WorkOrderService (وحدة التصنيع)، وملفُّ التجزئة لا يملكها.",
        ["1114"] = "محتجزات لدى العملاء — يكتبها ProgressBillingPostingService (وحدة المقاولات).",
        ["2105"] = "دفعات مقدمة من العملاء — كاتبُها الوحيد ProgressBillingPostingService (المقاولات).",
        ["2110"] = "محتجزات مقاولي الباطن — وحدة المقاولات.",
        ["4104"] = "إيرادات عقود المقاولات — وحدة المقاولات.",

        // ── (ب) لها كاتبٌ لا يعمل إلا بعملةٍ ثانية ──────────────────────────
        // ‏FxClosingTranslationService يكتبها، ومنشأتُنا بالريال وحده — فلا ترجمة.
        ["3106"] = "احتياطي ترجمة العملات — FxClosingTranslationService، ولا يعمل بعملةٍ واحدة.",
        ["4107"] = "أرباح فروق عملة غير محققة — إعادةُ تقييمٍ لا تجري بعملةٍ واحدة.",
        ["5217"] = "خسائر فروق عملة غير محققة — كسابقه.",

        // (CEEF-230 · ط1-٤) ‏والمحقَّقان كذلك — **ولهما كاتبٌ مُثبَتٌ بقياسه**، بخلاف
        // ‏سابقيهما: `ReceiptVoucherPostingService` يكتبهما حين يخالف سعرُ يوم التحصيل
        // ‏سعرَ يوم الفاتورة، و`RealizedFxTests` يُثبت الاتّجاهين بالأرقام ويُثبت أنّ
        // ‏الذمّةَ تُغلق تماماً. **فهذا إقرارٌ بأنّ الدورةَ العريضة بالريال، لا بأنّ
        // ‏الحسابَ بلا جسد.**
        ["4108"] =
            "أرباح فروق عملة محققة — يكتبها ReceiptVoucherPostingService عند تحصيلٍ بعملةٍ "
            + "أجنبية بسعرٍ يخالف سعرَ الفاتورة، ودورةُ التغطية هذه بالريال وحده. "
            + "والكاتبُ مُثبَتٌ في RealizedFxTests بالاتّجاهين مع صمود هـ-١.",
        ["5218"] = "خسائر فروق عملة محققة — كسابقه، وبالقياس نفسِه.",

        // ── (ج) لا كاتبَ له في المستودع كلّه — وهذا اعترافٌ لا تصنيف ────────
        ["2107"] =
            "صافي ضريبة القيمة المضافة المستحقة — **لا كاتبَ له إطلاقاً**: لا توجد خدمةُ تسويةٍ "
            + "ضريبية تُقفل 2106 و1111 في 2107 عند نهاية الفترة. فالإقرار يُحسب تقريراً ولا يصير "
            + "التزاماً في الأستاذ. وقد وُثّق في م2-١٦، وجُعل مؤشّرُ المطابقة يقول «غير متاح» بدل صفر.",
        ["5104"] =
            "تكاليف مقاولي الباطن — **لا كاتبَ له**، وثابتُه SubcontractorCosts بلا مستدعٍ واحد. "
            + "حسابٌ وثابتٌ وُضعا لميزةٍ لم تُكتب.",
        ["3101"] =
            "رأس المال — لا كاتبَ آليّ، وهذا **صحيحٌ محاسبياً**: رأسُ المال يُدخله المالك قيداً "
            + "يدوياً عند التأسيس، لا يُولّده مستند. فوجودُه في الدليل وعدٌ صادق.",
    };

    /// <summary>
    /// دورةُ أعمالٍ عريضة لمنشأةٍ تجارية بمستودعٍ واحد: شراءٌ ثمّ بيعٌ ثمّ تحصيلٌ ثمّ سدادٌ
    /// ثمّ مرتجعا بيعٍ وشراء. وكلُّها عملياتٌ يفعلها المالك في أوّل شهر.
    /// </summary>
    private async Task<HashSet<string>> RunBroadCycleAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة التغطية", "310123456700099", BusinessProfile.Retail);
        var companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var supplier = Supplier.Create(companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        var customer = Customer.Create(companyId, "عميل", "311122233300003");
        _db.Customers.Add(customer);
        var product = Product.Create(companyId, "P-1", "بضاعة", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // ── شراءٌ آجل بضريبةٍ قابلة للخصم ─────────────────────────────────
        var order = PurchaseOrder.CreateDraft(companyId, supplier.Id, new DateOnly(2026, 5, 1));
        order.AddLine("بضاعة", 100m, 60m, product.Id);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        var purchase = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            companyId, order.Id, "SUP-1", new DateOnly(2026, 5, 2), vatDeductible: true);

        // ── بيعٌ آجل ──────────────────────────────────────────────────────
        var sale = SalesInvoice.CreateDraft(companyId, customer.Id, new DateOnly(2026, 5, 3));
        sale.AddLine("بضاعة", 40m, 100m, product.Id);
        await new SalesInvoicePostingService(_db).PostAsync(sale);

        // ── تحصيلٌ نقديّ ثمّ سدادٌ بنكيّ ───────────────────────────────────
        var receipt = ReceiptVoucher.CreateDraft(
            companyId, customer.Id, new DateOnly(2026, 5, 4), 1000m, ReceiptMethod.Cash, "تحصيل");
        await new ReceiptVoucherPostingService(_db).PostAsync(receipt);

        var payment = PaymentVoucher.CreateDraft(
            companyId, supplier.Id, new DateOnly(2026, 5, 5), 2000m, ReceiptMethod.Bank, "سداد", "TRF-1");
        await new PaymentVoucherPostingService(_db).PostAsync(payment, []);

        // ── مرتجعُ مبيعات (إشعار دائن) ────────────────────────────────────
        var creditNote = CreditNote.CreateDraft(
            companyId, customer.Id, sale.Id, new DateOnly(2026, 5, 6), "بضاعة تالفة");
        creditNote.AddLine("بضاعة", 5m, 100m, product.Id);
        await new CreditNotePostingService(_db).PostAsync(creditNote);

        // ── مرتجعُ مشتريات (إشعار مدين) ───────────────────────────────────
        var debitNote = DebitNote.CreateDraft(
            companyId, supplier.Id, purchase.Id, new DateOnly(2026, 5, 7), "بضاعة مخالفة");
        debitNote.AddLine("بضاعة", 3m, 60m, product.Id);
        await new DebitNotePostingService(_db).PostAsync(debitNote);

        // ── شيكٌ واردٌ من عميل، وشيكٌ صادرٌ لمورّد ─────────────────────────
        var bankGl = await _db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "1102");
        var bank = BankAccount.Create(
            companyId, "BNK-1", "الحساب الجاري", "الراجحي", "1234567890",
            "SA0380000000608010167519", bankGl.Id, isDefault: true);
        _db.BankAccounts.Add(bank);
        await _db.SaveChangesAsync();

        var received = Cheque.CreateReceived(
            companyId, "CHQ-IN-1", new DateOnly(2026, 5, 8), 500m, "شيك عميل", bank.Id, customer.Id);
        await new ChequePostingService(_db).RegisterReceivedAsync(received);

        var issued = Cheque.CreateIssued(
            companyId, "CHQ-OUT-1", new DateOnly(2026, 5, 9), 700m, "شيك مورّد", bank.Id, supplier.Id);
        await new ChequePostingService(_db).RegisterIssuedAsync(issued);

        // ── إهلاكُ أصلٍ ثابت ───────────────────────────────────────────────
        var assetAccount = await _db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "1203");
        var depAccount = await _db.Accounts.FirstAsync(a => a.CompanyId == companyId && a.Code == "1207");
        var asset = FixedAsset.Create(
            companyId, "FA-1", "ثلاجة عرض", new DateOnly(2026, 1, 1),
            acquisitionCost: 12000m, salvageValue: 0m, usefulLifeMonths: 60,
            assetAccount.Id, depAccount.Id);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        await new DepreciationService(_db).RunMonthlyAsync(companyId, new DateOnly(2026, 5, 31), "admin");

        // ── رسمٌ بنكيّ في كشف الحساب لا يقابله مستند ──────────────────────
        var statement = BankStatement.Create(companyId, bank.Id, "statement-05.csv");
        statement.AddLine(new DateOnly(2026, 5, 20), "رسوم إدارة حساب", -25m, "FEE-1");
        _db.BankStatements.Add(statement);
        await _db.SaveChangesAsync();
        await new BankStatementDifferencePostingService(_db)
            .PostUnmatchedLineAsync(companyId, statement.Id, 0);

        // ── بيعُ الأصل الثابت (الثلاجة القديمة) ────────────────────────────
        await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 11_900m, new DateOnly(2026, 5, 25), "admin");

        // ── رواتبُ الشهر — ووحدةُ الرواتب **ضمن** ملفّ التجزئة، فالمحلّ له موظّفون ──
        var payroll = PayrollRun.CreateDraft(companyId, 2026, 5, new DateOnly(2026, 5, 28));
        payroll.AddLine("موظف المبيعات", basic: 4000m, housing: 1000m, gosiEmployee: 400m, gosiEmployer: 450m);
        await new PayrollPostingService(_db).PostAsync(payroll, "admin");

        // ── إقفالُ السنة ───────────────────────────────────────────────────
        await new YearEndCloseService(_db).CloseYearAsync(companyId, 2026, "admin");

        return await CoveredAccountCodesAsync(companyId);
    }

    /// <summary>أكوادُ الحسابات التي استقبلت سطرَ قيدٍ فعلاً — من الأستاذ نفسه.</summary>
    private async Task<HashSet<string>> CoveredAccountCodesAsync(long companyId)
    {
        var codes = await _db.JournalEntries
            .Where(e => e.CompanyId == companyId)
            .SelectMany(e => e.Lines)
            .Select(l => l.AccountCode)
            .Distinct()
            .ToListAsync();

        return [.. codes];
    }

    /// <summary>الحساباتُ النظامية غيرُ التجميعية في القالب السعودي.</summary>
    private static IReadOnlyList<SeedAccount> SystemLeafAccounts() =>
        [.. SaudiChartOfAccounts.Template.Where(a => a.IsSystem && !a.IsGroup)];

    /// <summary>
    /// <b>القياس المركزيّ.</b> كلُّ حسابٍ نظاميّ إمّا استقبل سطراً في الدورة العريضة،
    /// وإمّا هو مُقَرٌّ به في <see cref="DeclaredUnreached"/> بسببٍ مكتوب.
    /// </summary>
    [Fact]
    public async Task EverySystemAccount_EitherReceivesAPosting_OrIsDeclaredWithAReason()
    {
        var covered = await RunBroadCycleAsync();

        var undeclared = SystemLeafAccounts()
            .Where(a => !covered.Contains(a.Code) && !DeclaredUnreached.ContainsKey(a.Code))
            .Select(a => $"{a.Code} — {a.NameAr}")
            .ToList();

        Assert.True(undeclared.Count == 0,
            "حساباتٌ نظامية لا يكتب فيها أحدٌ ولا إقرارَ لها — كلٌّ منها اسمٌ بلا جسد "
            + "يراه المالك في دليله ويبقى صفراً:\n  " + string.Join("\n  ", undeclared));
    }

    /// <summary>
    /// <b>حارسٌ للحارس.</b> لو صار <see cref="DeclaredUnreached"/> يشمل كلَّ شيء لَما قاس
    /// الاختبارُ أعلاه شيئاً. فيُشترط أن تكون الدورةُ العريضة قد غطّت فعلاً حسابات المسار
    /// الأساسيّ للمنشأة التجارية: عميلٌ ومورّدٌ ومخزونٌ وإيرادٌ وتكلفةُ مبيعاتٍ وضريبتان.
    /// </summary>
    [Fact]
    public async Task TheBroadCycle_ActuallyExercisesTheRetailBackbone()
    {
        var covered = await RunBroadCycleAsync();

        string[] backbone =
        [
            SaudiChartOfAccounts.SystemCodes.AccountsReceivable,
            SaudiChartOfAccounts.SystemCodes.AccountsPayable,
            SaudiChartOfAccounts.SystemCodes.Inventory,
            SaudiChartOfAccounts.SystemCodes.SalesRevenue,
            SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold,
            SaudiChartOfAccounts.SystemCodes.VatInput,
            SaudiChartOfAccounts.SystemCodes.VatOutput,
            SaudiChartOfAccounts.SystemCodes.CashOnHand,
            SaudiChartOfAccounts.SystemCodes.CashAtBanks,
            SaudiChartOfAccounts.SystemCodes.SalesReturns,
        ];

        var missing = backbone.Where(c => !covered.Contains(c)).ToList();

        Assert.True(missing.Count == 0,
            "الدورةُ العريضة لم تمسّ العمودَ الفقريّ — فقياسُ التغطية بلا معنى. المفقود: "
            + string.Join(" · ", missing));
    }

    /// <summary>
    /// <b>والإقرارُ ينظّف نفسه.</b> إن صار حسابٌ مُقَرٌّ به مغطّى فعلاً — لأنّ أحداً نفّذ
    /// الميزة أخيراً — وجب حذفُ سطره، وإلا بقي الإقرارُ يكذب على قارئه بعد أن صدق يوماً.
    /// وهذا ما يمنع القائمة من أن تتحوّل إلى مقبرةٍ لا يراجعها أحد.
    /// </summary>
    [Fact]
    public async Task NoDeclaration_OutlivesTheGapItDeclares()
    {
        var covered = await RunBroadCycleAsync();

        var stale = DeclaredUnreached.Keys.Where(covered.Contains).ToList();

        Assert.True(stale.Count == 0,
            "إقراراتٌ بائتة — هذه الحسابات صارت تستقبل قيوداً فعلاً، فاحذف سطورها من "
            + "DeclaredUnreached: " + string.Join(" · ", stale));

        // ‏وإقرارٌ باسم حسابٍ غير موجود أسوأ من غيابه: يبدو تغطيةً وهو يشير إلى لا شيء.
        var systemCodes = SystemLeafAccounts().Select(a => a.Code).ToHashSet();
        var phantom = DeclaredUnreached.Keys.Where(c => !systemCodes.Contains(c)).ToList();

        Assert.True(phantom.Count == 0,
            "إقراراتٌ لحساباتٍ ليست في القالب أو ليست نظامية — تُوهم بتغطيةٍ لا وجود لها: "
            + string.Join(" · ", phantom));
    }

    /// <summary>
    /// <b>وحارسٌ رابع:</b> كلُّ ثابتٍ في <c>SystemCodes</c> يشير إلى حسابٍ موجودٍ في القالب.
    /// ثابتٌ يشير إلى لا شيء يُسقط الترحيل الذي يستعمله برسالةٍ غامضة وقت التشغيل.
    /// </summary>
    [Fact]
    public void EverySystemCodeConstant_PointsAtARealAccount()
    {
        var template = SaudiChartOfAccounts.Template.Select(a => a.Code).ToHashSet();

        var dangling = typeof(SaudiChartOfAccounts.SystemCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (Name: f.Name, Code: (string)f.GetRawConstantValue()!))
            .Where(x => !template.Contains(x.Code))
            .Select(x => $"{x.Name} = {x.Code}")
            .ToList();

        Assert.True(dangling.Count == 0,
            "ثوابتُ حساباتٍ تشير إلى أكوادٍ ليست في القالب: " + string.Join(" · ", dangling));
    }
}
