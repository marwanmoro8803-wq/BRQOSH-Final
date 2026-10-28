using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Reporting;

public record CashFlowLine(string Label, decimal Amount);

public record CashFlowSection(string NameAr, IReadOnlyList<CashFlowLine> Lines)
{
    public decimal Subtotal => Lines.Sum(l => l.Amount);
}

public record CashFlowStatementResult(
    DateOnly FromDate,
    DateOnly ToDate,
    CashFlowSection Operating,
    CashFlowSection Investing,
    CashFlowSection Financing,
    decimal NetCashChange,
    decimal OpeningCash,
    decimal ClosingCash)
{
    /// <summary>سطرُ المطابقة: صافي التغيّر النقديّ ناقصَ مجموع الأقسام — صفرٌ في قائمةٍ سليمة، وغيرُه يُعرض ولا يُخفى.</summary>
    public decimal UnreconciledDifference =>
        NetCashChange - (Operating.Subtotal + Investing.Subtotal + Financing.Subtotal);
}

/// <summary>قائمة التدفقات النقدية — طريقة غير مباشرة مبسطة من أرصدة الحسابات.</summary>
public class CashFlowStatementService(BrqoshDbContext db)
{
    private static readonly string[] CashCodes = ["1101", "1102", "1103"];

    /// <summary>مجمّع الإهلاك وأبناؤه — بند تعديلٍ غير نقديّ، لا استثمار.</summary>
    private const string DepreciationPrefix = "1207";

    public async Task<CashFlowStatementResult> ExecuteAsync(long companyId, DateOnly fromDate, DateOnly toDate)
    {
        var dayBefore = fromDate.AddDays(-1);
        var startBalances = await BuildBalancesAsync(companyId, dayBefore);
        var endBalances = await BuildBalancesAsync(companyId, toDate);

        var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, fromDate, toDate);
        var operating = new List<CashFlowLine>
        {
            new("صافي الدخل", pnl.NetIncome),
        };

        // (CEEF-228) كان `invert: true` يقلب الإشارة مرّة ثانية.
        // 1207 «مجمع الإهلاك» مصنّف Asset ورصيده دائن، فـSignedBalance سالب:
        //   إهلاك 1,000 -> endBal = -1000 -> delta = -1000 -> cashEffect = -delta = +1000
        // وهو الردّ الصحيح لمصروف غير نقديّ. ثم كان invert يجعله -1000، فيُطرح الإهلاك
        // مرّتين: مرّةً داخل صافي الدخل ومرّةً هنا.
        // (CEEF-229 · م2-١٤) بالبادئة لا بالمساواة — عرفٌ محاسبيّ شائع أن يكون لكل فئة
        // أصلٍ مجمّعُ إهلاكٍ خاصّ (120701، 120702). وبالمساواة التامّة كان ردُّ الإهلاك
        // يساوي صفراً عندئذٍ، فيُطرح الإهلاك كأنه نقدٌ خرج — وهو عين عطل SumCash.
        var depreciation = WorkingCapitalEffect(
            startBalances, endBalances, DepreciationPrefix, _ => false);
        if (depreciation != 0m)
            operating.Add(new("إهلاك الأصول (تعديل غير نقدي)", depreciation));

        var namedWorkingCapital = new List<string>();

        foreach (var (code, label) in new (string, string)[]
        {
            ("1104", "تغيّر ذمم العملاء"),
            ("1107", "تغيّر المخزون"),
            ("1109", "تغيّر مصروفات مدفوعة مقدماً"),
            ("2101", "تغيّر ذمم الموردين"),
            ("2104", "تغيّر مصروفات مستحقة"),
            ("2105", "تغيّر دفعات مقدمة من العملاء"),
            // (CEEF-228) هذه الخمسة كانت غائبة، وكلّها تتحرّك في كلّ عملية بيع أو شراء
            // أو مستخلص — فكانت القائمة لا تتوازن بنيوياً. مثال: فاتورة آجلة 1,000 + ضريبة 150
            // تُنتج صافي دخل +1,000 وتغيّر ذمم -1,150 وضريبةً لا تظهر = تدفق تشغيليّ -150
            // لم يحدث قطّ (الصحيح صفر: لم يُقبض شيء).
            (SaudiChartOfAccounts.SystemCodes.VatInput, "تغيّر ضريبة القيمة المضافة — مدخلات"),
            (SaudiChartOfAccounts.SystemCodes.VatOutput, "تغيّر ضريبة القيمة المضافة — مخرجات"),
            (SaudiChartOfAccounts.SystemCodes.WorkInProgress, "تغيّر أعمال تحت التنفيذ"),
            // ‏(ح-٩٨) ‏إنتاجُ المصنع تحت التشغيل — يُصفَّر بإكمال الأمر، ويُسمّى إن تحرّك.
            (SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess, "تغيّر إنتاج تحت التشغيل"),
            (SaudiChartOfAccounts.SystemCodes.RetentionReceivable, "تغيّر محتجزات لدى العملاء"),
            (SaudiChartOfAccounts.SystemCodes.RetentionPayableSubcontractors, "تغيّر محتجزات مقاولي الباطن"),
        })
        {
            var delta = DeltaForCodes(startBalances, endBalances, code, null);
            namedWorkingCapital.Add(code);
            if (delta != 0m)
                operating.Add(new(label, delta));
        }

