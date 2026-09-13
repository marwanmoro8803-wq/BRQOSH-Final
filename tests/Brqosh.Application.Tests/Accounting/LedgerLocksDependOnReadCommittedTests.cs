using System.Data;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (م-٤ · ن-١٧) <b>أقفالُ الدفتر تقوم كلُّها على «اقفل ثمّ أعِد القراءة» — وذلك النمطُ
/// صحيحٌ تحت <c>read committed</c> وحدَه، ولم يكن شيءٌ يقوله ولا يحرسه.</b>
///
/// <para><b>وتصحيحُ البند:</b> صيغ «لا خطَّ دفاعٍ أخير في القاعدة»، وعلاجاه المقترحان
/// «ارفعِ العزلَ على مسارات الإقفال» و«ضع قيداً في القاعدة». <b>وكلاهما قِيس أنّه يُسبّب
/// العطلَ لا يمنعه</b>: الزنادُ يجري <b>داخل لقطة المعاملة نفسِها</b>، فتحت
/// <c>repeatable read</c> يرى الفترةَ مفتوحةً ويسمح بالإدراج. <b>فخطُّ الدفاع المطلوبُ
/// مهزومٌ بالآليّة التي يُفترض أن يحرس منها.</b></para>
///
/// <para><b>والكسرُ مقيسٌ لا مُستنتَج</b> (PostgreSQL 17.10، والحجبُ مُثبَتٌ باستطلاع
/// <c>pg_locks</c> لا بالتوقيت): تحت <c>repeatable read</c> انتظر القارئُ القفلَ
/// <b>٩٧٨٨ مِلّي ثانية — فالقفلُ عمل</b> — ثمّ قرأ <c>Open</c> وقيمةُ الصفّ الملتزمة
/// <c>Closed</c>. وتحت <c>serializable</c> المثلُ، <b>وبلا خطأِ تسلسلٍ إطلاقاً</b> فلا
/// يُنبَّه أحد. <b>وشاهدُ ضبطٍ موجب:</b> بلا تشابكٍ يرى <c>repeatable read</c> القيمةَ
/// الصحيحة — فالمستوى ليس أعمى، بل اللقطةُ سبقت القفل.</para>
///
/// <para><b>والحمايةُ في الموضع قبل المقارنة:</b> PostgreSQL يرفض
/// <c>SET TRANSACTION ISOLATION LEVEL</c> بعد أيّ استعلامٍ في المعاملة (<c>25001</c>).
/// فكونُ مِسبار <c>UnitOfWork.EnsureReadCommittedAsync</c> <b>أوّلَ عبارةٍ</b> يُغلق بابَ
/// الرفع أصلاً. <b>ولذلك يُثبَّت الترتيبُ باختبارٍ</b> (<see cref="TheProbe_RunsBeforeAnyOtherStatement"/>):
/// نقلُ السطر سطراً واحداً إلى الأسفل يُبقي الحارسَ قائماً في المظهر <b>ويُزيل أثرَه كلَّه</b>.</para>
/// </summary>
public class LedgerLocksDependOnReadCommittedTests
{
    private const int Year = 2026;

    /// <summary>تاريخُ السباق — <b>ولا يُزرع عليه شيء</b>، فالعدُّ عنده صفرٌ إلّا بالعطل.</summary>
    private static readonly DateOnly RaceDate = new(Year, 6, 15);

    /// <summary>تاريخُ الزرع — مغايرٌ عمداً، وإلّا كان التوكيدُ الختاميُّ أحمرَ دائماً.</summary>
    private static readonly DateOnly SeedDate = new(Year, 3, 10);

    private static readonly TimeSpan BlockWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReleaseWindow = TimeSpan.FromSeconds(30);

    // ── القياسُ السلوكيّ المركزيّ ─────────────────────────────────────────────

