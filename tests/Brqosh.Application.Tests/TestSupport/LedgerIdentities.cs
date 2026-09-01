using Microsoft.EntityFrameworkCore;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;

namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · ح-٥) الهويات الجبرية — الميزان الذي كان غيابه يكلّف هذا المستودع تدقيقاً بعد تدقيق.
///
/// <para>كل هوية تحسب <b>الرقم نفسه بطريقتين مستقلّتين</b>: مرّة من الدفتر المساعد (الاستعلام الذي
/// يراه المستخدم على الشاشة)، ومرّة من الأستاذ العام (سطور القيود المرحّلة). تطابقُهما دليلٌ لا
/// صدفة، واختلافُهما يعني أن أحد الطرفين يكذب — ولا يهمّ أيّهما، فالمستخدم يرى الاثنين.</para>
///
/// <para><b>لماذا هذه بالذات:</b> أربعة عشر عطلاً في تدقيق CEEF-229 كانت انفصالاً بين هذين الطرفين
/// (الإشعارات المدينة، إشعارات نقاط البيع، المحتجز في المستخلصات، إذن الاستلام بلا قيد، مرتجع
/// المبيعات الذي لا يُرجع مخزوناً، إلغاء الحركات الافتتاحية). كلٌّ منها كان <b>سيُكشف في ثانية</b>
/// بهوية واحدة من هذه — ولا واحد منها كشفته 703 اختباراً.</para>
///
/// <para><b>الاستعمال:</b> تُستدعى في <b>ذيل كل اختبار سيناريو</b>، لا في اختبار خاص بها. الهوية
/// ليست ميزةً تُختبَر مرّة، بل شرطٌ يجب أن يصمد بعد كل عملية.</para>
///
/// <code>
/// await LedgerIdentities.AssertAllAsync(_db, company.Id);   // في ذيل السيناريو
/// </code>
///
/// <para><b>قياس بلا إسقاط:</b> استعمل <c>MeasureAsync</c> حين تريد رقم الانحراف دون أن يسقط
/// الاختبار — مفيدٌ لتوثيق حجم عطلٍ قائم قبل إصلاحه.</para>
/// </summary>
public static class LedgerIdentities
{
    /// <summary>تسامح الهللة الواحدة — أيّ فرق أكبر انفصالٌ حقيقيّ لا تقريب.</summary>
    public const decimal Tolerance = 0.01m;

    private static readonly DateOnly Dawn = new(2000, 1, 1);
    private static readonly DateOnly Dusk = new(2099, 12, 31);

    /// <summary>
    /// نتيجة هوية واحدة. <see cref="Subsidiary"/> من الدفتر المساعد و<see cref="Ledger"/> من
    /// الأستاذ العام — والاسمان مقصودان: أيّهما «الصحيح» سؤالٌ لا تجيب عنه الهوية، وإنما تكشف
    /// أنهما افترقا.
    /// </summary>
    public readonly record struct IdentityResult(string Name, decimal Subsidiary, decimal Ledger)
    {
        public decimal Difference => Math.Round(Subsidiary - Ledger, 2);

        public bool Holds => Math.Abs(Difference) <= Tolerance;

        public override string ToString() =>
            $"{Name}: الدفتر المساعد {Subsidiary:N2} · الأستاذ {Ledger:N2} · الفرق {Difference:N2}";
    }

    // ── هـ-١ · الذمم المدينة ────────────────────────────────────────────────

