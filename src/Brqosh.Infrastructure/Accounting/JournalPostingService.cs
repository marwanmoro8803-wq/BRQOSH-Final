using Brqosh.Domain.Accounting;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// بوابة الترحيل الموحدة: كل قيد يمر من هنا — يتحقق من الفترة المالية قبل الترحيل،
/// وينشئ الفترة مفتوحةً تلقائياً إن لم توجد (أول استخدام للشهر).
/// </summary>
public class JournalPostingService(BrqoshDbContext db)
{
    private readonly PostingOrchestrator _orchestrator = new(db);

    public Task<JournalEntry> PostAsync(JournalEntry entry) =>
        UnitOfWork.ExecuteInTransactionAsync(db, async _ =>
        {
            SecurityContext.EnsureCanPostAccounting();
            if (entry.BranchId is null)
                entry.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(entry.CompanyId, BranchSessionContext.PreferredBranchId));
            if (entry.IsManual && entry.WorkflowInstanceId is not null)
                await new ManualJournalEntryWorkflowService(db, new WorkflowEngine(db))
                    .EnsureWorkflowApprovedAsync(entry);

            await _orchestrator.ValidateJournalEntryAsync(entry);
            await EnsureCostCentersExistAsync(entry);

            var number = await new JournalEntryNumberGenerator(db).NextAsync(entry.CompanyId, entry.EntryDate);
            entry.Post(number);

            if (entry.Id == 0)
                db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();

            await AuditPostingHelper.LogPostAsync(
                db, entry.CompanyId, "JournalEntry", entry.EntryNumber ?? entry.Id.ToString(),
                reason: "ترحيل قيد يومية", details: entry.Description ?? "");

            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                entry.CompanyId, entry.Id, entry.EntryNumber!, entry.EntryDate));

            return entry;
        });

    /// <summary>
    /// (CEEF-230 · ط1-٧) <b>مركزُ التكلفة يجب أن يكون موجوداً ونشطاً وللمنشأة نفسِها.</b>
    ///
    /// <para><b>وكان يُقبل أيُّ رقم.</b> فالعمودُ بلا مفتاحٍ أجنبيّ — <b>وذلك قرارٌ لا سهو</b>:
    /// مركزُ التكلفة يُوقَف حذفاً ناعماً، ومفتاحٌ أجنبيٌّ إمّا يمنع إيقافَه أو يترك المرجعَ
    /// معلّقاً بلا معنى. <b>فالتحقّقُ في زمن الترحيل هو الطبقةُ الصحيحة</b>: يمنع دخولَ
    /// مرجعٍ خاطئ، ولا يمنع إيقافَ مركزٍ عليه حركةٌ تاريخية.</para>
    ///
    /// <para>و<b>«ونشطاً»</b> مقصودة: الترحيلُ إلى مركزٍ موقوفٍ يعني تصنيفاً على بابٍ
    /// أُغلق — والتقريرُ سيعرضه، فيُقرأ كأنّه قائم.</para>
    /// </summary>
    private async Task EnsureCostCentersExistAsync(JournalEntry entry)
    {
        var ids = entry.Lines
            .Where(l => l.CostCenterId is not null)
            .Select(l => l.CostCenterId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return;

        var valid = await db.CostCenters
            .Where(c => c.CompanyId == entry.CompanyId && c.IsActive && ids.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        var missing = ids.Except(valid).ToList();
        if (missing.Count > 0)
            throw new AccountingException(
                $"مركزُ تكلفةٍ غيرُ معروفٍ أو موقوف في القيد: {string.Join("، ", missing)}.");
    }

    /// <summary>
    /// (CEEF-229 · م2-٦) <b>اسمُها «تحقُّق» وفعلُها كتابة.</b>
    ///
    /// <para>حين لا توجد الفترة المالية تُنشئها الدالّة <b>وتحفظها</b> (السطور أدناه) — فهي
    /// ليست قراءةً كما يوحي اسمُها. وكانت الوحيدة بين عمليات خدمات الترحيل التي <b>لا ترفض
    /// «المُطّلِع»</b>، فكشفتها البوّابةُ السلوكية حين استدعت كلّ عملية بدورٍ لا يملك الترحيل.</para>
    ///
    /// <para><b>ولم تكن ثغرةً حيّة:</b> مستدعوها الخمسة والعشرون كلُّهم داخل خدمات ترحيلٍ
    /// وإلغاءٍ تفرض حارسَها قبلها، فلا يبلغها مُطّلِعٌ من أيّ مسارٍ في الواجهة. والحارسُ هنا
    /// عمقٌ دفاعيّ: الدالّة عامّةٌ وتكتب، وكلُّ مستدعٍ شرعيّ يملك حقَّ الترحيل أصلاً فلا يتغيّر
    /// عليه شيء.</para>
    /// </summary>
    /// <summary>
    /// (CEEF-229) <b>الفترةُ تُنشأ مرّةً واحدة — وكانت تُنشأ بعدد المرحّلين.</b>
    ///
    /// <para><b>العطل:</b> قراءةٌ ثمّ إنشاءٌ بلا قفل. فستّةُ مرحّلين في أوّل يومٍ من شهرٍ
    /// جديد يقرأون «لا فترة» ويُنشئونها ستّاً — <b>فيربح واحدٌ ويموت خمسة</b> على
    /// <c>IX_fiscal_periods_CompanyId_Code</c> برسالةِ قاعدة بيانات إنجليزية خامّة.
    /// ويقع في اليوم الأوّل من كلّ شهر في متجرٍ فيه صندوقان.</para>
    ///
    /// <para><b>ووُجد مرّتين بالمصادفة ولم يُقصد في أيّهما:</b> حجب أوّلاً قياسَ ترقيم
    /// المستندات (م1-١٤)، ثمّ <b>ستر سباقَ الإقفال السنويّ</b> (م1-١٥) فجعل اختباراً معطوباً
    /// يمرّ أخضر. <b>وعطلٌ يستر غيرَه أخطرُ من عطلٍ يُرى.</b></para>
    ///
    /// <para><b>والمعاملةُ شرطٌ لا زينة:</b> القفلُ الاستشاريّ مربوطٌ بها، وخارجَها يُحرَّر
    /// بانتهاء العبارة فلا يحمي شيئاً. ويُستدعى هذا التابعُ من داخل معاملاتِ الترحيل ومن
    /// خارجها معاً، فتُفتح واحدةٌ حين لا توجد — و<c>ExecuteInTransactionAsync</c> يعمل
    /// داخلياً حين توجد فلا تتداخل معاملتان.</para>
    /// </summary>
    public async Task EnsurePeriodOpenAsync(long companyId, DateOnly date)
    {
        SecurityContext.EnsureCanPost();

        var code = FiscalPeriod.CodeFor(date);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            // ‏(م-٢ · ن-٠٢/ن-٠٣) ‏**حاجزُ الدفتر أوّلاً — وهذا الموضعُ هو المصبُّ الوحيد.**
            //
            // ‏سبعٌ وثلاثون خدمةَ ترحيلٍ تكتب في `JournalEntries`، **وكلُّها بلا استثناء**
            // ‏تمرّ من هنا: تسعَ عشرةَ تستدعي هذا التابعَ رأساً، والثمانيَ عشرةَ الباقيةَ
            // ‏عبر `FinancialPostingSupport.ValidateAsync` ← `PostingOrchestrator` ←
            // ‏`SaudiPostingPolicy` ← هذا السطر. **فسطرٌ واحدٌ هنا يحرس الدفترَ كلَّه.**
            //
            // ‏ومشترَكٌ لا حصريّ: المرحّلون لا ينتظر بعضُهم بعضاً، ولا يحجبهم إلا إقفالٌ
            // ‏جارٍ على السنة نفسِها. وهو يُؤخذ **قبل** قفل الفترة — فترتيبُ الأقفال
            // ‏واحدٌ في كلّ المسارات، والجمودُ المتبادل يمتنع بناءً لا بالحظّ.
            await LedgerYearLock.AcquireForPostingAsync(db.Database, companyId, date.Year, token);

            await DocumentSequenceLock.AcquireAsync(
                db.Database, companyId, $"PERIOD{date.Month:D2}", date.Year, token);

            var period = await db.FiscalPeriods
                .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Code == code, token);

            if (period is null)
            {
                // ‏**وإنشاءُ الفترة كان بلا سؤالٍ عن السنة.**
                //
                // ‏الشهرُ الساكنُ في سنةٍ مقفلةٍ لا صفَّ له، فيُنشئه هذا السطرُ **مفتوحاً**
                // ‏ويقبل القيد. وقيس: سنةُ 2025 أُقفلت، ثمّ قيدٌ بتاريخ 2025-03-10 قُبل
                // ‏ورُحِّل — **فتغيّر دفترُ سنةٍ اعتمدها المراجع**، وإعادةُ الإقفال مرفوضةٌ
                // ‏لأنّ قيدَ الإقفال قائم، فيبقى الأثرُ في الإيراد والمصروف إلى الأبد.
                //
                // ‏**والسؤالُ هنا لا في الفترة**: وسمُ قيد الإقفال هو الحقيقةُ الوحيدةُ
                // ‏الباقيةُ في قواعدِ العملاء القائمة — لا عمودَ «سنةٌ مقفلة» ولا جدول.
                //
                // ‏**ولا يُسأل إلّا حين يغيب الصفّ** — فمسارُ الترحيل المعتادُ (وفترتُه
                // ‏موجودة) لا يدفع استعلاماً زائداً، **وإعادةُ فتح فترةٍ عمداً تبقى نافذة**:
                // ‏صفُّها قائمٌ ومفتوح، فلا يبلغها هذا الحارسُ أصلاً. ولولا هذا القيدُ
                // ‏لصار الإصلاحُ عطلاً آخرَ يمنع التصحيحَ المشروع.
                var closeMark = YearEndCloseService.SourceDocumentFor(date.Year);
                var yearClosed = await db.JournalEntries.AnyAsync(e =>
                    e.CompanyId == companyId
                    && e.SourceDocument == closeMark
                    && e.Status != JournalEntryStatus.Draft, token);

                if (yearClosed)
                    throw new AccountingException(
                        $"السنة {date.Year} مقفلةٌ بقيد إقفالٍ سنويّ — لا تُفتح لها فترةٌ جديدة {code} ولا يُرحَّل فيها. "
                        + "صحِّح بقيدٍ في سنةٍ مفتوحة، أو أعد فتح فترة السنة المعنيّة إن كنت مخوّلاً.");

                db.FiscalPeriods.Add(FiscalPeriod.Create(companyId, date.Year, date.Month));
                await db.SaveChangesAsync(token);
                return true;
            }

            if (period.Status == FiscalPeriodStatus.Closed)
                throw new AccountingException(
                    $"الفترة المالية {code} مقفلة — لا يمكن الترحيل فيها. أعد فتح الفترة أولاً إن كنت مخوّلاً.");

            return true;
        });
    }
}
