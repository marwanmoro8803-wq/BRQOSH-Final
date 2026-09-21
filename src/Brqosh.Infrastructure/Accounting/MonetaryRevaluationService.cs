using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>أثرُ إعادة تقييمٍ لعملةٍ واحدة — والمكشوفُ هو ما يُقاس عليه.</summary>
public record MonetaryExposure(
    string CurrencyCode,
    decimal ForeignReceivable,
    decimal BookedReceivable,
    decimal ForeignPayable,
    decimal BookedPayable);

/// <summary>حصيلةُ إعادة تقييمٍ — والقيدُ وعكسُه معاً.</summary>
public record MonetaryRevaluationResult(
    decimal ReceivableAdjustment,
    decimal PayableAdjustment,
    long? JournalEntryId,
    long? ReversalEntryId,
    IReadOnlyList<MonetaryExposure> Exposures);

/// <summary>
/// (CEEF-230 · ط4-٧) <b>إعادةُ تقييم الذمم بعملةٍ أجنبية آخرَ الفترة.</b>
///
/// <para><b>ما كان:</b> <c>FxRevaluationService</c> يُعيد تقييمَ <b>الأرصدة البنكية
/// وحدَها</b>. فذمّةٌ بالدولار تبقى في الدفاتر <b>بسعر يوم الفاتورة إلى الأبد</b> —
/// وحسابا الفروق غير المحقَّقة (<c>4107</c>/<c>5217</c>) بلا كاتبٍ لجانب الذمم.</para>
///
/// <para><b>وهو إلزامٌ محاسبيّ لا تحسين</b>: البنودُ النقدية تُترجَم بسعر الإقفال
/// (معيار المحاسبة الدولي 21). <b>ومنشأةٌ تُصدّر بالدولار تُقفل سنتَها بذممٍ تخالف
/// قيمتَها الحقيقية</b> — والفرقُ يكبر كلَّما تحرّك السعر.</para>
///
/// <para><b>والقيدُ على حسابِ تسويةٍ مستقلّ لا على حساب الذمم</b>: الدفترُ المساعد يُبنى
/// من المستندات بأسعار نشأتها، <b>وتقييمُ حساب الذمم نفسِه يكسر <c>هـ-١</c> في كلّ
/// إقفال</b>. والميزانيةُ تعرض <c>1104 + 1115</c> معاً.</para>
///
/// <para><b>ويُعكس القيدُ في اليوم التالي</b> — <b>وبغير العكس يتراكم التقييمُ فترةً
/// بعد فترة</b>، فيصير الرصيدُ مجموعَ تقديراتٍ لا تقديرَ اليوم. وهو اصطلاحُ الأنظمة
/// الكبرى، ويجعل التقييمَ <b>عرضاً للحظةٍ لا أثراً دائماً</b>.</para>
/// </summary>
public class MonetaryRevaluationService(BrqoshDbContext db)
{
    /// <summary>
    /// يُعيد تقييمَ الذمم المفتوحة بالعملات إلى أسعار الإقفال.
    /// </summary>
    /// <param name="closingRates">
    /// سعرُ الإقفال لكلّ عملة. <b>وعملةٌ مكشوفةٌ بلا سعرٍ تُرفض ولا تُتجاوَز</b>: تجاوزُها
    /// يُخرج تقييماً ناقصاً <b>يبدو كاملاً</b>، ولا شيءَ في القيد يدلّ على ما سقط.
    /// </param>
    public async Task<MonetaryRevaluationResult> RevalueAsync(
        long companyId, DateOnly asOf, IReadOnlyDictionary<string, decimal> closingRates,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var exposures = await ExposuresAsync(companyId, asOf, token);

            var missing = exposures
                .Where(e => !closingRates.ContainsKey(e.CurrencyCode))
                .Select(e => e.CurrencyCode)
                .ToList();

            if (missing.Count > 0)
                throw new AccountingException(
                    "عملاتٌ مكشوفةٌ بلا سعر إقفال: " + string.Join("، ", missing)
                    + " — وتقييمٌ ينقصه سعرٌ يُخرج رقماً يبدو كاملاً وهو ناقص.");

            decimal receivableAdjustment = 0m, payableAdjustment = 0m;
            foreach (var exposure in exposures)
            {
                var rate = closingRates[exposure.CurrencyCode];
                receivableAdjustment += Round(exposure.ForeignReceivable * rate) - exposure.BookedReceivable;
                payableAdjustment += Round(exposure.ForeignPayable * rate) - exposure.BookedPayable;
            }

            receivableAdjustment = Round(receivableAdjustment);
            payableAdjustment = Round(payableAdjustment);

            if (receivableAdjustment == 0m && payableAdjustment == 0m)
                return new MonetaryRevaluationResult(0m, 0m, null, null, exposures);

            var sourceDocument = $"MonetaryRevaluation:{asOf:yyyy-MM-dd}";

            // ‏**ولا يُقيَّد التقييمُ مرّتين للتاريخ نفسِه**: تشغيلٌ ثانٍ يُضاعف الأثرَ،
            // ‏**والعكسُ في اليوم التالي يُلغي واحداً ويترك الآخر** — فيبقى فرقٌ دائم.
            // (جاهزيّةُ الإطلاق · ح-٣٨) ‏**والسؤالُ عن تقييمٍ حيٍّ لا عن أثرٍ باق.
            //
            // ‏كان `Status != Draft` — **والمعكوسُ ليس مسودّةً**، فيبقى الحارسُ
            // ‏يرفض بعد العكس تماماً. **والرسالةُ كانت تقول «اعكسْ قيدَه
            // ‏إن أردت إعادته»** — **فتأمر بما لا يُجدي**: من يعكس يُمنع كما مُنِع قبلَه.
            //
            // ‏**ومعكوسٌ لا أثرَ له في الدفتر** فلا يمنع تقييماً جديداً.
            // ‏**وليس هذا نقضاً لـح-٢٦**: ذاك في الأرصدة حيث يبقى المعكوسُ
            // ‏وعكسُه معاً، **وهذا سؤالُ وجودٍ لا سؤالُ مبلغ**.
            var already = await db.JournalEntries.AnyAsync(e =>
                e.CompanyId == companyId && e.SourceDocument == sourceDocument
                && e.Status == JournalEntryStatus.Posted
                // ‏**وقيدُ العكس يرث `SourceDocument` من أصله** (`CreateReversal`)،
                // ‏**فيبقى `Posted` تحت المفتاح نفسِه بعد الإلغاء**. ولا وسمَ
                // ‏يقول «أنا عكس» — **بل الأصلُ هو من يُشير إليه**. فالتمييزُ
                // ‏أنّ لا قيدَ يُسمّيني عكساً له.
                && !db.JournalEntries.Any(o =>
                    o.CompanyId == companyId && o.ReversalEntryId == e.Id), token);

            if (already)
                throw new AccountingException(
                    $"أُعيد التقييمُ لهذا التاريخ سلفاً ({asOf:yyyy-MM-dd}) — "
                    + "ويُلغى من شاشة إعادة التقييم قبل إعادتِه بسعرٍ آخر.");

            var entry = await BuildAsync(companyId, asOf, receivableAdjustment, payableAdjustment,
                sourceDocument, $"إعادة تقييم الذمم بالعملات — {asOf:yyyy-MM-dd}", reverse: false, token);

            var reversal = await BuildAsync(companyId, asOf.AddDays(1),
                receivableAdjustment, payableAdjustment,
                $"{sourceDocument}:Reversal", $"عكس إعادة تقييم الذمم — {asOf:yyyy-MM-dd}",
                reverse: true, token);

            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "MonetaryRevaluation", $"{asOf:yyyy-MM-dd}",
                reason: "إعادة تقييم الذمم بالعملات",
                details: $"مدينة {receivableAdjustment:N2} · دائنة {payableAdjustment:N2}",
                ct: token);

