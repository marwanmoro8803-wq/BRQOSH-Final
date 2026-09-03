using Brqosh.Infrastructure.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Sales;

public record CustomerBalance(long CustomerId, decimal Invoiced, decimal Credited, decimal Received)
{
    /// <summary>الرصيد المدين المستحق على العميل.</summary>
    public decimal Balance => Invoiced - Credited - Received;
}

public record CustomerStatementLine(DateOnly Date, string DocumentNumber, string DocumentType,
    decimal Debit, decimal Credit, decimal RunningBalance);

/// <summary>
/// دفتر مساعد للعملاء: الرصيد وكشف الحساب يُبنيان من المستندات المرحّلة
/// (فواتير آجلة + مستخلصات − إشعارات دائنة − سندات قبض − شيكات واردة محصّلة/قيد التحصيل).
/// تُستبعد فواتير نقاط البيع النقدية لأنها لا تُقيَّد على ذمم العملاء.
/// </summary>
public class CustomerBalanceQuery(BrqoshDbContext db)
{
    /// <summary>شيك وارد سجّل تخفيض ذمة العميل عند الاستلام.</summary>
    private static readonly ChequeStatus[] ReceivedChequeCreditStatuses =
    [
        ChequeStatus.InPortfolio,
        ChequeStatus.Deposited,
        ChequeStatus.Cleared,
    ];

    private static IQueryable<SalesInvoice> ArSalesInvoices(BrqoshDbContext db, long companyId, long? customerId = null)
    {
        var q = db.SalesInvoices.Where(i =>
            i.CompanyId == companyId
            && i.Status == SalesInvoiceStatus.Posted
            && i.PosShiftId == null);
        return customerId is long cid ? q.Where(i => i.CustomerId == cid) : q;
    }

    /// <summary>
    /// (CEEF-229 · م0-٩) إشعارات الدائن التي تمسّ <b>الذمم</b> — دون إشعارات نقاط البيع.
    ///
    /// <para>الفواتير كانت تستثني نقاط البيع (<c>PosShiftId == null</c>) والإشعاراتُ لا تستثنيها،
    /// فيُطرح من الذمم ما لم يُضَف إليها قط. و<c>CreditNotePostingService</c> يوجّه إشعار فاتورة
    /// نقطة البيع إلى <b>الصندوق</b> لا إلى حساب العملاء — بحقّ، فالبيع كان نقدياً.</para>
    ///
    /// <para><b>الأثر:</b> عميل مسجَّل يشتري من الكاشير بـ1,150 نقداً ثم يُرجع البضاعة ⟵ الأستاذ
    /// يقول ذمم العميل صفر، والدفتر المساعد يقول <b>سالب 1,150</b>. رصيدٌ دائنٌ وهميّ يظهر في
    /// كشف الحساب وتقرير الأعمار، ومؤشّر «المدينون» أقلّ من الحقيقة عن كل حالة مماثلة. ويصيب
    /// قطاع التجزئة تحديداً — الشريحة المستهدفة الأولى.</para>
    /// </summary>
    private static IQueryable<CreditNote> ArCreditNotes(BrqoshDbContext db, long companyId, long? customerId = null)
    {
        var posInvoiceIds = db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.PosShiftId != null)
            .Select(i => i.Id);