        // (CEEF-229 · م2-١٤) **البقيّة — وهي ما كان يسقط صامتاً.**
        //
        // القائمة أعلاه معدودةٌ يدوياً، ودفعة CEEF-228 اضطرّت لإضافة خمسة أكواد غائبة، ثم
        // تبيّن أن «شيكات برسم الدفع» (2103) و«رواتب مستحقة» (2108) غائبان كذلك. فالعطل نمطٌ
        // لا حادثة: **كلّ حسابٍ جديد يُضاف إلى الدليل يسقط من القائمة بلا خطأ ولا تحذير**.
        //
        // والتعريف المحاسبيّ لا يحتاج تعداداً: رأس المال العامل هو **كل أصلٍ متداول غير نقديّ
        // وكل التزامٍ متداول** — بادئة "11" (منها النقدية مستثناة لأنها المقياس نفسه لا بندٌ
        // يفسّره) وبادئة "21". والاستثمار يأخذ "12" والتمويل "22" و"31"، فلا تقاطع.
        //
        // فتبقى الأسماء المعروفة سطوراً مقروءة، ويُجمع ما عداها في سطرٍ واحد. والنتيجة أن
        // الهوية «مجموع الأقسام == صافي التغيّر النقديّ» تصدق **مهما أُضيف من حسابات**.
        var workingCapitalTotal =
            WorkingCapitalEffect(startBalances, endBalances, "11", IsCashCode)
            + WorkingCapitalEffect(startBalances, endBalances, "21", _ => false);
        var residual = workingCapitalTotal
            - namedWorkingCapital.Sum(c => DeltaForCodes(startBalances, endBalances, c, null));
        if (residual != 0m)
            operating.Add(new("تغيّرات رأس مال عامل أخرى", residual));

        var investing = new List<CashFlowLine>();
        // الاستثناء بالبادئة كذلك — وإلا دخل ابنُ مجمّع الإهلاك الاستثمارَ والتشغيلَ معاً.
        var investingDelta = DeltaForPrefix(startBalances, endBalances, "12",
            excludePrefixes: [DepreciationPrefix]);
        if (investingDelta != 0m)
            investing.Add(new("شراء/بيع أصول ثابتة (صافي)", -investingDelta));

        var financing = new List<CashFlowLine>();
        var loanDelta = DeltaForPrefix(startBalances, endBalances, "22");
        if (loanDelta != 0m)
            financing.Add(new("قروض طويلة الأجل (صافي)", loanDelta));

        // (CEEF-228) 3103 «الأرباح المبقاة» كانت مشمولةً، وقيدُ الإقفال السنويّ يُدخل فيها
        // صافي الدخل كاملاً — فيُحتسب مرّتين: في التشغيل كـ«صافي الدخل» وفي التمويل كحركة
        // حقوق ملكية. شركة بصافي دخل 200,000 كانت تُظهر مجموع أقسام 400,000 مقابل
        // NetCashChange حقيقيّ 200,000. و3106 «احتياطي ترجمة العملات» تسويةٌ غير نقدية.
        // توزيعات الشركاء وسحوباتهم تُقيَّد على 3105 «جاري الشركاء» فتبقى ظاهرة في التمويل.
        var equityDelta = DeltaForPrefix(startBalances, endBalances, "31",
            excludePrefixes: [
                SaudiChartOfAccounts.SystemCodes.RetainedEarnings,
                SaudiChartOfAccounts.SystemCodes.CurrentYearProfit,
                SaudiChartOfAccounts.SystemCodes.TranslationReserve,
            ]);
        if (equityDelta != 0m)
            financing.Add(new("حقوق ملكية / رأس مال (صافي)", equityDelta));