    /// <summary>
    /// <b>مرحّلٌ يرفع مستوى العزل ثمّ ينتظر القفل، وفترةٌ تُقفَل بينما ينتظر.</b>
    ///
    /// <para><b>على الشجرة المعطوبة:</b> ترتيبُ العبارات <c>BEGIN</c> ← <c>SET … REPEATABLE
    /// READ</c> ← قفلٌ مشترَك. فتُلتقط اللقطةُ قبل أن يُمنح القفل، ويستيقظ المرحّلُ على
    /// <c>Open</c> بائتة، <b>فتدخل الفاتورةُ فترةً أُقفلت</b> — وهو الفساد الذي يسمّيه البند.</para>
    ///
    /// <para><b>وعلى الشجرة المُصلَحة:</b> المِسبارُ سبق الـ<c>SET</c>، فيرفضه الخادمُ
    /// بـ<c>25001</c> وتسقط المعاملةُ قبل أن تكتب حرفاً.</para>
    ///
    /// <para><b>والتوكيدُ على قيدٍ حقيقيٍّ لا على غياب استثناء:</b> تُرحَّل فاتورةُ مبيعاتٍ
    /// كاملة. فتوكيدٌ على «لم يُرمَ شيء» كان ليخضرّ على الشجرتين، <b>إذ لا يُنتج الرِّجُّ
    /// القيدَ الذي يدّعي منعَه</b>.</para>
    /// </summary>
    [PostgresFact]
    public async Task APostingThatRaisesTheIsolationLevel_CannotEnterAPeriodClosedWhileItWaited()
    {
        TestActor.Administrator();
        await using var scope = await PostgresTestDatabase.CreateAsync("n17_isolation_race");
        var companyId = await SeedAsync(scope.Db);

        var lockHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // ‏المُقفل: يمسك الشقَّ الحصريَّ من حاجز الدفتر، ثمّ يُقفل الفترةَ ويلتزم.
        var closer = Task.Run(async () =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
            {
                await LedgerYearLock.AcquireForClosingAsync(db.Database, companyId, Year, ct);
                lockHeld.TrySetResult();
                await release.Task;

                var period = await db.FiscalPeriods.SingleAsync(
                    p => p.CompanyId == companyId && p.Code == FiscalPeriod.CodeFor(RaceDate), ct);
                period.Close();
                await db.SaveChangesAsync(ct);
                return true;
            });
        });

        await lockHeld.Task.WaitAsync(ReleaseWindow);

        // ‏المرحّل: يرفع المستوى **أوّلَ ما يفعل**، ثمّ يرتطم بالقفل المشترَك داخل الترحيل.
        var poster = Task.Run(async () =>
        {
            await using var db = scope.OpenIndependentContext();
            TestActor.Administrator();

            try
            {
                await UnitOfWork.ExecuteInTransactionAsync(db, async ct =>
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "SET TRANSACTION ISOLATION LEVEL REPEATABLE READ", ct);

                    var customer = await db.Customers.FirstAsync(c => c.CompanyId == companyId, ct);
                    var invoice = SalesInvoice.CreateDraft(companyId, customer.Id, RaceDate);
                    invoice.AddLine("خدمةٌ في فترةٍ تُقفَل", 1m, 700m);
                    await new SalesInvoicePostingService(db).PostAsync(invoice);
                    return true;
                });
            }
            catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
            {
                // ‏رفضٌ سليم — والتوكيدُ على الدفتر لا على نوع الاستثناء.
            }
        });

        // ‏نافذةٌ يحتجب فيها المرحّلُ على الشجرة المعطوبة، ويسقط فيها سريعاً على المُصلَحة.
        // ‏ولا تُوكَّد أيُّ الحالتين — فكلتاهما مقبولة، والفرقُ يظهر في الدفتر وحدَه.
        await Task.WhenAny(poster, Task.Delay(BlockWindow));

        release.TrySetResult();
        await closer.WaitAsync(ReleaseWindow);
        await poster.WaitAsync(ReleaseWindow);

        var entries = await scope.Db.JournalEntries.IgnoreQueryFilters()
            .CountAsync(e => e.CompanyId == companyId && e.EntryDate == RaceDate);

        Assert.Equal(0, entries);
    }

    // ── فرعُ الرمي: لا يبلغه المسارُ العاديّ، فيُقاد مباشرةً ───────────────────

    /// <summary>
    /// <b>الحارسُ نفسُه يرفض مستوًى مرفوعاً.</b>
    ///
    /// <para><b>ولمَ اختبارٌ مستقلّ:</b> على المسار الإنتاجيّ <b>لا يُشعَل هذا الفرع أبداً</b>
    /// — المُشغِّلُ يُرسل المستوى صريحاً مع كلّ <c>BEGIN</c>، فالمِسبارُ يرى
    /// <c>read committed</c> دائماً. <b>فالقياسُ أعلاه يُثبّت موضعَ المِسبار ولا يُثبّت
    /// مقارنتَه</b>: استبدالُ جسمِه كلِّه بـ<c>SELECT 1</c> يُبقيه أخضر. وهذا هو الفرعُ الذي
    /// يحرس <b>انحرافَ المُشغِّل</b> — والحزمةُ مثبَّتةٌ على <c>9.0.*</c> عائم.</para>
    ///
    /// <para>ويُبنى سياقٌ <b>بلا استراتيجيّة إعادةِ محاولة</b>، إذ ترفض
    /// <c>NpgsqlRetryingExecutionStrategy</c> معاملةً يفتحها المستخدم — وهي بعينها التي
    /// أسقطت أوّلَ صيغةٍ لرِجٍّ سابقٍ في هذا المِرفاس.</para>
    /// </summary>
    [PostgresFact]
    public async Task TheGuard_RefusesAnElevatedIsolationLevel()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("n17_guard_throws");

        await using var db = NoRetryContext(scope);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => UnitOfWork.EnsureReadCommittedAsync(db, default));

        Assert.Contains("repeatable read", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من الرفض:</b> الحارسُ صامتٌ عند المستوى الصحيح.
    ///
    /// <para>فحارسٌ يرمي دائماً يُمرّر الاختبارَ أعلاه <b>ويُعطّل كلَّ معاملةٍ في النظام</b>،
    /// ولا يُفرَّق بينهما إلّا بهذا.</para>
    /// </summary>
    [PostgresFact]
    public async Task TheGuard_IsSilentAtReadCommitted()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("n17_guard_silent");

        await using var db = NoRetryContext(scope);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        await UnitOfWork.EnsureReadCommittedAsync(db, default);
    }

    /// <summary>
    /// <b>وشاهدٌ ثانٍ — على المزوّد الآخر:</b> SQLite لا يمرّ بالحارس إطلاقاً.
    ///
    /// <para><b>وهذا شرطٌ لا تفصيل:</b> مئةٌ وعشرون موضعَ اختبارٍ تعمل على SQLite، ومستواه
    /// الفعليُّ <c>Serializable</c> لا <c>read committed</c> — <b>فحارسٌ لا يسأل عن المزوّد
    /// كان ليُسقط المِرفاسَ كلَّه</b>. ولا يضرّ غيابُه هناك: <c>LedgerYearLock</c> نفسُه
    /// معطَّلٌ على غير PostgreSQL، فلا «اقفل ثمّ أعِد القراءة» أصلاً.</para>
    /// </summary>
    [Fact]
    public async Task TheGuard_DoesNotApplyToSqlite()
    {
        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, "Data Source=:memory:");
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        await UnitOfWork.EnsureReadCommittedAsync(db, default);
    }

    // ── تثبيتُ الموضع: وهو نصفُ الحماية ───────────────────────────────────────

    /// <summary>
    /// <b>المِسبارُ أوّلُ عبارةٍ داخل المعاملة — قبل عملِ المستدعي.</b>
    ///
    /// <para><b>ولمَ يُقاس الترتيبُ لا الوجود:</b> الحمايةُ الفعليّةُ آتيةٌ من أنّ
    /// PostgreSQL يرفض <c>SET TRANSACTION ISOLATION LEVEL</c> <b>بعد أيّ استعلام</b>. فنقلُ
    /// السطر إلى ما بعد <c>action(ct)</c> <b>يُبقي كلَّ اختبارٍ آخرَ أخضر ويُزيل الحمايةَ
    /// كلَّها</b> — وهو شكلُ «يبقى مظهرُ الحماية ويزول أثرُها» الذي يسمّيه
    /// <c>LedgerYearLock</c> في تعليقه.</para>
    /// </summary>
    [Fact]
    public void TheProbe_RunsBeforeAnyOtherStatement()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "Brqosh.Infrastructure", "Persistence", "UnitOfWork.cs"));

        var code = string.Join('\n', source.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        var probe = code.IndexOf("await EnsureReadCommittedAsync(db, ct);", StringComparison.Ordinal);
        var work = code.IndexOf("var value = await action(ct);", StringComparison.Ordinal);
        var begin = code.IndexOf("BeginTransactionAsync(IsolationLevel.ReadCommitted", StringComparison.Ordinal);

        Assert.True(begin >= 0, "‏لم يعد المستوى مُصرَّحاً به عند حدّ المعاملة.");
        Assert.True(probe >= 0, "‏لم يعد المِسبارُ مُستدعًى — فلا حارسَ لانحراف المُشغِّل.");
        Assert.True(work >= 0, "‏تغيّر شكلُ الاستدعاء، فبطل القياس.");

        Assert.True(begin < probe && probe < work,
            "‏المِسبارُ ليس أوّلَ عبارةٍ بعد البِدء — وموضعُه هو ما يُغلق بابَ رفعِ المستوى، "
            + "‏لا مقارنتُه. فحارسٌ بعد العمل يرى ما رآه العملُ ولا يمنع شيئاً.");
    }

    // ── مساعدات ───────────────────────────────────────────────────────────────

    /// <summary>سياقٌ بلا إعادةِ محاولة — يسمح بمعاملةٍ يفتحها الاختبارُ بمستوًى يختاره.</summary>
    private static BrqoshDbContext NoRetryContext(PostgresTestDatabase.PostgresScope scope) =>
        new(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseNpgsql(scope.ConnectionString)
            .Options);

    private static async Task<long> SeedAsync(BrqoshDbContext db)
    {
        var company = await new DatabaseInitializer(db)
            .InitializeAsync("منشأة العزل", "310123456700099", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(db).ApplyAsync();

        var customer = Customer.Create(company.Id, "عميل نقدي");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        // ‏قيدٌ حقيقيٌّ في تاريخٍ **غيرِ** تاريخ السباق — فالدفترُ ليس فارغاً، والعدُّ عند
        // ‏تاريخ السباق يبقى صفراً إلّا أن يقع العطل.
        var seeded = SalesInvoice.CreateDraft(company.Id, customer.Id, SeedDate);
        seeded.AddLine("خدمة", 1m, 1_000m);
        await new SalesInvoicePostingService(db).PostAsync(seeded);

        // ‏وفترةُ السباق تُفتح سلفاً — فالإقفالُ يجب أن يجد ما يُقفله.
        await new JournalPostingService(db).EnsurePeriodOpenAsync(company.Id, RaceDate);

        return company.Id;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
