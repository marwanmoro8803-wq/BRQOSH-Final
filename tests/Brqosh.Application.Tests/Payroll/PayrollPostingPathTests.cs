using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (CEEF-230 · ط2-٠) <b>مسارُ الشاشة — وهو المسارُ الوحيد الذي يسلكه المستخدم.</b>
///
/// <para><b>ما كان:</b> اختبارا الرواتب في المستودع يُرحّلان دورةً <b>لم تُحفَظ بعد</b>.
/// والشاشةُ تفعل غيرَ ذلك تماماً: <c>PayrollDialog</c> يحفظ المسودّة، ثمّ
/// <c>PayrollPage</c> يُعيد تحميلَها من القاعدة ويُمرّرها إلى الترحيل — <b>و<c>PostAsync</c>
/// يُضيفها مرّةً أخرى</b>.</para>
///
/// <para><b>فالنتيجة أنّ شاشةَ الرواتب لا تُرحّل شيئاً</b>: كلُّ محاولةٍ تسقط بـ
/// <c>23505</c> على المفتاح الأساسيّ. <b>ووحدةُ الرواتب مُعلَنةٌ على القطاعات الثمانية
/// كلِّها</b> — فالعطلُ يمسّ كلَّ عميلٍ يستعملها.</para>
///
/// <para><b>ولم يكشفه أيُّ اختبار</b> لأنّ الاختبارات كلَّها تسلك مساراً لا يسلكه أحد.
/// وكشفه <c>ط0-٣</c> عرَضاً حين وقعتُ في الفخّ نفسِه وأنا أكتب المسبار — <b>وسجّلتُه
/// حينها بنداً مفتوحاً عن اصطلاحَي استدعاءٍ لا يُعلنهما شيء</b>.</para>
/// </summary>
public class PayrollPostingPathTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public PayrollPostingPathTests()
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

    /// <summary>
    /// <b>القياسُ المركزيّ: تُحفَظ المسودّةُ ثمّ تُرحَّل — كما تفعل الشاشة حرفاً.</b>
    /// </summary>
    [Fact]
    public async Task ASavedDraft_CanBePosted()
    {
        await SetupAsync();

        // ‏(١) الشاشةُ تحفظ المسودّة.
        var run = PayrollRun.CreateDraft(_companyId, 2026, 3, new DateOnly(2026, 3, 28));
        run.AddLine("موظّف", basic: 8_000m, housing: 2_000m, transport: 500m,
            gosiEmployee: 900m, gosiEmployer: 1_100m);
        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync();

        // ‏(٢) الصفحةُ تُعيد تحميلَها من القاعدة ثمّ تُرحّل.
        var reloaded = await _db.PayrollRuns.SingleAsync(p => p.Id == run.Id);
        await new PayrollPostingService(_db).PostAsync(reloaded, "admin");

        Assert.Equal(PayrollRunStatus.Posted, reloaded.Status);
        Assert.NotNull(reloaded.JournalEntryId);
        Assert.Equal(1, await _db.PayrollRuns.CountAsync(p => p.CompanyId == _companyId));
    }

    /// <summary>
    /// <b>والمسودّةُ غيرُ المحفوظة تُرحَّل كذلك</b> — فالمسارُ القديم لا يُكسَر.
    ///
    /// <para>وهو ما يفعله <c>WorkOrderService</c> ومسبارُ القطاعات، <b>فلا يُصلَح مسارٌ
    /// بكسر آخر</b>.</para>
    /// </summary>
    [Fact]
    public async Task AnUnsavedDraft_CanStillBePosted()
    {
        await SetupAsync();

        var run = PayrollRun.CreateDraft(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        run.AddLine("موظّف", basic: 5_000m);
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        Assert.Equal(PayrollRunStatus.Posted, run.Status);
        Assert.Equal(1, await _db.PayrollRuns.CountAsync(p => p.CompanyId == _companyId));
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الرواتب", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }
}
