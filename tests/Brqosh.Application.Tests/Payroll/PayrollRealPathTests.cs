using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (CEEF-231 · ط2-٧) <b>الطريقُ الذي يسلكه المالكُ فعلاً.</b>
///
/// <para><b>ما كُشف:</b> كلُّ ما بنته <c>ط2</c> — سجلُّ الموظفين، ونِسَبُ التأمينات،
/// ومكافأةُ نهاية الخدمة، وملفُّ حماية الأجور، والقسيمة، والسُّلَف — <b>كان يُقاس بمسارٍ
/// لا يسلكه أحد</b>. فكلُّ اختبارات الرواتب تبني الدورةَ بـ<c>AddLine</c> أو
/// <c>AddEmployee</c> في الاختبار نفسِه، <b>والشاشةُ الوحيدةُ التي يُنشئ منها المالكُ دورةً
/// كانت جدولاً فارغاً يُكتب فيه الاسمُ نصّاً حرّاً</b> وتُكتب التأميناتُ بالأصابع.</para>
///
/// <para><b>وأثرُه ليس تجميلياً:</b> سطرٌ بلا <c>EmployeeId</c> يتخطّاه
/// <c>WageProtectionFileBuilder</c> — <b>فملفُّ حماية الأجور يخرج فارغاً</b>، وهو واجبٌ
/// إلزاميٌّ نظاماً؛ والقسيمةُ تخرج بلا هويّةٍ ولا آيبان؛ <b>والسُّلفةُ لا تُستقطَع أبداً</b>
/// لأنّ اشتقاقَ رصيدها يمرّ بالمعرّف.</para>
///
/// <para><b>وهذا الحارسُ يقيس المسارَ الحقيقيَّ وحدَه</b>: يبني بـ<c>PayrollRunBuilder</c>
/// — وهو ما تستدعيه الشاشة — <b>ثمّ يقيس ما يخرج من الأبواب البعيدة</b>: ملفُّ الأجور،
/// والسُّلفة، والقيد. <b>ولا يبني سطراً بيده في أيّ اختبارٍ هنا.</b></para>
/// </summary>
public class PayrollRealPathTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _saudiId;
    private long _expatId;
    private int _seq;

    public PayrollRealPathTests()
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

    // ── الهويّة: كلُّ سطرٍ يحمل صاحبه ────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: الدورةُ المبنيّةُ من الطريق الحقيقيّ تُخرج ملفَّ أجورٍ غيرَ فارغ.</b>
    ///
    /// <para><b>وهذا هو العطلُ بعينه</b>: الشاشةُ كانت تُنتج سطوراً بلا معرّف، <b>فيتخطّاها
    /// بانيَ الملفّ كلَّها ويخرج الملفُّ بصفرِ سجلّات</b> — ولا شيءَ في الشاشة يقول ذلك.
    /// والمنشأةُ تكتشفه عند الجهة لا عندها.</para>
    /// </summary>
    [Fact]
    public async Task ARunBuiltTheRealWay_ExportsEveryEmployeeToTheWageFile()
    {
        await SetupAsync();

        var run = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        Assert.All(run.Lines, l => Assert.NotNull(l.EmployeeId));

        var file = await new WageProtectionFileBuilder(_db).BuildAsync(_companyId, run.Id);

        Assert.Equal(2, file.Records);
        Assert.Empty(file.Refusals);
    }

    /// <summary>
    /// <b>والتأميناتُ تُحسب من النِّسَب والجنسية — لا تُكتب بالأصابع.</b>
    ///
    /// <para>السعوديُّ يحمل الحصّتين، <b>والوافدُ الأخطارَ المهنية وحدَها</b>. وكتابتُهما
    /// يدوياً تختلف بين موظّفٍ وآخر بلا سبب، <b>وتُنسخ خطأً في منشأةٍ بمئة موظّف</b>.</para>
    /// </summary>
    [Fact]
    public async Task GosiIsComputedFromThePolicy_NotTyped()
    {
        await SetupAsync();

        var run = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));

        var saudi = run.Lines.Single(l => l.EmployeeId == _saudiId);
        var expat = run.Lines.Single(l => l.EmployeeId == _expatId);

        // ‏٨٠٠٠ أساسيّ · ٩٫٧٥٪ موظّف · ١١٫٧٥٪ صاحب عمل
        Assert.Equal(780m, saudi.GosiEmployee);
        Assert.Equal(940m, saudi.GosiEmployer);

        // ‏والوافدُ لا يحمل حصّةَ موظّف، وصاحبُ العمل ٢٪ أخطاراً مهنية.
        Assert.Equal(0m, expat.GosiEmployee);
        Assert.Equal(120m, expat.GosiEmployer);
    }

    /// <summary>
    /// <b>ومَن انتهت خدمتُه قبل يوم الصرف لا يُدرَج.</b>
    /// </summary>
    [Fact]
    public async Task ATerminatedEmployee_IsNotInTheRun()
    {
        await SetupAsync();

        var employee = await _db.Employees.SingleAsync(e => e.Id == _expatId);
        employee.Terminate(new DateOnly(2026, 3, 31));
        await _db.SaveChangesAsync();

        var run = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));

        Assert.Single(run.Lines);
        Assert.Equal(_saudiId, run.Lines[0].EmployeeId);
    }

    // ── السُّلفة تُستقطَع من تلقاء نفسها ─────────────────────────────────────

    /// <summary>
    /// <b>وسُلفةٌ قائمةٌ تُستقطَع بلا أن يتذكّرها أحد.</b>
    ///
    /// <para><b>وهو الفرقُ بين قدرةٍ وميزة</b>: <c>AddLine</c> يقبل استقطاعاً منذ
    /// <c>ط2-٥</c>، <b>ولا شيءَ كان يملؤه</b>. فالسُّلفةُ تُصرف وتبقى إلى الأبد،
    /// والموظّفُ يقبض راتبَه كاملاً — <b>والمنشأةُ دفعت مرّتين</b>.</para>
    /// </summary>
    [Fact]
    public async Task AnOutstandingAdvance_IsDeductedWithoutAnyoneRememberingIt()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(_saudiId, amount: 5_000m, installment: 1_000m);

        var run = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));

        var line = run.Lines.Single(l => l.EmployeeId == _saudiId);
        Assert.Equal(1_000m, line.AdvanceDeduction);

        await new PayrollPostingService(_db).PostAsync(run, "admin");

        var balances = await new EmployeeAdvanceService(_db).BalancesAsync(_companyId);
        Assert.Equal(4_000m, balances.Single(b => b.AdvanceId == advance.Id).Outstanding);
    }

    /// <summary>
    /// <b>والقسطُ لا يبتلع الصافي.</b>
    ///
    /// <para>قسطٌ يتجاوز ما بقي بعد التأمينات <b>يُخرج صافياً سالباً</b> ويرفضه
    /// <c>AddLine</c> باسمه — <b>فتعتذر الشاشةُ عن حسابها هي</b>. فيُحَدُّ بما يسع،
    /// <b>والباقي يُرحَّل إلى الشهر التالي من تلقاء نفسه</b> لأنّ الرصيدَ مُشتقّ.</para>
    /// </summary>
    [Fact]
    public async Task AnInstallmentLargerThanTheNet_IsCappedNotRefused()
    {
        await SetupAsync();

        // ‏سُلفةٌ بقسطٍ يفوق الصافي: ٨٠٠٠ − ٧٨٠ تأمينات = ٧٢٢٠ هو السقف.
        await DisbursedAsync(_saudiId, amount: 20_000m, installment: 9_000m);

        var run = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));

        var line = run.Lines.Single(l => l.EmployeeId == _saudiId);
        Assert.Equal(7_220m, line.AdvanceDeduction);
        Assert.Equal(0m, line.NetPay);
    }

    /// <summary>
    /// <b>وترحيلُ الدورة يُقفل السُّلفةَ المسدَّدة — بلا فتح شاشتها.</b>
    ///
    /// <para><b>وما كان:</b> تحديثُ الحالة زرٌّ في شاشة السُّلَف. <b>والزرُّ يُغلق الفجوةَ
    /// لمَن يتذكّره</b> — وسُلفةٌ سُدِّدت وبقيت «قائمةً» تُستقطَع في الشهر التالي مرّةً
    /// أخرى لولا أنّ الرصيدَ مُشتقّ، <b>وتُقرأ في التقارير ديناً لا وجودَ له</b>.</para>
    /// </summary>
    [Fact]
    public async Task PostingARun_SettlesAFullyRepaidAdvance()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(_saudiId, amount: 1_000m, installment: 1_000m);

        var run = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        var stored = await _db.EmployeeAdvances.SingleAsync(a => a.Id == advance.Id);
        Assert.Equal(AdvanceStatus.Settled, stored.Status);
    }

    /// <summary>
    /// <b>وعكسُ الدورة يُعيد الدَّين.</b>
    ///
    /// <para>الاستقطاعُ الذي أقفل السُّلفةَ ارتدّ، <b>وسُلفةٌ تبقى «مسدَّدة» بعد ارتداد
    /// سدادها دَينٌ سقط بلا قبض</b>.</para>
    ///
    /// <para><b>ويُقاس الإقفالُ قبل العكس عمداً.</b> فبغيره <b>يمرّ الاختبارُ بالمصادفة</b>:
    /// طفرةٌ تمنع الإقفالَ أصلاً تُبقي الحالةَ «قائمة» من أوّلها إلى آخرها، <b>فيقرأ
    /// الحارسُ ما ينتظره ولم يجرِ شيء</b>. وقِيس ذلك بطفرةٍ نجت.</para>
    /// </summary>
    [Fact]
    public async Task ReversingARun_ReopensTheAdvance()
    {
        await SetupAsync();
        var advance = await DisbursedAsync(_saudiId, amount: 1_000m, installment: 1_000m);

        var run = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        // ‏**أُقفلت أوّلاً** — وإلا فما بعدها لا يقيس عكساً.
        Assert.Equal(AdvanceStatus.Settled,
            (await _db.EmployeeAdvances.SingleAsync(a => a.Id == advance.Id)).Status);

        await new PayrollReversalService(_db).ReverseAsync(
            _companyId, run.Id, new DateOnly(2026, 5, 2), "admin");

        var stored = await _db.EmployeeAdvances.SingleAsync(a => a.Id == advance.Id);
        Assert.Equal(AdvanceStatus.Outstanding, stored.Status);
        Assert.Equal(1_000m, (await new EmployeeAdvanceService(_db)
            .BalancesAsync(_companyId)).Single(b => b.AdvanceId == advance.Id).Outstanding);
    }

    // ── ملفُّ الأجور يتوازن ─────────────────────────────────────────────────

    /// <summary>
    /// <b>وملفُّ الأجور يتوازن: الإجماليُّ − الاستقطاعاتُ = الصافي.</b>
    ///
    /// <para><b>وهذا عطلٌ أدخلته <c>ط2-٥</c> نفسُها</b>: أُضيف استقطاعُ السُّلفة إلى
    /// <c>NetPay</c> <b>ولم يُضَف إلى عمود الاستقطاعات في الملفّ</b>. فالملفُّ يخرج
    /// بمتطابقةٍ مكسورة — <b>والجهةُ تفحصها فترفض الملفَّ كلَّه</b>، وسببُ الرفض لا يظهر
    /// في النظام.</para>
    /// </summary>
    [Fact]
    public async Task TheWageFile_Balances_WhenAnAdvanceIsDeducted()
    {
        await SetupAsync();
        await DisbursedAsync(_saudiId, amount: 5_000m, installment: 1_000m);

        var run = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 28));
        await new PayrollPostingService(_db).PostAsync(run, "admin");

        var file = await new WageProtectionFileBuilder(_db).BuildAsync(_companyId, run.Id);

        foreach (var row in file.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var cells = row.Trim().Split(',');
            var basic = decimal.Parse(cells[3]);
            var housing = decimal.Parse(cells[4]);
            var other = decimal.Parse(cells[5]);
            var deductions = decimal.Parse(cells[6]);
            var net = decimal.Parse(cells[7]);

            Assert.Equal(net, basic + housing + other - deductions);
        }
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<EmployeeAdvance> DisbursedAsync(long employeeId, decimal amount, decimal installment)
    {
        var advance = EmployeeAdvance.Create(_companyId, employeeId, $"ADV-{++_seq:000}",
            new DateOnly(2026, 3, 1), amount, installment);
        _db.EmployeeAdvances.Add(advance);
        await _db.SaveChangesAsync();

        await new EmployeeAdvanceService(_db).DisburseAsync(
            _companyId, advance.Id, new DateOnly(2026, 3, 1),
            SaudiChartOfAccounts.SystemCodes.CashOnHand);

        return advance;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;

        var saudi = Employee.Create(_companyId, "E-001", "سعوديّ", "1099999999",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2024, 1, 1));
        saudi.SetPay(basic: 8_000m, housing: 0m, transport: 0m);
        saudi.SetIban("SA0380000000608010167519");

        var expat = Employee.Create(_companyId, "E-002", "وافد", "2099999999",
            EmployeeNationality.NonSaudi, "فنّي", new DateOnly(2024, 1, 1));
        expat.SetPay(basic: 6_000m, housing: 0m, transport: 0m);
        expat.SetIban("SA0380000000608010167520");

        _db.Employees.AddRange(saudi, expat);
        await _db.SaveChangesAsync();

        _saudiId = saudi.Id;
        _expatId = expat.Id;
    }
}
