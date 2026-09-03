using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Reporting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (CEEF-231 · ط5-١١) <b>تقليمُ ما يجوز — ولا يُمَسّ ما لا يجوز.</b>
///
/// <para><b>ما كان:</b> لا خدمةَ أرشفةٍ ولا تقليمٍ في المستودع كلِّه. <b>فجداولُ الطابور
/// واللقطات وسجلِّ الدخول تنمو بلا حدٍّ ما دام النظامُ يعمل</b>.</para>
///
/// <para><b>وهويّةُ ح-٩:</b> بعد أيّ تقليم، <b>عددُ سطور سجلّ التدقيق كما كان</b>،
/// <b>وعددُ الأحداث المعلَّقة كما كان</b>. <b>وكسرُها ليس نقصَ ميزةٍ بل إبطالُ دليل</b>:
/// سجلُّ التدقيق سلسلةُ تجزئةٍ تربط كلَّ سطرٍ بسابقه، <b>وحذفُ سطرٍ يكسرها عند كلّ ما
/// بعده</b> — فيُقرأ النظامُ كلُّه «مُعبَثاً به».</para>
/// </summary>
public class DataRetentionTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    private static readonly DateOnly AsOf = new(2026, 9, 3);

    public DataRetentionTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── ح-٩: ما لا يُمَسّ ───────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: سجلُّ التدقيق لا يُمَسّ مهما قَدُم.</b>
    ///
    /// <para><b>وهو دفترٌ غيرُ قابلٍ للحذف يفرضه مُعترِضٌ في السياق</b>، <b>وسلسلةُ
    /// تجزئةٍ تربط سطورَه</b>. فحذفُ سطرٍ يكسر السلسلةَ عند كلّ ما بعده — <b>ويُبطل
    /// كلَّ ما بُني لإثبات عدم العبث</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheAuditLedger_IsNeverPruned()
    {
        await SetupAsync();

        // ‏سطرُ تدقيقٍ عمرُه عشرُ سنوات — **وأقدمُ من أيّ سياسة**.
        await new Brqosh.Infrastructure.Security.AuditService(_db)
            .LogAsync(_companyId, "admin", "ترحيل", "TestEntity", "K-1", "قديمٌ جدّاً");

        var before = await _db.AuditLogEntries.CountAsync();
        Assert.True(before > 0);

        await new DataRetentionService(_db).PruneAsync(_companyId,
            new RetentionPolicy(DispatchedEventDays: 1, ReportSnapshotDays: 1, LoginAuditDays: 1),
            AsOf);

        Assert.Equal(before, await _db.AuditLogEntries.CountAsync());
    }

    /// <summary>
    /// <b>والحدثُ المعلَّق لا يُمَسّ مهما قَدُم.</b>
    ///
    /// <para><b>وقِدَمُه علامةُ عطلٍ لا علامةُ انتهاء</b>: طابورٌ فيه حدثٌ عمرُه سنةٌ
    /// يعني أنّ الإرسالَ متوقّفٌ منذ سنة — <b>وحذفُه يُخفي العطلَ ويُفقد العمل</b>.</para>
    /// </summary>
    [Fact]
    public async Task APendingEvent_IsNeverPruned_HoweverOld()
    {
        await SetupAsync();

        var pending = StoredEvent(AsOf.AddDays(-3650), dispatched: false);
        var dispatched = StoredEvent(AsOf.AddDays(-3650), dispatched: true);
        _db.StoredDomainEvents.AddRange(pending, dispatched);
        await _db.SaveChangesAsync();

        var result = await new DataRetentionService(_db)
            .PruneAsync(_companyId, new RetentionPolicy(DispatchedEventDays: 90), AsOf);

        Assert.Equal(1, result.DispatchedEvents);

        var remaining = await _db.StoredDomainEvents.ToListAsync();
        Assert.Single(remaining);
        Assert.Null(remaining[0].DispatchedAtUtc);
    }

    // ── ما يجوز ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>والحديثُ يبقى — فالتقليمُ بالعمر لا بالنوع.</b>
    ///
    /// <para><b>وحارسٌ لا يقيس هذا يمرّ على تقليمٍ يمحو كلَّ شيء</b>.</para>
    /// </summary>
    [Fact]
    public async Task RecentRowsSurvive()
    {
        await SetupAsync();

        _db.StoredDomainEvents.Add(StoredEvent(AsOf.AddDays(-10), dispatched: true));
        _db.ReportSnapshots.Add(Snapshot(AsOf.AddDays(-10)));
        await _db.SaveChangesAsync();

        var result = await new DataRetentionService(_db)
            .PruneAsync(_companyId, new RetentionPolicy(90, 365, 1825), AsOf);

        Assert.Equal(0, result.Total);
        Assert.Equal(1, await _db.StoredDomainEvents.CountAsync());
        Assert.Equal(1, await _db.ReportSnapshots.CountAsync());
    }

    /// <summary>
    /// <b>واللقطةُ القديمةُ تُقلَّم فعلاً — وإلّا فالخدمةُ تُبلّغ صفراً وتُقرأ «نظيفة».</b>
    ///
    /// <para><b>وهذا نصفُ القياس المنسيّ</b>: حارسٌ يقيس البقاءَ وحدَه <b>يمرّ على خدمةٍ
    /// لا تحذف شيئاً أبداً</b> — وقد نجت هذه الطفرةُ حين قِيس البقاءُ بلا الحذف.
    /// <b>واللقطةُ مُعاد توليدُها من الأستاذ</b>، فالتقليمُ فيها مسموح.</para>
    /// </summary>
    [Fact]
    public async Task AStaleSnapshot_IsPruned()
    {
        await SetupAsync();

        _db.ReportSnapshots.Add(Snapshot(AsOf.AddDays(-400)));
        _db.ReportSnapshots.Add(Snapshot(AsOf.AddDays(-10)));
        await _db.SaveChangesAsync();

        var result = await new DataRetentionService(_db)
            .PruneAsync(_companyId, new RetentionPolicy(ReportSnapshotDays: 365), AsOf);

        Assert.Equal(1, result.ReportSnapshots);
        Assert.Equal(1, await _db.ReportSnapshots.CountAsync());
    }

    /// <summary>
    /// <b>وسجلُّ الدخول القديم يُقلَّم — وهو على مستوى النظام لا المنشأة.</b>
    ///
    /// <para><b>ومحاولةُ دخولٍ فاشلةٌ لا تخصّ منشأةً بعينها</b>: المهاجمُ لا يُعلن أيَّ
    /// منشأةٍ يقصد. <b>فتقليمٌ بمعرّف منشأةٍ يترك أخطرَ السطور بلا تقليمٍ إلى الأبد</b> —
    /// محاولاتِ مَن لا حساب له.</para>
    /// </summary>
    [Fact]
    public async Task AStaleLoginAudit_IsPruned_AcrossTheWholeSystem()
    {
        await SetupAsync();

        _db.LoginAuditEntries.Add(LoginAudit(AsOf.AddDays(-2000)));
        _db.LoginAuditEntries.Add(LoginAudit(AsOf.AddDays(-10)));
        await _db.SaveChangesAsync();

        var before = await _db.LoginAuditEntries.CountAsync();
        Assert.Equal(2, before);

        var result = await new DataRetentionService(_db)
            .PruneAsync(_companyId, new RetentionPolicy(LoginAuditDays: 1825), AsOf);

        Assert.Equal(1, result.LoginAudits);
        Assert.Equal(1, await _db.LoginAuditEntries.CountAsync());
    }

    /// <summary>
    /// <b>و<c>0</c> يعني «لا تُقلَّم» — لا «احذف كلَّ شيء».</b>
    ///
    /// <para><b>وهذا أخطرُ التباسٍ ممكن</b>: صفرٌ يُقرأ «مدّةُ حفظٍ صفر» فيمحو الجدولَ
    /// كلَّه، <b>ومنشأةٌ تريد إيقافَ التقليم تكتب صفراً</b>.</para>
    /// </summary>
    [Fact]
    public async Task ZeroMeansDoNotPrune_ForEveryKind()
    {
        await SetupAsync();

        // ‏**والأصنافُ الثلاثةُ معاً عمداً.** وقياسُ صنفٍ واحدٍ يترك الآخرَين بلا حارس —
        // ‏**وقد قِيس ذلك بطفرتين نجتا**: نُزع حدُّ الصفر عن اللقطات وعن سجلّ الدخول
        // ‏فمرّ الاختبارُ في الحالين، لأنّه لا يُمرّر صفراً إلّا للأحداث.
        _db.StoredDomainEvents.Add(StoredEvent(AsOf.AddDays(-3650), dispatched: true));
        _db.ReportSnapshots.Add(Snapshot(AsOf.AddDays(-3650)));
        _db.LoginAuditEntries.Add(LoginAudit(AsOf.AddDays(-3650)));
        await _db.SaveChangesAsync();

        var result = await new DataRetentionService(_db)
            .PruneAsync(_companyId, new RetentionPolicy(0, 0, 0), AsOf);

        Assert.Equal(0, result.Total);
        Assert.Equal(1, await _db.StoredDomainEvents.CountAsync());
        Assert.Equal(1, await _db.ReportSnapshots.CountAsync());
        Assert.Equal(1, await _db.LoginAuditEntries.CountAsync());
    }

    /// <summary>
    /// <b>والمعاينةُ تُقدّر ولا تحذف — وتُقدّر بالأرقام نفسِها التي يُنتجها التنفيذ.</b>
    ///
    /// <para><b>وحذفٌ بلا معاينةٍ قرارٌ لا رجعةَ فيه يُتّخذ بنقرة</b>، <b>ومعاينةٌ
    /// بمنطقٍ ثانٍ تكذب</b>.</para>
    /// </summary>
    [Fact]
    public async Task PreviewCountsWithoutDeleting()
    {
        await SetupAsync();

        _db.StoredDomainEvents.Add(StoredEvent(AsOf.AddDays(-3650), dispatched: true));
        await _db.SaveChangesAsync();

        var service = new DataRetentionService(_db);

        var preview = await service.PreviewAsync(_companyId, new RetentionPolicy(90), AsOf);
        Assert.Equal(1, preview.DispatchedEvents);
        Assert.Equal(1, await _db.StoredDomainEvents.CountAsync());

        var executed = await service.PruneAsync(_companyId, new RetentionPolicy(90), AsOf);
        Assert.Equal(preview.DispatchedEvents, executed.DispatchedEvents);
        Assert.Equal(0, await _db.StoredDomainEvents.CountAsync());
    }

    /// <summary>
    /// <b>ومدّةٌ سالبةٌ تُرفض — فهي تُحوّل الحدَّ إلى مستقبلٍ فيمحو كلَّ شيء.</b>
    /// </summary>
    [Fact]
    public async Task ANegativePolicy_IsRefused()
    {
        await SetupAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new DataRetentionService(_db).PruneAsync(
                _companyId, new RetentionPolicy(DispatchedEventDays: -1), AsOf));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private StoredDomainEvent StoredEvent(DateOnly occurredOn, bool dispatched)
    {
        var stored = StoredDomainEvent.From(
            new DocumentPostedEvent(_companyId, "TestDoc", 1L, "T-1"), _companyId, "{}");

        // ‏الزمنُ يُضبط بالانعكاس — والكيانُ يُغلقه عمداً، **وتاريخُ الحدث حقيقةٌ لا تُحرَّر**.
        typeof(StoredDomainEvent)
            .GetProperty(nameof(StoredDomainEvent.OccurredAtUtc))!
            .SetValue(stored, occurredOn.ToDateTime(TimeOnly.MinValue));

        if (dispatched) stored.MarkDispatched();

        return stored;
    }

    private static LoginAuditEntry LoginAudit(DateOnly at)
    {
        var entry = LoginAuditEntry.Record(0L, "admin", success: false, "كلمةٌ خاطئة");

        typeof(LoginAuditEntry)
            .GetProperty(nameof(LoginAuditEntry.CreatedAtUtc))!
            .SetValue(entry, at.ToDateTime(TimeOnly.MinValue));

        return entry;
    }

    private ReportSnapshot Snapshot(DateOnly capturedOn)
    {
        var snapshot = ReportSnapshot.Create(_companyId, "TrialBalance", capturedOn, "{}", 0);

        typeof(ReportSnapshot)
            .GetProperty(nameof(ReportSnapshot.CapturedAtUtc))!
            .SetValue(snapshot, capturedOn.ToDateTime(TimeOnly.MinValue));

        return snapshot;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الحفظ", "310123456700155", BusinessProfile.General);
        _companyId = company.Id;
    }
}
