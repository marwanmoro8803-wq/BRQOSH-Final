using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (م-٢ · ن-٠٢ / ن-٠٣) <b>الإقفالُ ضدّ الترحيل — المحورُ الذي لم يكن يُقاس.</b>
///
/// <para><b>وهذا الملفُّ وُلد من عطلٍ في الاختبارات لا في الكود.</b> في المستودع
/// اختبارا تزاحمٍ بالاسم نفسِه لهذين العطلين — <c>YearEndCloseConcurrencyTests</c>
/// و<c>FiscalPeriodConcurrencyTests</c> — <b>وكلاهما أخضرُ وصادقٌ ولا يمسّ العطل</b>.
/// إذ يُشغّل كلٌّ منهما ستَّ نسخٍ من <b>العملية نفسِها</b>: مُقفلٌ ضدّ مُقفل، ومرحّلٌ ضدّ
/// مرحّل. <b>وهو تزاحمٌ متجانس.</b> والدفترُ المحاسبيُّ يُفسَد على المحور الآخر: عمليّتان
/// <b>مختلفتان</b> تتشاركان صفوفاً واحدة.</para>
///
/// <para><b>وجذرُ العطل سطرٌ يُقاس:</b> المرحّلُ يأخذ قفلاً مفتاحُه من <c>"PERIOD{شهر}"</c>،
/// والمُقفلُ السنويُّ من <c>"YEAREND"</c> — <b>واشتقاقان مختلفان يعطيان قفلين مختلفين</b>.
/// فالحمايةُ كاملةُ المظهر بين متماثلين، ومعدومةٌ بين مختلفين. وأوّلُ اختبارٍ هنا يُثبّت
/// ذلك رقماً، لئلّا يُقرأ يوماً على أنّه قفلٌ واحد.</para>
/// </summary>
public class LedgerClosureVsPostingConcurrencyTests
{
    private const int Year = 2026;
    private static readonly DateOnly PostingDate = new(Year, 6, 15);

    /// <summary>مهلةُ إثباتِ الحجب: انتظارٌ يتجاوزها يعني قفلاً عاملاً لا بطئاً عابراً.</summary>
    private static readonly TimeSpan BlockWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReleaseWindow = TimeSpan.FromSeconds(30);

    // ── ١) جذرُ العطل، مُثبَّتاً رقماً ────────────────────────────────────────

    /// <summary>
    /// <b>الأقفالُ الثلاثةُ ثلاثةٌ لا واحد.</b> وهذا هو العطلُ كلُّه في سطرٍ واحد: حمايةٌ
    /// قائمةٌ على كلّ مسارٍ وحدَه، <b>ولا تقاطعَ بين المسارات</b>.
    ///
    /// <para>ويُثبَّت هنا لأنّ من يقرأ الكودَ بعد سنةٍ يرى <c>AcquireAsync</c> في الموضعين
    /// فيظنّهما قفلاً واحداً. <b>وظنُّه هو ما أنتج ١٣٠٠ بدل ٢٢٠٠.</b></para>
    /// </summary>
    [Fact]
    public void ThePostingAndClosingKeys_NeverIntersected_WhichIsTheDefect()
    {
        var poster = DocumentSequenceLockProbe.KeyFor($"PERIOD{PostingDate.Month:D2}", Year);
        var yearEnd = DocumentSequenceLockProbe.KeyFor("YEAREND", Year);
        var ledger = LedgerYearLockProbe.KeyFor(Year);

        Assert.NotEqual(poster, yearEnd);
        Assert.NotEqual(ledger, poster);
        Assert.NotEqual(ledger, yearEnd);

        // ‏وحاجزُ الدفتر لكلّ سنةٍ مفتاحُه — فإقفالُ ٢٠٢٥ لا يمسّ ترحيلَ ٢٠٢٦.
        Assert.NotEqual(LedgerYearLockProbe.KeyFor(Year), LedgerYearLockProbe.KeyFor(Year + 1));
    }