    /// <summary>مجموع أرصدة العملاء == رصيد حساب 1104 في الأستاذ.</summary>
    public static async Task<IdentityResult> ReceivablesAsync(BrqoshDbContext db, long companyId)
    {
        var balances = await new CustomerBalanceQuery(db).AllBalancesAsync(companyId);
        var subsidiary = balances.Values.Sum();
        var ledger = await SignedLedgerBalanceAsync(db, companyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
        return new IdentityResult("هـ-١ الذمم المدينة (1104)", Round(subsidiary), ledger);
    }

    // ── هـ-٢ · الذمم الدائنة ────────────────────────────────────────────────

    /// <summary>مجموع أرصدة الموردين == رصيد حساب 2101 في الأستاذ (بإشارة دائنة موجبة).</summary>
    public static async Task<IdentityResult> PayablesAsync(BrqoshDbContext db, long companyId)
    {
        var balances = await new SupplierBalanceQuery(db).AllBalancesAsync(companyId);
        var subsidiary = balances.Values.Sum();
        // 2101 حساب دائن الطبيعة: رصيده الموجب دائن، فتُعكس الإشارة ليُقارَن بدفتر موجب.
        var ledger = -await SignedLedgerBalanceAsync(db, companyId, SaudiChartOfAccounts.SystemCodes.AccountsPayable);
        return new IdentityResult("هـ-٢ الذمم الدائنة (2101)", Round(subsidiary), ledger);
    }

    // ── هـ-٣ · المخزون ──────────────────────────────────────────────────────

    /// <summary>مجموع قيمة المخزون == رصيد حساب 1107 في الأستاذ.</summary>
    public static async Task<IdentityResult> InventoryAsync(BrqoshDbContext db, long companyId)
    {
        var rows = await new StockBalanceQuery(db).ExecuteAsync(companyId, includeZeroBalances: false);
        var subsidiary = rows.Sum(r => r.TotalValue);
        var ledger = await SignedLedgerBalanceAsync(db, companyId, SaudiChartOfAccounts.SystemCodes.Inventory);
        return new IdentityResult("هـ-٣ المخزون (1107)", Round(subsidiary), ledger);
    }

    // ── هـ-٤ · التدفقات النقدية ─────────────────────────────────────────────

    /// <summary>
    /// التشغيل + الاستثمار + التمويل == صافي التغيّر النقديّ.
    ///
    /// هذه الهوية تُسقط <b>القائمة البيضاء</b> بنيوياً: أيّ حساب يُضاف إلى النظام ولا يُصنَّف في
    /// أحد الأقسام الثلاثة يُحدث فرقاً فوراً — وهو ما لا يستطيع أيّ اختبار موضعيّ كشفه.
    /// </summary>
    public static async Task<IdentityResult> CashFlowAsync(
        BrqoshDbContext db, long companyId, DateOnly from, DateOnly to)
    {
        var statement = await new CashFlowStatementService(db).ExecuteAsync(companyId, from, to);
        var sections = statement.Operating.Subtotal + statement.Investing.Subtotal + statement.Financing.Subtotal;
        return new IdentityResult("هـ-٤ التدفقات النقدية", Round(sections), Round(statement.NetCashChange));
    }

    // ── هـ-٨ · استحقاقُ البضاعة المستلمة غير المفوترة ───────────────────────

    /// <summary>
    /// (CEEF-229 · م1-٢٠) <b>2112 == قيمةُ ما استُلم ولم يُفوتَر بعد.</b>
    ///
    /// <para>الطرفُ المساعد يُحسب من المستندات: لكلّ أمر شراء وصنف، <c>الكميةُ المستلمة −
    /// الكميةُ المفوترة</c> مضروبةً في متوسط سعر الاستلام. والطرفُ الآخر رصيدُ 2112 في الأستاذ.</para>
    ///
    /// <para><b>وهي الهويةُ الوحيدة التي تكشف بقايا الاستحقاق.</b> فحين يختلف سعرُ الفاتورة
    /// عن سعر الاستلام، يبقى الفرقُ في 2112 <b>بلا كمّيةٍ تقابله</b> — التزامٌ وهميٌّ لا يُقفله
    /// شيءٌ أبداً. ولا تكشفه هـ-٢ (ذمّةُ المورد صادقة) ولا هـ-٣ (المخزونُ لم يُمَسّ مع وجود
    /// إذن)، <b>والقيدُ متوازنٌ تماماً</b>. مقيسٌ: استُلمت 10 بـ10 وفُوتِرت 10 بـ9.9 ⟵ يبقى ريال.</para>
    ///
    /// <para><b>ولا تُدرَج في <see cref="AssertAllAsync"/></b>: تلك تُستدعى في ذيل كلّ سيناريو
    /// بما فيها ما لا شراءَ فيه، وهذه تلزمها مستنداتُ شراءٍ لتقول شيئاً. فتُستدعى في ذيل
    /// سيناريوهات الشراء وحدها — <b>واستدعاؤها حيث لا استحقاق يجعلها صفراً == صفر، وهو
    /// أخضرُ لا يقول شيئاً</b>.</para>
    /// </summary>
    public static async Task<IdentityResult> GoodsReceivedNotInvoicedAsync(
        BrqoshDbContext db, long companyId)
    {
        // ‏التجميعُ في الذاكرة لا في SQL: إسقاطُ حقلٍ من الأب داخل SelectMany يلزمه APPLY
        // ‏ولا يدعمه SQLite. وحجمُ البيانات في اختبارٍ لا يجعل ذلك ثمناً.
        var receipts = await db.GoodsReceipts
            .Include(g => g.Lines)
            .Where(g => g.CompanyId == companyId && g.Status == GoodsReceiptStatus.Posted)
            .ToListAsync();

        var invoices = await db.PurchaseInvoices
            .Include(i => i.Lines)
            .Where(i => i.CompanyId == companyId && i.Status == PurchaseInvoiceStatus.Posted
                        && i.PurchaseOrderId != null)
            .ToListAsync();

        var invoiced = invoices
            .SelectMany(i => i.Lines
                .Where(l => l.ProductId != null)
                .Select(l => (Key: (i.PurchaseOrderId!.Value, l.ProductId!.Value), l.Quantity)))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var receiptLines = receipts
            .SelectMany(g => g.Lines
                .Where(l => l.ProductId != null)
                .Select(l => (Key: (g.PurchaseOrderId, l.ProductId!.Value), l.ReceivedQuantity, l.UnitCost)));

        var subsidiary = 0m;
        foreach (var group in receiptLines.GroupBy(l => l.Key))
        {
            var receivedQuantity = group.Sum(l => l.ReceivedQuantity);
            if (receivedQuantity <= 0) continue;

            var receivedValue = group.Sum(l =>
                Math.Round(l.ReceivedQuantity * l.UnitCost, 2, MidpointRounding.AwayFromZero));

            var remaining = Math.Max(0m, receivedQuantity - invoiced.GetValueOrDefault(group.Key));
            subsidiary += Math.Round(
                receivedValue * remaining / receivedQuantity, 2, MidpointRounding.AwayFromZero);
        }

        // ‏الاستحقاقُ التزامٌ، فرصيدُه في الأستاذ دائنٌ أيْ سالبٌ بالتوقيع. والمساعدُ يُقارَن بقيمته.
        var ledger = await SignedLedgerBalanceAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.GoodsReceivedNotInvoiced);

        return new IdentityResult("هـ-٨ بضاعة مستلمة غير مفوترة (2112)", Round(subsidiary), Round(-ledger));
    }