        var q = db.CreditNotes.Where(n =>
            n.CompanyId == companyId
            && n.Status == CreditNoteStatus.Posted
            && !posInvoiceIds.Contains(n.OriginalInvoiceId));
        return customerId is long cid ? q.Where(n => n.CustomerId == cid) : q;
    }

    /// <summary>
    /// (CEEF-232 · ج-١) <b>ما بلغ حسابَ الذمم من المستخلص — لا إجماليَّه.</b>
    ///
    /// <para><b>والمستخلصُ يُرحَّل على حسابين:</b> الصافي وضريبتُه على <c>1104</c>،
    /// <b>والمحتجزُ على <c>1114</c></b> — وهو الصواب، فالمحتجزُ ضمانُ تنفيذٍ لا يُطالَب
    /// به قبل انقضاء مدّته. <b>وكان الدفترُ المساعد يجمع <c>GrandTotal</c></b>، فتنكسر
    /// هويّةُ <c>هـ-١</c> بمقدار المحتجز في كلّ مستخلص.</para>
    ///
    /// <para><b>والأثرُ بالأرقام:</b> مقاولٌ يُخرج مئةَ مليونٍ باحتجاز عشرة بالمئة يرى
    /// <b>عشرةَ ملايينَ ذمّةً لا وجودَ لها في الأستاذ</b> — تُقرأ سيولةً قادمة، وتظهر في
    /// أعمار الديون <b>فيُطارَد بها عميلٌ لا يدين بها</b>، وتُقفل السنةُ على أصلٍ مُعلَنٍ
    /// مرّتين.</para>
    ///
    /// <para><b>والضريبةُ تبقى كاملةً</b>: الاحتجازُ تأجيلُ قبضٍ لا خصمٌ من الثمن،
    /// <b>والتوريدُ وقع بكامل قيمة الأعمال</b>.</para>
    /// </summary>
    private static decimal ReceivablePortionOf(ProgressBilling billing) =>
        billing.NetBillableAmount + billing.VatAmount;

    /// <summary>
    /// بادئةُ مصدر قيد إفراج المحتجز — <b>وهي مشتركةٌ بين جانبَي العميل والمقاول من الباطن</b>،
    /// فالنوعُ يُقرأ من المقطع الثالث (<c>RetentionRelease:{projectId}:{(int)type}</c>).
    /// </summary>
    private const string CustomerRetentionReleasePrefix = "RetentionRelease:";

    /// <summary>إفراجُ محتجزٍ واحدٌ لعميل — بتاريخه ورقم قيده ومبلغِ سطر الذمم منه.</summary>
    internal readonly record struct RetentionReleaseDocument(
        long CustomerId, long EntryId, DateOnly Date, string EntryNumber, decimal Amount);

    /// <summary>
    /// (CEEF-232 · ج-١) <b>والمحتجزُ المُفرَج عنه يعود ذمّةً — وهذا هو الطرفُ الذي يُنسى.</b>
    ///
    /// <para><b>فالإفراجُ يُحوّل <c>1114 ⟵ 1104</c> بقيدٍ لا مستندَ له</b>، ودفترُ العملاء
    /// يُبنى من المستندات. <b>فاستبعادُ المحتجز عند الفوترة بلا إدخاله عند الإفراج يكسر
    /// الهويّةَ في الاتجاه المقابل</b> — ويصير العميلُ مدينَ الأستاذ بريءَ الدفتر،
    /// <b>وهو أسوأ من الأوّل: مالٌ مستحقٌّ لا يُطالِب به أحد</b>.</para>
    ///
    /// <para><b>ولا يُقرأ القيدُ بمبلغه بل بسطر الذمم منه</b>: القيدُ طرفان، <b>وجمعُ
    /// الطرفين يُضاعف الإفراج</b>. والعميلُ يُبلَغ من المشروع لا من القيد — فلا حقلَ عميلٍ
    /// على القيد أصلاً.</para>
    /// </summary>
    private static async Task<decimal> ReleasedRetentionAsync(
        BrqoshDbContext db, long companyId, long customerId, DateOnly? asOf = null)
    {
        var byCustomer = await ReleasedRetentionByCustomerAsync(db, companyId, asOf, customerId);
        return byCustomer.GetValueOrDefault(customerId);
    }

    /// <summary>المُفرَج عنه لكلّ عميل — <see cref="ReleasedRetentionAsync"/>.</summary>
    private static async Task<IReadOnlyDictionary<long, decimal>> ReleasedRetentionByCustomerAsync(
        BrqoshDbContext db, long companyId, DateOnly? asOf = null, long? customerId = null)
    {
        var totals = new Dictionary<long, decimal>();
        foreach (var release in await ReleasedRetentionDocumentsAsync(db, companyId, asOf, customerId))
            totals[release.CustomerId] = totals.GetValueOrDefault(release.CustomerId) + release.Amount;

        return totals;
    }

    /// <summary>
    /// (CEEF-232 · ج-٢) <b>الإفراجاتُ مفردةً بتواريخها — لا مجموعاً واحداً.</b>
    ///
    /// <para><b>والمجموعُ وحدَه لا يكفي لقارئَين اثنين:</b> <b>كشفُ الحساب</b> يحتاج سطراً
    /// بتاريخه وإلّا خالف رأسُه سطورَه، <b>وتقريرُ الأعمار</b> يحتاج تاريخاً ليُعمِّر عليه —
    /// <b>والمحتجزُ المُفرَج عنه دَينٌ عمرُه من يوم الإفراج لا من يوم المستخلص</b>، فقد كان
    /// إلى ذلك اليوم ضماناً لا مطالبةً.</para>
    /// </summary>
    internal static async Task<IReadOnlyList<RetentionReleaseDocument>> ReleasedRetentionDocumentsAsync(
        BrqoshDbContext db, long companyId, DateOnly? asOf = null, long? customerId = null)
    {
        var projects = await db.ConstructionProjects
            .Where(p => p.CompanyId == companyId && p.CustomerId != null
                        && (customerId == null || p.CustomerId == customerId))
            .Select(p => new { p.Id, CustomerId = p.CustomerId!.Value })
            .ToDictionaryAsync(p => p.Id, p => p.CustomerId);
        if (projects.Count == 0)
            return [];

        var receivableAccountId = await db.Accounts
            .Where(a => a.CompanyId == companyId
                        && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsReceivable)
            .Select(a => (long?)a.Id)
            .FirstOrDefaultAsync();
        if (receivableAccountId is null)
            return [];

        var entries = await db.JournalEntries
            .WhereReportable(companyId, to: asOf)
            .Where(e => e.SourceDocument != null
                        && e.SourceDocument.StartsWith(CustomerRetentionReleasePrefix))
            .Select(e => new
            {
                e.Id,
                e.SourceDocument,
                e.EntryDate,
                e.EntryNumber,
                Amount = e.Lines
                    .Where(l => l.AccountId == receivableAccountId)
                    .Sum(l => l.Debit - l.Credit),
            })
            .ToListAsync();

        var releases = new List<RetentionReleaseDocument>();
        foreach (var entry in entries)
        {
            // (CEEF-232 · ج-٢) ‏**والنوعُ يُقرأ من المصدر لا يُفترض من الحساب.**
            // ‏فـ`RetentionReleaseService` يكتب البادئةَ نفسَها **لجانبَي العميل والمقاول
            // ‏من الباطن معاً** (`RetentionRelease:{projectId}:{(int)type}`). ‏وكان التمييزُ
            // ‏متروكاً لمُرشِّح سطر الذمم — **وهو ينجو بالصفر لا بالاستبعاد**: إفراجُ مقاولٍ
            // ‏من الباطن يدخل بمبلغٍ صفريّ، **فيُنشئ للعميل مفتاحاً في القاموس** يدخل اتّحادَ
            // ‏الأرصدة ويُظهر عميلاً برصيدٍ صفريٍّ في تقارير لا شأن له بها.
            var parts = entry.SourceDocument!.Split(':');
            if (parts.Length < 3
                || !long.TryParse(parts[1], out var projectId)
                || !int.TryParse(parts[2], out var type)
                || type != (int)RetentionReleaseType.CustomerRelease)
                continue;
            if (!projects.TryGetValue(projectId, out var owner))
                continue;

            releases.Add(new RetentionReleaseDocument(
                owner, entry.Id, entry.EntryDate, entry.EntryNumber ?? "", entry.Amount));
        }

        return releases;
    }

    /// <summary>
    /// (CEEF-232 · ح-٢) <b>وعربونُ الحجز المُنزَل يُنقص الذمّة — في الدفتر كما في الأستاذ.</b>
    ///
    /// <para><b>وهو الفخُّ نفسُه الذي وقع فيه المحتجز (ج-١):</b> الإنزالُ قيدٌ لا مستند
    /// (<c>مدين 2105 / دائن 1104</c>)، <b>ودفترُ العملاء يُبنى من المستندات</b>. فتنقص
    /// ذمّةُ النزيل في الأستاذ ولا تنقص في دفتره — <b>فيُطالَب بما سدّده مقدَّماً</b>،
    /// ويظهر في أعمار الديون وكشف الحساب مديناً بالعربون إلى الأبد.</para>
    ///
    /// <para><b>ولا يُقرأ القيدُ بمبلغه بل بسطر الذمم منه</b> — والنزيلُ يُبلَغ من الحجز
    /// لا من القيد، فلا حقلَ عميلٍ على القيد.</para>
    /// </summary>
    private static async Task<IReadOnlyDictionary<long, decimal>> AppliedDepositsByCustomerAsync(
        BrqoshDbContext db, long companyId, DateOnly? asOf = null, long? customerId = null)
    {
        var receivableAccountId = await db.Accounts
            .Where(a => a.CompanyId == companyId
                        && a.Code == SaudiChartOfAccounts.SystemCodes.AccountsReceivable)
            .Select(a => (long?)a.Id)
            .FirstOrDefaultAsync();
        if (receivableAccountId is null)
            return new Dictionary<long, decimal>();

        var guests = await db.Reservations
            .Where(r => r.CompanyId == companyId
                        && (customerId == null || r.GuestCustomerId == customerId))
            .Select(r => new { r.Id, r.GuestCustomerId })
            .ToDictionaryAsync(r => r.Id, r => r.GuestCustomerId);
        if (guests.Count == 0)
            return new Dictionary<long, decimal>();

        var entries = await db.JournalEntries
            .WhereReportable(companyId, to: asOf)
            .Where(e => e.SourceDocument != null
                        && e.SourceDocument.StartsWith(ReservationDepositService.AppliedPrefix))
            .Select(e => new
            {
                e.SourceDocument,
                Amount = e.Lines
                    .Where(l => l.AccountId == receivableAccountId)
                    .Sum(l => l.Credit - l.Debit),
            })
            .ToListAsync();

        var totals = new Dictionary<long, decimal>();
        foreach (var entry in entries)
        {
            var tail = entry.SourceDocument![ReservationDepositService.AppliedPrefix.Length..];
            if (!long.TryParse(tail, out var reservationId)) continue;
            if (!guests.TryGetValue(reservationId, out var guest)) continue;
            totals[guest] = totals.GetValueOrDefault(guest) + entry.Amount;
        }

        return totals;
    }

    public async Task<CustomerBalance> ForCustomerAsync(long companyId, long customerId)
    {
        var invoiced = await ArSalesInvoices(db, companyId, customerId).SumGrandTotalAsync();

        var billings = (await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.CustomerId == customerId && b.Status == ProgressBillingStatus.Posted)
            .ToListAsync())
            .Sum(ReceivablePortionOf);

        var retentionReleased = await ReleasedRetentionAsync(db, companyId, customerId);

        var credited = await ArCreditNotes(db, companyId, customerId).SumGrandTotalAsync();

        // (CEEF-230 · ط4-٠) ‏**ما حُرِّر من الذمّة لا ما دخل الصندوق** — كما في
        // ‏<see cref="AllBalancesAsync"/>. **وكان هذا المسار وحدَه بلا الإصلاح**: يطرح
        // ‏`Amount` وهو بعملة السند، فسندٌ دولاريٌّ يُنقص ذمّةً رياليةً بعددٍ دولاريّ.
        //
        // ‏**والأثرُ رقمان لشيءٍ واحد**: عميلٌ سدّد بالدولار كلَّ ما عليه يظهر **مسدَّداً
        // ‏في اللوحة ومديناً بـ3,162.50 في كشف حسابه** — والمحاسبُ لا يعرف أيَّهما يُصدّق.
        // ‏وأخرجه قياسٌ يُقابل المسارين، لا قراءةُ شيفرة: **كلاهما كان أخضرَ وحدَه.**
        var received = await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.CustomerId == customerId && v.Status == ReceiptVoucherStatus.Posted)
            .SumAsync(v => (decimal?)(v.ReceivableRelieved == 0m ? v.Amount : v.ReceivableRelieved)) ?? 0m;

        received += await db.Cheques
            .Where(c => c.CompanyId == companyId && c.CustomerId == customerId
                        && c.Type == ChequeType.Received
                        && ReceivedChequeCreditStatuses.Contains(c.Status))
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;

        // ‏(CEEF-232 · ح-٢) ‏**والعربونُ المُنزَل سدادٌ** — يُنقص الذمّة كسند القبض.
        received += (await AppliedDepositsByCustomerAsync(db, companyId, null, customerId))
            .GetValueOrDefault(customerId);

        return new CustomerBalance(customerId, invoiced + billings + retentionReleased, credited, received);
    }

    /// <summary>
    /// أرصدةُ العملاء كلِّهم.
    /// </summary>
    /// <param name="asOf">
    /// <b>حتّى تاريخٍ معلوم</b> — و<c>null</c> يعني الآن. <b>ولا يُقاس عمرُ دَينٍ بميزانٍ
    /// من زمنٍ آخر</b>: تقريرُ أعمارٍ في ٣٠ يونيو يُقابل خاناتِه برصيدِ اليوم يُخرج فرقاً
    /// يُنسب إلى التقرير وهو في المرجع. <b>وهذا الوسيطُ أخرجته طفرةٌ نجت</b> — ترشيحُ
    /// المستندات بالتاريخ كان قائماً بلا مرجعٍ يُقابله.
    /// </param>
    public async Task<IReadOnlyDictionary<long, decimal>> AllBalancesAsync(
        long companyId, DateOnly? asOf = null)
    {
        var invoiced = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.PosShiftId == null
                        && (asOf == null || i.InvoiceDate <= asOf))
            .GroupBy(i => i.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(i => i.Subtotal + i.VatAmount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var billings = (await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.Status == ProgressBillingStatus.Posted
                        && (asOf == null || b.BillingDate <= asOf))
            .ToListAsync())
            .GroupBy(b => b.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(ReceivablePortionOf));

        var retentionReleased = await ReleasedRetentionByCustomerAsync(db, companyId, asOf);

        var credited = await ArCreditNotes(db, companyId)
            .Where(n => asOf == null || n.NoteDate <= asOf)
            .GroupBy(n => n.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(n => n.Subtotal + n.VatAmount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var received = await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.Status == ReceiptVoucherStatus.Posted
                        && (asOf == null || v.VoucherDate <= asOf))
            .GroupBy(v => v.CustomerId)
            // (CEEF-230 · ط1-٤) ‏**ما حُرِّر من الذمّة لا ما دخل الصندوق.**
            //
            // ‏كان يطرح `Amount` — وهو بعملة السند. فسندٌ دولاريٌّ يُنقص الذمّةَ الريالية
            // ‏بعددٍ دولاريّ، **وينفصل الدفترُ المساعد عن الأستاذ**. وأسقطته هـ-١ في أوّل
            // ‏تشغيل: الأستاذُ صفرٌ والمساعدُ 3,162.50.
            //
            // ‏و`ReceivableRelieved == 0` يعني سنداً قديماً سابقاً لهذه الدفعة — و`Amount`
            // ‏هو الصوابُ له، إذ كان كلُّ سندٍ بالريال بالضرورة.
            .Select(g => new
            {
                CustomerId = g.Key,
                Total = g.Sum(v => v.ReceivableRelieved == 0m ? v.Amount : v.ReceivableRelieved),
            })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var appliedDeposits = await AppliedDepositsByCustomerAsync(db, companyId, asOf);

        var chequeReceived = await db.Cheques
            .Where(c => c.CompanyId == companyId && c.CustomerId != null
                        && c.Type == ChequeType.Received
                        && ReceivedChequeCreditStatuses.Contains(c.Status)
                        && (asOf == null || c.ChequeDate <= asOf))
            .GroupBy(c => c.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var all = invoiced.Keys
            .Union(billings.Keys)
            .Union(retentionReleased.Keys)
            .Union(credited.Keys)
            .Union(received.Keys)
            .Union(appliedDeposits.Keys)
            .Union(chequeReceived.Keys);

        return all.ToDictionary(id =>
            id,
            id => invoiced.GetValueOrDefault(id) + billings.GetValueOrDefault(id)
                  + retentionReleased.GetValueOrDefault(id)
                  - credited.GetValueOrDefault(id) - received.GetValueOrDefault(id)
                  - appliedDeposits.GetValueOrDefault(id)
                  - chequeReceived.GetValueOrDefault(id));
    }

    /// <summary>كشف حساب العميل زمنياً مع رصيد متحرك.</summary>
    public async Task<IReadOnlyList<CustomerStatementLine>> StatementAsync(long companyId, long customerId)
    {
        var rows = new List<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit)>();

        foreach (var i in await ArSalesInvoices(db, companyId, customerId).ToListAsync())
            rows.Add((i.InvoiceDate, i.InvoiceNumber ?? "", "فاتورة مبيعات", i.GrandTotal(), 0m));

        foreach (var i in await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.CustomerId == customerId
                        && i.Status == SalesInvoiceStatus.Cancelled && i.PosShiftId == null)
            .ToListAsync())
        {
            await AppendCancellationRowAsync(rows, i.InvoiceDate, i.InvoiceNumber ?? "", "فاتورة مبيعات",
                i.GrandTotal(), 0m, i.JournalEntryId);
        }

        foreach (var b in await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.CustomerId == customerId && b.Status == ProgressBillingStatus.Posted)
            .ToListAsync())
            rows.Add((b.BillingDate, b.BillingNumber ?? "", "مستخلص أعمال", ReceivablePortionOf(b), 0m));

        foreach (var b in await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.CustomerId == customerId && b.Status == ProgressBillingStatus.Cancelled)
            .ToListAsync())
        {
            await AppendCancellationRowAsync(rows, b.BillingDate, b.BillingNumber ?? "", "مستخلص أعمال",
                ReceivablePortionOf(b), 0m, b.JournalEntryId);
        }

        // (CEEF-232 · ج-٢) ‏**والمُفرَج عنه سطرٌ في الكشف — وإلّا خالف الرأسُ سطورَه.**
        //
        // ‏فالرصيدُ يضمّه (`ForCustomerAsync`)، **والكشفُ كان لا يعرفه**: عميلٌ أُفرج عن
        // ‏محتجزِه يُطالَب برقمٍ في رأس الكشف **لا يجد له في سطوره أصلاً**. وهي عينُ العلّة
        // ‏التي أُغلقت في `م0-٩` من الجهة المقابلة — **وأوّلُ ما يفحصه العميل جمعُ الأعمدة**،
        // ‏فإن لم يُطابق فَقَد الثقةَ في الرقمين معاً وصار كلُّ تحصيلٍ تفاوضاً.
        //
        // ‏**وجانبُ المورّدين يفعلها أصلاً** (`SupplierBalanceQuery`) — فالنقصُ كان هنا وحدَه.
        foreach (var release in await ReleasedRetentionDocumentsAsync(db, companyId, asOf: null, customerId))
            rows.Add((release.Date, release.EntryNumber, "إفراج محتجز", release.Amount, 0m));

        // (CEEF-229 · م0-٩) كشف الحساب يتبع الرصيد: إشعار نقطة البيع لا يمسّ الذمم فلا يظهر فيه.
        // وإلا اختلف الرصيد الجاري في الكشف عن الرصيد في رأسه — وهو أوّل ما يفحصه العميل.
        foreach (var n in await ArCreditNotes(db, companyId, customerId).ToListAsync())
            rows.Add((n.NoteDate, n.NoteNumber ?? "", "إشعار دائن", 0m, n.GrandTotal()));

        var posInvoiceIdsForStatement = db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.PosShiftId != null)
            .Select(i => i.Id);

        foreach (var n in await db.CreditNotes
            .Where(n => n.CompanyId == companyId && n.CustomerId == customerId
                        && n.Status == CreditNoteStatus.Cancelled
                        && !posInvoiceIdsForStatement.Contains(n.OriginalInvoiceId))
            .ToListAsync())
        {
            await AppendCancellationRowAsync(rows, n.NoteDate, n.NoteNumber ?? "", "إشعار دائن",
                0m, n.GrandTotal(), n.JournalEntryId);
        }

        foreach (var v in await db.ReceiptVouchers
            .Where(v => v.CompanyId == companyId && v.CustomerId == customerId
                        && (v.Status == ReceiptVoucherStatus.Posted || v.Status == ReceiptVoucherStatus.Reversed))
            .ToListAsync())
        {
            rows.Add((v.VoucherDate, v.VoucherNumber ?? "", "سند قبض", 0m, v.Amount));
            if (v.Status == ReceiptVoucherStatus.Reversed && v.JournalEntryId is long jeId)
            {
                var original = await db.JournalEntries.SingleOrDefaultAsync(e => e.Id == jeId);
                if (original?.ReversalEntryId is long revId)
                {
                    var reversal = await db.JournalEntries.SingleAsync(e => e.Id == revId);
                    rows.Add((reversal.EntryDate, v.VoucherNumber ?? "", "عكس سند قبض", v.Amount, 0m));
                }
            }
        }

        foreach (var c in await db.Cheques
            .Where(c => c.CompanyId == companyId && c.CustomerId == customerId && c.Type == ChequeType.Received)
            .ToListAsync())
        {
            if (ReceivedChequeCreditStatuses.Contains(c.Status))
                rows.Add((c.ChequeDate, c.ChequeNumber, "شيك وارد", 0m, c.Amount));
            else if (c.Status == ChequeStatus.Bounced)
            {
                rows.Add((c.ChequeDate, c.ChequeNumber, "شيك وارد", 0m, c.Amount));
                rows.Add((c.ChequeDate, c.ChequeNumber, "ارتجاع شيك", c.Amount, 0m));
            }
        }

        var running = 0m;
        return rows
            .OrderBy(r => r.Date).ThenBy(r => r.Number)
            .Select(r =>
            {
                running += r.Debit - r.Credit;
                return new CustomerStatementLine(r.Date, r.Number, r.Type, r.Debit, r.Credit, running);
            })
            .ToList();
    }

    private async Task AppendCancellationRowAsync(
        List<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit)> rows,
        DateOnly docDate, string number, string typeLabel,
        decimal debit, decimal credit, long? journalEntryId)
    {
        rows.Add((docDate, number, typeLabel, debit, credit));
        if (journalEntryId is not long jeId) return;

        var original = await db.JournalEntries.SingleOrDefaultAsync(e => e.Id == jeId);
        if (original?.ReversalEntryId is not long revId) return;

        var reversal = await db.JournalEntries.SingleAsync(e => e.Id == revId);
        rows.Add((reversal.EntryDate, number, $"إلغاء {typeLabel}", credit, debit));
    }
}