        // ‏توزيعاتُ الأرباح تُقيَّد على الأرباح المبقاة (3103) — وهي مستثناةٌ أعلاه لئلّا يُحسب صافي الدخل مرّتين،
        // ‏فكان التوزيعُ النقديّ يُخفض النقدَ بلا سطرٍ يفسّره. والمستخرَج هنا: تغيّرُ «وعاء الأرباح» (3103 + 3104 + أرصدة
        // ‏الدخل غير المقفلة) ناقصَ صافي دخل الفترة = ما وُزِّع أو سُوِّي على الأرباح المبقاة — وهو تمويل.
        var distributions = RetainedPool(endBalances) - RetainedPool(startBalances) - pnl.NetIncome;
        if (distributions != 0m)
            financing.Add(new("توزيعات أرباح وتسويات على الأرباح المبقاة", distributions));

        var openingCash = SumCash(startBalances);
        var closingCash = SumCash(endBalances);
        var netCashChange = closingCash - openingCash;

        return new CashFlowStatementResult(
            fromDate,
            toDate,
            new CashFlowSection("التشغيل", operating),
            new CashFlowSection("الاستثمار", investing),
            new CashFlowSection("التمويل", financing),
            netCashChange,
            openingCash,
            closingCash);
    }

    private async Task<Dictionary<string, (AccountType Type, decimal SignedBalance)>> BuildBalancesAsync(
        long companyId, DateOnly asOfDate)
    {
        var from = new DateOnly(1900, 1, 1);
        var rows = await new TrialBalanceQuery(db).ExecuteAsync(companyId, from, asOfDate);
        return rows.ToDictionary(
            r => r.AccountCode,
            r => (r.Type, SignedBalance(r)));
    }

    private static decimal RetainedPool(Dictionary<string, (AccountType Type, decimal SignedBalance)> balances) =>
        balances.Where(kv =>
                kv.Key.StartsWith(SaudiChartOfAccounts.SystemCodes.RetainedEarnings, StringComparison.Ordinal)
                || kv.Key.StartsWith(SaudiChartOfAccounts.SystemCodes.CurrentYearProfit, StringComparison.Ordinal))
            .Sum(kv => kv.Value.SignedBalance)
        + balances.Values.Where(v => v.Type == AccountType.Revenue).Sum(v => v.SignedBalance)
        - balances.Values.Where(v => v.Type == AccountType.Expense).Sum(v => v.SignedBalance);

    private static decimal SignedBalance(TrialBalanceRow row) => row.Type switch
    {
        AccountType.Asset => row.BalanceDebit - row.BalanceCredit,
        AccountType.Liability or AccountType.Equity => row.BalanceCredit - row.BalanceDebit,
        AccountType.Revenue => row.BalanceCredit - row.BalanceDebit,
        AccountType.Expense => row.BalanceDebit - row.BalanceCredit,
        _ => row.BalanceDebit - row.BalanceCredit,
    };

    /// <summary>
    /// (CEEF-229 · م2-١٤) أثرُ رأس المال العامل النقديّ لكل حسابٍ تحت بادئةٍ ما.
    /// يستعمل اصطلاح الإشارة نفسه في <see cref="DeltaForCodes"/> — نوعُ الحساب يُقرأ من
    /// الأرصدة لا يُفترض — فيصلح لخليطٍ من الأصول والالتزامات تحت البادئة الواحدة.
    /// </summary>
    private static decimal WorkingCapitalEffect(
        Dictionary<string, (AccountType Type, decimal SignedBalance)> start,
        Dictionary<string, (AccountType Type, decimal SignedBalance)> end,
        string prefix,
        Func<string, bool> skip)
    {
        var codes = start.Keys.Concat(end.Keys)
            .Where(c => c.StartsWith(prefix, StringComparison.Ordinal) && !skip(c))
            .Distinct();
        return codes.Sum(c => DeltaForCodes(start, end, c, null));
    }

    private static decimal DeltaForCodes(
        Dictionary<string, (AccountType Type, decimal SignedBalance)> start,
        Dictionary<string, (AccountType Type, decimal SignedBalance)> end,
        string code,
        AccountType? type,
        bool invert = false)
    {
        var startBal = start.GetValueOrDefault(code).SignedBalance;
        var endBal = end.GetValueOrDefault(code).SignedBalance;
        var delta = endBal - startBal;

        // (CEEF-228) كان النوع يُستنتج من أرصدة **بداية** المدة وحدها. وحسابٌ لا رصيد
        // افتتاحيّ له غائبٌ عن القاموس، فتعيد GetValueOrDefault صفراً — وهو ليس عضواً
        // صالحاً في AccountType (Asset = 1) فيسقط على الفرع الافتراضي: معاملةُ أصل.
        // النتيجة: كل التزامٍ نشأ داخل المدة (ذمم موردين، ضريبة مخرجات، مستحقات) يُحسب
        // بإشارة معكوسة. مثال مُقاس: فاتورة آجلة 1,000 + ضريبة 150 كانت تُنتج تدفقاً
        // تشغيلياً -300 بدل صفر.
        var accountType = type
            ?? (start.TryGetValue(code, out var startEntry) ? startEntry.Type
                : end.TryGetValue(code, out var endEntry) ? endEntry.Type
                : AccountType.Asset);
        var cashEffect = accountType switch
        {
            AccountType.Asset => -delta,
            AccountType.Liability or AccountType.Equity => delta,
            _ => -delta,
        };
        return invert ? -cashEffect : cashEffect;
    }

    /// <summary>
    /// (CEEF-229 · م2-١٤) الاستثناء صار <b>بالبادئة</b>، والمعامل الميت حُذف.
    ///
    /// <para>كان الاستثناء بمساواةٍ تامّة، فابنُ حسابٍ مستثنى يدخل القسم رغم استثناء أبيه —
    /// وهو نمط عطل <c>SumCash</c> نفسه في موضعٍ آخر. وكان المعامل <c>type</c> مُعلَناً
    /// <b>ولا يُستعمل قطّ</b>: الترشيح على البادئة وحدها وإشارةُ النقد تُدار عند النداء.
    /// ومعاملٌ يوحي بفحصٍ لا يجري أسوأ من غيابه — فحُذف.</para>
    /// </summary>
    private static decimal DeltaForPrefix(
        Dictionary<string, (AccountType Type, decimal SignedBalance)> start,
        Dictionary<string, (AccountType Type, decimal SignedBalance)> end,
        string prefix,
        string[]? excludePrefixes = null)
    {
        var excluded = excludePrefixes ?? [];
        var codes = start.Keys.Concat(end.Keys)
            .Where(c => c.StartsWith(prefix, StringComparison.Ordinal)
                        && !excluded.Any(e => c.StartsWith(e, StringComparison.Ordinal)))
            .Distinct();
        return codes.Sum(c =>
        {
            var startBal = start.GetValueOrDefault(c).SignedBalance;
            var endBal = end.GetValueOrDefault(c).SignedBalance;
            return endBal - startBal;
        });
    }

    /// <summary>
    /// (CEEF-229 · م2-١٤) النقدية <b>بالبادئة لا بالمساواة التامّة</b>.
    ///
    /// <para>كانت المطابقة مساواةً تامّة على <c>1101</c> و<c>1102</c> و<c>1103</c>. وكلُّ حسابٍ
    /// بنكيّ يُنشأ <b>ابناً</b> تحت <c>1102</c> بكودٍ مثل <c>110201</c>
    /// (<c>BankAccountService.NextChildCode</c>)، والمال يذهب إلى الابن لا إلى الأب: سندُ القبض
    /// البنكيّ وسندُ الصرف وإيداعُ الشيك وفروقُ كشف الحساب كلُّها تُقيَّد على
    /// <c>bank.GlAccountId</c>. والأب حسابٌ تجميعيّ لا يُرحَّل عليه فلا يظهر في ميزان المراجعة
    /// أصلاً — فكان <b>كلُّ نقد البنوك خارج الرصيدين الافتتاحيّ والختاميّ</b>.</para>
    ///
    /// <para>الأثر مقيس: منشأةٌ تبيع بـ10,000 وتُحصّل 11,500 في حسابها البنكيّ تُخرج رصيداً
    /// ختامياً <b>صفراً</b>. ولم يكشفه اختبار لأن اختبارات القائمة تؤكّد مجاميع الأقسام
    /// منفردةً ولا تربطها بالنقد.</para>
    ///
    /// <para>والمطابقة بالبادئة آمنة من الازدواج: الترحيل لا يقع إلا على حساباتٍ ورقية، فلا
    /// يظهر الأب وابنُه معاً في ميزان المراجعة. وهي المطابقة نفسها التي يستعملها
    /// <see cref="DeltaForPrefix"/> للاستثمار والتمويل — فالعطل كان في <b>عدم اطّرادها</b>.</para>
    /// </summary>
    private static decimal SumCash(Dictionary<string, (AccountType Type, decimal SignedBalance)> balances) =>
        balances.Where(kv => IsCashCode(kv.Key)).Sum(kv => kv.Value.SignedBalance);

    /// <summary>هل الكود نقديّ — أو ابنٌ لحسابٍ نقديّ؟</summary>
    private static bool IsCashCode(string code) =>
        CashCodes.Any(c => code.StartsWith(c, StringComparison.Ordinal));
}