    // ── التجميع ─────────────────────────────────────────────────────────────

    // ── هـ-٩ · الدفعاتُ مقابلَ رصيد المخزون ─────────────────────────────────

    /// <summary>
    /// (CEEF-230 · ط1-٣) <b>مجموعُ دفعات صنفٍ متتبَّعٍ في مستودعٍ == رصيدُه فيه.</b>
    ///
    /// <para>وهي هـ-٣ نازلةً درجةً: تلك تربط <b>قيمةَ</b> المخزون بالأستاذ، وهذه تربط
    /// <b>تفصيلَ الكمّية على الدفعات</b> بمجملها. <b>وانفصالُهما هو العطلُ الذي يجعل
    /// تتبّعَ الصلاحية كذبة</b>: رصيدٌ مئةُ حبّةٍ ودفعاتٌ مجموعُها ثمانون يعني عشرين حبّةً
    /// في المستودع <b>لا يعرف النظامُ متى تنتهي</b>.</para>
    ///
    /// <para><b>وتُحسب للأصناف المتتبَّعة وحدَها</b>: غيرُ المتتبَّع لا دفعاتٍ له بحكم
    /// المَخنَق، فإقحامُه يجعل الهويّةَ تسقط على كلّ منشأةٍ عاديّة — <b>وهويّةٌ تسقط دائماً
    /// لا تُقرأ، فتُطفأ</b>.</para>
    ///
    /// <para><b>ولا تُدرَج في <see cref="AssertAllAsync"/></b> لأنّها تقيس الكمّيات لا
    /// القيم، وذيلُ كلّ سيناريو ليس موضعَها: تُستدعى في اختبارات التتبّع بعينها.</para>
    /// </summary>
    public static async Task<IdentityResult> TrackedBatchesAsync(BrqoshDbContext db, long companyId)
    {
        var trackedIds = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId && p.Tracking != InventoryTracking.None)
            .Select(p => p.Id)
            .ToListAsync();