            return new MonetaryRevaluationResult(receivableAdjustment, payableAdjustment,
                entry.Id, reversal.Id, exposures);
        }, ct);
    }

    /// <summary>
    /// المكشوفُ لكلّ عملة — <b>ما بقي مفتوحاً من الفواتير الأجنبية</b>.
    ///
    /// <para><b>والمفتوحُ يُحسب بعملة الفاتورة ثمّ يُقابَل بقيمته المقيَّدة</b>: الفرقُ
    /// بينهما هو أثرُ حركة السعر وحدَه. <b>وحسابُه من الأرصدة الريالية يخلط أثرَ السعر
    /// بأثر السداد</b> — فيُنتج «فرقَ عملة» هو في حقيقته تحصيلٌ وقع.</para>
    /// </summary>
    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٣٨) <b>إلغاءُ تقييمٍ أُعيد بسعرٍ خاطئ —
    /// <b>من مستنده لا من القيد</b>.</b>
    ///
    /// <para><b>وكانت حلقةً مغلقة</b>: رسالةُ الإعادة تقول «اعكسْ قيدَه»،
    /// <b>و<c>JournalReversalService</c> يرفض العكسَ المباشر</b> لأنّ القيدَ مرتبطٌ
    /// بمستندٍ نظام («يُصحّح من المستند المصدر») — <b>ولا مسارَ تصحيحٍ
    /// في المستند أصلاً</b>. <b>فيُحال المستخدمُ من رسالةٍ إلى رسالةٍ
    /// إلى لا شيء</b> — <b>وسعرٌ خاطئٌ يبقى في دفاترِ سنةٍ تُقفَل عليه</b>.</para>
    ///
    /// <para><b>والطرفان يُعكسان معاً</b>: التقييمُ يُنشئ قيدَه
    /// وقيدَ عكسِه في اليوم التالي. <b>وعكسُ الأوّل وحدَه يترك الثاني
    /// حيّاً فيبقى فرقٌ دائم</b> — وهو عينُ ما يحذّر منه التعليقُ أعلاه.</para>
    /// </summary>
    public async Task<int> CancelAsync(
        long companyId, DateOnly asOf, DateOnly cancellationDate, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var sourceDocument = $"MonetaryRevaluation:{asOf:yyyy-MM-dd}";
            var keys = new[] { sourceDocument, $"{sourceDocument}:Reversal" };

            var entries = await db.JournalEntries
                .Where(e => e.CompanyId == companyId
                            && keys.Contains(e.SourceDocument)
                            && e.Status == JournalEntryStatus.Posted
                            // ‏**وقيدُ العكس يرث `SourceDocument` من أصله** (`CreateReversal`)،
                            // ‏**فيبقى `Posted` تحت المفتاح نفسِه بعد الإلغاء**. ولا وسمَ
                            // ‏يقول «أنا عكس» — **بل الأصلُ هو من يُشير إليه**. فالتمييزُ
                            // ‏أنّ لا قيدَ يُسمّيني عكساً له.
                            && !db.JournalEntries.Any(o =>
                                o.CompanyId == companyId && o.ReversalEntryId == e.Id))
                .OrderBy(e => e.EntryDate)
                .ToListAsync(token);

            if (entries.Count == 0)
                throw new AccountingException(
                    $"لا تقييمَ قائماً لهذا التاريخ ({asOf:yyyy-MM-dd}).");

            var reversal = new JournalReversalService(db);
            foreach (var entry in entries)
            {
                // ‏**ولا يسبق العكسُ أصلَه**: قيدُ العكس التلقائيُّ مؤرّخٌ بغدٍ،
                // ‏فيُؤخَذ المتأخّرُ من تاريخ الإلغاء وتاريخِ القيد — والنطاقُ
                // ‏يرفض عكساً يسبق أصلَه (درسُ ح-٣٤).
                var on = cancellationDate < entry.EntryDate ? entry.EntryDate : cancellationDate;
                await reversal.ReverseAsync(
                    companyId, entry.Id, on, username, fromSourceDocument: true, token);
            }

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "MonetaryRevaluation", $"{asOf:yyyy-MM-dd}",
                reason: "إلغاء إعادة تقييم الذمم",
                details: $"عُكِس {entries.Count}",
                ct: token);

            return entries.Count;
        }, ct);
    }

    public async Task<IReadOnlyList<MonetaryExposure>> ExposuresAsync(
        long companyId, DateOnly asOf, CancellationToken ct = default)
    {
        var allocations = await db.PaymentAllocationLines.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.AllocationDate <= asOf)
            .ToListAsync(ct);

        var salesAllocated = allocations
            .Where(a => a.DocumentType == PaymentAllocationDocumentType.SalesInvoice)
            .GroupBy(a => a.DocumentId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var purchaseAllocated = allocations
            .Where(a => a.DocumentType == PaymentAllocationDocumentType.PurchaseInvoice)
            .GroupBy(a => a.DocumentId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var sales = await db.SalesInvoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Posted
                        && i.CurrencyCode != "SAR" && i.InvoiceDate <= asOf)
            .ToListAsync(ct);

        var purchases = await db.PurchaseInvoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.Status == PurchaseInvoiceStatus.Posted
                        && i.CurrencyCode != "SAR" && i.InvoiceDate <= asOf)
            .ToListAsync(ct);

        var byCurrency = new Dictionary<string, (decimal Fr, decimal Br, decimal Fp, decimal Bp)>(
            StringComparer.Ordinal);

        foreach (var invoice in sales)
        {
            var open = invoice.ForeignGrandTotal - salesAllocated.GetValueOrDefault(invoice.Id);
            if (open <= 0m) continue;

            var current = byCurrency.GetValueOrDefault(invoice.CurrencyCode);
            byCurrency[invoice.CurrencyCode] =
                (current.Fr + open, current.Br + Round(open * invoice.ExchangeRate), current.Fp, current.Bp);
        }

        foreach (var invoice in purchases)
        {
            var open = invoice.ForeignGrandTotal - purchaseAllocated.GetValueOrDefault(invoice.Id);
            if (open <= 0m) continue;

            var current = byCurrency.GetValueOrDefault(invoice.CurrencyCode);
            byCurrency[invoice.CurrencyCode] =
                (current.Fr, current.Br, current.Fp + open, current.Bp + Round(open * invoice.ExchangeRate));
        }

        return [.. byCurrency
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new MonetaryExposure(kv.Key, kv.Value.Fr, kv.Value.Br, kv.Value.Fp, kv.Value.Bp))];
    }

    /// <summary>
    /// يبني قيدَ التقييم أو عكسَه.
    ///
    /// <para><b>والإشارةُ في جانب الموردين معكوسة</b>: ارتفاعُ السعر يزيد الذمّةَ المدينة
    /// <b>ربحاً</b> ويزيد الدائنة <b>خسارة</b>. <b>ونسخُ جانب العملاء يقلب النتيجةَ في
    /// كلّ إقفال</b> — فتُقفل سنةٌ بربحٍ وهي خسارة.</para>
    /// </summary>
    private async Task<JournalEntry> BuildAsync(long companyId, DateOnly date,
        decimal receivableAdjustment, decimal payableAdjustment,
        string sourceDocument, string description, bool reverse, CancellationToken ct)
    {
        var sign = reverse ? -1m : 1m;
        var receivable = receivableAdjustment * sign;
        var payable = payableAdjustment * sign;

        var arAdjust = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.FxAdjustmentReceivable, ct);
        var apAdjust = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.FxAdjustmentPayable, ct);
        var gain = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.UnrealizedFxGain, ct);
        var loss = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.UnrealizedFxLoss, ct);

        await FinancialPostingSupport.ValidateAsync(
            db, companyId, date, PostingDocumentKind.JournalEntry,
            accountIds: [arAdjust.Id, apAdjust.Id, gain.Id, loss.Id], ct: ct);

        var entry = JournalEntry.CreateDraft(companyId, date, description,
            sourceDocument: sourceDocument);

        if (receivable != 0m)
        {
            if (receivable > 0m)
            {
                entry.AddLine(arAdjust, receivable, 0m, "زيادة قيمة ذمم مدينة بالعملة");
                entry.AddLine(gain, 0m, receivable, "فرق عملة غير محقق");
            }
            else
            {
                entry.AddLine(loss, -receivable, 0m, "فرق عملة غير محقق");
                entry.AddLine(arAdjust, 0m, -receivable, "نقص قيمة ذمم مدينة بالعملة");
            }
        }

        if (payable != 0m)
        {
            // ‏**زيادةُ الذمّة الدائنة خسارةٌ** — وهي عكسُ المدينة تماماً.
            if (payable > 0m)
            {
                entry.AddLine(loss, payable, 0m, "فرق عملة غير محقق");
                entry.AddLine(apAdjust, 0m, payable, "زيادة قيمة ذمم دائنة بالعملة");
            }
            else
            {
                entry.AddLine(apAdjust, -payable, 0m, "نقص قيمة ذمم دائنة بالعملة");
                entry.AddLine(gain, 0m, -payable, "فرق عملة غير محقق");
            }
        }

        var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, date);
        entry.Post(number);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        return entry;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
