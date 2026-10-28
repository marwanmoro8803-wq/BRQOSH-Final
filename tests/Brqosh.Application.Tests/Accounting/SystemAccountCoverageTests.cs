using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Logistics;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Domain.Logistics;
using Brqosh.Domain.Hospitality;
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
        // ‏(جاهزيّةُ الإطلاق · ح-٩٨) ‏وكان سببُ 1113 «يكتبها WorkOrderService» — أمرُ الإنتاج كان يمرّ بأصل عقود المقاولات.
        ["1113"] = "أعمال تحت التنفيذ (مستخلصات غير مفوترة) — يكتبها ContractRevenueTrueUp (وحدة المقاولات).",
        ["1119"] = "إنتاج تحت التشغيل — يكتبه WorkOrderService (وحدة التصنيع)، وملفُّ التجزئة لا يملكها؛ ويُقاس في ProductionWipIsItsOwnAccountTests.",
        ["1114"] = "محتجزات لدى العملاء — يكتبها ProgressBillingPostingService (وحدة المقاولات).",
        ["2105"] = "دفعات مقدمة من العملاء — كاتبُها الوحيد ProgressBillingPostingService (المقاولات).",
        ["2110"] = "محتجزات مقاولي الباطن — وحدة المقاولات.",
        ["4104"] = "إيرادات عقود المقاولات — وحدة المقاولات.",

        // ── (ب) لها كاتبٌ لا يعمل إلا بعملةٍ ثانية ──────────────────────────
        // (جاهزيّةُ الإطلاق · ح-٤٦) ‏**وكان سببُ 3106 «يكتبها FxClosingTranslationService»** —
        // ‏وكتابتُها هي العطل: فرقُ الترجمة عرضٌ في القوائم المُترجَمة لا قيدٌ في دفاتر المنشأة
        // ‏(IAS 21). **فلا كاتبَ لها في دفترٍ عمداً**، والمركزُ الموحَّدُ يعرض الاحتياطيَ صفّاً
        // ‏مشتقّاً بهذا الرمز (ح-٤٥). ويبقى الحسابُ في الدليل لأنّ نزعَه من دليلٍ قائمٍ تغييرٌ آخر.
        ["3106"] =
            "احتياطي ترجمة العملات — لا يُكتب في دفاتر المنشأة عمداً (IAS 21): الترجمةُ عرضٌ لا قيد، "
            + "والمركزُ الموحَّدُ يعرضه صفّاً مشتقّاً. وكان FxClosingTranslationService يكتبه — وذاك "
            + "العطلُ الذي أُغلق في ح-٤٦.",

        // (CEEF-230 · ط4-٧) ‏**وصار لهما كاتبٌ مُثبَتٌ بالأرقام**: كان السببُ «إعادةُ تقييمٍ
        // ‏لا تجري بعملةٍ واحدة» — وكان صادقاً في حرفه ناقصاً في معناه: **لم تكن تجري
        // ‏على الذمم أصلاً بأيّ عملة**، فالتقييمُ كان للأرصدة البنكية وحدَها.
        // ‏و`MonetaryRevaluationService` يكتبهما الآن بمعيار المحاسبة الدولي 21،
        // ‏و`MonetaryRevaluationTests` يُثبت الاتّجاهين ويُثبت صمودَ هـ-١ وهـ-٢.
        ["4107"] =
            "أرباح فروق عملة غير محققة — يكتبها MonetaryRevaluationService عند إعادة تقييم "
            + "الذمم بالعملات آخرَ الفترة، ودورةُ التغطية هذه بالريال وحده. والكاتبُ مُثبَتٌ "
            + "في MonetaryRevaluationTests بالاتّجاهين.",
        ["5217"] = "خسائر فروق عملة غير محققة — كسابقه، وبالقياس نفسِه.",

        // (CEEF-230 · ط4-٧) ‏حسابا تسوية إعادة التقييم — **ووجودُهما هو ما يحفظ هـ-١ وهـ-٢**:
        // ‏التقييمُ يُقيَّد عليهما لا على حسابَي الذمم، **فلا ينفصل الدفترُ المساعد عن الأستاذ
        // ‏في كلّ إقفال**. ودورةُ التغطية بالريال فلا تُقيَّد عليهما.
        ["1115"] =
            "تسوية إعادة تقييم ذمم مدينة — يكتبها MonetaryRevaluationService، ودورةُ التغطية "
            + "بالريال وحده. ووجودُ الحساب هو ما يمنع كسرَ هـ-١ عند التقييم.",
        ["2115"] = "تسوية إعادة تقييم ذمم دائنة — كسابقه، ويحرس هـ-٢.",

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

        // (CEEF-230 · ط3-١) ‏تأميناتُ المستأجرين — **ولها كاتبٌ مُثبَتٌ بالأرقام**:
        // ‏`SecurityDepositService` يقبضها التزاماً ويُغلقها كاملاً عند الردّ، و`LeaseTests`
        // ‏يُثبت أنّها لا تمسّ الإيرادَ بريال وأنّ المخصوم وحده يصير دخلاً. **ودورةُ التغطية
        // ‏هذه ملفُّ تجزئةٍ بلا عقود إيجار** — فهذا إقرارٌ بحدود الدورة لا بحسابٍ بلا جسد.
        ["2113"] =
            "تأمينات مستأجرين محتفظ بها — يكتبها SecurityDepositService في قطاع العقارات، "
            + "ودورةُ التغطية هذه ملفُّ تجزئةٍ لا عقودَ إيجارَ فيه. والكاتبُ مُثبَتٌ في "
            + "LeaseTests: قبضٌ يُنشئ التزاماً لا إيراداً، وردٌّ يُغلقه كاملاً.",

        // (CEEF-230 · ط3-٤) ‏تحصيلٌ نيابةً عن التجّار — **وكاتبُه مُثبَتٌ بالأرقام**:
        // ‏`CodSettlementService` يقيّده التزاماً عند التحصيل ويُغلقه عند التوريد،
        // ‏و`CodSettlementTests` يُثبت أنّه لا يمسّ الإيرادَ بريال. **ودورةُ التغطية هذه
        // ‏ملفُّ تجزئةٍ بلا بوالص.**
        ["2114"] =
            "تحصيلٌ نيابةً عن التجّار — يكتبها CodSettlementService في قطاع اللوجستيات، "
            + "ودورةُ التغطية هذه ملفُّ تجزئةٍ لا بوالصَ فيه. والكاتبُ مُثبَتٌ في "
            + "CodSettlementTests: تحصيلٌ يُنشئ التزاماً لا إيراداً، وتوريدٌ يُغلقه.",

        // (جاهزيّةُ الإطلاق · ح-٧٩) ‏إيراداتُ الإيجار المقبوضة مقدّماً — **ولها كاتبٌ مُثبَتٌ بالأرقام**:
        // ‏`LeaseRentRecognition` يُؤجّل فاتورةَ الدورة ويكتسبها شهراً بشهر، و`RentBilledInAdvanceIsEarnedOverItsPeriodTests`
        // ‏يُثبت الرصيدَ في كلّ شهر. **ودورةُ التغطية هذه بلا عقود إيجار** — إقرارٌ بحدود الدورة كما في 2113.
        ["2117"] =
            "إيرادات إيجار مقبوضة مقدّماً — يكتبها LeaseRentRecognition لعقود الإيجار الربعيّة فما فوق، "
            + "ودورةُ التغطية هذه لا عقودَ إيجارَ فيها. والكاتبُ مُثبَتٌ في RentBilledInAdvanceIsEarnedOverItsPeriodTests: "
            + "ربعٌ يُكتسب ألفاً لكلّ شهر، وسنويٌّ يترك نصفَه مقبوضاً مقدّماً آخرَ السنة.",

        // (جاهزيّةُ الإطلاق · ح-٨٢) ‏إيراداتُ الإقامة المستحقّة غيرُ المفوترة — **ولها كاتبٌ مُثبَتٌ بالأرقام**: `RoomNightAccrual`
        // ‏يستحقّ الليالي في شهرها وتُطلقها المغادرة، و`InHouseNightsReachTheLedgerInTheirMonthTests` يُثبت الرصيدَ قبلها وبعدها.
        // ‏**ودورةُ التغطية هذه بلا فندق** — إقرارٌ بحدود الدورة كما في 2113.
        ["1118"] =
            "إيرادات إقامة مستحقة غير مفوترة — يكتبها RoomNightAccrual عند تقييد الليالي في قطاع الفنادق، "
            + "ودورةُ التغطية هذه لا فندقَ فيها. والكاتبُ مُثبَتٌ في InHouseNightsReachTheLedgerInTheirMonthTests: "
            + "ليالي ديسمبر ١٬٥٠٠ إيراداً لديسمبر ومستحقّاً، والمغادرةُ في يناير تُعيده صفراً.",

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

        // (Phase 7) ‏مجمّعاتُ الإهلاك لكلّ فئة — يكتبها تشغيلُ الإهلاك لأصلٍ اختيرت له، ودورةُ التغطية أصلُها على 1207 العامّ.
        ["12071"] = DeclaredPerClassDepreciation,
        ["12072"] = DeclaredPerClassDepreciation,
        ["12073"] = DeclaredPerClassDepreciation,
        ["12074"] = DeclaredPerClassDepreciation,
        ["12075"] = DeclaredPerClassDepreciation,
        ["12076"] = DeclaredPerClassDepreciation,
        ["2118"] =
            "ضريبة الاستقطاع المستحقة — **لا كاتبَ آليّ بعد**: يُقيَّد عليها يدوياً أو بدورٍ مُسنَد (WithholdingTaxPayable) "
            + "عند الدفع لغير المقيم، إلى أن تُكتب خدمةُ الاستقطاع.",
        ["5221"] =
            "تكاليف التمويل — **لا كاتبَ آليّ بعد**: فوائدُ القروض وعمولاتُ التمويل تُقيَّد يدوياً أو بدورٍ مُسنَد (FinanceCosts) "
            + "إلى أن تُكتب خدمةُ القروض.",
    };

    private const string DeclaredPerClassDepreciation =
        "مجمّع إهلاك فئة أصول — يكتبه تشغيلُ الإهلاك متى اختير حساباً لمجمّع الأصل (الافتراضيُّ من AccumulatedDepreciationFor)، "
        + "ودورةُ التغطية هذه أصلُها على 1207 العامّ.";

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

        // ── تكلفةٌ إضافيّةٌ على الحساب (شحنٌ وجمارك) — و2104 يُدان بها ──────
        //
        // ‏(جاهزيّةُ الإطلاق · ح-٢٧) ‏**والمقابلُ مصروفٌ مستحقّ لا ذمّةُ مورِّد**:
        // ‏كان يُدائن `2101` — حسابَ مراقبة الموردين — **بلا مورِّدٍ البتّة**، فتنكسر
        // ‏الهويّةُ هـ-٢ كسراً دائماً. وشحنٌ لم تَرِد فاتورتُه التزامٌ مستحقّ.
        await new LandedCostAllocationService(_db).AllocateAsync(
            companyId, new DateOnly(2026, 5, 26), "شحن وجمارك", 300m,
            [(product.Id, (await _db.Warehouses.FirstAsync(w => w.CompanyId == companyId)).Id, 1m)],
            "admin", creditCash: false);

        // ── بيعُ الأصل الثابت (الثلاجة القديمة) ────────────────────────────
        await new FixedAssetDisposalPostingService(_db)
            .PostDisposalAsync(asset, saleProceeds: 11_900m, new DateOnly(2026, 5, 25), "admin");

        // ── الموظّفُ أوّلاً — فالرواتبُ تُبنى من سجلّه لا من أصابعِ كاتب ──────
        //
        // ‏(CEEF-231 · ط2-٧) ‏وكانت الدورةُ هنا تُبنى بـ`AddLine` نصّاً حرّاً — **وهو
        // ‏المسارُ الذي لا يسلكه مستخدم**. فتُقاس الرواتبُ بطريقٍ غيرِ طريقها، ويبقى ما
        // ‏يمرّ بالمعرّف (ملفُّ الأجور · القسيمة · السُّلَف) بلا قياسٍ في هذه الدورة.
        var employee = Employee.Create(companyId, "EMP-1", "موظف المبيعات", "1012345678",
            EmployeeNationality.Saudi, "بائع", new DateOnly(2023, 1, 1));
        employee.SetPay(basic: 4000m, housing: 1000m, transport: 0m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        // ── سُلفةٌ تُصرف ثمّ تُستقطَع — والحسابُ 1116 يُدين ويُدان ────────────
        //
        // ‏(CEEF-230 · ط2-٥) ‏وهي من صلب الدورة لا استثناءٍ منها: **منشأةٌ لها موظّفون
        // ‏تُسلّفهم**. والصرفُ أصلٌ، والاستقطاعُ يُعفيه — فلولا الاثنين معاً لبقي الحسابُ
        // ‏«اسماً بلا جسد» يراه المالكُ في دليله ويبقى صفراً.
        var advance = EmployeeAdvance.Create(companyId, employee.Id, "ADV-001",
            new DateOnly(2026, 5, 1), amount: 1_000m, installmentAmount: 1_000m);
        _db.EmployeeAdvances.Add(advance);
        await _db.SaveChangesAsync();

        await new EmployeeAdvanceService(_db).DisburseAsync(
            companyId, advance.Id, new DateOnly(2026, 5, 2),
            SaudiChartOfAccounts.SystemCodes.CashOnHand);

        // ── رواتبُ الشهر — ووحدةُ الرواتب **ضمن** ملفّ التجزئة، فالمحلّ له موظّفون ──
        var payroll = await new PayrollRunBuilder(_db)
            .BuildAsync(companyId, 2026, 5, new DateOnly(2026, 5, 28));
        await new PayrollPostingService(_db).PostAsync(payroll, "admin");

        // ── مكافأةُ نهاية الخدمة — التزامٌ يتراكم بمجرّد وجود موظّف ──────────
        //
        // ‏(CEEF-230 · ط2-٣) ‏وأُضيفت إلى الدورة **لا إلى قائمة الإقرار**: التخصيصُ يقع
        // ‏في كلّ منشأةٍ لها موظّفون، **فهو من صلب دورة المحلّ لا استثناءٍ منها**. وحسابُ
        // ‏المخصّص `2202` كان في الشجرة بلا كاتبٍ منذ اليوم الأوّل.
        await new EndOfServiceAccrualService(_db)
            .RunMonthlyAsync(companyId, new DateOnly(2026, 5, 31), "admin");

        // ── تخصيصُ الإجازات — التزامٌ يتراكم كلَّ يومٍ كذلك ─────────────────
        //
        // ‏(CEEF-233 · ز-٣) ‏وأُضيف إلى الدورة للسبب نفسِه: **رصيدُ الإجازات دَينٌ نقديٌّ
        // ‏بنصّ المادة 111** يقع في كلّ منشأةٍ لها موظّفون. وحسابا `2203` و`5220`
        // ‏**أُنشئا في هذه الحملة**، فلولا كاتبٌ لهما لكانا في الشجرة بلا حركةٍ من يومهما.
        await new LeaveAccrualService(_db)
            .RunMonthlyAsync(companyId, new DateOnly(2026, 5, 31), "admin");

        // ── إقفالُ السنة ───────────────────────────────────────────────────
        await new YearEndCloseService(_db).CloseYearAsync(companyId, 2026, "admin");

        return await CoveredAccountCodesAsync(companyId);
    }

    /// <summary>أكوادُ الحسابات التي استقبلت سطرَ قيدٍ فعلاً — من الأستاذ نفسه.</summary>
    /// <summary>
    /// (CEEF-230) <b>دورةٌ ثانيةٌ لمنشأةٍ قطاعية — ولمَ لم تُضَف إلى دورة المحلّ.</b>
    ///
    /// <para><b>ومحلُّ التجزئة لا يجمع إكراميات ولا يُسيّر رحلات.</b> فإقحامُهما في دورته
    /// <b>يُخضّر الحسابين بطريقٍ لا يسلكه صاحبُ ذلك الملفّ</b> — وهو الغشُّ نفسُه الذي
    /// تمنعه هذه الحزمة: <b>قياسٌ بمسارٍ غير مسارِه</b>.</para>
    ///
    /// <para><b>وكانا مُقَرَّين في <c>DeclaredUnreached</c></b> بسببٍ صادقٍ في حرفه —
    /// «لهما كاتبان، وملفُّ التجزئة لا يفتح وحدتَيهما». <b>والإقرارُ الصادق أضعفُ من
    /// القياس</b>: لا يقول إنّ الكاتبَ يكتب في الحساب الصحيح، بل إنّ أحداً لم ينظر.
    /// <b>فبُنيت لهما دورتُهما</b> وأُسقط الإقرار.</para>
    /// </summary>
    private async Task<HashSet<string>> RunSectorCycleAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة قطاعية", "310123456700110", BusinessProfile.Hospitality);
        var companyId = company.Id;

        var waiter = Employee.Create(companyId, "EMP-T1", "نادل", "1099999999",
            EmployeeNationality.Saudi, "نادل", new DateOnly(2025, 1, 1));
        waiter.SetPay(basic: 4000m, housing: 0m, transport: 0m);
        _db.Employees.Add(waiter);

        var shipper = Customer.Create(companyId, "تاجر", "311122233300004");
        _db.Customers.Add(shipper);
        await _db.SaveChangesAsync();

        // ── إكرامياتٌ تُحصَّل ثمّ تُصرف — و2116 يُدان ثمّ يُدين ──────────────
        var pool = TipPool.Open(companyId, new DateOnly(2026, 5, 10));
        pool.Collect(TipSource.Cash, 300m);
        pool.DistributeEvenly([waiter.Id]);
        _db.TipPools.Add(pool);
        await _db.SaveChangesAsync();

        var tips = new TipPoolService(_db);
        await tips.CloseAndPostAsync(companyId, pool.Id, new DateOnly(2026, 5, 10));
        await tips.PayOutAsync(companyId, pool.Id, new DateOnly(2026, 5, 12));

        // ── عهدةُ رحلةٍ تُسلَّم ثمّ تُسوَّى — و1117 يُدان ثمّ يُفرَّغ ─────────
        var shipment = Shipment.Create(companyId, "WB-COV1", shipper.Id,
            consigneeName: "مستلم", destinationCity: "جدة",
            shipDate: new DateOnly(2026, 5, 15), freightCharge: 400m);
        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();

        var trip = DeliveryTrip.Create(companyId, "TR-COV1", "الرياض — جدة", "COV-1234",
            new DateOnly(2026, 5, 15));
        trip.LoadShipment(shipment.Id, shipment.Status);
        _db.DeliveryTrips.Add(trip);
        await _db.SaveChangesAsync();

        var trips = new DeliveryTripService(_db);
        await trips.AdvanceFloatAsync(companyId, trip.Id, 500m, new DateOnly(2026, 5, 15));

        trip.Dispatch();
        trip.AddExpense(TripExpenseKind.Fuel, 400m, fromDriverFloat: true);
        trip.Complete(new DateOnly(2026, 5, 17));
        await _db.SaveChangesAsync();

        await trips.SettleAsync(companyId, trip.Id, 100m, new DateOnly(2026, 5, 17));

        return await CoveredAccountCodesAsync(companyId);
    }

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
        // ‏**والقطاعيةُ أوّلاً**: دورةُ المحلّ تُقفل السنةَ في ذيلها، **وإقفالُ الفترة
        // ‏يمنع أيَّ ترحيلٍ بعده** — فلو تلتها لَسقطت بـ«الفترة مقفلة» لا بعطلٍ حقيقيّ.
        var covered = await RunSectorCycleAsync();
        covered.UnionWith(await RunBroadCycleAsync());

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