        var balances = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.CompanyId == companyId && trackedIds.Contains(b.ProductId))
            .SumAsync(b => (decimal?)b.QuantityOnHand) ?? 0m;

        var batches = await db.StockBatches.AsNoTracking()
            .Where(b => b.CompanyId == companyId && trackedIds.Contains(b.ProductId))
            .SumAsync(b => (decimal?)b.Quantity) ?? 0m;

        return new IdentityResult("هـ-٩ دفعاتُ الأصناف المتتبَّعة", Round(batches), Round(balances));
    }

    /// <summary>يقيس الهويات الثلاث الرصيدية بلا إسقاط — لتوثيق حجم انفصالٍ قائم.</summary>
    public static async Task<IReadOnlyList<IdentityResult>> MeasureAsync(BrqoshDbContext db, long companyId) =>
    [
        await ReceivablesAsync(db, companyId),
        await PayablesAsync(db, companyId),
        await InventoryAsync(db, companyId),
    ];

    /// <summary>
    /// يفرض الهويات الثلاث الرصيدية معاً. يُستدعى في <b>ذيل كل اختبار سيناريو</b>.
    /// ورسالة الفشل تحمل الطرفين والفرق — فيُقرأ العطل من السطر الأول بلا تنقيب.
    /// </summary>
    public static async Task AssertAllAsync(BrqoshDbContext db, long companyId)
    {
        var results = await MeasureAsync(db, companyId);
        var broken = results.Where(r => !r.Holds).ToList();
        if (broken.Count == 0)
            return;

        var detail = string.Join("\n  ", broken.Select(r => r.ToString()));
        throw new LedgerIdentityException(
            $"انفصل الدفتر المساعد عن الأستاذ في {broken.Count} هوية:\n  {detail}");
    }

    /// <summary>يفرض هويةً واحدة بعينها.</summary>
    public static void Assert(IdentityResult result)
    {
        if (!result.Holds)
            throw new LedgerIdentityException($"لم تصمد الهوية — {result}");
    }

    // ── الداخل ──────────────────────────────────────────────────────────────

    /// <summary>
    /// رصيد الحساب من الأستاذ بإشارة مدينة موجبة (مدين − دائن).
    /// يُقرأ من <see cref="TrialBalanceQuery"/> نفسه الذي يراه المستخدم — فالهوية تقارن ما يراه
    /// بما يراه، لا ما يحسبه الاختبار بنفسه. حارسٌ يعيد حساب الطرفين بيده يُثبت رياضياته لا النظام.
    /// </summary>
    private static async Task<decimal> SignedLedgerBalanceAsync(
        BrqoshDbContext db, long companyId, string accountCode)
    {
        var rows = await new TrialBalanceQuery(db).ExecuteAsync(companyId, Dawn, Dusk);
        var row = rows.FirstOrDefault(r => r.AccountCode == accountCode);
        if (row is null)
            return 0m;
        return Round(row.TotalDebit - row.TotalCredit);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// يُرمى حين ينفصل الدفتر المساعد عن الأستاذ. نوعٌ مستقلّ لا <c>Assert.Equal</c> عادية: انفصال
/// الدفترين فئةُ عطلٍ بذاتها، وتمييزها في المخرجات يجعل السبب مقروءاً من سطر الفشل الأول.
/// </summary>
public sealed class LedgerIdentityException(string message) : Exception(message);