    // ── ٢) دلالاتُ الحاجز نفسِه ──────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من القياس نفسِه.</b> مرحّلان لا يحجب أحدُهما الآخر.
    ///
    /// <para>فقفلٌ حصريٌّ واحدٌ كان يُغلق العطلَ <b>ويُسلسل ترحيلَ المنشأة كلِّها</b> — وهو
    /// اعتراضٌ مكتوبٌ بيد من كتب <c>YearEndCloseService</c>، وصحيح. وبغير هذا الاختبار
    /// يمرّ إصلاحٌ يُبطئ النظامَ إلى نصفه ويبدو ناجحاً.</para>
    /// </summary>
    [PostgresFact]
    public async Task TwoPosters_DoNotBlockEachOther()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("ledgerlock_ss");

        await using var first = scope.OpenIndependentContext();
        await using var tx = await first.Database.BeginTransactionAsync();
        await LockAsync(first, shared: true);

        // ‏الثاني يجب أن يمرّ فوراً بينما الأوّلُ ممسكٌ بالمشترَك.
        await using var second = scope.OpenIndependentContext();
        await using var tx2 = await second.Database.BeginTransactionAsync();

        var acquired = LockAsync(second, shared: true);
        var finished = await Task.WhenAny(acquired, Task.Delay(BlockWindow));

        Assert.True(ReferenceEquals(finished, acquired),
            "‏مرحّلٌ حجب مرحّلاً — الحاجزُ صار حصريّاً للجميع، وهو إصلاحٌ يُسلسل الترحيلَ كلَّه.");
        await acquired;
    }

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> الإقفالُ ينتظر ترحيلاً طائراً حتى يلتزم — ثمّ يمضي.
    ///
    /// <para>وشقّاه لازمان: أنّه يُحجَب (وإلّا فلا حماية)، <b>وأنّه يُفرَج عنه</b> (وإلّا
    /// فهو جمودٌ لا قفل). واختبارٌ يقيس الأوّلَ وحدَه يُبارك تعليقاً دائماً.</para>
    /// </summary>
    [PostgresFact]
    public async Task AClosure_WaitsForAnInFlightPosting_ThenProceeds()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("ledgerlock_sx");

        await using var poster = scope.OpenIndependentContext();
        await using var posterTx = await poster.Database.BeginTransactionAsync();
        await LockAsync(poster, shared: true);

        await using var closer = scope.OpenIndependentContext();
        await using var closerTx = await closer.Database.BeginTransactionAsync();
        var closing = LockAsync(closer, shared: false);

        var raced = await Task.WhenAny(closing, Task.Delay(BlockWindow));
        Assert.False(ReferenceEquals(raced, closing),
            "‏الإقفالُ لم ينتظر ترحيلاً طائراً — وهذا بعينه ما يُنتج قيدَ إقفالٍ ناقصاً.");

        await posterTx.CommitAsync();

        var released = await Task.WhenAny(closing, Task.Delay(ReleaseWindow));
        Assert.True(ReferenceEquals(released, closing),
            "‏الإقفالُ بقي محجوباً بعد التزام المرحّل — جمودٌ لا قفل.");
        await closing;
    }

    /// <summary><b>شاهدُ ضبطٍ موجب:</b> إقفالُ سنةٍ لا يحجب ترحيلاً في سنةٍ أخرى.</summary>
    [PostgresFact]
    public async Task ClosingOneYear_DoesNotBlockPostingInAnother()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("ledgerlock_yr");

        await using var closer = scope.OpenIndependentContext();
        await using var closerTx = await closer.Database.BeginTransactionAsync();
        await LockAsync(closer, shared: false);

        await using var poster = scope.OpenIndependentContext();
        await using var posterTx = await poster.Database.BeginTransactionAsync();

        var acquired = LockAsync(poster, shared: true, year: Year + 1);
        var finished = await Task.WhenAny(acquired, Task.Delay(BlockWindow));

        Assert.True(ReferenceEquals(finished, acquired),
            "‏إقفالُ سنةٍ جمّد ترحيلَ سنةٍ أخرى — والمفتاحُ يجب أن يكون لكلّ (منشأة · سنة).");
        await acquired;
    }

    /// <summary>
    /// <b>فرضيّةٌ يقوم عليها التصميمُ كلُّه — فتُقاس لا تُؤخَذ من التوثيق.</b>
    ///
    /// <para><c>YearEndCloseService</c> يمسك الحصريَّ ثمّ يستدعي <c>EnsurePeriodOpenAsync</c>
    /// الذي يطلب <b>المشترَكَ على المفتاح نفسِه</b>. ولو حجبت الجلسةُ نفسَها لتجمّد الإقفالُ
    /// السنويُّ تجمّداً تامّاً — <b>وعلى المسار الأكثرِ حساسيةً في النظام</b>.</para>
    /// </summary>
    [PostgresFact]
    public async Task ASessionHoldingTheExclusiveLock_MayStillTakeItShared()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("ledgerlock_self");

        await using var db = scope.OpenIndependentContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        await LockAsync(db, shared: false);
        var reentrant = LockAsync(db, shared: true);

        var finished = await Task.WhenAny(reentrant, Task.Delay(BlockWindow));
        Assert.True(ReferenceEquals(finished, reentrant),
            "‏الجلسةُ حجبت نفسَها — والإقفالُ السنويُّ يتجمّد على `EnsurePeriodOpenAsync` بداخله.");
        await reentrant;
    }

    // ── ٣) المساراتُ الحقيقيّة ───────────────────────────────────────────────

    /// <summary>
    /// (ن-٠٣) <b>إقفالُ الفترة ينتظر ترحيلاً طائراً — عبر الخدمات الحقيقيّة لا الأقفالِ الخامّة.</b>
    ///
    /// <para><b>وما قِيس قبل الإصلاح:</b> التزم الإقفالُ بعد ٤٢٩ م.ث <b>بلا انتظارٍ إطلاقاً</b>،
    /// والنتيجةُ <c>الفترة=Closed · الفاتورة=Posted</c> — فاتورةٌ مرحّلةٌ في فترةٍ مقفلة، وهي
    /// الحالةُ التي وُجد كلُّ حارسِ فتراتٍ في النظام ليمنعها.</para>
    /// </summary>
    [PostgresFact]
    public async Task ClosingAPeriod_WaitsForAnInFlightPosting()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("periodclose_race");
        var companyId = await SeedAsync(scope.Db);

        var periodId = await scope.Db.FiscalPeriods
            .Where(p => p.CompanyId == companyId && p.Code == FiscalPeriod.CodeFor(PostingDate))
            .Select(p => p.Id).SingleAsync();

        // ‏مرحّلٌ يبلغ الحارسَ ويقف — فيمسك شقَّ الدفتر المشترَك ومعاملتُه مفتوحة.
        //
        // ‏**والمعاملةُ تُفتح بـ`UnitOfWork` لا بـ`BeginTransactionAsync`** — وهذا ليس ذوقاً:
        // ‏السياقُ مضبوطٌ على `NpgsqlRetryingExecutionStrategy`، وهي ترفض معاملةً يفتحها
        // ‏المستخدمُ بنفسه ثمّ يُشغّل داخلها استعلامَ EF. **وأوّلُ صيغةٍ لهذا الاختبار سقطت
        // ‏بذلك لا بالعطل** — وسقوطٌ لسببٍ خاطئ يُصدّق إصلاحاً لم يقع.
        var atTheGuard = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var posting = Task.Run(async () =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();
            await UnitOfWork.ExecuteInTransactionAsync(db, async _ =>
            {
                await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, PostingDate);
                atTheGuard.TrySetResult();
                await release.Task;
                return true;
            });
        });

        await atTheGuard.Task.WaitAsync(ReleaseWindow);

        var close = Task.Run(async () =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();
            await new FiscalPeriodService(db).CloseAsync(companyId, periodId, "مدير");
        });

        var raced = await Task.WhenAny(close, Task.Delay(BlockWindow));
        Assert.False(ReferenceEquals(raced, close),
            "‏أُقفلت الفترةُ بينما ترحيلٌ فيها طائر — وهو ما قِيس: الفترة=Closed والفاتورة=Posted.");

        release.TrySetResult();
        await posting.WaitAsync(ReleaseWindow);

        var released = await Task.WhenAny(close, Task.Delay(ReleaseWindow));
        Assert.True(ReferenceEquals(released, close), "‏بقي الإقفالُ محجوباً بعد التزام المرحّل.");
        await close;
    }

    /// <summary>
    /// (ن-٠٣) <b>وإقفالان متزامنان على فترةٍ واحدة يُنتجان سطرَ تدقيقٍ واحداً لا اثنين.</b>
    ///
    /// <para><b>والعطلُ كان ثلاثيّاً:</b> لا معاملةَ تجمع الحالةَ وسطرَ التدقيق، ولا قفلَ
    /// يُسلسل المُقفلَين، <b>ولا رمزَ تزامنٍ على الصفّ</b>. فيقرأ الاثنان «مفتوحة» ويكتبان
    /// «مقفلة» — <b>ولا يشتكي شيء</b>، لأنّ الكتابةَ الثانية تُصيب القيمةَ نفسَها. والأثرُ
    /// الوحيدُ المرئيُّ سطرا تدقيقٍ لإقفالٍ واحد.</para>
    ///
    /// <para><b>وإعادةُ القراءة بعد القفل هي ما يُحوّل الثانيَ إلى رفضٍ صريح.</b> فبغيرها
    /// يُنفَّذ الاستعلامُ على القاعدة <b>ويُطرَح صفُّها</b>: خريطةُ هويّة EF تُعيد المثيلَ
    /// المتعقَّبَ الذي قُرئ قبل الانتظار. فيرى المُقفلُ الثاني «مفتوحة» وقد أُقفلت، ويكتب
    /// فوقها ويُسجّل سطرَ تدقيقٍ لإقفالٍ لم يقع.</para>
    ///
    /// <para><b>ولمَ يُقاس هكذا لا بأربعة متسابقين:</b> جُرِّبت الصيغةُ المتسابقة أوّلاً
    /// <b>ومرّت على الشجرة المعطوبة</b> — تسلسلت الأربعةُ بالتوقيت فبدا النظامُ محميّاً.
    /// <b>وكناريٌّ يمرّ على العطل زينةٌ لا حارس.</b> وهذه الصيغةُ تُثبّت التشابكَ بيدها
    /// بدل انتظاره، فتُميّز في كلّ تشغيلٍ لا في بعضه.</para>
    /// </summary>
    [PostgresFact]
    public async Task ASecondClose_OnAPeriodClosedMeanwhile_IsRefusedNotRepeated()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("periodclose_stale");
        var companyId = await SeedAsync(scope.Db);

        var code = FiscalPeriod.CodeFor(PostingDate);

        // ‏سياقُ المُقفل الثاني — يقرأ الفترةَ **مفتوحةً** فتدخل خريطةَ هويّته.
        await using var stale = scope.OpenIndependentContext();
        var tracked = await stale.FiscalPeriods
            .SingleAsync(p => p.CompanyId == companyId && p.Code == code);
        Assert.Equal(FiscalPeriodStatus.Open, tracked.Status);

        // ‏ويُقفلها الأوّلُ من سياقٍ مستقلّ — وهو ما كان يقع أثناء انتظار الثاني على القفل.
        await using var first = scope.OpenIndependentContext();
        TestActor.Administrator();
        await new FiscalPeriodService(first).CloseAsync(companyId, tracked.Id, "مدير");

        // ‏فيجب أن يُرفَض الثاني — لا أن يُقفل ما أُقفل ويكتب سطرَ تدقيقٍ ثانياً.
        TestActor.Administrator();
        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new FiscalPeriodService(stale).CloseAsync(companyId, tracked.Id, "مدير"));
        Assert.Contains("مسبقاً", ex.Message);

        var audits = await scope.Db.AuditLogEntries
            .IgnoreQueryFilters()
            .CountAsync(a => a.CompanyId == companyId && a.EntityKey == code
                          && a.Action == "إغلاق فترة مالية");

        Assert.True(audits == 1, $"‏سجلُّ التدقيق يحمل {audits} سطراً لإقفالٍ واحد.");
    }

    /// <summary>
    /// (ن-٠٢) <b>قانونُ الحفظ: ما دخل الدفترَ في السنة دخل قيدَ إقفالها.</b>
    ///
    /// <para><b>ولا يقيس هذا توقيتاً</b> — وهو مقصود. فأيُّ تشابكٍ وقع، يبقى الواجبُ واحداً:
    /// إمّا أن تُرحَّل الفاتورةُ <b>وتدخل القيد</b>، وإمّا أن تُرفض لأنّ الفترةَ أُقفلت.
    /// <b>والحالةُ الثالثة — تُرحَّل ولا تدخل — هي العطل</b>، وهي التي قِيست: ١٣٠٠ بدل ٢٢٠٠.</para>
    ///
    /// <para>وقائمةُ الدخل تستثني قيودَ الإقفال، <b>فلا يظهر الفرقُ فيها إطلاقاً</b> ويستقرّ
    /// صامتاً في حقوق الملكية. ولذلك يُقارَن طرفان: ما التقطه القيدُ وما في الدفتر فعلاً.</para>
    /// </summary>
    [PostgresFact]
    public async Task AYearEndClose_CapturesEveryInvoicePostedInThatYear()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("yearclose_race");
        var companyId = await SeedAsync(scope.Db);

        // ‏فتراتُ السنة تُهيَّأ متسلسلةً — لئلّا يُقاس سباقُ إنشائها بدل السباق المقصود.
        var posting = new JournalPostingService(scope.Db);
        for (var month = 1; month <= 12; month++)
            await posting.EnsurePeriodOpenAsync(companyId, new DateOnly(Year, month, 1));

        var closer = Task.Run(async () =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();
            try { await new YearEndCloseService(db).CloseYearAsync(companyId, Year, "مدير"); return true; }
            catch (AccountingException) { return false; }
        });

        var posters = Enumerable.Range(0, 4).Select(i => Task.Run(async () =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();
            var customer = await db.Customers.FirstAsync(c => c.CompanyId == companyId);
            var invoice = SalesInvoice.CreateDraft(companyId, customer.Id, PostingDate);
            invoice.AddLine($"خدمة {i}", 1m, 500m);
            try { await new SalesInvoicePostingService(db).PostAsync(invoice); return true; }
            catch (AccountingException) { return false; }   // ‏فترةٌ أُقفلت — رفضٌ سليم.
        })).ToArray();

        await Task.WhenAll(posters.Append(closer));

        var closingEntries = await scope.Db.JournalEntries.IgnoreQueryFilters()
            .CountAsync(e => e.CompanyId == companyId && e.SourceDocument == $"YearEndClose:{Year}");
        Assert.True(closingEntries <= 1, $"‏كُتب {closingEntries} قيدَ إقفالٍ لسنةٍ واحدة.");

        if (closingEntries == 0) return;   // ‏لم يُقفَل — فلا شيءَ يُقارَن.

        var ledgerNetIncome = await NetIncomeAsync(scope.Db, companyId);
        var retained = await RetainedEarningsAsync(scope.Db, companyId);

        // ‏`retained` دائنٌ فيخرج سالباً من ميزان المراجعة.
        Assert.True(-retained == ledgerNetIncome,
            $"‏قيدُ الإقفال حمل {-retained:N2} والدفترُ يقول {ledgerNetIncome:N2} — "
            + $"فاتورةٌ رُحِّلت في السنة ولم تدخل الإقفال، والفرقُ {ledgerNetIncome + retained:N2} "
            + "استقرّ صامتاً في حقوق الملكية.");
    }

    // ── التهيئةُ والقياس ─────────────────────────────────────────────────────

    /// <summary>القفلُ الخامُّ مباشرةً — يقيس دلالةَ الحاجز لا مسارَ خدمة.</summary>
    private static Task LockAsync(BrqoshDbContext db, bool shared, int year = Year) =>
        db.Database.ExecuteSqlRawAsync(
            shared
                ? "SELECT pg_advisory_xact_lock_shared({0}::int, {1}::int)"
                : "SELECT pg_advisory_xact_lock({0}::int, {1}::int)",
            new object[] { 1L, LedgerYearLockProbe.KeyFor(year) });

    private static async Task<long> SeedAsync(BrqoshDbContext db)
    {
        var company = await new DatabaseInitializer(db)
            .InitializeAsync("منشأة الدفتر", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(db).ApplyAsync();

        var customer = Customer.Create(company.Id, "عميل نقدي");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, PostingDate);
        invoice.AddLine("خدمة", 1m, 10_000m);
        await new SalesInvoicePostingService(db).PostAsync(invoice);

        return company.Id;
    }

    private static async Task<decimal> NetIncomeAsync(BrqoshDbContext db, long companyId)
    {
        var statement = await new IncomeStatementQuery(db)
            .ExecuteAsync(companyId, new DateOnly(Year, 1, 1), new DateOnly(Year, 12, 31));
        return statement.NetIncome;
    }

    private static async Task<decimal> RetainedEarningsAsync(BrqoshDbContext db, long companyId)
    {
        var rows = await new TrialBalanceQuery(db)
            .ExecuteAsync(companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));
        var row = rows.FirstOrDefault(r => r.AccountCode == SaudiChartOfAccounts.SystemCodes.RetainedEarnings);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }
}
